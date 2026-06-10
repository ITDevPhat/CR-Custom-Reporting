using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Validation;

namespace Report.QueryEngine.Expressions.Binding;

public sealed class ExpressionSemanticBinder(SemanticFunctionRegistry functions)
{
    public BoundExpression Bind(ExpressionNode ast, SemanticModel model)
    {
        var fields = new List<SemanticField>();
        var metrics = new List<SemanticMetric>();
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Walk(ast);

        return new BoundExpression
        {
            Ast = ast,
            Dependencies = dependencies.ToList(),
            ReferencedFields = fields,
            ReferencedMetrics = metrics,
            BaseTableIds = baseTables.ToList()
        };

        void Walk(ExpressionNode node)
        {
            switch (node)
            {
                case FieldReferenceNode fieldRef:
                    var field = model.Fields.FirstOrDefault(f => f.FieldId.Equals(fieldRef.FieldId, StringComparison.OrdinalIgnoreCase));
                    if (field is null)
                    {
                        throw new ExpressionValidationException("UNKNOWN_FIELD_REFERENCE", $"Unknown field reference: {fieldRef.FieldId}.");
                    }
                    if (field.IsHidden)
                    {
                        throw new ExpressionValidationException("UNKNOWN_FIELD_REFERENCE", $"Field reference is hidden: {fieldRef.FieldId}.");
                    }
                    dependencies.Add(field.FieldId);
                    fields.Add(field);
                    baseTables.Add(field.TableId);
                    break;
                case MetricReferenceNode metricRef:
                    var metric = model.Metrics.FirstOrDefault(m => m.MetricId.Equals(metricRef.MetricId, StringComparison.OrdinalIgnoreCase));
                    if (metric is null)
                    {
                        throw new ExpressionValidationException("UNKNOWN_METRIC_REFERENCE", $"Unknown metric reference: {metricRef.MetricId}.");
                    }
                    if (metric.IsHidden)
                    {
                        throw new ExpressionValidationException("UNKNOWN_METRIC_REFERENCE", $"Metric reference is hidden: {metricRef.MetricId}.");
                    }
                    dependencies.Add(metric.MetricId);
                    metrics.Add(metric);
                    if (!string.IsNullOrWhiteSpace(metric.BaseTableId)) baseTables.Add(metric.BaseTableId);
                    break;
                case BinaryExpressionNode binary:
                    Walk(binary.Left);
                    Walk(binary.Right);
                    break;
                case UnaryExpressionNode unary:
                    Walk(unary.Operand);
                    break;
                case FunctionCallNode call:
                    if (!functions.TryGet(call.FunctionName, out var definition))
                    {
                        throw new ExpressionValidationException("INVALID_FUNCTION", $"Invalid function: {call.FunctionName}.");
                    }
                    if (call.Arguments.Count < definition.MinArguments || definition.MaxArguments is int max && call.Arguments.Count > max)
                    {
                        throw new ExpressionValidationException("INVALID_FUNCTION", $"{call.FunctionName} expects {FormatArity(definition)} argument(s).");
                    }
                    foreach (var arg in call.Arguments) Walk(arg);
                    break;
            }
        }
    }

    private static string FormatArity(SemanticFunctionDefinition definition) =>
        definition.MaxArguments is null || definition.MaxArguments != definition.MinArguments
            ? $"{definition.MinArguments}+"
            : definition.MinArguments.ToString();
}
