using Report.Contracts.Exports;
using Report.QueryEngine.Artifacts;
using Report.Contracts.Artifacts;


namespace Report.Api.Rendering;

public sealed class TelerikArtifactRenderService
{
    private readonly IReportExecutionRepository _repository; private readonly IReportArtifactStore _store; private readonly ReportArtifactLoader _loader; private readonly SnapshotExportRouter _router;
    public TelerikArtifactRenderService(IReportExecutionRepository repository, IReportArtifactStore store, ReportArtifactLoader loader, SnapshotExportRouter router){_repository=repository;_store=store;_loader=loader;_router=router;}
    public async Task<(bool Ok, string? Error, string? Code, int Status, RenderedReportResult? Result)> RenderByExecutionAsync(string executionId, string format, CancellationToken ct)
    {
        var normalized = format.Trim().ToLowerInvariant();
        if (!new[] { "csv", "json", "tsv", "pdf", "xlsx", "docx" }.Contains(normalized)) return (false,"Unsupported export format.","UNSUPPORTED_EXPORT_FORMAT",400,null);
        var rec = await _repository.GetAsync(executionId, ct); if (rec is null) return (false,"Execution not found.","EXECUTION_NOT_FOUND",404,null);
        if (!string.Equals(rec.Status, "Completed", StringComparison.OrdinalIgnoreCase)) return (false,$"Execution status is '{rec.Status}' and cannot be exported.","EXECUTION_NOT_COMPLETED",400,null);
        if (string.IsNullOrWhiteSpace(rec.ArtifactKey)) return (false,"Execution does not have an artifact key.","ARTIFACT_KEY_MISSING",400,null);
        if (!await _store.ExistsAsync(rec.ArtifactKey, ct)) { await _repository.MarkArtifactMissingAsync(executionId, ct); return (false,"Artifact not found.","ARTIFACT_NOT_FOUND",404,null); }

        try
        {
            await using var stream = await _store.LoadAsync(rec.ArtifactKey, ct);
            var loaded = await _loader.LoadAsync(stream, ct);
            var result = await _router.ExportAsync(normalized, rec, loaded, ct);
            return (true,null,null,200,result);
        }
        catch (ReportArtifactException ex)
        {
            return (false, ex.Message, ex.Code, 400, null);
        }
        catch (InvalidDataException ex)
        {
            return (false, ex.Message, "ARTIFACT_INVALID", 400, null);
        }
        catch (InvalidCastException ex)
        {
            return (false, ex.Message, "ARTIFACT_VALUE_CONVERSION_FAILED", 400, null);
        }
        catch (FormatException ex)
        {
            return (false, ex.Message, "ARTIFACT_VALUE_CONVERSION_FAILED", 400, null);
        }
    }
}
