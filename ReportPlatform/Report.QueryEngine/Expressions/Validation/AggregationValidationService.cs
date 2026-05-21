using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Binding;

namespace Report.QueryEngine.Expressions.Validation;

public sealed class AggregationValidationService(SemanticFunctionRegistry functions)
{
    public void Validate(ExpressionNode ast, ExpressionScope scope, string targetKind)
    {
        if (targetKind.Equals("calculated_column", StringComparison.OrdinalIgnoreCase) && scope == ExpressionScope.Aggregate)
        {
            throw new ExpressionValidationException("AGGREGATE_SCOPE_CONFLICT", "Calculated columns cannot reference metrics or aggregate functions.");
        }

        Walk(ast, insideAggregate: false, scope);
    }

    private void Walk(ExpressionNode node, bool insideAggregate, ExpressionScope scope)
    {
        switch (node)
        {
            case FieldReferenceNode field when scope == ExpressionScope.Aggregate && !insideAggregate:
                throw new ExpressionValidationException(
                    "AGGREGATE_SCOPE_CONFLICT",
                    $"Field [{field.FieldId}] is row-level. Use an aggregate function such as SUM([{field.FieldId}]) inside calculated measures.");
            case FunctionCallNode call:
                var isAggregate = functions.IsAggregate(call.FunctionName);
                if (isAggregate && insideAggregate)
                {
                    throw new ExpressionValidationException("AGGREGATE_SCOPE_CONFLICT", "Nested aggregate functions are not allowed.");
                }
                if (isAggregate && call.Arguments.Any(arg => arg is MetricReferenceNode))
                {
                    throw new ExpressionValidationException("AGGREGATE_SCOPE_CONFLICT", "Aggregate functions cannot aggregate metric references.");
                }
                foreach (var arg in call.Arguments) Walk(arg, insideAggregate || isAggregate, scope);
                break;
            case BinaryExpressionNode binary:
                Walk(binary.Left, insideAggregate, scope);
                Walk(binary.Right, insideAggregate, scope);
                break;
            case UnaryExpressionNode unary:
                Walk(unary.Operand, insideAggregate, scope);
                break;
        }
    }
}
