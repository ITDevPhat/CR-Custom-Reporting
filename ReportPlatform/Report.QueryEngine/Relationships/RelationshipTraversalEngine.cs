using Report.Metadata.Models;
using Report.QueryEngine.Context;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Validation;

namespace Report.QueryEngine.Relationships;

public sealed class RelationshipTraversalEngine
{
    public JoinPlan Build(EvaluationContext context, List<ExpandedMeasure> measures, SemanticModel model)
    {
        var requestedTables = context.GroupFields
            .Select(f => f.TableId)
            .Concat(measures.Select(m => m.BaseTableId))
            .Concat(context.Filters.WhereFilters.Select(f => f.PhysicalTable))
            .Distinct()
            .ToList();

        var baseTable = ChooseBaseTable(context, measures, model, requestedTables);

        var requiredTables = requestedTables
            .Where(t => t != baseTable)
            .ToList();

        var joins = new List<JoinDef>();

        foreach (var table in requiredTables)
        {
            var candidates = model.Relationships
                .Where(r => r.IsActive &&
                    r.Cardinality is "N:1" or "1:1" &&
                    r.CrossFilterDirection == "single" &&
                    r.FromTableId == baseTable &&
                    r.ToTableId == table)
                .OrderByDescending(r => r.IsPrimary)
                .ThenByDescending(r => r.Source == "database_fk")
                .ThenByDescending(r => r.Confidence)
                .ToList();

            if (candidates.Count == 0)
            {
                throw new SemanticQueryValidationException(new Dictionary<string, string[]>
                {
                    ["errorCode"] = ["NO_ACTIVE_RELATIONSHIP_PATH"],
                    ["relationships"] = [$"No active relationship path from {baseTable} to {table}."]
                });
            }
            if (candidates.Count > 1)
            {
                throw new SemanticQueryValidationException(new Dictionary<string, string[]>
                {
                    ["errorCode"] = ["AMBIGUOUS_RELATIONSHIP_PATH"],
                    ["message"] = [$"Multiple active relationships exist between {baseTable} and {table}. Make exactly one relationship active."],
                    ["details"] = candidates.Select(c => $"{c.FromTableId}.{c.FromColumn} -> {c.ToTableId}.{c.ToColumn}").ToArray()
                });
            }
            var rel = candidates[0];

            joins.Add(new JoinDef
            {
                FromTableId = rel.FromTableId,
                ToTableId = rel.ToTableId,
                JoinType = rel.JoinType,
                FromColumn = rel.FromColumn,
                ToColumn = rel.ToColumn
            });
        }

        return new JoinPlan { BaseTableId = baseTable, Joins = joins };
    }

    private static string ChooseBaseTable(
        EvaluationContext context,
        List<ExpandedMeasure> measures,
        SemanticModel model,
        List<string> requestedTables)
    {
        var measureBaseTable = measures.FirstOrDefault()?.BaseTableId;
        if (!string.IsNullOrWhiteSpace(measureBaseTable))
        {
            return measureBaseTable;
        }

        if (requestedTables.Count <= 1)
        {
            return requestedTables.FirstOrDefault()
                ?? throw new SemanticQueryValidationException(new Dictionary<string, string[]>
                {
                    ["fields"] = ["Select at least one field or metric."]
                });
        }

        var activeRelationships = model.Relationships
            .Where(r => r.IsActive && r.Cardinality is "N:1" or "1:1" && r.CrossFilterDirection == "single")
            .ToList();

        var bridgeTable = activeRelationships
            .Where(r => r.IsPrimary)
            .Select(r => r.FromTableId)
            .Distinct()
            .FirstOrDefault(candidate => requestedTables.All(table =>
                table == candidate ||
                activeRelationships.Any(r =>
                    r.IsPrimary &&
                    r.FromTableId == candidate &&
                    r.ToTableId == table)));

        if (!string.IsNullOrWhiteSpace(bridgeTable))
        {
            return bridgeTable;
        }

        return context.GroupFields.First().TableId;
    }
}
