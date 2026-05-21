using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Binding;

namespace Report.QueryEngine.Expressions.Validation;

public sealed class ExpressionScopeResolver(SemanticFunctionRegistry functions)
{
    public ExpressionScope Resolve(ExpressionNode ast) => ContainsAggregateShape(ast) ? ExpressionScope.Aggregate : ExpressionScope.Row;

    private bool ContainsAggregateShape(ExpressionNode node) => node switch
    {
        MetricReferenceNode => true,
        FunctionCallNode call => functions.IsAggregate(call.FunctionName) || call.Arguments.Any(ContainsAggregateShape),
        BinaryExpressionNode binary => ContainsAggregateShape(binary.Left) || ContainsAggregateShape(binary.Right),
        UnaryExpressionNode unary => ContainsAggregateShape(unary.Operand),
        _ => false
    };
}
