namespace Report.QueryEngine.Binding;

public sealed class ResolvedFilter
{
    public string FieldId { get; init; } = "";
    public string PhysicalTable { get; init; } = "";
    public string PhysicalColumn { get; init; } = "";
    public string PhysicalExpression { get; init; } = "";
    public string DataType { get; init; } = "";
    public string Operator { get; init; } = "";
    public object? Value { get; init; }
    public string TargetType { get; init; } = "dimension";
}
