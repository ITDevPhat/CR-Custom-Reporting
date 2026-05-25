using Report.Contracts.Artifacts;

namespace Report.Metadata.Stores;

public sealed class InMemoryReportExecutionRegistry : IReportExecutionRegistry
{
    private readonly Dictionary<string, ReportExecutionRecord> _records = new();
    public void Save(ReportExecutionRecord record) => _records[record.ExecutionId] = record;
    public ReportExecutionRecord? Find(string executionId) => _records.TryGetValue(executionId, out var v) ? v : null;
}
