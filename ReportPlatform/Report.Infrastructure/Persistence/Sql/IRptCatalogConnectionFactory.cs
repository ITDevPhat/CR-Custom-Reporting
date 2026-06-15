using System.Data;

namespace Report.Infrastructure.Persistence.Sql;

public interface IRptCatalogConnectionFactory
{
    Task<IDbConnection> CreateAsync(CancellationToken ct);
}
