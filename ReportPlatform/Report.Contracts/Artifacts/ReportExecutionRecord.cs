namespace Report.Contracts.Artifacts;

public sealed class ReportExecutionRecord
{
    public string ExecutionId { get; init; } = "";
    public string ArtifactKey { get; init; } = "";
    public string ReportId { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public string QueryFingerprint { get; init; } = "";
    public string SemanticModelVersion { get; init; } = "v1";
    public DateTime ExecutedAtUtc { get; init; }
    public int RowCount { get; init; }
}
