namespace Report.Metadata.Models;

public sealed class SemanticMetric
{
    public string MetricId { get; init; } = "";
    public string DatasetId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Formula { get; init; } = "";
    public string BaseTableId { get; init; } = "";
    public string AggregationBehavior { get; init; } = "additive";
    public string DataType { get; init; } = "decimal";
    public string Format { get; init; } = "general";
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
}
