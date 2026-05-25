using System.IO.Compression;
using System.Text.Json;
using Report.Contracts.Artifacts;

namespace Report.Infrastructure.Persistence;

public sealed class ArtifactBackedReportExecutionRepository : IReportExecutionRepository
{
    private readonly IReportExecutionRepository _inner;
    private readonly string _artifactRoot;

    public ArtifactBackedReportExecutionRepository(IReportExecutionRepository inner, string artifactRoot)
    {
        _inner = inner;
        _artifactRoot = Path.GetFullPath(artifactRoot);
    }

    public Task UpsertAsync(ReportExecutionRecord record, CancellationToken ct)
        => _inner.UpsertAsync(record, ct);

    public async Task<ReportExecutionRecord?> GetAsync(string executionId, CancellationToken ct)
    {
        var record = await _inner.GetAsync(executionId, ct);
        if (record is not null)
        {
            return record;
        }

        var artifactRecords = await LoadArtifactRecordsAsync(ct);
        return artifactRecords.FirstOrDefault(r =>
            string.Equals(r.ExecutionId, executionId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ReportExecutionRecord>> ListAsync(ReportExecutionListQuery query, CancellationToken ct)
    {
        var records = new Dictionary<string, ReportExecutionRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in await LoadArtifactRecordsAsync(ct))
        {
            records[record.ExecutionId] = record;
        }

        foreach (var record in await _inner.ListAsync(new ReportExecutionListQuery { Limit = 500 }, ct))
        {
            records[record.ExecutionId] = record;
        }

        var filtered = records.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(r => string.Equals(r.Status, query.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.StorageMode))
        {
            filtered = filtered.Where(r => string.Equals(r.StorageMode, query.StorageMode, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FromUtc is not null)
        {
            filtered = filtered.Where(r => r.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc is not null)
        {
            filtered = filtered.Where(r => r.CreatedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(r =>
                r.ExecutionId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.ReportId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (r.ReportName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var limit = query.Limit is > 0 and <= 500 ? query.Limit : 100;
        var offset = Math.Max(0, query.Offset);

        return filtered
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public Task MarkProcessingAsync(string executionId, DateTime startedAtUtc, CancellationToken ct)
        => _inner.MarkProcessingAsync(executionId, startedAtUtc, ct);

    public Task MarkCompletedAsync(string executionId, int rowCount, string artifactKey, long durationMs, CancellationToken ct)
        => _inner.MarkCompletedAsync(executionId, rowCount, artifactKey, durationMs, ct);

    public Task MarkFailedAsync(string executionId, string errorMessage, CancellationToken ct)
        => _inner.MarkFailedAsync(executionId, errorMessage, ct);

    public Task MarkArtifactMissingAsync(string executionId, CancellationToken ct)
        => _inner.MarkArtifactMissingAsync(executionId, ct);

    private async Task<IReadOnlyList<ReportExecutionRecord>> LoadArtifactRecordsAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_artifactRoot))
        {
            return [];
        }

        var records = new List<ReportExecutionRecord>();

        foreach (var file in Directory.EnumerateFiles(_artifactRoot, "*.seaf", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var record = await TryLoadRecordAsync(file, ct);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private async Task<ReportExecutionRecord?> TryLoadRecordAsync(string file, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(file);
            using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false);
            var artifact = await JsonSerializer.DeserializeAsync<ReportExecutionArtifact>(gzip, cancellationToken: ct);
            var header = artifact?.Header;
            if (header is null || string.IsNullOrWhiteSpace(header.ExecutionId))
            {
                return null;
            }

            var artifactKey = Path.GetRelativePath(_artifactRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            var timestamp = header.ExecutedAtUtc == default
                ? File.GetLastWriteTimeUtc(file)
                : header.ExecutedAtUtc;

            return new ReportExecutionRecord
            {
                ExecutionId = header.ExecutionId,
                ReportId = string.IsNullOrWhiteSpace(header.ReportId) ? "rpt_001" : header.ReportId,
                ReportName = string.IsNullOrWhiteSpace(header.ReportName) ? header.ReportId : header.ReportName,
                TemplateId = header.TemplateId,
                Status = "Completed",
                RowCount = header.RowCount,
                ArtifactKey = artifactKey,
                ArtifactAvailable = true,
                StorageMode = "Local",
                QueryFingerprint = header.QueryFingerprint,
                SemanticModelVersion = header.SemanticModelVersion,
                CreatedAtUtc = timestamp,
                StartedAtUtc = timestamp,
                CompletedAtUtc = timestamp
            };
        }
        catch
        {
            return null;
        }
    }
}
