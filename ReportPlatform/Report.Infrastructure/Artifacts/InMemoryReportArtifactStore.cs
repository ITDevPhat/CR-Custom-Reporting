using Report.Contracts.Artifacts;

namespace Report.Infrastructure.Artifacts;

public sealed class InMemoryReportArtifactStore : IReportArtifactStore
{
    private readonly Dictionary<string, byte[]> _storage = new();

    public async Task<string> SaveAsync(string artifactKey, Stream artifactStream, CancellationToken ct)
    {
        if (artifactStream.CanSeek)
            artifactStream.Position = 0;

        using var ms = new MemoryStream();
        await artifactStream.CopyToAsync(ms, ct);

        _storage[artifactKey] = ms.ToArray();

        return artifactKey;
    }

    public Task<Stream> LoadAsync(string artifactKey, CancellationToken ct)
    {
        if (!_storage.TryGetValue(artifactKey, out var bytes))
            throw new FileNotFoundException($"Report artifact not found: {artifactKey}");

        Stream stream = new MemoryStream(bytes, writable: false);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string artifactKey, CancellationToken ct)
    {
        return Task.FromResult(_storage.ContainsKey(artifactKey));
    }
}