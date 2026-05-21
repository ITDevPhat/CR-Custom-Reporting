namespace Report.Metadata.Models;

public enum SemanticObjectType
{
    PhysicalColumn,
    CalculatedColumn,
    CalculatedMeasure,
    Metric,
    ExpressionFragment
}

public enum ExpressionScope
{
    Row,
    Aggregate,
    Filter,
    Window,
    Relationship
}

public sealed class SemanticObject
{
    public string Id { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string? TableId { get; set; }
    public string DisplayName { get; set; } = "";
    public SemanticObjectType ObjectType { get; set; }
    public ExpressionScope Scope { get; set; }
    public string Expression { get; set; } = "";
    public string DataType { get; set; } = "";
    public string? Format { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public bool IsHidden { get; set; }
    public bool IsDraggable { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
