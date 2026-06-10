namespace Report.Api.DTOs.PowerBI;

public sealed record PowerBIConfigurationDto(
    string? TenantId,
    string? ClientId,
    string? ClientSecret,
    string? WorkspaceId,
    string? ReportId,
    string? DatasetId,
    string? AuthorityUrl,
    string? ApiBaseUrl,
    bool HasClientSecret,
    string[] Sources);

public sealed record SavePowerBIConfigurationRequest(
    string? TenantId,
    string? ClientId,
    string? ClientSecret,
    string? WorkspaceId,
    string? ReportId,
    string? DatasetId,
    string? AuthorityUrl,
    string? ApiBaseUrl);

public sealed record PowerBIConnectionTestResponse(
    bool Success,
    string AuthenticationStatus,
    bool WorkspaceAccessible,
    bool ReportAccessible,
    string? WorkspaceName,
    string? ReportName,
    string? Message,
    string[] Diagnostics);

public sealed record PowerBIEmbedTokenRequest(
    string? WorkspaceId,
    string? ReportId,
    string? DatasetId);

public sealed record PowerBIEmbedTokenResponse(
    string ReportId,
    string ReportName,
    string EmbedUrl,
    string EmbedToken,
    string TokenType,
    DateTimeOffset Expiration);

public sealed record PowerBIErrorResponse(
    string Code,
    string Message,
    string? Detail = null);
