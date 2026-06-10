namespace Report.QueryEngine.Planning;

public sealed class LogicalQueryPlan
{
    public string BaseTableId { get; init; } = "";
    public Dictionary<string, string> TableExpressions { get; init; } = [];
    public Dictionary<string, string> Aliases { get; init; } = [];
    public List<SelectItem> Select { get; init; } = [];
    public List<JoinItem> Joins { get; init; } = [];
    public List<FilterItem> Where { get; init; } = [];
    public List<FilterItem> Having { get; init; } = [];
    public List<string> GroupBy { get; init; } = [];
    public List<OrderItem> OrderBy { get; init; } = [];
    public Dictionary<string, object?> ParameterBindings { get; init; } = [];
    public int Limit { get; init; }
    public int Offset { get; init; }
}

public sealed class SelectItem
{
    public string Expression { get; init; } = "";
    public string Alias { get; init; } = "";
    public string Role { get; init; } = "";
}

public sealed class JoinItem
{
    public string JoinType { get; init; } = "";
    public string TableId { get; init; } = "";
    public string Alias { get; init; } = "";
    public string Condition { get; init; } = "";
}

public sealed class FilterItem
{
    public string Expression { get; init; } = "";
    public string Operator { get; init; } = "";
    public object? Value { get; init; }
}

public sealed class OrderItem
{
    public string Expression { get; init; } = "";
    public bool IsAlias { get; init; }
    public string Direction { get; init; } = "";
}
