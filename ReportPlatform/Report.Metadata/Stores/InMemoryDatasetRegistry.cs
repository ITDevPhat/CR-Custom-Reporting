using System.Collections.Concurrent;
using Report.Metadata.Connections;
using Report.Metadata.Models;

namespace Report.Metadata.Stores;

public sealed class InMemoryDatasetRegistry : IDatasetRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredDataset> _datasets = new();
    private int _nextId;

    public RegisteredDataset Save(string datasetName, ConnectionDefinition connection, SemanticModel model)
    {
        var normalizedName = NormalizeDatasetName(datasetName);
        var datasetId = $"dataset_{normalizedName}_{Interlocked.Increment(ref _nextId):000}";
        var savedModel = new SemanticModel
        {
            DatasetId = datasetId,
            DisplayName = string.IsNullOrWhiteSpace(datasetName) ? datasetId : datasetName,
            ConnectionId = connection.ConnectionId,
            Tables = model.Tables,
            Fields = model.Fields.Select(f => new SemanticField
            {
                FieldId = f.FieldId,
                DatasetId = datasetId,
                TableId = f.TableId,
                PhysicalSchema = f.PhysicalSchema,
                PhysicalTable = f.PhysicalTable,
                PhysicalColumn = f.PhysicalColumn,
                OrdinalPosition = f.OrdinalPosition,
                IsNullable = f.IsNullable,
                IsPrimaryKey = f.IsPrimaryKey,
                IsForeignKey = f.IsForeignKey,
                ParticipatesInRelationship = f.ParticipatesInRelationship,
                IsUnique = f.IsUnique,
                ReferencedSchema = f.ReferencedSchema,
                ReferencedTable = f.ReferencedTable,
                ReferencedColumn = f.ReferencedColumn,
                ForeignKeyName = f.ForeignKeyName,
                DisplayName = f.DisplayName,
                DataType = f.DataType,
                SqlDataType = f.SqlDataType,
                CharacterMaximumLength = f.CharacterMaximumLength,
                NumericPrecision = f.NumericPrecision,
                NumericScale = f.NumericScale,
                DatetimePrecision = f.DatetimePrecision,
                Role = f.Role,
                Grain = f.Grain,
                SemanticType = f.SemanticType,
                DefaultAggregation = f.DefaultAggregation,
                Format = f.Format,
                Expression = f.Expression,
                BaseTableId = f.BaseTableId,
                IsDerived = f.IsDerived,
                IsHidden = f.IsHidden,
                IsDraggable = f.IsDraggable,
                ClassificationReason = f.ClassificationReason
            }).ToList(),
            Metrics = model.Metrics.Select(m => new SemanticMetric
            {
                MetricId = m.MetricId,
                DatasetId = datasetId,
                DisplayName = m.DisplayName,
                Formula = m.Formula,
                BaseTableId = m.BaseTableId,
                AggregationBehavior = m.AggregationBehavior,
                DataType = m.DataType,
                Format = m.Format,
                IsHidden = m.IsHidden,
                IsDraggable = m.IsDraggable
            }).ToList(),
            SemanticObjects = model.SemanticObjects,
            Relationships = model.Relationships.Select(r => new SemanticRelationship
            {
                RelationshipId = string.IsNullOrWhiteSpace(r.RelationshipId) ? $"rel_{Guid.NewGuid():N}" : r.RelationshipId,
                DatasetId = datasetId,
                FromTableId = r.FromTableId,
                FromColumn = r.FromColumn,
                ToTableId = r.ToTableId,
                ToColumn = r.ToColumn,
                JoinType = r.JoinType,
                Cardinality = r.Cardinality,
                CrossFilterDirection = r.CrossFilterDirection,
                IsActive = r.IsActive,
                IsPrimary = r.IsPrimary,
                Source = r.Source,
                Confidence = r.Confidence,
                Status = r.Status,
                Warning = r.Warning
            }).ToList()
        };

        var dataset = new RegisteredDataset
        {
            DatasetId = datasetId,
            DatasetName = datasetName,
            ConnectionId = connection.ConnectionId,
            Model = savedModel
        };

        _datasets[datasetId] = dataset;
        return dataset;
    }

    public RegisteredDataset SaveExisting(string datasetId, string datasetName, string connectionId, SemanticModel model)
    {
        var savedModel = new SemanticModel
        {
            DatasetId = datasetId,
            DisplayName = string.IsNullOrWhiteSpace(datasetName) ? datasetId : datasetName,
            ConnectionId = connectionId,
            Tables = model.Tables,
            Fields = model.Fields,
            Metrics = model.Metrics,
            SemanticObjects = model.SemanticObjects,
            Relationships = model.Relationships
        };

        var dataset = new RegisteredDataset
        {
            DatasetId = datasetId,
            DatasetName = datasetName,
            ConnectionId = connectionId,
            Model = savedModel
        };

        _datasets[datasetId] = dataset;
        return dataset;
    }

    public RegisteredDataset? Find(string datasetId)
    {
        return _datasets.TryGetValue(datasetId, out var dataset) ? dataset : null;
    }

    private static string NormalizeDatasetName(string value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "dataset" : value;
        var chars = source.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        return new string(chars).Trim('_');
    }
}
