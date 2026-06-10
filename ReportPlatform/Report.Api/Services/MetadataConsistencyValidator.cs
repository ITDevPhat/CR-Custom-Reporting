using Report.Contracts.Connections;
using Report.Metadata.Models;

namespace Report.Api.Services;

public sealed class MetadataConsistencyValidator
{
    private static readonly HashSet<string> Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        "dimension",
        "measure_candidate",
        "key",
        "derived_field",
        "hidden"
    };

    public MetadataConsistencyValidationResult Validate(
        IReadOnlyCollection<TableDto> discoveredTables,
        IReadOnlyCollection<SelectedTableDto> selectedTables,
        SemanticModel model)
    {
        var warnings = new List<string>();
        var consistency = new List<MetadataConsistencyDto>();
        var selectedTableIds = selectedTables
            .Select(t => BuildTableId(t.Schema, t.Table))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableIds = model.Tables.Select(t => t.TableId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var field in model.Fields)
        {
            if (!fieldIds.Add(field.FieldId))
            {
                warnings.Add($"Duplicate fieldId '{field.FieldId}'.");
            }

            if (!tableIds.Contains(field.TableId))
            {
                warnings.Add($"Field '{field.FieldId}' references missing tableId '{field.TableId}'.");
            }

            if (string.IsNullOrWhiteSpace(field.PhysicalTable))
            {
                warnings.Add($"Field '{field.FieldId}' has empty physicalTable.");
            }

            if (string.IsNullOrWhiteSpace(field.PhysicalColumn))
            {
                warnings.Add($"Field '{field.FieldId}' has empty physicalColumn.");
            }

            if (string.IsNullOrWhiteSpace(field.DataType))
            {
                warnings.Add($"Field '{field.FieldId}' has empty dataType.");
            }

            if (!Roles.Contains(field.Role))
            {
                warnings.Add($"Field '{field.FieldId}' has invalid role '{field.Role}'.");
            }
        }

        foreach (var table in discoveredTables.Where(t => selectedTableIds.Contains(BuildTableId(t.Schema, t.Table))))
        {
            var tableId = BuildTableId(table.Schema, table.Table);
            var registeredFields = model.Fields
                .Where(f => f.TableId.Equals(tableId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var missingColumns = new List<string>();

            foreach (var column in table.Columns)
            {
                var matches = registeredFields
                    .Where(f => f.PhysicalColumn.Equals(column.Column, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    warnings.Add($"Table {tableId} column '{column.Column}' is missing a SemanticField.");
                    missingColumns.Add(column.Column);
                }
                else if (matches.Count > 1)
                {
                    warnings.Add($"Table {tableId} column '{column.Column}' has {matches.Count} SemanticFields.");
                }
            }

            if (table.Columns.Count != registeredFields.Count)
            {
                warnings.Add($"Table {tableId}: discovered {table.Columns.Count} columns but registered {registeredFields.Count} fields.");
            }

            consistency.Add(new MetadataConsistencyDto
            {
                TableId = tableId,
                PhysicalColumnCount = table.Columns.Count,
                RegisteredFieldCount = registeredFields.Count,
                MissingColumns = missingColumns
            });
        }

        return new MetadataConsistencyValidationResult(warnings, consistency);
    }

    private static string BuildTableId(string schema, string table)
    {
        return schema.Equals("dbo", StringComparison.OrdinalIgnoreCase)
            ? table
            : $"{schema}.{table}";
    }
}

public sealed record MetadataConsistencyValidationResult(
    List<string> Warnings,
    List<MetadataConsistencyDto> Consistency);
