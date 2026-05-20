using Microsoft.Data.SqlClient;
using Report.Contracts.Connections;
using Report.Metadata.Connections;

namespace Report.Infrastructure.Connections;

public static class SqlServerConnectionFactory
{
    public static ConnectionDefinition ToDefinition(CreateConnectionRequest request)
    {
        return new ConnectionDefinition
        {
            Provider = request.Provider,
            Server = request.Server,
            Database = request.Database,
            AuthenticationType = request.AuthenticationType,
            Username = request.Username,
            Password = request.Password,
            TrustServerCertificate = request.TrustServerCertificate,
            Encrypt = request.Encrypt,
            CommandTimeoutSeconds = request.CommandTimeoutSeconds <= 0 ? 30 : request.CommandTimeoutSeconds
        };
    }

    public static string BuildConnectionString(ConnectionDefinition definition, string? databaseOverride = null)
    {
        if (!definition.Provider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Provider '{definition.Provider}' is not supported.");
        }

        if (definition.AuthenticationType.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Windows authentication is not implemented for this MVP.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = definition.Server,
            InitialCatalog = databaseOverride ?? definition.Database,
            UserID = definition.Username,
            Password = definition.Password,
            TrustServerCertificate = definition.TrustServerCertificate,
            Encrypt = definition.Encrypt,
            ConnectTimeout = definition.CommandTimeoutSeconds <= 0 ? 30 : definition.CommandTimeoutSeconds
        };

        return builder.ConnectionString;
    }
}
