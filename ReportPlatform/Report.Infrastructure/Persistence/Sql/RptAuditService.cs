using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Report.Infrastructure.Persistence.Sql;

public sealed class RptAuditService
{
    private readonly IRptCatalogConnectionFactory _connectionFactory;
    private readonly ILogger<RptAuditService> _logger;

    public RptAuditService(IRptCatalogConnectionFactory connectionFactory, ILogger<RptAuditService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task WriteAsync(
        string? datasetId,
        string tableName,
        string recordId,
        string operation,
        string? changedBy,
        string? fieldName = null,
        string? oldValue = null,
        string? newValue = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(
                "rpt.usp_WriteAudit",
                new
                {
                    DatasetId = datasetId,
                    TableName = tableName,
                    RecordId = recordId,
                    Operation = operation,
                    ChangedBy = changedBy,
                    FieldName = fieldName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    CorrelationId = correlationId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write rpt audit record for {TableName}/{RecordId}.", tableName, recordId);
        }
    }
}
