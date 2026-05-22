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

        byte[] bytes;
        try
        {
            var source = new InstanceReportSource { ReportDocument = report };
            var processor = new ReportProcessor();
            var rendered = processor.RenderReport(format, source, null)
                ?? throw new ReportExportException($"Telerik renderer returned null output for '{format}'.");
            bytes = rendered.DocumentBytes
                ?? throw new ReportExportException($"Telerik renderer produced null document bytes for '{format}'.");
        }
        catch (ReportExportException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ReportExportException($"Telerik render failed for format '{format}': {ex.Message}", 500, ex);
        }

        ValidateBinaryPayload(format, bytes);
        return BuildResult(format, bytes, result.Rows.Count, result.Columns.Count);
    }

    private RenderedReportResult BuildResult(string format, byte[] bytes, int rowCount, int columnCount)
    {
        var contentType = format switch
        {
            "PDF" => "application/pdf",
            "XLSX" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "CSV" => "text/csv; charset=utf-8",
            _ => throw new ReportExportException($"Unsupported format '{format}'.", 400),
        };

        var extension = format.ToLowerInvariant();
        var fileName = $"report-{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
        _logger.LogInformation(
            "Export render completed. Format={Format} Rows={Rows} Columns={Columns} Bytes={Bytes} ContentType={ContentType} FileName={FileName} Magic={Magic}",
            format,
            rowCount,
            columnCount,
            bytes.Length,
            contentType,
            fileName,
            GetMagicBytes(bytes));

        return new RenderedReportResult
        {
            Bytes = bytes,
            ContentType = contentType,
            FileName = fileName,
        };
    }

    private void EnsureRenderingExtensionAvailable(string format)
    {
        var available = ReportProcessor.ListRenderingExtensions().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!available.Contains(format))
        {
            throw new ReportExportException($"Telerik rendering extension '{format}' is not available.");
        }
    }

    private static void ValidateBinaryPayload(string format, byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ReportExportException($"Export payload for '{format}' is empty.");
        }

        if (format == "PDF" && (bytes.Length <= 100 || !Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4)).StartsWith("%PDF", StringComparison.Ordinal)))
        {
            throw new ReportExportException("Rendered PDF bytes are invalid or corrupted.");
        }

        if (format == "XLSX" && (bytes.Length <= 100 || bytes[0] != 0x50 || bytes[1] != 0x4B))
        {
            throw new ReportExportException("Rendered XLSX bytes are invalid or corrupted.");
        }
    }

    private static string GetMagicBytes(byte[] bytes)
    {
        return string.Join('-', bytes.Take(8).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string Normalize(string format)
    {
        var normalized = format.Trim().ToUpperInvariant();
        return normalized switch
        {
            "PDF" => "PDF",
            "XLSX" => "XLSX",
            "CSV" => "CSV",
            _ => throw new ReportExportException("Unsupported export format. Supported formats: PDF, XLSX, CSV.", 400),
        };
    }
}
