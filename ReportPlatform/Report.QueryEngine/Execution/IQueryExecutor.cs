using Report.Contracts.Results;
using Report.QueryEngine.Compilation;

namespace Report.QueryEngine.Execution;

public interface IQueryExecutor
{
    Task<QueryResult> ExecuteAsync(
        string connectionId,
        SqlCompilationResult compilation,
        IReadOnlyList<QueryColumn> expectedColumns,
        CancellationToken ct);
}
