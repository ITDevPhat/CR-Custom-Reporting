using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Ast;

namespace Report.QueryEngine.Expressions.Validation;

public sealed record ExpressionValidationIssue(string Code, string Message);

public sealed class ExpressionValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class BoundExpression
{
    public required ExpressionNode Ast { get; init; }
    public List<string> Dependencies { get; init; } = [];
    public List<SemanticField> ReferencedFields { get; init; } = [];
    public List<SemanticMetric> ReferencedMetrics { get; init; } = [];
    public List<string> BaseTableIds { get; init; } = [];
}

public sealed class SemanticExpressionValidationResult
{
    public bool Valid { get; init; }
    public string DetectedKind { get; init; } = "calculated_column";
    public ExpressionScope DetectedScope { get; init; } = ExpressionScope.Row;
    public string DataType { get; init; } = "null";
    public List<string> Dependencies { get; init; } = [];
    public string? SqlPreview { get; init; }
    public List<ExpressionValidationIssue> Errors { get; init; } = [];
    public List<ExpressionValidationIssue> Warnings { get; init; } = [];
    public BoundExpression? BoundExpression { get; init; }
}
