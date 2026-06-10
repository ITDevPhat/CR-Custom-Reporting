using System.Data;
using System.Globalization;
using System.Text;
using Report.Contracts.Artifacts;
using Report.Contracts.Exports;
using Report.QueryEngine.Artifacts;

namespace Report.Api.Rendering;

public sealed class RawCsvExportService : IRawCsvExportService
{
    private readonly IReportExecutionRepository _repository;
    private readonly IReportArtifactStore _artifactStore;
    private readonly ReportArtifactLoader _artifactLoader;
    private readonly ILogger<RawCsvExportService> _logger;

    public RawCsvExportService(
        IReportExecutionRepository repository,
        IReportArtifactStore artifactStore,
        ReportArtifactLoader artifactLoader,
        ILogger<RawCsvExportService> logger)
    {
        _repository = repository;
        _artifactStore = artifactStore;
        _artifactLoader = artifactLoader;
        _logger = logger;
    }

    public async Task<RenderedReportResult> ExportAsync(string executionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new ReportExportException("Execution id is required.", 400);
        }

        var execution = await _repository.GetAsync(executionId, ct)
            ?? throw new ReportExportException($"Execution '{executionId}' not found.", 404);

        if (!string.Equals(execution.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ReportExportException($"Execution '{executionId}' is not completed.", 409);
        }

        if (string.IsNullOrWhiteSpace(execution.ArtifactKey))
        {
            throw new ReportExportException($"Execution '{executionId}' does not have an artifact key.", 422);
        }

        if (!await _artifactStore.ExistsAsync(execution.ArtifactKey, ct))
        {
            await _repository.MarkArtifactMissingAsync(executionId, ct);
            throw new ReportExportException($"Artifact for execution '{executionId}' is missing.", 422);
        }

        try
        {
            await using var stream = await _artifactStore.LoadAsync(execution.ArtifactKey, ct);
            var loaded = await _artifactLoader.LoadAsync(stream, ct);
            var bytes = WriteCsv(loaded.DataTable);

            _logger.LogInformation(
                "Raw CSV export completed. ExecutionId={ExecutionId} Rows={Rows} Columns={Columns} Bytes={Bytes}",
                executionId,
                loaded.DataTable.Rows.Count,
                loaded.DataTable.Columns.Count,
                bytes.Length);

            return new RenderedReportResult
            {
                Bytes = bytes,
                ContentType = "text/csv; charset=utf-8",
                FileName = BuildFileName(execution, "csv")
            };
        }
        catch (ReportArtifactException ex) when (
            string.Equals(ex.Code, "ARTIFACT_VERSION_UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
        {
            throw new ReportExportException("Artifact version is incompatible.", 422, ex);
        }
        catch (ReportArtifactException ex)
        {
            throw new ReportExportException("Artifact is corrupted and cannot be exported.", 422, ex);
        }
        catch (InvalidDataException ex)
        {
            throw new ReportExportException("Artifact is corrupted and cannot be exported.", 422, ex);
        }
    }

    private static byte[] WriteCsv(DataTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))));

        foreach (DataRow row in table.Rows)
        {
            var values = table.Columns.Cast<DataColumn>()
                .Select(column => Escape(FormatValue(row[column])));
            sb.AppendLine(string.Join(',', values));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Encoding.UTF8.GetPreamble().Concat(bytes).ToArray();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DBNull => string.Empty,
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Escape(string value)
    {
        var mustQuote =
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        var escaped = value.Replace("\"", "\"\"");
        return mustQuote ? $"\"{escaped}\"" : escaped;
    }

    private static string BuildFileName(ReportExecutionRecord execution, string format)
    {
        var name = string.IsNullOrWhiteSpace(execution.ReportName)
            ? execution.ReportId
            : execution.ReportName;

        var safe = string.Join(
            "-",
            name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "report";
        }

        return $"{safe}-{execution.ExecutionId}.{format}";
    }
}
