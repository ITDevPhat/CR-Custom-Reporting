namespace Report.Infrastructure.Artifacts;

public sealed class LocalReportArtifactStore : IReportArtifactStore
{
    private readonly string _rootPath;
    public LocalReportArtifactStore(string rootPath) { _rootPath = Path.GetFullPath(rootPath); Directory.CreateDirectory(_rootPath); }

    public async Task<string> SaveAsync(string artifactKey, Stream artifactStream, CancellationToken ct)
    {
        var fullPath = ResolvePath(artifactKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        artifactStream.Position = 0;
        await artifactStream.CopyToAsync(file, ct);
        return artifactKey;
    }

    public Task<Stream> LoadAsync(string artifactKey, CancellationToken ct)
    {
        var fullPath = ResolvePath(artifactKey);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Artifact not found", artifactKey);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string artifactKey, CancellationToken ct) => Task.FromResult(File.Exists(ResolvePath(artifactKey)));

    private string ResolvePath(string artifactKey)
    {
        if (string.IsNullOrWhiteSpace(artifactKey) || artifactKey.Contains("..") || Path.IsPathRooted(artifactKey) || artifactKey.Contains(':') || artifactKey.StartsWith('/') || artifactKey.StartsWith('\\'))
            throw new InvalidOperationException("Invalid artifact key.");
        var normalized = artifactKey.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        if (!full.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path traversal detected.");
        return full;
    }
}
