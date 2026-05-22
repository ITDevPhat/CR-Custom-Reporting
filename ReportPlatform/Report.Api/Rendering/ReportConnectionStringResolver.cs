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

        return SqlServerConnectionFactory.BuildConnectionString(connection);
    }
}
