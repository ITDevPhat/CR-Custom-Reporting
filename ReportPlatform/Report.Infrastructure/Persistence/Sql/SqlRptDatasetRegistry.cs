using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Report.Metadata.Connections;
using Report.Metadata.Models;
using Report.Metadata.Stores;

namespace Report.Infrastructure.Persistence.Sql;

public sealed class SqlRptDatasetRegistry : IDatasetRegistry
{
    private readonly IRptCatalogConnectionFactory _connectionFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;
    private readonly RptAuditService _audit;

    public SqlRptDatasetRegistry(
        IRptCatalogConnectionFactory connectionFactory,
        IServiceProvider serviceProvider,
        IMemoryCache cache,
        RptAuditService audit)
    {
        _connectionFactory = connectionFactory;
        _serviceProvider = serviceProvider;
        _cache = cache;
        _audit = audit;
    }

    public RegisteredDataset Save(string datasetName, ConnectionDefinition connection, SemanticModel model)
    {
        var datasetId = string.IsNullOrWhiteSpace(model.DatasetId) ? $"dataset_{Guid.NewGuid():N}" : model.DatasetId;
        return SaveExisting(datasetId, datasetName, connection.ConnectionId, model);
    }

    public RegisteredDataset SaveExisting(string datasetId, string datasetName, string connectionId, SemanticModel model)
    {
        SaveDatasetAsync(datasetId, datasetName, connectionId, CancellationToken.None).GetAwaiter().GetResult();
        _cache.Remove(SqlRptSemanticModelStore.CacheKey(datasetId));

        return new RegisteredDataset
        {
            DatasetId = datasetId,
            DatasetName = datasetName,
            ConnectionId = connectionId,
            Model = new SemanticModel
            {
                DatasetId = datasetId,
                DisplayName = string.IsNullOrWhiteSpace(datasetName) ? datasetId : datasetName,
                ConnectionId = connectionId,
                Tables = model.Tables,
                Fields = model.Fields,
                Metrics = model.Metrics,
                SemanticObjects = model.SemanticObjects,
                Relationships = model.Relationships
            }
        };
    }

    public RegisteredDataset? Find(string datasetId)
        => FindAsync(datasetId, CancellationToken.None).GetAwaiter().GetResult();

    private async Task<RegisteredDataset?> FindAsync(string datasetId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT TOP (1) DatasetId, DisplayName, ConnectionId
            FROM rpt.Datasets
            WHERE DatasetId = @DatasetId AND IsActive = 1;
            """,
            new { DatasetId = datasetId },
            cancellationToken: ct));

        if (row is null) return null;

        var modelStore = _serviceProvider.GetRequiredService<ISemanticModelStore>();
        var model = await modelStore.LoadAsync(datasetId, ct);

        return new RegisteredDataset
        {
            DatasetId = Row.GetString(row, "DatasetId"),
            DatasetName = Row.GetString(row, "DisplayName"),
            ConnectionId = Row.GetString(row, "ConnectionId"),
            Model = model
        };
    }

    private async Task SaveDatasetAsync(string datasetId, string datasetName, string connectionId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE rpt.Datasets AS target
            USING (SELECT @DatasetId AS DatasetId) AS source
                ON target.DatasetId = source.DatasetId
            WHEN MATCHED THEN UPDATE SET
                DisplayName = @DisplayName,
                ConnectionId = @ConnectionId,
                IsActive = 1,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (DatasetId, DatasetName, DisplayName, ConnectionId, Status, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@DatasetId, @DatasetName, @DisplayName, @ConnectionId, 'draft', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            new
            {
                DatasetId = datasetId,
                DatasetName = string.IsNullOrWhiteSpace(datasetName) ? datasetId : datasetName,
                DisplayName = string.IsNullOrWhiteSpace(datasetName) ? datasetId : datasetName,
                ConnectionId = connectionId
            },
            cancellationToken: ct));

        await _audit.WriteAsync(datasetId, "rpt.Datasets", datasetId, "upsert", null, ct: ct);
    }
}
