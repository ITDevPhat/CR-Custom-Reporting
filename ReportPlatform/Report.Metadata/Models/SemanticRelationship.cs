namespace Report.Metadata.Models;

public sealed class SemanticRelationship
{
    public string RelationshipId { get; init; } = "";
    public string DatasetId { get; init; } = "";
    public string FromTableId { get; init; } = "";
    public string FromColumn { get; init; } = "";
    public string ToTableId { get; init; } = "";
    public string ToColumn { get; init; } = "";
    public string JoinType { get; init; } = "INNER";
    public string Cardinality { get; init; } = "N:1";
    public string CrossFilterDirection { get; init; } = "single";
    public bool IsActive { get; init; } = true;
    public bool IsPrimary { get; init; } = true;
    public string Source { get; init; } = "manual";
    public decimal Confidence { get; init; } = 1.0m;
    public string Status { get; init; } = "active";
    public string? Warning { get; init; }
}
