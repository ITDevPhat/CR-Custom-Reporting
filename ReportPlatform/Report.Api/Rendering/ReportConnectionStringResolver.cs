using Report.Infrastructure.Connections;
using Report.Metadata.Connections;

namespace Report.Api.Rendering;

public sealed class ReportConnectionStringResolver : IReportConnectionStringResolver
{
    private readonly IConnectionRegistry _connectionRegistry;

    public ReportConnectionStringResolver(IConnectionRegistry connectionRegistry)
    {
        _connectionRegistry = connectionRegistry;
    }

    public string Resolve(string connectionId)
    {
        var connection = _connectionRegistry.Find(connectionId);
        if (connection is null)
        {
            throw new ReportExportException($"Unknown connectionId '{connectionId}'.", 400);
        }

        var connectionString = SqlServerConnectionFactory.BuildConnectionString(connection);

        return NormalizeForTelerikSqlDataSource(connectionString);
    }

    private static string NormalizeForTelerikSqlDataSource(string connectionString)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var normalizedParts = new List<string>();

        foreach (var part in parts)
        {
            var index = part.IndexOf('=');
            if (index <= 0)
            {
                normalizedParts.Add(part);
                continue;
            }

            var key = part[..index].Trim();
            var value = part[(index + 1)..].Trim();

            key = key switch
            {
                var k when k.Equals("Trust Server Certificate", StringComparison.OrdinalIgnoreCase)
                    => "TrustServerCertificate",

                var k when k.Equals("TrustServerCertificate", StringComparison.OrdinalIgnoreCase)
                    => "TrustServerCertificate",

                _ => key
            };

            normalizedParts.Add($"{key}={value}");
        }

        return string.Join(';', normalizedParts);
    }
}
