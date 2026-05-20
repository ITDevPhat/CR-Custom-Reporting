using Report.Contracts.Semantic;

namespace Report.Metadata.Stores;

public interface IReportRegistry
{
    ReportDefinition Save(SaveReportDefinitionRequest request, string? reportId = null);
    ReportDefinition? Find(string reportId);
    List<ReportDefinition> List(string? datasetId);
    bool Delete(string reportId);
}
