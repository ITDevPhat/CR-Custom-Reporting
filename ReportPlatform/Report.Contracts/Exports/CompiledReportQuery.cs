using Report.Contracts.Results;

namespace Report.Contracts.Exports;

public sealed class CompiledReportQuery
{
    public string ConnectionId { get; init; } = "";
    public string Sql { get; init; } = "";
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<QueryColumn> ExpectedColumns { get; init; } = [];
    public IReadOnlyList<QueryValidationMessage> Warnings { get; init; } = [];
}
