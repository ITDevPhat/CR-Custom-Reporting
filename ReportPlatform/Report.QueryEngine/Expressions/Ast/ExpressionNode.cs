namespace Report.QueryEngine.Expressions.Ast;

public abstract record ExpressionNode;

public sealed record FieldReferenceNode(string FieldId) : ExpressionNode;

public sealed record MetricReferenceNode(string MetricId) : ExpressionNode;

public sealed record NumberLiteralNode(decimal Value) : ExpressionNode;

public sealed record StringLiteralNode(string Value) : ExpressionNode;

public sealed record BooleanLiteralNode(bool Value) : ExpressionNode;

public sealed record NullLiteralNode() : ExpressionNode;

public sealed record BinaryExpressionNode(
    ExpressionNode Left,
    string Operator,
    ExpressionNode Right) : ExpressionNode;

public sealed record UnaryExpressionNode(
    string Operator,
    ExpressionNode Operand) : ExpressionNode;

public sealed record FunctionCallNode(
    string FunctionName,
    IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;
