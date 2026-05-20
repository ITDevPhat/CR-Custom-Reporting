namespace Report.Contracts.Results;

public sealed class QueryResult
{
    public string Status { get; init; } = "success";
    public List<QueryColumn> Columns { get; init; } = [];
    public List<Dictionary<string, object?>> Rows { get; init; } = [];
    public QueryExecutionMetadata Metadata { get; init; } = new();
}

public sealed class QueryColumn
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "string";
}

public sealed class QueryExecutionMetadata
{
    public int RowCount { get; init; }
    public long ExecutionMs { get; init; }
    public string Sql { get; init; } = "";
    public Dictionary<string, object?> Parameters { get; init; } = [];
    public List<QueryValidationMessage> Warnings { get; init; } = [];
}

public sealed class QueryExecutionError
{
    public string Status { get; init; } = "error";
    public string ErrorCode { get; init; } = "QUERY_EXECUTION_FAILED";
    public string Message { get; init; } = "";
    public string Sql { get; init; } = "";
    public object? Details { get; init; }
}

public sealed class QueryValidationMessage
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}
