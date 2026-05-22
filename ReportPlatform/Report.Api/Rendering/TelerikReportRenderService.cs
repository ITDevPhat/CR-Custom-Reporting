using Report.Contracts.Exports;
using Report.QueryEngine.Services;
using Telerik.Reporting;
using Telerik.Reporting.Processing;

namespace Report.Api.Rendering;

public sealed class TelerikReportRenderService : IReportRenderService
{
    private readonly ReportQueryService _queryService;
    private readonly ITelerikReportFactory _factory;

    public TelerikReportRenderService(ReportQueryService queryService, ITelerikReportFactory factory)
    {
        _queryService = queryService;
        _factory = factory;
    }

    public async Task<RenderedReportResult> RenderAsync(RenderReportRequest request, CancellationToken ct)
    {
        var format = Normalize(request.Format);
        var result = await _queryService.ExecuteAsync(request.Query, ct);
        var report = _factory.CreateTableReport(result, request.ReportTitle);

        var source = new InstanceReportSource { ReportDocument = report };
        var processor = new ReportProcessor();
        var rendered = processor.RenderReport(format, source, null);

        return new RenderedReportResult
        {
            Bytes = rendered.DocumentBytes,
            ContentType = format == "PDF"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"report-{DateTime.UtcNow:yyyyMMddHHmmss}.{(format == "PDF" ? "pdf" : "xlsx")}",
        };
    }

    private static string Normalize(string format)
    {
        var normalized = format.Trim().ToUpperInvariant();
        return normalized switch
        {
            "PDF" => "PDF",
            "XLSX" => "XLSX",
            _ => throw new ArgumentException("Unsupported export format. Supported formats: PDF, XLSX.", nameof(format)),
        };
    }
}
