using Report.Contracts.Metadata;
using Report.Metadata.Models;
using Report.Metadata.Stores;

namespace Report.Api.Services;

public sealed class DatasetMetadataService
{
    private readonly ISemanticModelStore _modelStore;

    public DatasetMetadataService(ISemanticModelStore modelStore)
    {
        _modelStore = modelStore;
    }

    public async Task<DatasetMetadataResponse> GetMetadataAsync(
        string datasetId,
        CancellationToken ct)
    {
        var model = await _modelStore.LoadAsync(datasetId, ct);
        var visibleFields = model.Fields
            .Where(field => !IsHidden(field))
            .ToList();

        return new DatasetMetadataResponse
        {
            DatasetId = model.DatasetId,
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName)
                ? BuildDatasetDisplayName(model.DatasetId)
                : model.DisplayName,
            ConnectionId = model.ConnectionId,
            Tables = BuildTables(model, visibleFields),
            Metrics = model.Metrics
                .OrderBy(metric => metric.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(MapMetric)
                .ToList(),
            Relationships = model.Relationships
                .OrderBy(relationship => relationship.FromTableId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(relationship => relationship.ToTableId, StringComparer.OrdinalIgnoreCase)
                .Select(MapRelationship)
                .ToList()
        };
    }

    private static List<MetadataTableDto> BuildTables(SemanticModel model, List<SemanticField> visibleFields)
    {
        var tableMetadata = model.Tables.ToDictionary(t => t.TableId, StringComparer.OrdinalIgnoreCase);

        return visibleFields
            .GroupBy(field => field.TableId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                tableMetadata.TryGetValue(group.Key, out var table);
                var fields = group
                    .OrderBy(field => field.OrdinalPosition == 0 ? int.MaxValue : field.OrdinalPosition)
                    .ThenBy(field => field.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(MapField)
                    .ToList();

                return new MetadataTableDto
                {
                    TableId = group.Key,
                    DisplayName = table?.DisplayName ?? BuildTableDisplayName(group.Key),
                    TableType = table?.TableType ?? InferTableType(group.Key),
                    Grain = table?.Grain ?? fields
                        .Select(field => field.Grain)
                        .FirstOrDefault(grain => !string.IsNullOrWhiteSpace(grain)) ?? "",
                    Fields = fields
                };
            })
            .OrderBy(table => GetTableTypeSortOrder(table.TableType))
            .ThenBy(table => table.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MetadataFieldDto MapField(SemanticField field)
    {
        return new MetadataFieldDto
        {
            FieldId = field.FieldId,
            DisplayName = field.DisplayName,
            TableId = field.TableId,
            PhysicalSchema = field.PhysicalSchema,
            PhysicalTable = field.PhysicalTable,
            PhysicalColumn = field.PhysicalColumn,
            OrdinalPosition = field.OrdinalPosition,
            IsNullable = field.IsNullable,
            IsPrimaryKey = field.IsPrimaryKey,
            IsForeignKey = field.IsForeignKey,
            ParticipatesInRelationship = field.ParticipatesInRelationship,
            IsUnique = field.IsUnique,
            ReferencedSchema = field.ReferencedSchema,
            ReferencedTable = field.ReferencedTable,
            ReferencedColumn = field.ReferencedColumn,
            ForeignKeyName = field.ForeignKeyName,
            DataType = field.DataType,
            SqlDataType = string.IsNullOrWhiteSpace(field.SqlDataType) ? field.DataType : field.SqlDataType,
            CharacterMaximumLength = field.CharacterMaximumLength,
            NumericPrecision = field.NumericPrecision,
            NumericScale = field.NumericScale,
            DatetimePrecision = field.DatetimePrecision,
            Role = field.Role,
            Grain = field.Grain,
            SemanticType = field.SemanticType,
            DefaultAggregation = field.DefaultAggregation,
            Format = field.Format,
            Expression = field.Expression,
            BaseTableId = field.BaseTableId,
            IsDerived = field.IsDerived,
            IsHidden = field.IsHidden,
            IsDraggable = field.IsDraggable,
            ClassificationReason = field.ClassificationReason
        };
    }

    private static MetadataMetricDto MapMetric(SemanticMetric metric)
    {
        return new MetadataMetricDto
        {
            MetricId = metric.MetricId,
            DisplayName = metric.DisplayName,
            BaseTableId = metric.BaseTableId,
            Formula = metric.Formula,
            AggregationBehavior = metric.AggregationBehavior,
            DataType = metric.DataType,
            Format = metric.Format,
            IsHidden = metric.IsHidden,
            IsDraggable = metric.IsDraggable
        };
    }

    private static MetadataRelationshipDto MapRelationship(SemanticRelationship relationship)
    {
        return new MetadataRelationshipDto
        {
            RelationshipId = relationship.RelationshipId,
            DatasetId = relationship.DatasetId,
            FromTableId = relationship.FromTableId,
            FromColumn = relationship.FromColumn,
            ToTableId = relationship.ToTableId,
            ToColumn = relationship.ToColumn,
            JoinType = relationship.JoinType,
            Cardinality = relationship.Cardinality,
            CrossFilterDirection = relationship.CrossFilterDirection,
            IsActive = relationship.IsActive,
            IsPrimary = relationship.IsPrimary,
            Source = relationship.Source,
            Confidence = relationship.Confidence,
            Status = relationship.Status,
            Warning = relationship.Warning
        };
    }

    private static bool IsHidden(SemanticField field)
    {
        return field.IsHidden || field.Role.Equals("hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDatasetDisplayName(string datasetId)
    {
        return $"{ToTitleCase(datasetId)} Dataset";
    }

    private static string BuildTableDisplayName(string tableId)
    {
        var name = tableId.StartsWith("Dim", StringComparison.OrdinalIgnoreCase)
            ? tableId[3..]
            : tableId.StartsWith("Fact", StringComparison.OrdinalIgnoreCase)
                ? tableId[4..]
                : tableId;

        return SplitPascalCase(name);
    }

    private static string InferTableType(string tableId)
    {
        return tableId.StartsWith("Fact", StringComparison.OrdinalIgnoreCase)
            ? "fact"
            : "dimension";
    }

    private static int GetTableTypeSortOrder(string tableType)
    {
        return tableType.Equals("dimension", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return string.Join(
            " ",
            value.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var chars = new List<char> { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(value[i]);
        }

        return new string(chars.ToArray());
    }
}
