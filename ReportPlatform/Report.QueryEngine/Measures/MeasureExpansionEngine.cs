using Report.Metadata.Models;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;

namespace Report.QueryEngine.Measures;

public sealed class MeasureExpansionEngine
{
    public List<ExpandedMeasure> Expand(EvaluationContext context, SemanticModel model)
    {
        return context.Measures.Select(metric =>
        {
            return new ExpandedMeasure
            {
                MetricId = metric.MetricId,
                Alias = SqlIdentifier.SafeAlias(metric.DisplayName),
                BaseTableId = metric.BaseTableId,
                SqlExpression = SemanticExpressionCompiler.CompileMetricFormula(metric.Formula, model)
            };
        }).ToList();
    }
}
