namespace Report.Metadata.Models;

public sealed class SemanticModel
{
    public string DatasetId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public List<SemanticTable> Tables { get; init; } = [];
    public List<SemanticField> Fields { get; init; } = [];
    public List<SemanticMetric> Metrics { get; init; } = [];
    public List<SemanticObject> SemanticObjects { get; init; } = [];
    public List<SemanticRelationship> Relationships { get; init; } = [];
}
