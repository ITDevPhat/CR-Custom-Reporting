using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public interface IExecutionReportExportService
{
    Task<RenderedReportResult> ExportAsync(
        string executionId,
        string format,
        CancellationToken ct);
}
