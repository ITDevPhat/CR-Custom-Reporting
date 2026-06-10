namespace Report.Api.Options.PowerBI;

public sealed class PowerBIOptions
{
    public const string SectionName = "PowerBI";

    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? WorkspaceId { get; set; }
    public string? ReportId { get; set; }
    public string? DatasetId { get; set; }
    public string AuthorityUrl { get; set; } = "https://login.microsoftonline.com";
    public string ApiBaseUrl { get; set; } = "https://api.powerbi.com/v1.0/myorg";
    public string? LocalConfigurationPath { get; set; }
}
