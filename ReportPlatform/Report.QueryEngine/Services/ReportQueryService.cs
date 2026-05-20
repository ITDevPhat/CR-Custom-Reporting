using Report.Contracts.Requests;
using Report.Contracts.Results;
using Report.Contracts.Semantic;
using Report.Metadata.Stores;
using Report.QueryEngine.Binding;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;
using Report.QueryEngine.Execution;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Planning;
using Report.QueryEngine.Relationships;
using Report.QueryEngine.Validation;

namespace Report.QueryEngine.Services;

public sealed class ReportQueryService
{
    private readonly ISemanticModelStore _modelStore;
    private readonly SemanticModelBinder _binder;
    private readonly EvaluationContextBuilder _contextBuilder;
    private readonly MeasureExpansionEngine _measureEngine;
    private readonly RelationshipTraversalEngine _relationshipEngine;
    private readonly LogicalPlanBuilder _planBuilder;
    private readonly SqlCompiler _sqlCompiler;
    private readonly IQueryExecutor _queryExecutor;
    private readonly GrainValidationService _grainValidationService;

    public ReportQueryService(
        ISemanticModelStore modelStore,
        SemanticModelBinder binder,
        EvaluationContextBuilder contextBuilder,
        MeasureExpansionEngine measureEngine,
        RelationshipTraversalEngine relationshipEngine,
        LogicalPlanBuilder planBuilder,
        SqlCompiler sqlCompiler,
        IQueryExecutor queryExecutor,
        GrainValidationService grainValidationService)
    {
        _modelStore = modelStore;
        _binder = binder;
        _contextBuilder = contextBuilder;
        _measureEngine = measureEngine;
        _relationshipEngine = relationshipEngine;
        _planBuilder = planBuilder;
        _sqlCompiler = sqlCompiler;
        _queryExecutor = queryExecutor;
        _grainValidationService = grainValidationService;
    }

    public async Task<object> CompileAsync(VisualQueryRequest request, CancellationToken ct)
    {
        var result = await BuildCompilationAsync(request, ct);

        return new
        {
            result.Bound,
            result.Context,
            result.Measures,
            result.JoinPlan,
            result.GrainValidation,
            logicalPlan = result.LogicalPlan,
            result.Sql
        };
    }

    public async Task<QueryResult> ExecuteAsync(VisualQueryRequest request, CancellationToken ct)
    {
        var result = await BuildCompilationAsync(request, ct);
        var expectedColumns = BuildExpectedColumns(result.LogicalPlan);

        var queryResult = await _queryExecutor.ExecuteAsync(
            request.ConnectionId,
            result.Sql,
            expectedColumns,
            ct);

        queryResult.Metadata.Warnings.AddRange(result.GrainValidation.Warnings.Select(w => new QueryValidationMessage
        {
            Code = w.Code,
            Message = w.Message
        }));

        return queryResult;
    }

    private static List<QueryColumn> BuildExpectedColumns(LogicalQueryPlan logicalPlan)
    {
        return logicalPlan.Select
            .Select(item => new QueryColumn
            {
                Name = item.Alias,
                Type = item.Role == "metric" ? "decimal" : InferColumnType(item.Alias)
            })
            .ToList();
    }

    private async Task<QueryCompilationPipelineResult> BuildCompilationAsync(
        VisualQueryRequest request,
        CancellationToken ct)
    {
        var model = await _modelStore.LoadAsync(request.DatasetId, ct);

        var bound = _binder.Bind(request, model);
        var context = _contextBuilder.Build(bound);
        var measures = _measureEngine.Expand(context, model);
        var joinPlan = _relationshipEngine.Build(context, measures, model);
        var grainValidation = _grainValidationService.Validate(context, measures, joinPlan, model);
        if (!grainValidation.Valid)
        {
            throw new SemanticQueryValidationException(grainValidation.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()));
        }
        var lqp = _planBuilder.Build(context, measures, joinPlan, model);
        var sql = _sqlCompiler.Compile(lqp);

        return new QueryCompilationPipelineResult(
            bound,
            context,
            measures,
            joinPlan,
            grainValidation,
            lqp,
            sql);
    }

    private static string InferColumnType(string columnName)
    {
        return columnName.Contains("Year", StringComparison.OrdinalIgnoreCase)
            ? "number"
            : "string";
    }

    private sealed record QueryCompilationPipelineResult(
        BoundSemanticQuery Bound,
        EvaluationContext Context,
        List<ExpandedMeasure> Measures,
        JoinPlan JoinPlan,
        GrainValidationResult GrainValidation,
        LogicalQueryPlan LogicalPlan,
        SqlCompilationResult Sql);
}
