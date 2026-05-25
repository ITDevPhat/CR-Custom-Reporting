using Report.Contracts.Exports;
using Report.Metadata.Stores;
using Report.QueryEngine.Artifacts;
using Report.Infrastructure.Artifacts;

namespace Report.Api.Rendering;

public sealed class TelerikArtifactRenderService
{
    private readonly IReportExecutionRegistry _registry; private readonly IReportArtifactStore _store; private readonly ReportArtifactLoader _loader; private readonly IArtifactReportRenderer _renderer;
    public TelerikArtifactRenderService(IReportExecutionRegistry registry, IReportArtifactStore store, ReportArtifactLoader loader, IArtifactReportRenderer renderer){_registry=registry;_store=store;_loader=loader;_renderer=renderer;}
    public async Task<(bool Ok, string? Error, int Status, RenderedReportResult? Result)> RenderByExecutionAsync(string executionId, string format, CancellationToken ct)
    {
        var normalized = format.Trim().ToLowerInvariant();
        var map = new Dictionary<string,(string fmt,string ctype)>{{"pdf",("PDF","application/pdf")},{"xlsx",("XLSX","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")},{"csv",("CSV","text/csv")},{"docx",("DOCX","application/vnd.openxmlformats-officedocument.wordprocessingml.document")}};
        if (!map.ContainsKey(normalized)) return (false,"Unsupported export format.",400,null);
        var rec = _registry.Find(executionId); if (rec is null) return (false,"Execution not found.",404,null);
        if (!await _store.ExistsAsync(rec.ArtifactKey, ct)) return (false,"Artifact not found.",404,null);
        await using var stream = await _store.LoadAsync(rec.ArtifactKey, ct);
        var loaded = await _loader.LoadAsync(stream, ct);
        var bytes = await _renderer.RenderAsync(map[normalized].fmt, rec.TemplateId, loaded.DataTable, ct);
        return (true,null,200,new RenderedReportResult{Bytes=bytes,ContentType=map[normalized].ctype,FileName=$"report-{executionId}.{normalized}"});
    }
}
