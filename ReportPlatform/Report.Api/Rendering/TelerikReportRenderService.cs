using Microsoft.Extensions.Logging;
using Report.Contracts.Exports;
using Report.Contracts.Requests;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Execution;
using Report.QueryEngine.Services;
using System.Globalization;
using System.Text;

namespace Report.Api.Rendering;

public sealed class TelerikReportRenderService : IReportRenderService
{
    private readonly ReportQueryService _queryService;
    private readonly ITelerikReportFactory _factory;
    private readonly IReportConnectionStringResolver _connectionStringResolver;
    private readonly ILogger<TelerikReportRenderService> _logger;

    public TelerikReportRenderService(
    ReportQueryService queryService,
    ITelerikReportFactory factory,
    IReportConnectionStringResolver connectionStringResolver,
    IQueryExecutor queryExecutor,
    ILogger<TelerikReportRenderService> logger)
    {
        _queryService = queryService;
        _factory = factory;
        _connectionStringResolver = connectionStringResolver;
        _queryExecutor = queryExecutor;
        _logger = logger;
    }
    private readonly IQueryExecutor _queryExecutor;
    public async Task<RenderedReportResult> RenderAsync(
        RenderReportRequest request,
        CancellationToken ct)
    {
        var format = Normalize(request.Format);
        var exportQuery = request.ExportFullData
            ? CreateExportQueryRequest(request.Query)
            : request.Query;

        _logger.LogInformation(
            "Export format={Format} PreviewLimitRemovedForExport={Removed} OriginalLimit={OriginalLimit} ExportLimit={ExportLimit}",
            format,
            request.ExportFullData,
            request.Query.Limit,
            exportQuery.Limit);

        if (format == "CSV")
        {
            var compiledCsv = await _queryService.CompileForExportAsync(exportQuery, ct);

            var queryResult = await _queryExecutor.ExecuteAsync(
                compiledCsv.ConnectionId,
                new SqlCompilationResult
                {
                    Sql = compiledCsv.Sql,
                    Parameters = compiledCsv.Parameters.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value)
                },
                compiledCsv.ExpectedColumns,
                ct);

            var csvBytes = CsvExportWriter.Write(queryResult);

            return BuildResult(
                format,
                csvBytes,
                queryResult.Rows.Count,
                queryResult.Columns.Count);
        }

        var compiled = await _queryService.CompileForExportAsync(exportQuery, ct);
        var connectionString = _connectionStringResolver.Resolve(compiled.ConnectionId);

        _logger.LogInformation(
            "Telerik SQL-backed export. TelerikSqlDataSource=true Sql={Sql} Parameters={@Parameters}",
            compiled.Sql,
            compiled.Parameters);

        var report = _factory.CreateSqlBackedTableReport(
            compiled,
            connectionString,
            request.ReportTitle);

        byte[] bytes;

        try
        {
            var source = new Telerik.Reporting.InstanceReportSource
            {
                ReportDocument = report
            };

            var processor = new Telerik.Reporting.Processing.ReportProcessor();
            var rendered = processor.RenderReport(
                format,
                source,
                new System.Collections.Hashtable())
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
            throw new ReportExportException(
                $"Telerik render failed for format '{format}': {ex.Message}",
                500,
                ex);
        }

        ValidateBinaryPayload(format, bytes);

        return BuildResult(
            format,
            bytes,
            -1,
            compiled.ExpectedColumns.Count);
    }

    private static VisualQueryRequest CreateExportQueryRequest(VisualQueryRequest source)
    {
        return new VisualQueryRequest
        {
            ConnectionId = source.ConnectionId,
            DatasetId = source.DatasetId,
            ReportId = source.ReportId,
            VisualType = source.VisualType,
            Rows = [.. source.Rows],
            Columns = [.. source.Columns],
            Values = [.. source.Values],
            Filters = [.. source.Filters],
            Sort = [.. source.Sort],
            Limit = int.MaxValue,
            Offset = 0
        };
    }

    private RenderedReportResult BuildResult(
        string format,
        byte[] bytes,
        int rowCount,
        int columnCount)
    {
        var contentType = format switch
        {
            "PDF" => "application/pdf",
            "XLSX" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "CSV" => "text/csv; charset=utf-8",
            _ => throw new ReportExportException($"Unsupported format '{format}'.", 400)
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
            FileName = fileName
        };
    }

    private static void ValidateBinaryPayload(string format, byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ReportExportException($"Export payload for '{format}' is empty.");
        }

        if (format == "PDF")
        {
            var header = Encoding.ASCII.GetString(
                bytes,
                0,
                Math.Min(bytes.Length, 4));

            if (bytes.Length <= 100 ||
                !header.StartsWith("%PDF", StringComparison.Ordinal))
            {
                throw new ReportExportException("Rendered PDF bytes are invalid or corrupted.");
            }
        }

        if (format == "XLSX")
        {
            if (bytes.Length <= 100 || bytes[0] != 0x50 || bytes[1] != 0x4B)
            {
                throw new ReportExportException("Rendered XLSX bytes are invalid or corrupted.");
            }
        }
    }

    private static string GetMagicBytes(byte[] bytes)
    {
        return string.Join(
            '-',
            bytes.Take(8).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string Normalize(string format)
    {
        var normalized = format.Trim().ToUpperInvariant();

        return normalized switch
        {
            "PDF" => "PDF",
            "XLSX" => "XLSX",
            "CSV" => "CSV",
            _ => throw new ReportExportException(
                "Unsupported export format. Supported formats: PDF, XLSX, CSV.",
                400)
        };
    }
}
