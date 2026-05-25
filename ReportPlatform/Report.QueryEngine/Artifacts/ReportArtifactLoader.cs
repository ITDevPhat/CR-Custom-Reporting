using System.Data;
using System.IO.Compression;
using System.Text.Json;
using Report.Contracts.Artifacts;

namespace Report.QueryEngine.Artifacts;

public sealed class ReportArtifactLoader
{
    public async Task<ReportArtifactLoadResult> LoadAsync(Stream artifactStream, CancellationToken ct)
    {
        using var gzip = new GZipStream(artifactStream, CompressionMode.Decompress, leaveOpen: true);
        var artifact = await JsonSerializer.DeserializeAsync<ReportExecutionArtifact>(gzip, cancellationToken: ct)
            ?? throw new InvalidDataException("Invalid artifact payload.");

        var table = new DataTable("artifact");
        foreach (var c in artifact.Header.Columns.OrderBy(c => c.Ordinal))
        {
            var type = Type.GetType(c.DataType) ?? typeof(string);
            table.Columns.Add(new DataColumn(c.Name, type) { AllowDBNull = c.Nullable });
        }

        var rows = JsonSerializer.Deserialize<List<object?[]>>(artifact.RowPayload) ?? [];
        foreach (var row in rows)
        {
            var dr = table.NewRow();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var cell = row[i];
                dr[i] = cell is null ? DBNull.Value : ConvertTo(table.Columns[i].DataType, cell);
            }
            table.Rows.Add(dr);
        }

        return new ReportArtifactLoadResult { Header = artifact.Header, DataTable = table };
    }

    private static object ConvertTo(Type type, object value)
    {
        if (value is JsonElement je)
        {
            if (type == typeof(string)) return je.GetString() ?? "";
            if (type == typeof(int)) return je.GetInt32();
            if (type == typeof(decimal)) return je.GetDecimal();
            if (type == typeof(DateTime)) return je.GetDateTime();
            if (type == typeof(bool)) return je.GetBoolean();
            if (type == typeof(long)) return je.GetInt64();
            if (type == typeof(double)) return je.GetDouble();
        }
        return Convert.ChangeType(value, type);
    }
}
