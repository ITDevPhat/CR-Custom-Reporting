using Microsoft.Identity.Client;
using Report.Api.Options.PowerBI;
using Report.Api.Validators.PowerBI;

namespace Report.Api.Services.PowerBI;

public sealed class MsalPowerBIAuthService : IPowerBIAuthService
{
    private const string PowerBIScope = "https://analysis.windows.net/powerbi/api/.default";
    private readonly ILogger<MsalPowerBIAuthService> _logger;

    public MsalPowerBIAuthService(ILogger<MsalPowerBIAuthService> logger)
    {
        _logger = logger;
    }

    public async Task<string> AcquireAccessTokenAsync(PowerBIOptions options, CancellationToken ct)
    {
        var validationErrors = PowerBIConfigurationValidator.ValidateForAuthentication(options);
        if (validationErrors.Count > 0)
        {
            throw new PowerBIIntegrationException(
                "POWERBI_INVALID_CONFIGURATION",
                "Power BI authentication configuration is incomplete or invalid.",
                StatusCodes.Status400BadRequest,
                string.Join(" ", validationErrors));
        }

        try
        {
            var authority = $"{options.AuthorityUrl.TrimEnd('/')}/{options.TenantId}";
            var app = ConfidentialClientApplicationBuilder
                .Create(options.ClientId)
                .WithClientSecret(options.ClientSecret)
                .WithAuthority(authority)
                .Build();

            var result = await app
                .AcquireTokenForClient(new[] { PowerBIScope })
                .ExecuteAsync(ct);

            _logger.LogInformation("Power BI Entra token acquired for client {ClientId}.", MaskClientId(options.ClientId));
            return result.AccessToken;
        }
        catch (MsalServiceException ex)
        {
            _logger.LogWarning(ex, "Power BI Entra authentication failed. ErrorCode={ErrorCode}", ex.ErrorCode);
            throw new PowerBIIntegrationException(
                MapAuthenticationCode(ex),
                "Microsoft Entra authentication failed.",
                StatusCodes.Status401Unauthorized,
                ex.Message);
        }
        catch (MsalClientException ex)
        {
            _logger.LogWarning(ex, "Power BI Entra client authentication failed. ErrorCode={ErrorCode}", ex.ErrorCode);
            throw new PowerBIIntegrationException(
                "POWERBI_AUTH_CLIENT_FAILURE",
                "Unable to acquire a Power BI access token.",
                StatusCodes.Status401Unauthorized,
                ex.Message);
        }
    }

    private static string MapAuthenticationCode(MsalServiceException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        if (message.Contains("tenant") || message.Contains("authority"))
        {
            return "POWERBI_INVALID_TENANT";
        }

        if (message.Contains("client") && message.Contains("secret"))
        {
            return "POWERBI_INVALID_CLIENT_SECRET";
        }

        if (message.Contains("application") || message.Contains("client_id"))
        {
            return "POWERBI_INVALID_CLIENT_ID";
        }

        if (message.Contains("disabled"))
        {
            return "POWERBI_SERVICE_PRINCIPAL_DISABLED";
        }

        return "POWERBI_AUTHENTICATION_FAILED";
    }

    private static string? MaskClientId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 8)
        {
            return "********";
        }

        return $"{value[..4]}****{value[^4..]}";
    }
}
