using System.Text.RegularExpressions;
using Report.Contracts.Connections;
using Report.Metadata.Models;

namespace Report.Api.Services;

public sealed partial class SemanticMetadataGenerator
{
    public SemanticModel Generate(
        string datasetName,
        DiscoverSchemaResponse discovered,
        IReadOnlyCollection<SelectedTableDto> selectedTables)
    {
        var selected = selectedTables
            .Select(t => BuildTableId(t.Schema, t.Table))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tables = discovered.Tables
            .Where(t => selected.Contains(BuildTableId(t.Schema, t.Table)))
            .ToList();

        var semanticTables = tables.Select(table =>
        {
            var tableType = InferTableType(table.Table);
            return new SemanticTable
            {
                TableId = BuildTableId(table.Schema, table.Table),
                DisplayName = SplitName(RemoveKnownPrefix(table.Table)),
                TableType = tableType,
                Grain = InferGrain(table.Table),
                PhysicalSchema = table.Schema,
                PhysicalTable = table.Table
            };
        }).ToList();

        var fields = tables
            .SelectMany(table =>
            {
                var tableType = InferTableType(table.Table);
                return table.Columns
                    .OrderBy(column => column.OrdinalPosition)
                    .Select(column => MapField(table, column, tableType, tables));
            })
            .ToList();

        var relationships = discovered.Relationships
            .Where(r => selected.Contains(BuildTableId(r.FromSchema, r.FromTable)) &&
                selected.Contains(BuildTableId(r.ToSchema, r.ToTable)))
            .Select(r => new SemanticRelationship
            {
                RelationshipId = $"rel_{Guid.NewGuid():N}",
                FromTableId = BuildTableId(r.FromSchema, r.FromTable),
                FromColumn = r.FromColumn,
                ToTableId = BuildTableId(r.ToSchema, r.ToTable),
                ToColumn = r.ToColumn,
                JoinType = "INNER",
                Cardinality = "N:1",
                IsPrimary = true
                ,
                CrossFilterDirection = "single",
                IsActive = true,
                Source = "database_fk",
                Confidence = 1.0m,
                Status = "active"
            })
            .Concat(InferRelationships(tables, discovered.Relationships))
            .GroupBy(r => $"{r.FromTableId}|{r.FromColumn}|{r.ToTableId}|{r.ToColumn}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var factTableIds = semanticTables
            .Where(t => t.TableType == "fact")
            .Select(t => t.TableId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metrics = fields
            .Where(field => factTableIds.Contains(field.TableId) && field.Role == "measure_candidate")
            .Select(field =>
            {
                var aggregation = InferDefaultAggregation(field.PhysicalColumn);
                return new SemanticMetric
                {
                    MetricId = $"metric.{aggregation.ToLowerInvariant()}_{NormalizeId(field.TableId)}_{NormalizeId(field.PhysicalColumn)}",
                    DisplayName = BuildMetricDisplayName(field.DisplayName, aggregation),
                    Formula = $"{aggregation}([{field.FieldId}])",
                    BaseTableId = field.TableId,
                    AggregationBehavior = aggregation == "AVG" ? "non_additive" : "additive",
                    DataType = field.DataType,
                    Format = InferFormat(field.PhysicalColumn, field.DataType),
                    IsHidden = false,
                    IsDraggable = true
                };
            })
            .ToList();

        return new SemanticModel
        {
            DisplayName = string.IsNullOrWhiteSpace(datasetName) ? discovered.Database : datasetName,
            Tables = semanticTables,
            Fields = fields,
            Relationships = relationships,
            Metrics = metrics
        };
    }

    private static SemanticField MapField(TableDto table, ColumnDto column, string tableType, IReadOnlyCollection<TableDto> selectedTables)
    {
        var classification = ClassifyField(table, column, tableType, selectedTables);

        return new SemanticField
        {
            FieldId = $"{NormalizeId(table.Table)}.{NormalizeId(column.Column)}",
            TableId = BuildTableId(table.Schema, table.Table),
            PhysicalSchema = table.Schema,
            PhysicalTable = BuildTableId(table.Schema, table.Table),
            PhysicalColumn = column.Column,
            OrdinalPosition = column.OrdinalPosition,
            IsNullable = column.IsNullable,
            IsPrimaryKey = column.IsPrimaryKey,
            IsForeignKey = column.IsForeignKey,
            ParticipatesInRelationship = column.ParticipatesInRelationship,
            IsUnique = column.IsUnique,
            ReferencedSchema = column.ReferencedSchema,
            ReferencedTable = column.ReferencedTable,
            ReferencedColumn = column.ReferencedColumn,
            ForeignKeyName = column.ForeignKeyName,
            DisplayName = SplitName(column.Column),
            DataType = column.DataType,
            SqlDataType = string.IsNullOrWhiteSpace(column.SqlDataType) ? column.DataType : column.SqlDataType,
            CharacterMaximumLength = column.CharacterMaximumLength,
            NumericPrecision = column.NumericPrecision,
            NumericScale = column.NumericScale,
            DatetimePrecision = column.DatetimePrecision,
            Role = classification.Role,
            Grain = InferGrain(table.Table),
            SemanticType = classification.SemanticType,
            DefaultAggregation = classification.DefaultAggregation,
            Format = classification.Format,
            IsHidden = classification.IsHidden,
            IsDraggable = classification.IsDraggable,
            ClassificationReason = classification.Reason
        };
    }

    private static IEnumerable<SemanticRelationship> InferRelationships(
        IReadOnlyCollection<TableDto> tables,
        IReadOnlyCollection<RelationshipDiscoveryDto> discoveredRelationships)
    {
        if (discoveredRelationships.Count > 0)
        {
            yield break;
        }

        var factTables = tables.Where(t => t.Table.StartsWith("Fact", StringComparison.OrdinalIgnoreCase)).ToList();
        var dimTables = tables.Where(t => t.Table.StartsWith("Dim", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var fact in factTables)
        {
            foreach (var dim in dimTables)
            {
                var dimKey = dim.Columns.FirstOrDefault(c => c.Column.EndsWith("Key", StringComparison.OrdinalIgnoreCase));
                if (dimKey is null)
                {
                    continue;
                }

                var factKey = fact.Columns.FirstOrDefault(c =>
                    c.Column.Equals(dimKey.Column, StringComparison.OrdinalIgnoreCase) ||
                    c.Column.EndsWith(dimKey.Column, StringComparison.OrdinalIgnoreCase));

                if (factKey is null)
                {
                    continue;
                }

                yield return new SemanticRelationship
                {
                    RelationshipId = $"rel_{Guid.NewGuid():N}",
                    FromTableId = BuildTableId(fact.Schema, fact.Table),
                    FromColumn = factKey.Column,
                    ToTableId = BuildTableId(dim.Schema, dim.Table),
                    ToColumn = dimKey.Column,
                    JoinType = "INNER",
                    Cardinality = "N:1",
                    IsPrimary = true,
                    CrossFilterDirection = "single",
                    IsActive = true,
                    Source = "inferred",
                    Confidence = 0.85m,
                    Status = "active",
                    Warning = "Inferred relationship. Please verify before production use."
                };
            }
        }
    }

    private static FieldClassification ClassifyField(TableDto table, ColumnDto column, string tableType, IReadOnlyCollection<TableDto> selectedTables)
    {
        if (column.IsPrimaryKey)
        {
            return new FieldClassification(
                Role: "key",
                SemanticType: "identifier",
                DefaultAggregation: "none",
                Format: "general",
                IsHidden: false,
                IsDraggable: false,
                Reason: "SQL Server primary key");
        }

        if (column.IsForeignKey)
        {
            return new FieldClassification(
                Role: "key",
                SemanticType: "identifier",
                DefaultAggregation: "none",
                Format: "general",
                IsHidden: false,
                IsDraggable: false,
                Reason: "SQL Server foreign key");
        }

        if (column.ParticipatesInRelationship)
        {
            return new FieldClassification(
                Role: "key",
                SemanticType: "identifier",
                DefaultAggregation: "none",
                Format: "general",
                IsHidden: false,
                IsDraggable: false,
                Reason: "Participates in discovered relationship");
        }

        if (IsInferredFactForeignKeyCandidate(table, column, tableType, selectedTables))
        {
            return new FieldClassification(
                Role: "key",
                SemanticType: "identifier",
                DefaultAggregation: "none",
                Format: "general",
                IsHidden: false,
                IsDraggable: false,
                Reason: "Inferred fact-to-dimension key candidate");
        }

        if (IsDateDimension(table.Table, tableType, column.Column))
        {
            return new FieldClassification(
                Role: "dimension",
                SemanticType: InferDateDimensionSemanticType(column.Column, column.DataType),
                DefaultAggregation: "none",
                Format: InferFormat(column.Column, column.DataType),
                IsHidden: false,
                IsDraggable: true,
                Reason: "Date dimension/date-part field");
        }

        if (IsBusinessIdentifier(column.Column))
        {
            return new FieldClassification(
                Role: "dimension",
                SemanticType: "identifier",
                DefaultAggregation: "none",
                Format: "general",
                IsHidden: false,
                IsDraggable: true,
                Reason: "Business identifier: ID/Key/Code/Number pattern but not PK/FK");
        }

        if (tableType.Equals("dimension", StringComparison.OrdinalIgnoreCase))
        {
            return new FieldClassification(
                Role: "dimension",
                SemanticType: InferSemanticType(column.Column, column.DataType, "dimension"),
                DefaultAggregation: "none",
                Format: InferFormat(column.Column, column.DataType),
                IsHidden: false,
                IsDraggable: true,
                Reason: "Dimension table non-key field");
        }

        if (tableType.Equals("fact", StringComparison.OrdinalIgnoreCase) &&
            IsNumeric(column.DataType) &&
            !IsDatePartOrIdentifier(column.Column))
        {
            return new FieldClassification(
                Role: "measure_candidate",
                SemanticType: InferSemanticType(column.Column, column.DataType, "measure_candidate"),
                DefaultAggregation: InferDefaultAggregation(column.Column),
                Format: InferFormat(column.Column, column.DataType),
                IsHidden: false,
                IsDraggable: true,
                Reason: "Fact numeric non-key measure candidate");
        }

        return new FieldClassification(
            Role: "dimension",
            SemanticType: InferSemanticType(column.Column, column.DataType, "dimension"),
            DefaultAggregation: "none",
            Format: InferFormat(column.Column, column.DataType),
            IsHidden: false,
            IsDraggable: true,
            Reason: "Fallback dimension field");
    }

    private static bool IsNumeric(string dataType)
    {
        return dataType.ToLowerInvariant() is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney";
    }

    private static bool IsDateDimension(string tableName, string tableType, string columnName)
    {
        return tableName.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            tableType.Equals("dimension", StringComparison.OrdinalIgnoreCase) &&
            ContainsAny(columnName, "year", "month", "quarter", "day", "week", "date", "fiscal");
    }

    private static bool IsDatePartOrIdentifier(string columnName)
    {
        return ContainsAny(columnName, "year", "month", "quarter", "day", "week", "date", "fiscal") ||
            IsBusinessIdentifier(columnName);
    }

    private static bool IsInferredFactForeignKeyCandidate(
        TableDto table,
        ColumnDto column,
        string tableType,
        IReadOnlyCollection<TableDto> selectedTables)
    {
        if (!tableType.Equals("fact", StringComparison.OrdinalIgnoreCase) ||
            !column.Column.EndsWith("Key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return selectedTables
            .Where(candidate => candidate.Table.StartsWith("Dim", StringComparison.OrdinalIgnoreCase))
            .SelectMany(candidate => candidate.Columns)
            .Any(candidateColumn =>
                candidateColumn.IsPrimaryKey &&
                candidateColumn.Column.Equals(column.Column, StringComparison.OrdinalIgnoreCase) ||
                candidateColumn.IsPrimaryKey &&
                column.Column.EndsWith(candidateColumn.Column, StringComparison.OrdinalIgnoreCase));
    }

    private static string InferDateDimensionSemanticType(string columnName, string dataType)
    {
        if (dataType.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("time", StringComparison.OrdinalIgnoreCase)) return "date";
        return IsNumeric(dataType) ? "number" : "category";
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EndsWithAny(string value, params string[] suffixes)
    {
        return suffixes.Any(suffix => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBusinessIdentifier(string columnName)
    {
        return columnName.EndsWith("ID", StringComparison.OrdinalIgnoreCase) ||
            columnName.EndsWith("Key", StringComparison.OrdinalIgnoreCase) ||
            columnName.EndsWith("Code", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Number", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferTableType(string table)
    {
        if (table.StartsWith("Fact", StringComparison.OrdinalIgnoreCase)) return "fact";
        if (table.StartsWith("Dim", StringComparison.OrdinalIgnoreCase)) return "dimension";
        return "unknown";
    }

    private static string InferGrain(string table)
    {
        return NormalizeId(RemoveKnownPrefix(table));
    }

    private static string BuildTableId(string schema, string table)
    {
        return schema.Equals("dbo", StringComparison.OrdinalIgnoreCase)
            ? table
            : $"{schema}.{table}";
    }

    private static string NormalizeId(string value)
    {
        return IdCleaner().Replace(value.ToLowerInvariant(), "_").Trim('_');
    }

    private static string InferDefaultAggregation(string columnName)
    {
        return columnName.Contains("Discount", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Percent", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Percentage", StringComparison.OrdinalIgnoreCase)
            ? "AVG"
            : "SUM";
    }

    private static string InferSemanticType(string columnName, string dataType, string role)
    {
        if (role == "key") return "identifier";
        if (IsBusinessIdentifier(columnName)) return "identifier";
        if (dataType.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            dataType.Contains("time", StringComparison.OrdinalIgnoreCase)) return "date";
        if (columnName.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Sales", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Profit", StringComparison.OrdinalIgnoreCase)) return "currency";
        if (columnName.Contains("Discount", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Percent", StringComparison.OrdinalIgnoreCase)) return "percentage";
        return IsNumeric(dataType) ? "number" : "category";
    }

    private static string InferFormat(string columnName, string dataType)
    {
        if (columnName.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Sales", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Profit", StringComparison.OrdinalIgnoreCase)) return "currency";
        if (columnName.Contains("Discount", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Rate", StringComparison.OrdinalIgnoreCase) ||
            columnName.Contains("Percent", StringComparison.OrdinalIgnoreCase)) return "percentage";
        return IsNumeric(dataType) ? "decimal" : "general";
    }

    private static string BuildMetricDisplayName(string fieldName, string aggregation)
    {
        if (aggregation == "AVG")
        {
            return $"Average {fieldName}";
        }

        return fieldName.Contains("Sales", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("Profit", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("Amount", StringComparison.OrdinalIgnoreCase)
            ? $"Total {fieldName}"
            : $"Sum {fieldName}";
    }

    private static string RemoveKnownPrefix(string value)
    {
        return value.StartsWith("Fact", StringComparison.OrdinalIgnoreCase)
            ? value[4..]
            : value.StartsWith("Dim", StringComparison.OrdinalIgnoreCase)
                ? value[3..]
                : value;
    }

    private static string SplitName(string value)
    {
        return PascalSplitter().Replace(value.Replace("_", " "), "$1 $2").Trim();
    }

    private sealed record FieldClassification(
        string Role,
        string SemanticType,
        string DefaultAggregation,
        string Format,
        bool IsHidden,
        bool IsDraggable,
        string Reason);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex IdCleaner();

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex PascalSplitter();
}
