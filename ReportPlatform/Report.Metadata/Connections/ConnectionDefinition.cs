namespace Report.Metadata.Connections;

public sealed class ConnectionDefinition
{
    public string ConnectionId { get; init; } = "";
    public string Provider { get; init; } = "sqlserver";
    public string Server { get; init; } = "";
    public string Database { get; init; } = "";
    public string AuthenticationType { get; init; } = "sql";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public bool TrustServerCertificate { get; init; } = true;
    public bool Encrypt { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;
}
