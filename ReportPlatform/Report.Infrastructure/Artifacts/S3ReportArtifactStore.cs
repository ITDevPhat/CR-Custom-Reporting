namespace Report.Infrastructure.Artifacts;

using Report.Contracts.Artifacts;

public sealed class S3ReportArtifactStore : IReportArtifactStore
{
    private readonly LocalReportArtifactStore _fallback;
    public S3ReportArtifactStore(string rootPath) { _fallback = new LocalReportArtifactStore(rootPath); }
    public Task<string> SaveAsync(string artifactKey, Stream artifactStream, CancellationToken ct) => _fallback.SaveAsync(artifactKey, artifactStream, ct);
    public Task<Stream> LoadAsync(string artifactKey, CancellationToken ct) => _fallback.LoadAsync(artifactKey, ct);
    public Task<bool> ExistsAsync(string artifactKey, CancellationToken ct) => _fallback.ExistsAsync(artifactKey, ct);
}
