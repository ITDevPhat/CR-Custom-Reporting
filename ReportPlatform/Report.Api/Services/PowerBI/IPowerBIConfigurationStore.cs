using Report.Api.DTOs.PowerBI;
using Report.Api.Options.PowerBI;

namespace Report.Api.Services.PowerBI;

public interface IPowerBIConfigurationStore
{
    Task<PowerBIOptions> GetEffectiveOptionsAsync(CancellationToken ct);
    Task<PowerBIConfigurationDto> GetMaskedConfigurationAsync(CancellationToken ct);
    Task<PowerBIConfigurationDto> SaveAsync(SavePowerBIConfigurationRequest request, CancellationToken ct);
}
