namespace Report.Metadata.Models;

public sealed class SemanticTable
{
    public string TableId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string TableType { get; init; } = "";
    public string Grain { get; init; } = "";
    public string PhysicalSchema { get; init; } = "";
    public string PhysicalTable { get; init; } = "";
}
