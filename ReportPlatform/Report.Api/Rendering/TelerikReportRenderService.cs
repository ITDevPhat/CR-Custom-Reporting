using Report.Contracts.Exports;
using Report.QueryEngine.Services;
using Telerik.Reporting;
using Telerik.Reporting.Processing;
using Microsoft.Extensions.Logging;

namespace Report.Api.Rendering;

public sealed class TelerikReportRenderService : IReportRenderService
{
    private readonly ReportQueryService _queryService;
    private readonly ITelerikReportFactory _factory;
    private readonly ILogger<TelerikReportRenderService> _logger;

    public TelerikReportRenderService(
        ReportQueryService queryService,
        ITelerikReportFactory factory,
        ILogger<TelerikReportRenderService> logger)
    {
        _queryService = queryService;
        _factory = factory;
        _logger = logger;
    }

    public async Task<RenderedReportResult> RenderAsync(RenderReportRequest request, CancellationToken ct)
    {
        var format = Normalize(request.Format);
        var result = await _queryService.ExecuteAsync(request.Query, ct);
        _logger.LogInformation(
            "Export query returned {ColumnCount} columns and {RowCount} rows.",
            result.Columns.Count,
            result.Rows.Count);
        _logger.LogInformation("Generated SQL: {Sql}", result.Metadata.Sql);
        _logger.LogInformation("Parameters: {@Parameters}", result.Metadata.Parameters);

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
