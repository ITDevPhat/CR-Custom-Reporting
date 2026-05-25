namespace Report.Infrastructure.Artifacts;

public interface IReportArtifactStore
{
    Task<string> SaveAsync(string artifactKey, Stream artifactStream, CancellationToken ct);
    Task<Stream> LoadAsync(string artifactKey, CancellationToken ct);
    Task<bool> ExistsAsync(string artifactKey, CancellationToken ct);
}
