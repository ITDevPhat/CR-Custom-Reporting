using Report.Api.DTOs.PowerBI;
using Report.Api.Options.PowerBI;

namespace Report.Api.Services.PowerBI;

public interface IPowerBIRestClient
{
    Task<IReadOnlyList<PowerBIWorkspaceDto>> GetWorkspacesAsync(PowerBIOptions options, CancellationToken ct);
    Task<IReadOnlyList<PowerBIReportDto>> GetReportsAsync(PowerBIOptions options, string workspaceId, CancellationToken ct);
    Task<PowerBIReportDto> GetReportAsync(PowerBIOptions options, string workspaceId, string reportId, CancellationToken ct);
    Task<IReadOnlyList<PowerBIDatasetDto>> GetDatasetsAsync(PowerBIOptions options, string workspaceId, CancellationToken ct);
    Task<PowerBIEmbedTokenResponse> GenerateEmbedTokenAsync(PowerBIOptions options, string workspaceId, string reportId, string? datasetId, CancellationToken ct);
}
