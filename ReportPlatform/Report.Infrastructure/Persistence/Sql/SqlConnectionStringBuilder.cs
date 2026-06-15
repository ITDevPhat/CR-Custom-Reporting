using Microsoft.Data.SqlClient;
using Report.Metadata.Connections;

namespace Report.Infrastructure.Persistence.Sql;

public static class SqlConnectionStringBuilder
{
    public static string Build(ConnectionDefinition def)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = def.Server,
            InitialCatalog = def.Database,
            TrustServerCertificate = def.TrustServerCertificate,
            ConnectTimeout = def.CommandTimeoutSeconds > 0 ? def.CommandTimeoutSeconds : 30
        };
        builder["Encrypt"] = def.Encrypt;

        switch ((def.AuthenticationType ?? "sql").Trim().ToLowerInvariant())
        {
            case "windows":
                builder.IntegratedSecurity = true;
                break;
            case "managed_identity":
            case "managedidentity":
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
                break;
            case "entra":
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
                break;
            case "sql":
            default:
                builder.UserID = def.Username;
                // TODO: resolve/decrypt the password from the configured secret provider when available.
                builder.Password = def.Password;
                break;
        }

        return builder.ConnectionString;
    }
}
