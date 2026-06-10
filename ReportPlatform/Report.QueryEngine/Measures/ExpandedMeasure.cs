namespace Report.QueryEngine.Measures;

public sealed class ExpandedMeasure
{
    public string MetricId { get; init; } = "";
    public string Alias { get; init; } = "";
    public string BaseTableId { get; init; } = "";
    public string SqlExpression { get; init; } = "";
}