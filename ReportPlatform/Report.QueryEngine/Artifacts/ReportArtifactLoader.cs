using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
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

        ValidateChecksum(artifact);

        var table = new DataTable("artifact");
        foreach (var c in artifact.Header.Columns.OrderBy(c => c.Ordinal))
        {
            var type = Type.GetType(c.DataType) ?? typeof(string);
            table.Columns.Add(new DataColumn(c.Name, type) { AllowDBNull = c.Nullable });
        }

        var rows = JsonSerializer.Deserialize<List<object?[]>>(artifact.RowPayload) ?? [];
        if (artifact.Header.RowCount != rows.Count)
        {
            throw new ReportArtifactException("ARTIFACT_ROW_COUNT_INVALID", "Artifact row count does not match the row payload.");
        }

        if (artifact.Header.ColumnCount != 0 && artifact.Header.ColumnCount != table.Columns.Count)
        {
            throw new ReportArtifactException("ARTIFACT_COLUMN_COUNT_INVALID", "Artifact column count does not match the column manifest.");
        }

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

    private static void ValidateChecksum(ReportExecutionArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.Header.Checksum))
        {
            return;
        }

        var actual = $"sha256-{Convert.ToHexString(SHA256.HashData(artifact.RowPayload)).ToLowerInvariant()}";
        if (!string.Equals(actual, artifact.Header.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReportArtifactException("ARTIFACT_CHECKSUM_INVALID", "Artifact checksum validation failed.");
        }
    }

    private static object ConvertTo(Type type, object value)
    {
        if (type == typeof(object))
        {
            return value is JsonElement objectElement ? UnwrapJsonElement(objectElement) ?? DBNull.Value : value;
        }

        if (value is JsonElement je)
        {
            if (je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return DBNull.Value;
            if (type == typeof(string)) return je.GetString() ?? "";
            if (type == typeof(int)) return je.GetInt32();
            if (type == typeof(short)) return je.GetInt16();
            if (type == typeof(byte)) return je.GetByte();
            if (type == typeof(decimal)) return je.GetDecimal();
            if (type == typeof(DateTime)) return je.GetDateTime();
            if (type == typeof(DateTimeOffset)) return je.GetDateTimeOffset();
            if (type == typeof(bool)) return je.GetBoolean();
            if (type == typeof(long)) return je.GetInt64();
            if (type == typeof(float)) return je.GetSingle();
            if (type == typeof(double)) return je.GetDouble();
            if (type == typeof(Guid)) return je.GetGuid();
        }
        return Convert.ChangeType(value, type);
    }

    private static object? UnwrapJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.TryGetDateTimeOffset(out var dto)
                ? dto
                : element.TryGetGuid(out var guid)
                    ? guid
                    : element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var i)
                ? i
                : element.TryGetInt64(out var l)
                    ? l
                    : element.TryGetDecimal(out var d)
                        ? d
                        : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText()
        };
    }
}
