using Report.Api.Options.PowerBI;

namespace Report.Api.Services.PowerBI;

public static class PowerBIServiceCollectionExtensions
{
    public static IServiceCollection AddPowerBIEmbedLab(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PowerBIOptions>(configuration.GetSection(PowerBIOptions.SectionName));
        services.AddSingleton<IPowerBIConfigurationStore, PowerBIConfigurationStore>();
        services.AddSingleton<IPowerBIAuthService, MsalPowerBIAuthService>();
        services.AddHttpClient<IPowerBIRestClient, PowerBIRestClient>();

        return services;
    }
}
