using Report.Contracts.Artifacts;

namespace Report.Metadata.Stores;

public interface IReportExecutionRegistry
{
    void Save(ReportExecutionRecord record);
    ReportExecutionRecord? Find(string executionId);
}
