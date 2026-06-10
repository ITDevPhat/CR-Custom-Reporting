using System.Data;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Report.Contracts.Artifacts;

namespace Report.QueryEngine.Artifacts;

public sealed class ReportArtifactBuilder
{
    public ReportArtifactBuildResult Build(string artifactKey, ReportExecutionArtifactHeader header, DataTable table)
    {
        var columns = table.Columns.Cast<DataColumn>().Select((c, i) => new ReportArtifactColumn
        {
            Name = c.ColumnName,
            DataType = c.DataType.AssemblyQualifiedName ?? typeof(string).AssemblyQualifiedName!,
            Ordinal = i,
            Nullable = c.AllowDBNull,
            ClrTypeName = c.DataType.FullName,
            SqlTypeName = c.ExtendedProperties["SqlTypeName"] as string
        }).ToList();
        var rows = table.Rows.Cast<DataRow>().Select(r => columns.Select(c => r[c.Ordinal] is DBNull ? null : r[c.Ordinal]).ToArray()).ToList();
        var rowPayload = JsonSerializer.SerializeToUtf8Bytes(rows);
        var checksum = $"sha256-{Convert.ToHexString(SHA256.HashData(rowPayload)).ToLowerInvariant()}";
        var fullHeader = new ReportExecutionArtifactHeader
        {
            ArtifactVersion = header.ArtifactVersion,
            ExecutionId = header.ExecutionId,
            ReportId = header.ReportId,
            ReportName = header.ReportName,
            TemplateId = header.TemplateId,
            QueryFingerprint = header.QueryFingerprint,
            SemanticModelVersion = header.SemanticModelVersion,
            ExecutedAtUtc = header.ExecutedAtUtc,
            RowCount = table.Rows.Count,
            ColumnCount = table.Columns.Count,
            Compression = "gzip",
            Serialization = "json",
            Checksum = checksum,
            Columns = columns,
            SemanticMetadata = header.SemanticMetadata,
            RenderHints = header.RenderHints
        };

        var artifact = new ReportExecutionArtifact { Header = fullHeader, RowPayload = rowPayload };
        var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzip, artifact);
        }

        output.Position = 0;
        return new ReportArtifactBuildResult { ArtifactKey = artifactKey, ArtifactStream = output, Header = fullHeader };
    }
}
