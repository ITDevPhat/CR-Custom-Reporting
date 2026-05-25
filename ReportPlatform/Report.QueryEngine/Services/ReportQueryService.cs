using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Report.Contracts.Artifacts;
using Report.Contracts.Exports;
using Report.Contracts.Requests;
using Report.Contracts.Results;
using Report.Contracts.Semantic;
using Report.Contracts.Validation;
using Report.Metadata.Stores;
using Report.QueryEngine.Artifacts;
using Report.QueryEngine.Binding;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;
using Report.QueryEngine.Execution;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Planning;
using Report.QueryEngine.Relationships;
using Report.QueryEngine.Validation;
using Report.QueryEngine.Validation.Logging;
using Report.QueryEngine.Validation.Stages;
using Report.Infrastructure.Artifacts;

namespace Report.QueryEngine.Services;

public sealed class ReportQueryService
{
    private readonly ISemanticModelStore _modelStore; private readonly SemanticModelBinder _binder; private readonly EvaluationContextBuilder _contextBuilder; private readonly MeasureExpansionEngine _measureEngine; private readonly RelationshipTraversalEngine _relationshipEngine; private readonly LogicalPlanBuilder _planBuilder; private readonly SqlCompiler _sqlCompiler; private readonly IQueryExecutor _queryExecutor; private readonly ValidationLogger _validationLogger; private readonly SemanticBindingValidator _stage1; private readonly ContextBuildingValidator _stage2; private readonly MeasureExpansionValidator _stage3; private readonly RelationshipTraversalValidator _stage4; private readonly LogicalPlanValidator _stage5; private readonly SqlCompilationValidator _stage6; private readonly QueryExecutionValidator _stage7; private readonly ReportArtifactBuilder _artifactBuilder; private readonly IReportArtifactStore _artifactStore; private readonly QueryFingerprintService _fingerprint; private readonly IReportExecutionRegistry _execRegistry;
    public ReportQueryService(ISemanticModelStore modelStore, SemanticModelBinder binder, EvaluationContextBuilder contextBuilder, MeasureExpansionEngine measureEngine, RelationshipTraversalEngine relationshipEngine, LogicalPlanBuilder planBuilder, SqlCompiler sqlCompiler, IQueryExecutor queryExecutor, ValidationLogger validationLogger, SemanticBindingValidator stage1, ContextBuildingValidator stage2, MeasureExpansionValidator stage3, RelationshipTraversalValidator stage4, LogicalPlanValidator stage5, SqlCompilationValidator stage6, QueryExecutionValidator stage7, ReportArtifactBuilder artifactBuilder, IReportArtifactStore artifactStore, QueryFingerprintService fingerprint, IReportExecutionRegistry execRegistry)
    { _modelStore = modelStore; _binder = binder; _contextBuilder = contextBuilder; _measureEngine = measureEngine; _relationshipEngine = relationshipEngine; _planBuilder = planBuilder; _sqlCompiler = sqlCompiler; _queryExecutor = queryExecutor; _validationLogger = validationLogger; _stage1 = stage1; _stage2 = stage2; _stage3 = stage3; _stage4 = stage4; _stage5 = stage5; _stage6 = stage6; _stage7 = stage7; _artifactBuilder = artifactBuilder; _artifactStore = artifactStore; _fingerprint = fingerprint; _execRegistry = execRegistry; }
    public Task<object> CompileAsync(VisualQueryRequest request, CancellationToken ct) => Task.FromResult<object>(new { message = "Use execute for comprehensive validation response." });
    public async Task<ComprehensiveQueryResponse> ExecuteAsync(VisualQueryRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew(); var results = new List<ValidationResult>(); var model = await _modelStore.LoadAsync(request.DatasetId, ct); var context = new ValidationContext(request, model);
        var st1 = Run(_stage1.Validate(context)); results.Add(st1); if (!st1.IsValid) return Build(false, results, sw.ElapsedMilliseconds, null, null, null, null, null);
        var st2 = Run(_stage2.Validate(context)); results.Add(st2); if (!st2.IsValid) return Build(false, results, sw.ElapsedMilliseconds, null, null, null, null, null);
        var bound = _binder.Bind(request, model); var eval = _contextBuilder.Build(bound); var measures = _measureEngine.Expand(eval, model);
        var st3 = Run(_stage3.Validate(measures)); results.Add(st3); if (!st3.IsValid) return Build(false, results, sw.ElapsedMilliseconds, null, null, null, null, null);
        var joins = _relationshipEngine.Build(eval, measures, model); var st4 = Run(_stage4.Validate(joins)); results.Add(st4); if (!st4.IsValid) return Build(false, results, sw.ElapsedMilliseconds, null, null, null, null, null);
        var logical = _planBuilder.Build(eval, measures, joins, model); var st5 = Run(_stage5.Validate(logical)); results.Add(st5); if (!st5.IsValid) return Build(false, results, sw.ElapsedMilliseconds, null, null, null, null, null);
        var sql = _sqlCompiler.Compile(logical); var st6 = Run(_stage6.Validate(sql)); results.Add(st6); if (!st6.IsValid) return Build(false, results, sw.ElapsedMilliseconds, sql, null, null, null, null);
        var expected = logical.Select.Select(s => new QueryColumn { Name = s.Alias, Type = s.Role == "metric" ? "decimal" : "string" }).ToList(); var queryResult = await _queryExecutor.ExecuteAsync(request.ConnectionId, sql, expected, ct); var st7 = Run(_stage7.Validate(queryResult)); results.Add(st7);
        var executionId = $"exec-{Guid.NewGuid():N}"; var semanticVersion = "v1"; var fingerprint = _fingerprint.Compute(request, semanticVersion);
        var artifactKey = $"reports/{request.ReportId}/{fingerprint}/{semanticVersion}/{executionId}/artifact-v1.seaf";
        var table = ToDataTable(queryResult);
        var header = new ReportExecutionArtifactHeader { ExecutionId = executionId, ReportId = request.ReportId, TemplateId = request.ReportId, QueryFingerprint = fingerprint, SemanticModelVersion = semanticVersion, ExecutedAtUtc = DateTime.UtcNow, SemanticMetadata = new ReportArtifactSemanticMetadata { GroupFields = request.Rows, MetricFields = request.Values, Filters = request.Filters.Select(f => JsonSerializer.SerializeToElement(f)).ToArray(), Sort = request.Sort.Select(s => JsonSerializer.SerializeToElement(s)).ToArray() } };
        var built = _artifactBuilder.Build(artifactKey, header, table); await _artifactStore.SaveAsync(artifactKey, built.ArtifactStream, ct);
        _execRegistry.Save(new ReportExecutionRecord { ExecutionId = executionId, ArtifactKey = artifactKey, ExecutedAtUtc = built.Header.ExecutedAtUtc, QueryFingerprint = fingerprint, ReportId = request.ReportId, RowCount = queryResult.Rows.Count, SemanticModelVersion = semanticVersion, TemplateId = request.ReportId });
        return Build(st7.IsValid && results.All(r => r.IsValid), results, sw.ElapsedMilliseconds, sql, queryResult, executionId, artifactKey, fingerprint, semanticVersion);
    }
    public async Task<CompiledReportQuery> CompileForExportAsync(VisualQueryRequest request, CancellationToken ct) { var response = await ExecuteAsync(request, ct); if (!response.Success || response.Compilation is null) throw new InvalidOperationException("Cannot export invalid query"); return new CompiledReportQuery { ConnectionId = request.ConnectionId, Sql = response.Compilation.Sql, Parameters = response.Compilation.Parameters, ExpectedColumns = response.Columns.Select(c => new QueryColumn { Name = c.Name, Type = c.Type }).ToList(), Warnings = response.ValidationResults.SelectMany(v => v.Warnings).Select(w => new QueryValidationMessage { Code = w.Code, Message = w.Message }).ToList() }; }
    private ValidationResult Run(ValidationResult result) { _validationLogger.LogStage(result, "query_execute"); return result; }
    private static DataTable ToDataTable(QueryResult query){ var t=new DataTable(); foreach(var c in query.Columns){ t.Columns.Add(c.Name,typeof(object)); } foreach(var r in query.Rows){ var dr=t.NewRow(); for(int i=0;i<query.Columns.Count;i++){ dr[i]=r.TryGetValue(query.Columns[i].Name,out var v)&&v is not null?v:DBNull.Value; } t.Rows.Add(dr);} return t; }
    private static ComprehensiveQueryResponse Build(bool success, List<ValidationResult> results, long duration, SqlCompilationResult? sql, QueryResult? query, string? executionId, string? artifactKey, string? queryFingerprint, string? semanticModelVersion)
    => new() { Success = success, Columns = success && query is not null ? query.Columns.Select(c => new ColumnMetadata { Name = c.Name, Type = c.Type }).ToList() : [], Data = success && query is not null ? query.Rows : [], Compilation = sql is null ? null : new CompilationResult { Success = success, Sql = sql.Sql, Parameters = sql.Parameters }, ValidationResults = results, ExecutionId=executionId, ArtifactKey=artifactKey, QueryFingerprint=queryFingerprint, SemanticModelVersion=semanticModelVersion, Metadata = new ExecutionMetadata { TotalDurationMs = duration, ErrorCount = results.Sum(r => r.Errors.Count), WarningCount = results.Sum(r => r.Warnings.Count), ExecutedStages = results.Select(r => r.Stage).ToList() } };
}
