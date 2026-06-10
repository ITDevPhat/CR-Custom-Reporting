using System.Text.Json;
using Microsoft.Extensions.Options;
using Report.Api.DTOs.PowerBI;
using Report.Api.Options.PowerBI;

namespace Report.Api.Services.PowerBI;

public sealed class PowerBIConfigurationStore : IPowerBIConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IConfiguration _configuration;
    private readonly IOptions<PowerBIOptions> _defaults;
    private readonly ILogger<PowerBIConfigurationStore> _logger;

    public PowerBIConfigurationStore(
        IConfiguration configuration,
        IOptions<PowerBIOptions> defaults,
        ILogger<PowerBIConfigurationStore> logger)
    {
        _configuration = configuration;
        _defaults = defaults;
        _logger = logger;
    }

    public async Task<PowerBIOptions> GetEffectiveOptionsAsync(CancellationToken ct)
    {
        var options = Clone(_defaults.Value);
        var sources = new List<string>();

        var persisted = await ReadPersistedOptionsAsync(ct);
        if (persisted is not null)
        {
            Apply(options, persisted);
            sources.Add("local");
        }

        ApplyEnvironmentOverrides(options, sources);
        NormalizeDefaults(options);

        return options;
    }

    public async Task<PowerBIConfigurationDto> GetMaskedConfigurationAsync(CancellationToken ct)
    {
        var effective = await GetEffectiveOptionsAsync(ct);
        var sources = GetSources(effective);
        return ToMaskedDto(effective, sources);
    }

    public async Task<PowerBIConfigurationDto> SaveAsync(
        SavePowerBIConfigurationRequest request,
        CancellationToken ct)
    {
        var current = await ReadPersistedOptionsAsync(ct) ?? new PowerBIOptions();

        current.TenantId = Normalize(request.TenantId);
        current.ClientId = Normalize(request.ClientId);
        current.WorkspaceId = Normalize(request.WorkspaceId);
        current.ReportId = Normalize(request.ReportId);
        current.DatasetId = Normalize(request.DatasetId);
        current.AuthorityUrl = Normalize(request.AuthorityUrl) ?? PowerBIOptionDefaults.AuthorityUrl;
        current.ApiBaseUrl = Normalize(request.ApiBaseUrl) ?? PowerBIOptionDefaults.ApiBaseUrl;

        var normalizedSecret = Normalize(request.ClientSecret);
        if (!string.IsNullOrWhiteSpace(normalizedSecret) && !IsMaskedValue(normalizedSecret))
        {
            current.ClientSecret = normalizedSecret;
        }

        await WritePersistedOptionsAsync(current, ct);
        _logger.LogInformation("Power BI Embedded lab configuration saved to local configuration store.");

        return await GetMaskedConfigurationAsync(ct);
    }

    private async Task<PowerBIOptions?> ReadPersistedOptionsAsync(CancellationToken ct)
    {
        var path = GetConfigurationPath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PowerBIOptions>(stream, SerializerOptions, ct);
    }

    private async Task WritePersistedOptionsAsync(PowerBIOptions options, CancellationToken ct)
    {
        var path = GetConfigurationPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, options, SerializerOptions, ct);
    }

    private string GetConfigurationPath()
    {
        var configuredPath = _configuration[$"{PowerBIOptions.SectionName}:LocalConfigurationPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ReportPlatform", "PowerBIEmbedLab", "powerbi-settings.local.json");
    }

    private static PowerBIOptions Clone(PowerBIOptions options)
    {
        return new PowerBIOptions
        {
            TenantId = options.TenantId,
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            WorkspaceId = options.WorkspaceId,
            ReportId = options.ReportId,
            DatasetId = options.DatasetId,
            AuthorityUrl = options.AuthorityUrl,
            ApiBaseUrl = options.ApiBaseUrl,
            LocalConfigurationPath = options.LocalConfigurationPath
        };
    }

    private static void Apply(PowerBIOptions target, PowerBIOptions source)
    {
        target.TenantId = Coalesce(source.TenantId, target.TenantId);
        target.ClientId = Coalesce(source.ClientId, target.ClientId);
        target.ClientSecret = Coalesce(source.ClientSecret, target.ClientSecret);
        target.WorkspaceId = Coalesce(source.WorkspaceId, target.WorkspaceId);
        target.ReportId = Coalesce(source.ReportId, target.ReportId);
        target.DatasetId = Coalesce(source.DatasetId, target.DatasetId);
        target.AuthorityUrl = Coalesce(source.AuthorityUrl, target.AuthorityUrl) ?? PowerBIOptionDefaults.AuthorityUrl;
        target.ApiBaseUrl = Coalesce(source.ApiBaseUrl, target.ApiBaseUrl) ?? PowerBIOptionDefaults.ApiBaseUrl;
    }

    private static void ApplyEnvironmentOverrides(PowerBIOptions options, List<string> sources)
    {
        var applied = false;

        applied |= ApplyEnvironment("POWERBI_TENANT_ID", value => options.TenantId = value);
        applied |= ApplyEnvironment("POWERBI_CLIENT_ID", value => options.ClientId = value);
        applied |= ApplyEnvironment("POWERBI_CLIENT_SECRET", value => options.ClientSecret = value);
        applied |= ApplyEnvironment("POWERBI_WORKSPACE_ID", value => options.WorkspaceId = value);
        applied |= ApplyEnvironment("POWERBI_REPORT_ID", value => options.ReportId = value);
        applied |= ApplyEnvironment("POWERBI_DATASET_ID", value => options.DatasetId = value);
        applied |= ApplyEnvironment("POWERBI_AUTHORITY_URL", value => options.AuthorityUrl = value);
        applied |= ApplyEnvironment("POWERBI_API_BASE_URL", value => options.ApiBaseUrl = value);

        if (applied)
        {
            sources.Add("environment");
        }
    }

    private static bool ApplyEnvironment(string key, Action<string> apply)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        apply(value);
        return true;
    }

    private static string[] GetSources(PowerBIOptions options)
    {
        var sources = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.TenantId) ||
            !string.IsNullOrWhiteSpace(options.ClientId) ||
            !string.IsNullOrWhiteSpace(options.WorkspaceId) ||
            !string.IsNullOrWhiteSpace(options.ReportId))
        {
            sources.Add("configuration");
        }

        if (Environment.GetEnvironmentVariable("POWERBI_TENANT_ID") is not null ||
            Environment.GetEnvironmentVariable("POWERBI_CLIENT_ID") is not null ||
            Environment.GetEnvironmentVariable("POWERBI_CLIENT_SECRET") is not null)
        {
            sources.Add("environment");
        }

        return sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void NormalizeDefaults(PowerBIOptions options)
    {
        options.AuthorityUrl = Normalize(options.AuthorityUrl) ?? PowerBIOptionDefaults.AuthorityUrl;
        options.ApiBaseUrl = Normalize(options.ApiBaseUrl) ?? PowerBIOptionDefaults.ApiBaseUrl;
    }

    private static PowerBIConfigurationDto ToMaskedDto(PowerBIOptions options, string[] sources)
    {
        return new PowerBIConfigurationDto(
            options.TenantId,
            options.ClientId,
            options.ClientSecret is null ? null : "********",
            options.WorkspaceId,
            options.ReportId,
            options.DatasetId,
            options.AuthorityUrl,
            options.ApiBaseUrl,
            !string.IsNullOrWhiteSpace(options.ClientSecret),
            sources);
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= 8
            ? "********"
            : $"{value[..4]}****{value[^4..]}";
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Coalesce(string? primary, string? fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary.Trim();
    }

    private static bool IsMaskedValue(string value)
    {
        return value.All(static c => c == '*');
    }

    private static class PowerBIOptionDefaults
    {
        public const string AuthorityUrl = "https://login.microsoftonline.com";
        public const string ApiBaseUrl = "https://api.powerbi.com/v1.0/myorg";
    }
}
