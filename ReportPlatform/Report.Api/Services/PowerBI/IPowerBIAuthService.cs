using Report.Api.Options.PowerBI;

namespace Report.Api.Services.PowerBI;

public interface IPowerBIAuthService
{
    Task<string> AcquireAccessTokenAsync(PowerBIOptions options, CancellationToken ct);
}
