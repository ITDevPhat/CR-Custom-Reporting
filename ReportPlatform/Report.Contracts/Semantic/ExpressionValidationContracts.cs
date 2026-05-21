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
    public string ReturnType { get; init; } = "nvarchar";
    public List<string> Dependencies { get; init; } = [];
    public string CompiledSqlPreview { get; init; } = "";
    public List<string> Errors { get; init; } = [];
}
