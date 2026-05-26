using Report.Contracts.Artifacts;
using Report.QueryEngine.Artifacts;
using Telerik.Reporting.Services;

namespace Report.Api.Rendering;

public sealed class SnapshotReportSourceResolver : IReportSourceResolver
{
    private readonly IReportExecutionRepository _repository;
    private readonly IReportArtifactStore _artifactStore;
    private readonly ReportArtifactLoader _artifactLoader;
    private readonly ITelerikSnapshotReportFactory _factory;

    public SnapshotReportSourceResolver(
        IReportExecutionRepository repository,
        IReportArtifactStore artifactStore,
        ReportArtifactLoader artifactLoader,
        ITelerikSnapshotReportFactory factory)
    {
        _repository = repository;
        _artifactStore = artifactStore;
        _artifactLoader = artifactLoader;
        _factory = factory;
    }

    public Telerik.Reporting.ReportSource Resolve(
        string report,
        OperationOrigin operationOrigin,
        IDictionary<string, object> currentParameterValues)
    {
        var executionId = ParseExecutionId(report);
        var execution = _repository.GetAsync(executionId, CancellationToken.None).GetAwaiter().GetResult()
            ?? throw new ReportExportException($"Execution '{executionId}' not found.", 404);

        if (!string.Equals(execution.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            throw new ReportExportException($"Execution '{executionId}' is not completed.", 400);
        if (string.IsNullOrWhiteSpace(execution.ArtifactKey))
            throw new ReportExportException($"Execution '{executionId}' does not have an artifact key.", 400);

        if (!_artifactStore.ExistsAsync(execution.ArtifactKey, CancellationToken.None).GetAwaiter().GetResult())
        {
            _repository.MarkArtifactMissingAsync(executionId, CancellationToken.None).GetAwaiter().GetResult();
            throw new ReportExportException($"Artifact for execution '{executionId}' is missing.", 404);
        }

        try
        {
            using var stream = _artifactStore.LoadAsync(execution.ArtifactKey, CancellationToken.None).GetAwaiter().GetResult();
            var loaded = _artifactLoader.LoadAsync(stream, CancellationToken.None).GetAwaiter().GetResult();
            var reportDocument = _factory.CreateSnapshotBackedTableReport(
                loaded.DataTable,
                execution.ReportName,
                execution,
                loaded.Header);
            return new Telerik.Reporting.InstanceReportSource { ReportDocument = reportDocument };
        }
        catch (ReportArtifactException ex) when (string.Equals(ex.Code, "ARTIFACT_VERSION_UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
        {
            _repository.MarkFailedAsync(executionId, "ArtifactVersionMismatch", CancellationToken.None).GetAwaiter().GetResult();
            throw new ReportExportException("Artifact version is incompatible.", 400, ex);
        }
        catch (ReportArtifactException ex)
        {
            _repository.MarkFailedAsync(executionId, "ArtifactCorrupted", CancellationToken.None).GetAwaiter().GetResult();
            throw new ReportExportException("Artifact is corrupted and cannot be previewed.", 400, ex);
        }
        catch (InvalidDataException ex)
        {
            _repository.MarkFailedAsync(executionId, "ArtifactCorrupted", CancellationToken.None).GetAwaiter().GetResult();
            throw new ReportExportException("Artifact is corrupted and cannot be previewed.", 400, ex);
        }
    }

    private static string ParseExecutionId(string report)
    {
        if (string.IsNullOrWhiteSpace(report)) throw new ReportExportException("Missing report source.", 400);
        var value = report.Trim();
        if (value.StartsWith("execution:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["execution:".Length..];
        }
        if (string.IsNullOrWhiteSpace(value)) throw new ReportExportException("Missing execution id.", 400);
        return value;
    }
}
