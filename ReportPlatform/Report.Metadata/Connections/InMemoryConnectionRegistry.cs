using System.Collections.Concurrent;

namespace Report.Metadata.Connections;

public sealed class InMemoryConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConnectionDefinition> _connections = new();
    private int _nextId;

    public ConnectionDefinition Save(ConnectionDefinition definition)
    {
        var connectionId = string.IsNullOrWhiteSpace(definition.ConnectionId)
            ? $"conn_{Interlocked.Increment(ref _nextId):000}"
            : definition.ConnectionId;

        var saved = new ConnectionDefinition
        {
            ConnectionId = connectionId,
            Provider = definition.Provider,
            Server = definition.Server,
            Database = definition.Database,
            AuthenticationType = definition.AuthenticationType,
            Username = definition.Username,
            Password = definition.Password,
            TrustServerCertificate = definition.TrustServerCertificate,
            Encrypt = definition.Encrypt,
            CommandTimeoutSeconds = definition.CommandTimeoutSeconds
        };

        _connections[connectionId] = saved;
        return saved;
    }

    public ConnectionDefinition? Find(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var definition)
            ? definition
            : null;
    }
}
