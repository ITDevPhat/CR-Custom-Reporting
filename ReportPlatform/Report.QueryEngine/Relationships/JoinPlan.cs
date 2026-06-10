namespace Report.QueryEngine.Relationships;

public sealed class JoinPlan
{
    public string BaseTableId { get; init; } = "";
    public List<JoinDef> Joins { get; init; } = [];
}

public sealed class JoinDef
{
    public string RelationshipId { get; init; } = "";
    public string FromTableId { get; init; } = "";
    public string ToTableId { get; init; } = "";
    public string JoinType { get; init; } = "";
    public string FromColumn { get; init; } = "";
    public string ToColumn { get; init; } = "";
}
