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

        var activeRelationships = model.Relationships
            .Where(r => r.IsActive && r.Cardinality is "N:1" or "1:1" && r.CrossFilterDirection == "single")
            .ToList();
        var joins = new List<JoinDef>();
        var seenJoinKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in requiredTables)
        {
            var path = FindPath(baseTable, table, activeRelationships);
            foreach (var rel in path)
            {
                var key = $"{rel.FromTableId}.{rel.FromColumn}->{rel.ToTableId}.{rel.ToColumn}";
                if (!seenJoinKeys.Add(key)) continue;
                joins.Add(new JoinDef
                {
                    RelationshipId = rel.RelationshipId,
                    FromTableId = rel.FromTableId,
                    ToTableId = rel.ToTableId,
                    JoinType = rel.JoinType,
                    FromColumn = rel.FromColumn,
                    ToColumn = rel.ToColumn
                });
            }
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

    private static List<SemanticRelationship> FindPath(string baseTable, string targetTable, List<SemanticRelationship> relationships)
    {
        if (baseTable == targetTable) return [];
        var queue = new Queue<(string table, List<SemanticRelationship> path)>();
        var distance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [baseTable] = 0 };
        var bestPaths = new List<List<SemanticRelationship>>();
        int? shortest = null;

        queue.Enqueue((baseTable, []));

        while (queue.Count > 0)
        {
            var (table, path) = queue.Dequeue();
            if (shortest.HasValue && path.Count > shortest.Value) continue;
            if (table == targetTable)
            {
                shortest ??= path.Count;
                bestPaths.Add(path);
                continue;
            }

            var outgoing = relationships.Where(r => r.FromTableId == table)
                .OrderByDescending(r => r.IsPrimary)
                .ThenByDescending(r => r.Source == "database_fk")
                .ThenByDescending(r => r.Confidence)
                .ToList();

            foreach (var rel in outgoing)
            {
                var nextLen = path.Count + 1;
                if (distance.TryGetValue(rel.ToTableId, out var existing) && existing < nextLen) continue;
                distance[rel.ToTableId] = nextLen;
                var next = new List<SemanticRelationship>(path) { rel };
                queue.Enqueue((rel.ToTableId, next));
            }
        }

        if (bestPaths.Count == 0)
        {
            throw new SemanticQueryValidationException(new Dictionary<string, string[]>
            {
                ["errorCode"] = ["NO_RELATIONSHIP_PATH"],
                ["relationships"] = [$"No relationship path from {baseTable} to {targetTable}."]
            });
        }

        var ranked = bestPaths
            .OrderByDescending(PathPrimaryScore)
            .ThenByDescending(PathDatabaseFkScore)
            .ThenByDescending(PathConfidenceScore)
            .ThenBy(p => p.Count)
            .ToList();

        if (ranked.Count > 1 &&
            PathPrimaryScore(ranked[0]) == PathPrimaryScore(ranked[1]) &&
            PathDatabaseFkScore(ranked[0]) == PathDatabaseFkScore(ranked[1]) &&
            Math.Abs(PathConfidenceScore(ranked[0]) - PathConfidenceScore(ranked[1])) < 0.000001m &&
            ranked[0].Count == ranked[1].Count)
        {
            throw new SemanticQueryValidationException(new Dictionary<string, string[]>
            {
                ["errorCode"] = ["AMBIGUOUS_RELATIONSHIP_PATH"],
                ["message"] = [$"Multiple equally ranked relationship paths exist between {baseTable} and {targetTable}."],
                ["details"] = ranked.Take(2).Select(path => string.Join(" | ", path.Select(r => $"{r.FromTableId}.{r.FromColumn} -> {r.ToTableId}.{r.ToColumn}"))).ToArray()
            });
        }

        return ranked[0];
    }

    private static int PathPrimaryScore(IEnumerable<SemanticRelationship> path) => path.Count(r => r.IsPrimary);
    private static int PathDatabaseFkScore(IEnumerable<SemanticRelationship> path) => path.Count(r => r.Source == "database_fk");
    private static decimal PathConfidenceScore(IEnumerable<SemanticRelationship> path) => path.Sum(r => r.Confidence);
}
