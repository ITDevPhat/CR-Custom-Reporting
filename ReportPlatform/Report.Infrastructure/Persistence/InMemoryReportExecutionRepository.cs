using Report.Contracts.Artifacts;

namespace Report.Infrastructure.Persistence;

public sealed class InMemoryReportExecutionRepository : IReportExecutionRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ReportExecutionRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public Task UpsertAsync(ReportExecutionRecord record, CancellationToken ct)
    {
        lock (_gate)
        {
            _records[record.ExecutionId] = record;
        }

        return Task.CompletedTask;
    }

    public Task<ReportExecutionRecord?> GetAsync(string executionId, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_records.GetValueOrDefault(executionId));
        }
    }

    public Task<IReadOnlyList<ReportExecutionRecord>> ListAsync(ReportExecutionListQuery query, CancellationToken ct)
    {
        lock (_gate)
        {
            var records = _records.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                records = records.Where(r => string.Equals(r.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.StorageMode))
            {
                records = records.Where(r => string.Equals(r.StorageMode, query.StorageMode, StringComparison.OrdinalIgnoreCase));
            }

            if (query.FromUtc is not null)
            {
                records = records.Where(r => r.CreatedAtUtc >= query.FromUtc.Value);
            }

            if (query.ToUtc is not null)
            {
                records = records.Where(r => r.CreatedAtUtc <= query.ToUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                records = records.Where(r =>
                    r.ExecutionId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.ReportId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.ReportName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var limit = query.Limit is > 0 and <= 500 ? query.Limit : 100;
            var offset = Math.Max(0, query.Offset);

            return Task.FromResult<IReadOnlyList<ReportExecutionRecord>>(
                records
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .Skip(offset)
                    .Take(limit)
                    .ToList());
        }
    }

    public Task MarkProcessingAsync(string executionId, DateTime startedAtUtc, CancellationToken ct)
        => UpdateAsync(executionId, r => r with { Status = "Processing", StartedAtUtc = startedAtUtc }, ct);

    public Task MarkCompletedAsync(string executionId, int rowCount, string artifactKey, long durationMs, CancellationToken ct)
        => UpdateAsync(executionId, r => r with
        {
            Status = "Completed",
            RowCount = rowCount,
            ArtifactKey = artifactKey,
            ArtifactAvailable = true,
            CompletedAtUtc = DateTime.UtcNow,
            DurationMs = durationMs,
            ErrorMessage = null
        }, ct);

    public Task MarkFailedAsync(string executionId, string errorMessage, CancellationToken ct)
        => UpdateAsync(executionId, r => r with
        {
            Status = "Failed",
            FailedAtUtc = DateTime.UtcNow,
            ErrorMessage = errorMessage
        }, ct);

    public Task MarkArtifactMissingAsync(string executionId, CancellationToken ct)
        => UpdateAsync(executionId, r => r with
        {
            Status = "ArtifactMissing",
            ArtifactAvailable = false,
            ErrorMessage = "Artifact file is missing."
        }, ct);

    private Task UpdateAsync(string executionId, Func<ReportExecutionRecord, ReportExecutionRecord> update, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_records.TryGetValue(executionId, out var record))
            {
                _records[executionId] = update(record);
            }
        }

        return Task.CompletedTask;
    }
}
