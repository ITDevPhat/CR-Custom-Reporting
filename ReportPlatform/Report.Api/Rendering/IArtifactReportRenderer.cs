using System.Data;

namespace Report.Api.Rendering;

public interface IArtifactReportRenderer
{
    Task<byte[]> RenderAsync(string format, string templateId, DataTable dataTable, CancellationToken ct);
}
