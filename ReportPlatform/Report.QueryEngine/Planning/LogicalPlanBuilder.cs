using Report.QueryEngine.Binding;
using Report.Metadata.Models;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Relationships;
using Report.QueryEngine.Validation;

namespace Report.QueryEngine.Planning;

public sealed class LogicalPlanBuilder
{
    public LogicalQueryPlan Build(
        EvaluationContext context,
        List<ExpandedMeasure> measures,
        JoinPlan joinPlan,
        SemanticModel model)
    {
        var baseTable = joinPlan.BaseTableId;

        var allTables = new[] { baseTable }
            .Concat(context.GroupFields.Select(f => f.TableId))
            .Concat(context.Filters.WhereFilters.Select(f => f.PhysicalTable))
            .Concat(measures.Select(m => m.BaseTableId))
            .Concat(joinPlan.Joins.Select(j => j.FromTableId))
            .Concat(joinPlan.Joins.Select(j => j.ToTableId))
            .Distinct()
            .ToList();

        var aliases = AssignAliases(allTables);

        var select = new List<SelectItem>();

        foreach (var field in context.GroupFields)
        {
            select.Add(new SelectItem
            {
                Expression = field.IsDerived && field.Expression is not null
                    ? ApplyAliases(SemanticExpressionCompiler.CompileDerivedExpression(field.Expression, model), aliases)
                    : $"{aliases[field.TableId]}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}",
                Alias = SqlIdentifier.SafeAlias(field.DisplayName),
                Role = "dimension"
            });
        }

        var aliasedMeasures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var measure in measures)
        {
            var expr = ApplyAliases(measure.SqlExpression, aliases);
            aliasedMeasures[measure.MetricId] = expr;

            select.Add(new SelectItem
            {
                Expression = expr,
                Alias = measure.Alias,
                Role = "metric"
            });
        }

        var joins = joinPlan.Joins.Select(j =>
            new JoinItem
            {
                JoinType = j.JoinType,
                TableId = j.ToTableId,
                Alias = aliases[j.ToTableId],
                Condition = $"{aliases[j.FromTableId]}.{SqlIdentifier.QuoteColumn(j.FromColumn)} = {aliases[j.ToTableId]}.{SqlIdentifier.QuoteColumn(j.ToColumn)}"
            }).ToList();

        var groupBy = measures.Count > 0 && context.GroupFields.Count > 0
            ? context.GroupFields
                .Select(f => f.IsDerived && f.Expression is not null
                    ? ApplyAliases(SemanticExpressionCompiler.CompileDerivedExpression(f.Expression, model), aliases)
                    : $"{aliases[f.TableId]}.{SqlIdentifier.QuoteColumn(f.PhysicalColumn)}")
                .ToList()
            : [];

        return new LogicalQueryPlan
        {
            BaseTableId = baseTable,
            TableExpressions = allTables.ToDictionary(table => table, table => SqlIdentifier.QuoteTable(model, table)),
            Aliases = aliases,
            Select = select,
            Joins = joins,
            Where = context.Filters.WhereFilters
                .Select(filter => BuildDimensionFilter(filter, aliases))
                .ToList(),
            Having = context.Filters.HavingFilters
                .Select(filter => BuildMetricFilter(filter, aliasedMeasures))
                .ToList(),
            GroupBy = groupBy,
            OrderBy = context.Sort.Select(sort => BuildOrderItem(sort, context, aliases)).ToList(),
            Limit = context.Limit,
            Offset = context.Offset
        };
    }

    private static OrderItem BuildOrderItem(
        ResolvedSort sort,
        EvaluationContext context,
        Dictionary<string, string> aliases)
    {
        if (sort.TargetType == "metric")
        {
            return new OrderItem
            {
                Expression = sort.Alias,
                IsAlias = true,
                Direction = sort.Direction
            };
        }

        var selectedField = context.GroupFields.Any(field =>
            string.Equals(field.FieldId, sort.FieldId, StringComparison.OrdinalIgnoreCase));

        if (selectedField)
        {
            return new OrderItem
            {
                Expression = sort.Alias,
                IsAlias = true,
                Direction = sort.Direction
            };
        }

        return new OrderItem
        {
            Expression = ApplyAliases(sort.PhysicalExpression, aliases),
            IsAlias = false,
            Direction = sort.Direction
        };
    }

    private static FilterItem BuildDimensionFilter(
        ResolvedFilter filter,
        Dictionary<string, string> aliases)
    {
        if (!aliases.TryGetValue(filter.PhysicalTable, out var alias))
        {
            throw new SemanticQueryValidationException(new Dictionary<string, string[]>
            {
                ["filters"] = [$"No table alias found for filter field '{filter.FieldId}'."]
            });
        }

        return new FilterItem
        {
            Expression = string.IsNullOrWhiteSpace(filter.PhysicalExpression)
                ? $"{alias}.{SqlIdentifier.QuoteColumn(filter.PhysicalColumn)}"
                : ApplyAliases(filter.PhysicalExpression, aliases),
            Operator = filter.Operator,
            Value = filter.Value
        };
    }

    private static FilterItem BuildMetricFilter(
        ResolvedFilter filter,
        Dictionary<string, string> aliasedMeasures)
    {
        if (!aliasedMeasures.TryGetValue(filter.FieldId, out var expression))
        {
            throw new SemanticQueryValidationException(new Dictionary<string, string[]>
            {
                ["filters"] = [$"Metric filter '{filter.FieldId}' must target a selected metric."]
            });
        }

        return new FilterItem
        {
            Expression = expression,
            Operator = filter.Operator,
            Value = filter.Value
        };
    }

    private static string ApplyAliases(string expression, Dictionary<string, string> aliases)
    {
        foreach (var kv in aliases)
        {
            expression = expression.Replace($"{kv.Key}.", $"{kv.Value}.");
        }

        return expression;
    }

    private static Dictionary<string, string> AssignAliases(List<string> tables)
    {
        var result = new Dictionary<string, string>();
        var used = new HashSet<string>();

        foreach (var table in tables)
        {
            var alias = PreferredAlias(table);

            if (used.Contains(alias))
            {
                alias = "t" + used.Count;
            }

            used.Add(alias);
            result[table] = alias;
        }

        return result;
    }

    private static string PreferredAlias(string tableId)
    {
        var name = tableId.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? tableId;

        if (name.StartsWith("Fact", StringComparison.OrdinalIgnoreCase))
        {
            return "f";
        }

        if (name.StartsWith("Dim", StringComparison.OrdinalIgnoreCase))
        {
            name = name[3..];
        }

        return name switch
        {
            "Customer" => "c",
            "Date" => "d",
            "Product" => "p",
            _ when name.Length > 0 => name[..1].ToLowerInvariant(),
            _ => "t"
        };
    }
}
