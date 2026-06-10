namespace Report.QueryEngine.Binding;

public sealed class ResolvedSort
{
    public string FieldId { get; init; } = "";
    public string TargetType { get; init; } = "dimension";
    public string PhysicalExpression { get; init; } = "";
    public string Alias { get; init; } = "";
    public string Direction { get; init; } = "ASC";
}
