using System.Text.Json;

namespace Report.Contracts.Artifacts;

public sealed class ReportExecutionArtifact
{
    public ReportExecutionArtifactHeader Header { get; init; } = new();
    public byte[] RowPayload { get; init; } = [];
}

public sealed class ReportExecutionArtifactHeader
{
    public string ArtifactVersion { get; init; } = "v1";
    public string ExecutionId { get; init; } = "";
    public string ReportId { get; init; } = "";
    public string? ReportName { get; init; }
    public string? TemplateId { get; init; }
    public string QueryFingerprint { get; init; } = "";
    public string SemanticModelVersion { get; init; } = "v1";
    public DateTime ExecutedAtUtc { get; init; }
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public string Compression { get; init; } = "gzip";
    public string Serialization { get; init; } = "json";
    public string Checksum { get; init; } = "";
    public List<ReportArtifactColumn> Columns { get; init; } = [];
    public ReportArtifactSemanticMetadata SemanticMetadata { get; init; } = new();
    public ReportArtifactRenderHints RenderHints { get; init; } = new();
}

public sealed class ReportArtifactColumn
{
    public string Name { get; init; } = "";
    public string DataType { get; init; } = "string";
    public int Ordinal { get; init; }
    public bool Nullable { get; init; }
    public string? ClrTypeName { get; init; }
    public string? SqlTypeName { get; init; }
}

public sealed class ReportArtifactSemanticMetadata
{
    public List<string> GroupFields { get; init; } = [];
    public List<string> MetricFields { get; init; } = [];
    public JsonElement[] Filters { get; init; } = [];
    public JsonElement[] Sort { get; init; } = [];
}

public sealed class ReportArtifactRenderHints
{
    public string PageSize { get; init; } = "A4";
    public string Orientation { get; init; } = "Portrait";
    public Dictionary<string, double> ColumnWidthHints { get; init; } = [];
}

public sealed class ReportArtifactBuildResult
{
    public required string ArtifactKey { get; init; }
    public required Stream ArtifactStream { get; init; }
    public required ReportExecutionArtifactHeader Header { get; init; }
}

public sealed class ReportArtifactLoadResult
{
    public required ReportExecutionArtifactHeader Header { get; init; }
    public required System.Data.DataTable DataTable { get; init; }
}

public sealed class ReportArtifactException : IOException
{
    public ReportArtifactException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
