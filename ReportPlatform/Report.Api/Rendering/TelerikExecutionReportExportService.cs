using System.Collections;
using Report.Contracts.Exports;
using Telerik.Reporting.Services;

namespace Report.Api.Rendering;

public sealed class TelerikExecutionReportExportService : IExecutionReportExportService
{
    private static readonly IReadOnlyDictionary<string, ExportFormatInfo> Formats =
        new Dictionary<string, ExportFormatInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["pdf"] = new("PDF", "application/pdf", "pdf"),
            ["xlsx"] = new(
                "XLSX",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xlsx"),
            ["docx"] = new(
                "DOCX",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "docx"),
            ["pptx"] = new(
                "PPTX",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "pptx"),
            ["rtf"] = new("RTF", "application/rtf", "rtf"),
            ["tiff"] = new(
                "IMAGE",
                "image/tiff",
                "tiff",
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OutputFormat"] = "TIFF"
                })
    };

    private readonly IReportSourceResolver _reportSourceResolver;
    private readonly IRawCsvExportService _rawCsvExportService;
    private readonly ILogger<TelerikExecutionReportExportService> _logger;

    public TelerikExecutionReportExportService(
        IReportSourceResolver reportSourceResolver,
        IRawCsvExportService rawCsvExportService,
        ILogger<TelerikExecutionReportExportService> logger)
    {
        _reportSourceResolver = reportSourceResolver;
        _rawCsvExportService = rawCsvExportService;
        _logger = logger;
    }

    public Task<RenderedReportResult> ExportAsync(
        string executionId,
        string format,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ReportExportException("Execution id is required.", 400);
        }

        if (string.Equals(format.Trim(), "csv", StringComparison.OrdinalIgnoreCase))
        {
            return _rawCsvExportService.ExportAsync(executionId.Trim(), ct);
        }

        var formatInfo = NormalizeFormat(format);
        var reportUri = $"execution:{executionId.Trim()}";

        try
        {
            ct.ThrowIfCancellationRequested();

            var reportSource = _reportSourceResolver.Resolve(
                reportUri,
                default,
                new Dictionary<string, object>
                {
                    [SnapshotReportSourceResolver.RenderModeParameterName] = "Export"
                });

            var processor = new Telerik.Reporting.Processing.ReportProcessor();
            var rendered = processor.RenderReport(
                formatInfo.TelerikFormat,
                reportSource,
                formatInfo.CreateDeviceInfo())
                ?? throw new ReportExportException(
                    $"Telerik renderer returned null output for '{formatInfo.TelerikFormat}'.");

            var bytes = rendered.DocumentBytes
                ?? throw new ReportExportException(
                    $"Telerik renderer produced null document bytes for '{formatInfo.TelerikFormat}'.");

            if (bytes.Length == 0)
            {
                throw new ReportExportException(
                    $"Telerik renderer produced an empty '{formatInfo.TelerikFormat}' export.");
            }

            _logger.LogInformation(
                "Execution report export completed. ExecutionId={ExecutionId} Format={Format} ReportUri={ReportUri} Bytes={Bytes}",
                executionId,
                formatInfo.TelerikFormat,
                reportUri,
                bytes.Length);

            return Task.FromResult(new RenderedReportResult
            {
                Bytes = bytes,
                ContentType = formatInfo.ContentType,
                FileName = $"report-{executionId.Trim()}.{formatInfo.Extension}"
            });
        }
        catch (ReportExportException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telerik execution export failed. ExecutionId={ExecutionId} Format={Format} ReportUri={ReportUri} InnerException={InnerException}",
                executionId,
                format,
                reportUri,
                ex.InnerException?.Message);

            throw new ReportExportException(
                $"Telerik export failed for execution '{executionId}' and format '{formatInfo.TelerikFormat}'.",
                500,
                ex);
        }
    }

    private static ExportFormatInfo NormalizeFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ReportExportException("Export format is required.", 400);
        }

        var key = format.Trim();
        if (Formats.TryGetValue(key, out var formatInfo))
        {
            return formatInfo;
        }

        throw new ReportExportException(
            "Unsupported export format. Supported formats: PDF, XLSX, CSV, DOCX, PPTX, RTF, TIFF. CSV is exported through RawCsvExportService.",
            400);
    }

    private sealed record ExportFormatInfo(
        string TelerikFormat,
        string ContentType,
        string Extension,
        IReadOnlyDictionary<string, object>? DeviceInfo = null)
    {
        public Hashtable CreateDeviceInfo()
        {
            var deviceInfo = new Hashtable(StringComparer.OrdinalIgnoreCase);

            if (DeviceInfo is null)
            {
                return deviceInfo;
            }

            foreach (var item in DeviceInfo)
            {
                deviceInfo[item.Key] = item.Value;
            }

            return deviceInfo;
        }
    }
}
