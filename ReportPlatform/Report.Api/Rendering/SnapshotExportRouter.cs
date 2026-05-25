using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Report.Contracts.Artifacts;
using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public interface IReportSnapshotExportProcessor
{
    bool CanExport(string format);

    Task<RenderedReportResult> ExportAsync(
        string format,
        ReportExecutionRecord execution,
        ReportArtifactLoadResult snapshot,
        CancellationToken ct);
}

public sealed class SnapshotExportRouter
{
    private readonly IEnumerable<IReportSnapshotExportProcessor> _processors;

    public SnapshotExportRouter(IEnumerable<IReportSnapshotExportProcessor> processors)
    {
        _processors = processors;
    }

    public Task<RenderedReportResult> ExportAsync(
        string format,
        ReportExecutionRecord execution,
        ReportArtifactLoadResult snapshot,
        CancellationToken ct)
    {
        var normalized = format.Trim().ToLowerInvariant();
        var processor = _processors.FirstOrDefault(p => p.CanExport(normalized))
            ?? throw new ReportExportException($"Unsupported export format '{format}'.");

        return processor.ExportAsync(normalized, execution, snapshot, ct);
    }
}

public sealed class DataFirstSnapshotProcessor : IReportSnapshotExportProcessor
{
    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        "csv",
        "json",
        "tsv"
    };

    public bool CanExport(string format) => Formats.Contains(format);

    public Task<RenderedReportResult> ExportAsync(
        string format,
        ReportExecutionRecord execution,
        ReportArtifactLoadResult snapshot,
        CancellationToken ct)
    {
        var bytes = format switch
        {
            "json" => WriteJson(snapshot),
            "tsv" => WriteDelimited(snapshot.DataTable, '\t', includeBom: false),
            _ => WriteDelimited(snapshot.DataTable, ',', includeBom: true)
        };

        return Task.FromResult(new RenderedReportResult
        {
            Bytes = bytes,
            ContentType = format switch
            {
                "json" => "application/json",
                "tsv" => "text/tab-separated-values",
                _ => "text/csv"
            },
            FileName = BuildFileName(execution, format)
        });
    }

    private static byte[] WriteJson(ReportArtifactLoadResult snapshot)
    {
        var rows = snapshot.DataTable.Rows.Cast<DataRow>()
            .Select(row => snapshot.DataTable.Columns.Cast<DataColumn>()
                .ToDictionary(column => column.ColumnName, column => NormalizeValue(row[column])))
            .ToList();

        var payload = new
        {
            snapshot.Header.ArtifactVersion,
            snapshot.Header.ExecutionId,
            snapshot.Header.ReportId,
            snapshot.Header.ReportName,
            snapshot.Header.TemplateId,
            snapshot.Header.QueryFingerprint,
            snapshot.Header.SemanticModelVersion,
            snapshot.Header.ExecutedAtUtc,
            snapshot.Header.RowCount,
            snapshot.Header.ColumnCount,
            columns = snapshot.Header.Columns,
            rows
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static byte[] WriteDelimited(DataTable table, char delimiter, bool includeBom)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(delimiter, table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName, delimiter))));

        foreach (DataRow row in table.Rows)
        {
            var values = table.Columns.Cast<DataColumn>()
                .Select(column => Escape(FormatValue(row[column]), delimiter));
            sb.AppendLine(string.Join(delimiter, values));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return includeBom ? Encoding.UTF8.GetPreamble().Concat(bytes).ToArray() : bytes;
    }

    private static object? NormalizeValue(object? value) => value is null or DBNull ? null : value;

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

    private static string Escape(string value, char delimiter)
    {
        var mustQuote =
            value.Contains(delimiter) ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        var escaped = value.Replace("\"", "\"\"");
        return mustQuote ? $"\"{escaped}\"" : escaped;
    }

    internal static string BuildFileName(ReportExecutionRecord execution, string format)
    {
        var name = string.IsNullOrWhiteSpace(execution.ReportName) ? execution.ReportId : execution.ReportName;
        var safe = string.Join("-", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safe)) safe = "report";
        return $"{safe}-{execution.ExecutionId}.{format}";
    }
}

public sealed class TelerikSnapshotRenderProcessor : IReportSnapshotExportProcessor
{
    private static readonly Dictionary<string, (string Format, string ContentType)> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = ("PDF", "application/pdf"),
        ["xlsx"] = ("XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        ["docx"] = ("DOCX", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
    };

    private readonly IArtifactReportRenderer _renderer;

    public TelerikSnapshotRenderProcessor(IArtifactReportRenderer renderer)
    {
        _renderer = renderer;
    }

    public bool CanExport(string format) => Formats.ContainsKey(format);

    public async Task<RenderedReportResult> ExportAsync(
        string format,
        ReportExecutionRecord execution,
        ReportArtifactLoadResult snapshot,
        CancellationToken ct)
    {
        var mapped = Formats[format];
        var bytes = await _renderer.RenderAsync(
            mapped.Format,
            execution.TemplateId ?? execution.ReportId,
            snapshot.DataTable,
            ct);

        return new RenderedReportResult
        {
            Bytes = bytes,
            ContentType = mapped.ContentType,
            FileName = DataFirstSnapshotProcessor.BuildFileName(execution, format)
        };
    }
}
