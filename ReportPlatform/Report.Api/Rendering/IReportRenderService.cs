using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public interface IReportRenderService
{
    Task<RenderedReportResult> RenderAsync(RenderReportRequest request, CancellationToken ct);
}
