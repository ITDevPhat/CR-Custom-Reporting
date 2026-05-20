using Microsoft.AspNetCore.Mvc;
using Report.Contracts.Connections;
using Report.Infrastructure.Connections;
using Report.Metadata.Connections;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/connections")]
public sealed class ConnectionsController : ControllerBase
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly SqlServerSchemaDiscoveryService _discoveryService;

    public ConnectionsController(
        IConnectionRegistry connectionRegistry,
        SqlServerSchemaDiscoveryService discoveryService)
    {
        _connectionRegistry = connectionRegistry;
        _discoveryService = discoveryService;
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(
        [FromBody] TestConnectionRequest request,
        CancellationToken ct)
    {
        try
        {
            var definition = SqlServerConnectionFactory.ToDefinition(request);
            await _discoveryService.TestConnectionAsync(definition, ct);
            var saved = _connectionRegistry.Save(definition);

            return Ok(new ConnectionTestResponse
            {
                Success = true,
                Message = "Connection test succeeded.",
                Connection = ToDto(saved)
            });
        }
        catch (Exception ex)
        {
            return Ok(new ConnectionTestResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    [HttpGet("{connectionId}/databases")]
    public async Task<IActionResult> Databases(string connectionId, CancellationToken ct)
    {
        var connection = _connectionRegistry.Find(connectionId);
        if (connection is null)
        {
            return NotFound($"Connection '{connectionId}' was not found.");
        }

        return Ok(await _discoveryService.GetDatabasesAsync(connection, ct));
    }

    [HttpPost("discover")]
    public async Task<IActionResult> Discover(
        [FromBody] CreateConnectionRequest request,
        CancellationToken ct)
    {
        var definition = SqlServerConnectionFactory.ToDefinition(request);
        var response = await _discoveryService.DiscoverAsync(definition, ct);
        return Ok(response);
    }

    [HttpPost("preview-table")]
    public async Task<IActionResult> PreviewTable(
        [FromBody] PreviewTableRequest request,
        CancellationToken ct)
    {
        var definition = SqlServerConnectionFactory.ToDefinition(request.Connection);
        var response = await _discoveryService.PreviewTableAsync(
            definition,
            request.Schema,
            request.Table,
            request.Limit,
            ct);

        return Ok(response);
    }

    private static ConnectionDto ToDto(ConnectionDefinition definition)
    {
        return new ConnectionDto
        {
            ConnectionId = definition.ConnectionId,
            Provider = definition.Provider,
            Server = definition.Server,
            Database = definition.Database,
            AuthenticationType = definition.AuthenticationType,
            TrustServerCertificate = definition.TrustServerCertificate,
            Encrypt = definition.Encrypt,
            CommandTimeoutSeconds = definition.CommandTimeoutSeconds
        };
    }
}
