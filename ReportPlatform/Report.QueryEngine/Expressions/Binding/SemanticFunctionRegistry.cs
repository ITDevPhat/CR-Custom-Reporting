namespace Report.QueryEngine.Expressions.Binding;

public sealed record SemanticFunctionDefinition(
    string Name,
    int MinArguments,
    int? MaxArguments,
    string Category,
    bool IsAggregate);

public sealed class SemanticFunctionRegistry
{
    private readonly Dictionary<string, SemanticFunctionDefinition> _functions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUM"] = new("SUM", 1, 1, "Aggregate", true),
        ["AVG"] = new("AVG", 1, 1, "Aggregate", true),
        ["MIN"] = new("MIN", 1, 1, "Aggregate", true),
        ["MAX"] = new("MAX", 1, 1, "Aggregate", true),
        ["COUNT"] = new("COUNT", 1, 1, "Aggregate", true),
        ["COUNT_DISTINCT"] = new("COUNT_DISTINCT", 1, 1, "Aggregate", true),
        ["ROUND"] = new("ROUND", 2, 2, "Scalar", false),
        ["ABS"] = new("ABS", 1, 1, "Scalar", false),
        ["CEILING"] = new("CEILING", 1, 1, "Scalar", false),
        ["FLOOR"] = new("FLOOR", 1, 1, "Scalar", false),
        ["NULLIF"] = new("NULLIF", 2, 2, "Null", false),
        ["COALESCE"] = new("COALESCE", 2, null, "Null", false),
        ["ISNULL"] = new("ISNULL", 2, 2, "Null", false),
        ["IF"] = new("IF", 3, 3, "Conditional", false),
        ["YEAR"] = new("YEAR", 1, 1, "Date", false),
        ["MONTH"] = new("MONTH", 1, 1, "Date", false),
        ["DAY"] = new("DAY", 1, 1, "Date", false),
        ["CONCAT"] = new("CONCAT", 2, null, "String", false),
        ["LEFT"] = new("LEFT", 2, 2, "String", false),
        ["RIGHT"] = new("RIGHT", 2, 2, "String", false),
        ["LEN"] = new("LEN", 1, 1, "String", false),
        ["UPPER"] = new("UPPER", 1, 1, "String", false),
        ["LOWER"] = new("LOWER", 1, 1, "String", false)
    };

    public bool TryGet(string name, out SemanticFunctionDefinition definition) =>
        _functions.TryGetValue(name, out definition!);

    public bool IsAggregate(string name) => TryGet(name, out var definition) && definition.IsAggregate;
}
