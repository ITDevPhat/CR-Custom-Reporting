using Report.Metadata.Models;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;
using Report.QueryEngine.Expressions.Validation;
using Report.QueryEngine.Validation;

namespace Report.QueryEngine.Measures;

public sealed class MeasureExpansionEngine
{
    public List<ExpandedMeasure> Expand(EvaluationContext context, SemanticModel model)
    {
        return context.Measures.Select(metric =>
        {
            string sqlExpression;
            try
            {
                sqlExpression = SemanticExpressionCompiler.CompileMetricFormula(metric.Formula, model);
            }
            catch (ExpressionValidationException ex)
            {
                throw new SemanticQueryValidationException(new Dictionary<string, string[]>
                {
                    ["errorCode"] = [ex.Code],
                    [$"metric.{metric.MetricId}"] = [ex.Message]
                });
            }

            return new ExpandedMeasure
            {
                MetricId = metric.MetricId,
                Alias = SqlIdentifier.SafeAlias(metric.DisplayName),
                BaseTableId = metric.BaseTableId,
                SqlExpression = sqlExpression
            };
        }).ToList();
    }
}
