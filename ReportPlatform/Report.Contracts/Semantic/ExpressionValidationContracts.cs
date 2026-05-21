namespace Report.Contracts.Semantic;

public sealed class ExpressionValidationRequest
{
    public string Expression { get; init; } = "";
    public string TargetKind { get; init; } = "auto";
}

public sealed class ExpressionValidationResponse
{
    public bool Valid { get; init; }
    public string DetectedKind { get; init; } = "calculated_column";
    public string DetectedScope { get; init; } = "Row";
    public string DataType { get; init; } = "nvarchar";
    public string ReturnType { get; init; } = "nvarchar";
    public List<string> Dependencies { get; init; } = [];
    public string? SqlPreview { get; init; }
    public string CompiledSqlPreview { get; init; } = "";
    public List<ExpressionValidationMessage> Errors { get; init; } = [];
    public List<ExpressionValidationMessage> Warnings { get; init; } = [];
}

public sealed class ExpressionValidationMessage
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class CreateCalculatedObjectRequest
{
    public string DisplayName { get; init; } = "";
    public string Expression { get; init; } = "";
    public string TargetKind { get; init; } = "auto";
    public string? Format { get; init; }
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
}

public sealed class CreateCalculatedObjectResponse
{
    public string Id { get; init; } = "";
    public string DetectedKind { get; init; } = "";
    public string Scope { get; init; } = "";
    public string DataType { get; init; } = "";
    public List<string> Dependencies { get; init; } = [];
}
