using Report.Api.Options.PowerBI;

namespace Report.Api.Validators.PowerBI;

public static class PowerBIConfigurationValidator
{
    public static IReadOnlyList<string> ValidateForAuthentication(PowerBIOptions options)
    {
        var errors = new List<string>();

        ValidateGuid(options.TenantId, "Tenant ID", errors);
        ValidateGuid(options.ClientId, "Client ID", errors);

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            errors.Add("Client Secret is required.");
        }

        if (!Uri.TryCreate(options.AuthorityUrl, UriKind.Absolute, out _))
        {
            errors.Add("Authority URL must be an absolute URL.");
        }

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("API Base URL must be an absolute URL.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateWorkspaceAndReport(
        string? workspaceId,
        string? reportId)
    {
        var errors = new List<string>();
        ValidateGuid(workspaceId, "Workspace ID", errors);
        ValidateGuid(reportId, "Report ID", errors);
        return errors;
    }

    public static void ValidateGuid(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (!Guid.TryParse(value, out _))
        {
            errors.Add($"{fieldName} must be a valid GUID.");
        }
    }
}
