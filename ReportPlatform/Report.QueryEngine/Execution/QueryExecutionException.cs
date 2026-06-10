namespace Report.QueryEngine.Execution;

public sealed class QueryExecutionException : Exception
{
    public QueryExecutionException(string message, string sql, Exception? innerException = null)
        : base(message, innerException)
    {
        Sql = sql;
    }

    public string Sql { get; }
}
