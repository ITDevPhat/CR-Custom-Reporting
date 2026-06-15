using Dapper;
using Report.Metadata.Connections;

namespace Report.Infrastructure.Persistence.Sql;

public sealed class SqlRptConnectionRegistry : IConnectionRegistry
{
    private readonly IRptCatalogConnectionFactory _connectionFactory;
    private readonly RptAuditService _audit;

    public SqlRptConnectionRegistry(IRptCatalogConnectionFactory connectionFactory, RptAuditService audit)
    {
        _connectionFactory = connectionFactory;
        _audit = audit;
    }

    public ConnectionDefinition Save(ConnectionDefinition definition)
    {
        var saved = string.IsNullOrWhiteSpace(definition.ConnectionId)
            ? Clone(definition, $"conn_{Guid.NewGuid():N}")
            : Clone(definition, definition.ConnectionId);

        SaveAsync(saved, CancellationToken.None).GetAwaiter().GetResult();
        return saved;
    }

    public ConnectionDefinition? Find(string connectionId)
        => FindAsync(connectionId, CancellationToken.None).GetAwaiter().GetResult();

    private async Task<ConnectionDefinition?> FindAsync(string connectionId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT TOP (1)
                ConnectionId,
                Provider,
                ServerName AS Server,
                DatabaseName AS [Database],
                AuthenticationType,
                Username,
                CAST(NULL AS nvarchar(max)) AS Password,
                TrustServerCertificate,
                EncryptConnection AS Encrypt,
                CommandTimeoutSeconds
            FROM rpt.Connections
            WHERE ConnectionId = @ConnectionId AND IsActive = 1;
            """,
            new { ConnectionId = connectionId },
            cancellationToken: ct));

        return row is null ? null : Map(row);
    }

    private async Task SaveAsync(ConnectionDefinition definition, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE rpt.Connections AS target
            USING (SELECT @ConnectionId AS ConnectionId) AS source
                ON target.ConnectionId = source.ConnectionId
            WHEN MATCHED THEN UPDATE SET
                Provider = @Provider,
                ServerName = @Server,
                DatabaseName = @Database,
                AuthenticationType = @AuthenticationType,
                Username = @Username,
                TrustServerCertificate = @TrustServerCertificate,
                EncryptConnection = @Encrypt,
                CommandTimeoutSeconds = @CommandTimeoutSeconds,
                IsActive = 1,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (ConnectionId, DisplayName, Provider, ServerName, DatabaseName, AuthenticationType, Username,
                 TrustServerCertificate, EncryptConnection, CommandTimeoutSeconds, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@ConnectionId, @DisplayName, @Provider, @Server, @Database, @AuthenticationType, @Username,
                 @TrustServerCertificate, @Encrypt, @CommandTimeoutSeconds, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            new
            {
                definition.ConnectionId,
                DisplayName = string.IsNullOrWhiteSpace(definition.Database) ? definition.ConnectionId : definition.Database,
                definition.Provider,
                definition.Server,
                definition.Database,
                definition.AuthenticationType,
                definition.Username,
                definition.TrustServerCertificate,
                definition.Encrypt,
                definition.CommandTimeoutSeconds
            },
            cancellationToken: ct));

        await _audit.WriteAsync(null, "rpt.Connections", definition.ConnectionId, "upsert", null, ct: ct);
    }

    private static ConnectionDefinition Map(dynamic row) => new()
    {
        ConnectionId = Row.GetString(row, "ConnectionId"),
        Provider = Row.GetString(row, "Provider", "sqlserver"),
        Server = Row.GetString(row, "Server"),
        Database = Row.GetString(row, "Database"),
        AuthenticationType = Row.GetString(row, "AuthenticationType", "sql"),
        Username = Row.GetString(row, "Username"),
        Password = "",
        TrustServerCertificate = Row.GetBool(row, "TrustServerCertificate", true),
        Encrypt = Row.GetBool(row, "Encrypt"),
        CommandTimeoutSeconds = Row.GetInt(row, "CommandTimeoutSeconds", 30)
    };

    private static ConnectionDefinition Clone(ConnectionDefinition source, string connectionId) => new()
    {
        ConnectionId = connectionId,
        Provider = source.Provider,
        Server = source.Server,
        Database = source.Database,
        AuthenticationType = source.AuthenticationType,
        Username = source.Username,
        Password = source.Password,
        TrustServerCertificate = source.TrustServerCertificate,
        Encrypt = source.Encrypt,
        CommandTimeoutSeconds = source.CommandTimeoutSeconds
    };
}
