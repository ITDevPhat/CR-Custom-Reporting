namespace Report.Infrastructure.Artifacts;

public sealed class InMemoryReportArtifactStore : IReportArtifactStore
{
    private readonly Dictionary<string, byte[]> _data = new();
    public async Task<string> SaveAsync(string artifactKey, Stream artifactStream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        artifactStream.Position = 0;
        await artifactStream.CopyToAsync(ms, ct);
        _data[artifactKey] = ms.ToArray();
        return artifactKey;
    }
    public Task<Stream> LoadAsync(string artifactKey, CancellationToken ct) =>
        _data.TryGetValue(artifactKey, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException("Artifact not found", artifactKey);
    public Task<bool> ExistsAsync(string artifactKey, CancellationToken ct) => Task.FromResult(_data.ContainsKey(artifactKey));
}
