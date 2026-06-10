using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public interface IRawCsvExportService
{
    Task<RenderedReportResult> ExportAsync(string executionId, CancellationToken ct);
}
