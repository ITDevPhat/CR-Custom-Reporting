namespace Report.Contracts.Artifacts;

public sealed record ReportExecutionRecord
{
    public string ExecutionId { get; init; } = "";
    public string ReportId { get; init; } = "";
    public string? ReportName { get; init; }
    public string? TemplateId { get; init; }
    public string Status { get; init; } = "Requested";
    public int? RowCount { get; init; }
    public string? ArtifactKey { get; init; }
    public bool ArtifactAvailable { get; init; }
    public string StorageMode { get; init; } = "Local";
    public string? QueryFingerprint { get; init; }
    public string SemanticModelVersion { get; init; } = "v1";
    public string? CompiledSql { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? FailedAtUtc { get; init; }
    public long? DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ReportExecutionListQuery
{
    public string? Status { get; init; }
    public string? StorageMode { get; init; }
    public string? Search { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; }
}

public interface IReportExecutionRepository
{
    Task UpsertAsync(ReportExecutionRecord record, CancellationToken ct);

    Task<ReportExecutionRecord?> GetAsync(string executionId, CancellationToken ct);

    Task<IReadOnlyList<ReportExecutionRecord>> ListAsync(ReportExecutionListQuery query, CancellationToken ct);

    Task MarkProcessingAsync(string executionId, DateTime startedAtUtc, CancellationToken ct);

    Task MarkCompletedAsync(string executionId, int rowCount, string artifactKey, long durationMs, CancellationToken ct);

    Task MarkFailedAsync(string executionId, string errorMessage, CancellationToken ct);

    Task MarkArtifactMissingAsync(string executionId, CancellationToken ct);
}
