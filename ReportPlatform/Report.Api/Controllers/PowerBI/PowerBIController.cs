using Microsoft.AspNetCore.Mvc;
using Report.Api.DTOs.PowerBI;
using Report.Api.Services.PowerBI;
using Report.Api.Validators.PowerBI;

namespace Report.Api.Controllers.PowerBI;

[ApiController]
[Route("api/powerbi")]
public sealed class PowerBIController : ControllerBase
{
    private readonly IPowerBIConfigurationStore _configurationStore;
    private readonly IPowerBIRestClient _powerBIClient;
    private readonly ILogger<PowerBIController> _logger;

    public PowerBIController(
        IPowerBIConfigurationStore configurationStore,
        IPowerBIRestClient powerBIClient,
        ILogger<PowerBIController> logger)
    {
        _configurationStore = configurationStore;
        _powerBIClient = powerBIClient;
        _logger = logger;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        return Ok(await _configurationStore.GetMaskedConfigurationAsync(ct));
    }

    [HttpPost("config/save")]
    public async Task<IActionResult> SaveConfiguration(
        [FromBody] SavePowerBIConfigurationRequest request,
        CancellationToken ct)
    {
        return Ok(await _configurationStore.SaveAsync(request, ct));
    }

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection(CancellationToken ct)
    {
        try
        {
            var options = await _configurationStore.GetEffectiveOptionsAsync(ct);
            var authErrors = PowerBIConfigurationValidator.ValidateForAuthentication(options);
            if (authErrors.Count > 0)
            {
                return BadRequest(new PowerBIConnectionTestResponse(
                    false,
                    "Failed",
                    false,
                    false,
                    null,
                    null,
                    "Configuration validation failed.",
                    authErrors.ToArray()));
            }

            var workspaces = await _powerBIClient.GetWorkspacesAsync(options, ct);
            var workspace = !string.IsNullOrWhiteSpace(options.WorkspaceId)
                ? workspaces.FirstOrDefault(item => string.Equals(item.Id, options.WorkspaceId, StringComparison.OrdinalIgnoreCase))
                : null;

            PowerBIReportDto? report = null;
            if (workspace is not null && !string.IsNullOrWhiteSpace(options.ReportId))
            {
                report = await _powerBIClient.GetReportAsync(options, workspace.Id, options.ReportId, ct);
            }

            return Ok(new PowerBIConnectionTestResponse(
                true,
                "Authenticated",
                workspace is not null,
                report is not null,
                workspace?.Name,
                report?.Name,
                "Power BI authentication succeeded.",
                Array.Empty<string>()));
        }
        catch (PowerBIIntegrationException ex)
        {
            _logger.LogWarning("Power BI connection test failed. Code={Code}", ex.Code);
            return StatusCode(ex.StatusCode, new PowerBIConnectionTestResponse(
                false,
                "Failed",
                false,
                false,
                null,
                null,
                ex.Message,
                new[] { ex.Code, ex.Detail ?? string.Empty }.Where(static item => !string.IsNullOrWhiteSpace(item)).ToArray()));
        }
    }

    [HttpGet("workspaces")]
    public async Task<IActionResult> GetWorkspaces(CancellationToken ct)
    {
        return await ExecutePowerBICallAsync(async () =>
        {
            var options = await _configurationStore.GetEffectiveOptionsAsync(ct);
            return await _powerBIClient.GetWorkspacesAsync(options, ct);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/reports")]
    public async Task<IActionResult> GetReports(string workspaceId, CancellationToken ct)
    {
        return await ExecutePowerBICallAsync(async () =>
        {
            var options = await _configurationStore.GetEffectiveOptionsAsync(ct);
            return await _powerBIClient.GetReportsAsync(options, workspaceId, ct);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/reports/{reportId:guid}")]
    public async Task<IActionResult> GetReport(string workspaceId, string reportId, CancellationToken ct)
    {
        return await ExecutePowerBICallAsync(async () =>
        {
            var options = await _configurationStore.GetEffectiveOptionsAsync(ct);
            return await _powerBIClient.GetReportAsync(options, workspaceId, reportId, ct);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/datasets")]
    public async Task<IActionResult> GetDatasets(string workspaceId, CancellationToken ct)
    {
        return await ExecutePowerBICallAsync(async () =>
        {
            var options = await _configurationStore.GetEffectiveOptionsAsync(ct);
            return await _powerBIClient.GetDatasetsAsync(options, workspaceId, ct);
        });
    }

    [HttpPost("embed-token")]
    public async Task<IActionResult> GenerateEmbedToken(
        [FromBody] PowerBIEmbedTokenRequest request,
        CancellationToken ct)
    {
        return await ExecutePowerBICallAsync(async () =>
        {
            var options = await _configurationStore.GetEffectiveOptionsAsync(ct);
            var workspaceId = request.WorkspaceId ?? options.WorkspaceId;
            var reportId = request.ReportId ?? options.ReportId;
            var datasetId = request.DatasetId ?? options.DatasetId;

            var validationErrors = PowerBIConfigurationValidator.ValidateWorkspaceAndReport(workspaceId, reportId);
            if (validationErrors.Count > 0)
            {
                throw new PowerBIIntegrationException(
                    "POWERBI_INVALID_EMBED_REQUEST",
                    "Power BI embed token request is invalid.",
                    StatusCodes.Status400BadRequest,
                    string.Join(" ", validationErrors));
            }

            return await _powerBIClient.GenerateEmbedTokenAsync(options, workspaceId!, reportId!, datasetId, ct);
        });
    }

    private async Task<IActionResult> ExecutePowerBICallAsync<T>(Func<Task<T>> execute)
    {
        try
        {
            return Ok(await execute());
        }
        catch (PowerBIIntegrationException ex)
        {
            _logger.LogWarning("Power BI API request failed. Code={Code}", ex.Code);
            return StatusCode(ex.StatusCode, new PowerBIErrorResponse(ex.Code, ex.Message, ex.Detail));
        }
    }
}
