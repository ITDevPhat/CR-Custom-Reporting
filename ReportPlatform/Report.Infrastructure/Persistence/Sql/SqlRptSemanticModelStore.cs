using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Report.Metadata.Models;
using Report.Metadata.Stores;

namespace Report.Infrastructure.Persistence.Sql;

public sealed class SqlRptSemanticModelStore : ISemanticModelStore
{
    private readonly IRptCatalogConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public SqlRptSemanticModelStore(
        IRptCatalogConnectionFactory connectionFactory,
        IMemoryCache cache,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        var configuredTtl = configuration["SemanticModelCache:TtlMinutes"];
        var ttlMinutes = int.TryParse(configuredTtl, out var parsedTtl) ? parsedTtl : 10;
        _ttl = TimeSpan.FromMinutes(ttlMinutes <= 0 ? 10 : ttlMinutes);
    }

    public async Task<SemanticModel> LoadAsync(string datasetId, CancellationToken ct)
    {
        var cacheKey = CacheKey(datasetId);
        if (_cache.TryGetValue(cacheKey, out SemanticModel? cached) && cached is not null)
        {
            return cached;
        }

        using var rootConnection = await _connectionFactory.CreateAsync(ct);
        var dataset = await rootConnection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT TOP (1) DatasetId, DisplayName, ConnectionId
            FROM rpt.Datasets
            WHERE DatasetId = @DatasetId
              AND IsActive = 1
              AND (Status IS NULL OR Status IN ('published', 'draft'));
            """,
            new { DatasetId = datasetId },
            cancellationToken: ct));

        if (dataset is null)
        {
            throw new KeyNotFoundException($"Dataset '{datasetId}' was not found in rpt.Datasets.");
        }

        var tablesTask = QueryAsync("""
            SELECT *
            FROM rpt.ReportingEntities
            WHERE DatasetId = @DatasetId AND IsActive = 1;
            """, datasetId, ct);
        var fieldsTask = QueryAsync("""
            SELECT *
            FROM rpt.vw_SemanticFields
            WHERE DatasetId = @DatasetId;
            """, datasetId, ct);
        var metricsTask = QueryAsync("""
            SELECT *
            FROM rpt.ReportingMetrics
            WHERE DatasetId = @DatasetId AND IsActive = 1;
            """, datasetId, ct);
        var relationshipsTask = QueryAsync("""
            SELECT *
            FROM rpt.vw_SemanticRelationships
            WHERE DatasetId = @DatasetId AND IsActive = 1;
            """, datasetId, ct);
        var objectsTask = QueryAsync("""
            SELECT *
            FROM rpt.SemanticObjects
            WHERE DatasetId = @DatasetId AND IsActive = 1;
            """, datasetId, ct);

        await Task.WhenAll(tablesTask, fieldsTask, metricsTask, relationshipsTask, objectsTask);

        var model = new SemanticModel
        {
            DatasetId = Row.GetString(dataset, "DatasetId"),
            DisplayName = Row.GetString(dataset, "DisplayName"),
            ConnectionId = Row.GetString(dataset, "ConnectionId"),
            Tables = tablesTask.Result.Select(MapTable).ToList(),
            Fields = fieldsTask.Result.Select(MapField).ToList(),
            Metrics = metricsTask.Result.Select(MapMetric).ToList(),
            Relationships = relationshipsTask.Result.Select(MapRelationship).ToList(),
            SemanticObjects = objectsTask.Result.Select(MapObject).ToList()
        };

        _cache.Set(cacheKey, model, _ttl);
        return model;
    }

    public static string CacheKey(string datasetId) => $"semantic_model_{datasetId}";

    private async Task<IEnumerable<dynamic>> QueryAsync(string sql, string datasetId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        return await connection.QueryAsync(new CommandDefinition(sql, new { DatasetId = datasetId }, cancellationToken: ct));
    }

    private static SemanticTable MapTable(dynamic row) => new()
    {
        TableId = Row.GetFirstString(row, "", "EntityKey", "EntityId"),
        DisplayName = Row.GetString(row, "DisplayName"),
        TableType = Row.GetString(row, "EntityType", "unknown"),
        Grain = Row.GetString(row, "Grain"),
        PhysicalSchema = Row.GetString(row, "PhysicalSchemaName"),
        PhysicalTable = Row.GetString(row, "PhysicalTableName")
    };

    private static SemanticField MapField(dynamic row) => new()
    {
        FieldId = Row.GetFirstString(row, "", "FieldKey", "FieldId"),
        DatasetId = Row.GetString(row, "DatasetId"),
        TableId = Row.GetFirstString(row, "", "EntityKey", "EntityId"),
        PhysicalSchema = Row.GetString(row, "PhysicalSchemaName"),
        PhysicalTable = Row.GetString(row, "PhysicalTableName"),
        PhysicalColumn = Row.GetString(row, "PhysicalColumnName"),
        OrdinalPosition = Row.GetInt(row, "OrdinalPosition"),
        IsNullable = Row.GetBool(row, "IsNullable", true),
        IsPrimaryKey = Row.GetBool(row, "IsPrimaryKey"),
        IsForeignKey = Row.GetBool(row, "IsForeignKey"),
        ParticipatesInRelationship = Row.GetBool(row, "ParticipatesInRelationship"),
        IsUnique = Row.GetBool(row, "IsUnique"),
        ReferencedSchema = Row.GetString(row, "ReferencedSchema"),
        ReferencedTable = Row.GetString(row, "ReferencedTable"),
        ReferencedColumn = Row.GetString(row, "ReferencedColumn"),
        ForeignKeyName = Row.GetString(row, "ForeignKeyName"),
        DisplayName = Row.GetString(row, "DisplayName"),
        DataType = Row.GetString(row, "DataType"),
        SqlDataType = Row.GetString(row, "SqlDataType"),
        CharacterMaximumLength = Row.GetNullableInt(row, "CharacterMaximumLength"),
        NumericPrecision = Row.GetByte(row, "NumericPrecision"),
        NumericScale = Row.GetNullableInt(row, "NumericScale"),
        DatetimePrecision = Row.GetShort(row, "DatetimePrecision"),
        Role = Row.GetString(row, "FieldRole"),
        Grain = Row.GetString(row, "Grain"),
        SemanticType = Row.GetString(row, "SemanticType"),
        DefaultAggregation = Row.GetString(row, "DefaultAggregation", "none"),
        Format = Row.GetString(row, "FormatString", "general"),
        IsHidden = Row.GetBool(row, "IsHidden"),
        IsDraggable = Row.GetBool(row, "IsDraggable", true),
        ClassificationReason = Row.GetString(row, "ClassificationReason"),
        Expression = NullIfEmpty(Row.GetString(row, "ExpressionSql")),
        BaseTableId = NullIfEmpty(Row.GetFirstString(row, "", "BaseEntityKey", "BaseEntityId")),
        IsDerived = Row.GetBool(row, "IsDerived")
    };

    private static SemanticMetric MapMetric(dynamic row) => new()
    {
        MetricId = Row.GetFirstString(row, "", "MetricKey", "MetricId"),
        DatasetId = Row.GetString(row, "DatasetId"),
        DisplayName = Row.GetString(row, "DisplayName"),
        Formula = Row.GetString(row, "Formula"),
        BaseTableId = Row.GetFirstString(row, "", "BaseEntityKey", "BaseEntityId"),
        AggregationBehavior = Row.GetString(row, "AggregationBehavior", "additive"),
        DataType = Row.GetString(row, "DataType", "decimal"),
        Format = Row.GetString(row, "FormatString", "general"),
        IsHidden = Row.GetBool(row, "IsHidden"),
        IsDraggable = Row.GetBool(row, "IsDraggable", true)
    };

    private static SemanticRelationship MapRelationship(dynamic row)
    {
        var isActive = Row.GetBool(row, "IsActive", true);
        return new SemanticRelationship
        {
            RelationshipId = Row.GetString(row, "RelationshipId"),
            DatasetId = Row.GetString(row, "DatasetId"),
            FromTableId = Row.GetFirstString(row, "", "FromEntityKey", "FromEntityId"),
            FromColumn = Row.GetString(row, "FromColumnName"),
            ToTableId = Row.GetFirstString(row, "", "ToEntityKey", "ToEntityId"),
            ToColumn = Row.GetString(row, "ToColumnName"),
            JoinType = Row.GetString(row, "JoinType", "INNER"),
            Cardinality = Row.GetString(row, "Cardinality", "N:1"),
            CrossFilterDirection = Row.GetString(row, "CrossFilterDirection", "single"),
            IsActive = isActive,
            IsPrimary = Row.GetBool(row, "IsPrimary", true),
            Source = Row.GetString(row, "Source", "manual"),
            Confidence = Row.GetDecimal(row, "Confidence", 1m),
            Status = isActive ? "active" : "inactive",
            Warning = NullIfEmpty(Row.GetString(row, "Warning"))
        };
    }

    private static SemanticObject MapObject(dynamic row) => new()
    {
        Id = Row.GetFirstString(row, "", "ObjectKey", "ObjectId"),
        DatasetId = Row.GetString(row, "DatasetId"),
        TableId = NullIfEmpty(Row.GetFirstString(row, "", "EntityKey", "EntityId")),
        DisplayName = Row.GetString(row, "DisplayName"),
        ObjectType = Enum.Parse<SemanticObjectType>(Row.GetString(row, "ObjectType", nameof(SemanticObjectType.ExpressionFragment)), true),
        Scope = Enum.Parse<ExpressionScope>(Row.GetString(row, "Scope", nameof(ExpressionScope.Row)), true),
        Expression = Row.GetString(row, "ExpressionSql"),
        DataType = Row.GetString(row, "DataType"),
        Format = NullIfEmpty(Row.GetString(row, "FormatString")),
        Dependencies = ParseDependencies(Row.GetString(row, "Dependencies", "[]")),
        IsHidden = Row.GetBool(row, "IsHidden"),
        IsDraggable = Row.GetBool(row, "IsDraggable", true)
    };

    private static List<string> ParseDependencies(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
