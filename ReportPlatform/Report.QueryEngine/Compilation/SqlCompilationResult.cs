namespace Report.QueryEngine.Compilation;

public sealed class SqlCompilationResult
{
    public string Sql { get; init; } = "";
    public Dictionary<string, object?> Parameters { get; init; } = [];
}