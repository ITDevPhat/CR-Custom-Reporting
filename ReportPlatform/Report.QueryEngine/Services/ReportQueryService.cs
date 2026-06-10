using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Report.Contracts.Artifacts;
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

namespace Report.QueryEngine.Services;

public sealed class ReportQueryService
{
    private readonly ISemanticModelStore _modelStore; 
    private readonly SemanticModelBinder _binder; 
    private readonly EvaluationContextBuilder _contextBuilder; 
    private readonly MeasureExpansionEngine _measureEngine; 
    private readonly RelationshipTraversalEngine _relationshipEngine; private readonly LogicalPlanBuilder _planBuilder; private readonly SqlCompiler _sqlCompiler; private readonly IQueryExecutor _queryExecutor; private readonly ValidationLogger _validationLogger; private readonly SemanticBindingValidator _stage1; private readonly ContextBuildingValidator _stage2; private readonly MeasureExpansionValidator _stage3; private readonly RelationshipTraversalValidator _stage4; private readonly LogicalPlanValidator _stage5; private readonly SqlCompilationValidator _stage6; private readonly QueryExecutionValidator _stage7; private readonly ReportArtifactBuilder _artifactBuilder; private readonly IReportArtifactStore _artifactStore; private readonly QueryFingerprintService _fingerprint; private readonly IReportExecutionRepository _executionRepository; private readonly ReportQueryServiceOptions _options;
    public ReportQueryService(ISemanticModelStore modelStore, SemanticModelBinder binder, EvaluationContextBuilder contextBuilder, MeasureExpansionEngine measureEngine, RelationshipTraversalEngine relationshipEngine, LogicalPlanBuilder planBuilder, SqlCompiler sqlCompiler, IQueryExecutor queryExecutor, ValidationLogger validationLogger, SemanticBindingValidator stage1, ContextBuildingValidator stage2, MeasureExpansionValidator stage3, RelationshipTraversalValidator stage4, LogicalPlanValidator stage5, SqlCompilationValidator stage6, QueryExecutionValidator stage7, ReportArtifactBuilder artifactBuilder, IReportArtifactStore artifactStore, QueryFingerprintService fingerprint, IReportExecutionRepository executionRepository, IOptions<ReportQueryServiceOptions> options)
    { _modelStore = modelStore; _binder = binder; _contextBuilder = contextBuilder; _measureEngine = measureEngine; _relationshipEngine = relationshipEngine; _planBuilder = planBuilder; _sqlCompiler = sqlCompiler; _queryExecutor = queryExecutor; _validationLogger = validationLogger; _stage1 = stage1; _stage2 = stage2; _stage3 = stage3; _stage4 = stage4; _stage5 = stage5; _stage6 = stage6; _stage7 = stage7; _artifactBuilder = artifactBuilder; _artifactStore = artifactStore; _fingerprint = fingerprint; _executionRepository = executionRepository; _options = options.Value; }
    public Task<object> CompileAsync(VisualQueryRequest request, CancellationToken ct) => Task.FromResult<object>(new { message = "Use execute for comprehensive validation response." });
    public async Task<ComprehensiveQueryResponse> ExecuteAsync(VisualQueryRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<ValidationResult>();
        var executionId = $"exec-{Guid.NewGuid():N}";
        var semanticVersion = "v1";
        var fingerprint = _fingerprint.Compute(request, semanticVersion);
        var reportId = string.IsNullOrWhiteSpace(request.ReportId) ? "rpt_001" : request.ReportId;
        var startedAtUtc = DateTime.UtcNow;

        await _executionRepository.UpsertAsync(new ReportExecutionRecord
        {
            ExecutionId = executionId,
            ReportId = reportId,
            ReportName = reportId,
            TemplateId = reportId,
            Status = "Processing",
            StorageMode = _options.StorageMode,
            QueryFingerprint = fingerprint,
            SemanticModelVersion = semanticVersion,
            CreatedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc
        }, ct);

        try
        {
            var model = await _modelStore.LoadAsync(request.DatasetId, ct); 
            var context = new ValidationContext(request, model);
            var st1 = Run(_stage1.Validate(context)); results.Add(st1); if (!st1.IsValid) return await FailAsync(results, sw.ElapsedMilliseconds, null, null, executionId, null, fingerprint, semanticVersion, ct);
            var st2 = Run(_stage2.Validate(context)); results.Add(st2); if (!st2.IsValid) return await FailAsync(results, sw.ElapsedMilliseconds, null, null, executionId, null, fingerprint, semanticVersion, ct);
            var bound = _binder.Bind(request, model); var eval = _contextBuilder.Build(bound); var measures = _measureEngine.Expand(eval, model);
            var st3 = Run(_stage3.Validate(measures)); results.Add(st3); if (!st3.IsValid) return await FailAsync(results, sw.ElapsedMilliseconds, null, null, executionId, null, fingerprint, semanticVersion, ct);
            var joins = _relationshipEngine.Build(eval, measures, model); var st4 = Run(_stage4.Validate(joins)); results.Add(st4); if (!st4.IsValid) return await FailAsync(results, sw.ElapsedMilliseconds, null, null, executionId, null, fingerprint, semanticVersion, ct);
            var logical = _planBuilder.Build(eval, measures, joins, model); var st5 = Run(_stage5.Validate(logical)); results.Add(st5); if (!st5.IsValid) return await FailAsync(results, sw.ElapsedMilliseconds, null, null, executionId, null, fingerprint, semanticVersion, ct);
            var sql = _sqlCompiler.Compile(logical);
            var artifactCompiled = CompileQuery(CreateArtifactQueryRequest(request), model, ct);
            await _executionRepository.UpsertAsync(new ReportExecutionRecord
            {
                ExecutionId = executionId,
                ReportId = reportId,
                ReportName = reportId,
                TemplateId = reportId,
                Status = "Processing",
                StorageMode = _options.StorageMode,
                QueryFingerprint = fingerprint,
                SemanticModelVersion = semanticVersion,
                CompiledSql = artifactCompiled.Sql.Sql,
                CreatedAtUtc = startedAtUtc,
                StartedAtUtc = startedAtUtc
            }, ct);

            var st6 = Run(_stage6.Validate(sql)); results.Add(st6); if (!st6.IsValid) return await FailAsync(results, sw.ElapsedMilliseconds, sql, null, executionId, null, fingerprint, semanticVersion, ct);
            var expected = logical.Select.Select(s => new QueryColumn { Name = s.Alias, Type = s.Role == "metric" ? "decimal" : "string" }).ToList(); var queryResult = await _queryExecutor.ExecuteAsync(request.ConnectionId, sql, expected, ct); var st7 = Run(_stage7.Validate(queryResult)); results.Add(st7);
            if (!st7.IsValid || results.Any(r => !r.IsValid)) return await FailAsync(results, sw.ElapsedMilliseconds, sql, queryResult, executionId, null, fingerprint, semanticVersion, ct);
            var artifactQueryResult = await _queryExecutor.ExecuteAsync(
                request.ConnectionId,
                artifactCompiled.Sql,
                artifactCompiled.ExpectedColumns,
                ct);
            var artifactKey = $"reports/{reportId}/{fingerprint}/{semanticVersion}/{executionId}/artifact-v1.seaf";
            var table = ToDataTable(artifactQueryResult);
            var header = new ReportExecutionArtifactHeader { ExecutionId = executionId, ReportId = reportId, TemplateId = reportId, QueryFingerprint = fingerprint, SemanticModelVersion = semanticVersion, ExecutedAtUtc = DateTime.UtcNow, SemanticMetadata = new ReportArtifactSemanticMetadata { GroupFields = request.Rows, MetricFields = request.Values, Filters = request.Filters.Select(f => JsonSerializer.SerializeToElement(f)).ToArray(), Sort = request.Sort.Select(s => JsonSerializer.SerializeToElement(s)).ToArray() } };
            var built = _artifactBuilder.Build(artifactKey, header, table); 
            await _artifactStore.SaveAsync(artifactKey, built.ArtifactStream, ct);
            await _executionRepository.MarkCompletedAsync(executionId, artifactQueryResult.Rows.Count, artifactKey, sw.ElapsedMilliseconds, ct);
            return Build(true, results, sw.ElapsedMilliseconds, artifactCompiled.Sql, queryResult, executionId, artifactKey, fingerprint, semanticVersion);
        }
        catch (Exception ex)
        {
            await _executionRepository.MarkFailedAsync(executionId, ex.Message, CancellationToken.None);
            throw;
        }
    }
    private CompiledQueryArtifacts CompileQuery(
        VisualQueryRequest request,
        Metadata.Models.SemanticModel model,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var bound = _binder.Bind(request, model);
        var eval = _contextBuilder.Build(bound);
        var measures = _measureEngine.Expand(eval, model);
        var joins = _relationshipEngine.Build(eval, measures, model);
        var logical = _planBuilder.Build(eval, measures, joins, model);
        var sql = _sqlCompiler.Compile(logical);

        return new CompiledQueryArtifacts(
            sql,
            logical.Select
                .Select(s => new QueryColumn
                {
                    Name = s.Alias,
                    Type = s.Role == "metric" ? "decimal" : "string"
                })
                .ToList());
    }
    private static VisualQueryRequest CreateArtifactQueryRequest(VisualQueryRequest source)
        => new()
        {
            ConnectionId = source.ConnectionId,
            DatasetId = source.DatasetId,
            ReportId = source.ReportId,
            VisualType = source.VisualType,
            Rows = [.. source.Rows],
            Columns = [.. source.Columns],
            Values = [.. source.Values],
            Filters = [.. source.Filters],
            Sort = [.. source.Sort],
            Limit = 0,
            Offset = 0
        };
    private ValidationResult Run(ValidationResult result) { _validationLogger.LogStage(result, "query_execute"); return result; }
    private async Task<ComprehensiveQueryResponse> FailAsync(List<ValidationResult> results, long duration, SqlCompilationResult? sql, QueryResult? query, string executionId, string? artifactKey, string fingerprint, string semanticVersion, CancellationToken ct)
    {
        var message = string.Join(" ", results.SelectMany(r => r.Errors).Select(e => e.Message));
        if (string.IsNullOrWhiteSpace(message)) message = "Report execution failed validation.";
        await _executionRepository.MarkFailedAsync(executionId, message, ct);
        return Build(false, results, duration, sql, query, executionId, artifactKey, fingerprint, semanticVersion);
    }
    private static DataTable ToDataTable(QueryResult query)
    {
        var t = new DataTable();
        foreach (var c in query.Columns)
        {
            var type = InferColumnType(c, query.Rows);
            var column = new DataColumn(c.Name, type) { AllowDBNull = true };
            column.ExtendedProperties["SqlTypeName"] = c.Type;
            t.Columns.Add(column);
        }

        foreach (var r in query.Rows)
        {
            var dr = t.NewRow();
            for (int i = 0; i < query.Columns.Count; i++)
            {
                var column = t.Columns[i];
                dr[i] = r.TryGetValue(query.Columns[i].Name, out var v) && v is not null
                    ? CoerceValue(column.DataType, v)
                    : DBNull.Value;
            }
            t.Rows.Add(dr);
        }

        return t;
    }

    private static Type InferColumnType(QueryColumn column, List<Dictionary<string, object?>> rows)
    {
        foreach (var row in rows)
        {
            if (row.TryGetValue(column.Name, out var value) && value is not null and not DBNull)
            {
                var valueType = value.GetType();
                if (valueType != typeof(object)) return valueType;
            }
        }

        return column.Type.ToLowerInvariant() switch
        {
            "int" or "integer" => typeof(int),
            "bigint" or "long" => typeof(long),
            "smallint" => typeof(short),
            "tinyint" => typeof(byte),
            "decimal" or "numeric" or "money" or "currency" => typeof(decimal),
            "float" => typeof(double),
            "real" => typeof(float),
            "bit" or "boolean" => typeof(bool),
            "date" or "datetime" or "datetime2" => typeof(DateTime),
            "datetimeoffset" => typeof(DateTimeOffset),
            "uniqueidentifier" or "guid" => typeof(Guid),
            _ => typeof(string)
        };
    }

    private static object CoerceValue(Type type, object value)
    {
        if (value is DBNull) return DBNull.Value;
        if (type.IsInstanceOfType(value)) return value;
        if (type == typeof(string)) return value.ToString() ?? "";
        if (type == typeof(Guid)) return value is Guid guid ? guid : Guid.Parse(value.ToString() ?? "");
        if (type == typeof(DateTimeOffset)) return value is DateTimeOffset dto ? dto : DateTimeOffset.Parse(value.ToString() ?? "");
        return Convert.ChangeType(value, type);
    }
    private static ComprehensiveQueryResponse Build(bool success, List<ValidationResult> results, long duration, SqlCompilationResult? sql, QueryResult? query, string? executionId, string? artifactKey, string? queryFingerprint, string? semanticModelVersion)
    => new() { 
        Success = success, 
        Columns = success && query is not null ? query.Columns.Select(c => new ColumnMetadata 
        { Name = c.Name, Type = c.Type }).ToList() : []
        , Data = success && query is not null ? query.Rows : []
        , Compilation = sql is null ? null : new CompilationResult 
        { Success = success, Sql = sql.Sql, Parameters = sql.Parameters }, 
        ValidationResults = results, ExecutionId=executionId,
        ArtifactKey=artifactKey, QueryFingerprint=queryFingerprint, 
        SemanticModelVersion=semanticModelVersion, 
        Metadata = new ExecutionMetadata { TotalDurationMs = duration, ErrorCount = results.Sum(r => r.Errors.Count), 
            WarningCount = results.Sum(r => r.Warnings.Count), 
            ExecutedStages = results.Select(r => r.Stage).ToList() } };
}

public sealed class ReportQueryServiceOptions
{
    public string StorageMode { get; set; } = "Local";
}

internal sealed record CompiledQueryArtifacts(
    SqlCompilationResult Sql,
    List<QueryColumn> ExpectedColumns);
