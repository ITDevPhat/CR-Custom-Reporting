using Report.Contracts.Artifacts;

namespace Report.Infrastructure.Artifacts;

public sealed class FileSystemReportArtifactStore : IReportArtifactStore
{
    private readonly string _rootPath;

    public FileSystemReportArtifactStore(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string artifactKey, Stream artifactStream, CancellationToken ct)
    {
        var fullPath = GetFullPath(artifactKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (artifactStream.CanSeek)
            artifactStream.Position = 0;

        await using var output = File.Create(fullPath);
        await artifactStream.CopyToAsync(output, ct);

        return artifactKey;
    }

    public Task<Stream> LoadAsync(string artifactKey, CancellationToken ct)
    {
        var fullPath = GetFullPath(artifactKey);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Report artifact not found: {artifactKey}", fullPath);

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string artifactKey, CancellationToken ct)
    {
        var fullPath = GetFullPath(artifactKey);
        return Task.FromResult(File.Exists(fullPath));
    }

    private string GetFullPath(string artifactKey)
    {
        var safePath = artifactKey
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        return Path.Combine(_rootPath, safePath);
    }
}