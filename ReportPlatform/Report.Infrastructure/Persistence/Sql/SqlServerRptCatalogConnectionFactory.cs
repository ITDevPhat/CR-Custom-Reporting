using System.Data;
using Microsoft.Data.SqlClient;

namespace Report.Infrastructure.Persistence.Sql;

public sealed class SqlServerRptCatalogConnectionFactory : IRptCatalogConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerRptCatalogConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
