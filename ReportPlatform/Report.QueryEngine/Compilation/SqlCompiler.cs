using Report.QueryEngine.Planning;

namespace Report.QueryEngine.Compilation;

public sealed class SqlCompiler
{
    public SqlCompilationResult Compile(LogicalQueryPlan plan)
    {
        var parameters = new Dictionary<string, object?>();
        var nextParameterIndex = 0;
        var baseAlias = plan.Aliases[plan.BaseTableId];

        var usesTopForLimit = plan.Limit > 0 && plan.Offset == 0 && plan.OrderBy.Count == 0;
        var top = usesTopForLimit ? $" TOP ({plan.Limit})" : "";

        var select = string.Join(",\n  ",
            plan.Select.Select(x => $"{x.Expression} AS [{x.Alias}]"));

        var joins = string.Join("\n",
            plan.Joins.Select(j =>
                $"{j.JoinType} JOIN {plan.TableExpressions[j.TableId]} {j.Alias} ON {j.Condition}"));

        var where = plan.Where.Count > 0
            ? "WHERE\n  " + string.Join("\n  AND ", plan.Where.Select(CompileFilter))
            : "";

        var groupBy = plan.GroupBy.Count > 0
            ? "GROUP BY\n  " + string.Join(",\n  ", plan.GroupBy)
            : "";

        var having = plan.Having.Count > 0
            ? "HAVING\n  " + string.Join("\n  AND ", plan.Having.Select(CompileFilter))
            : "";

        var paging = plan.Limit > 0 && !usesTopForLimit
            ? $"OFFSET {plan.Offset} ROWS FETCH NEXT {plan.Limit} ROWS ONLY"
            : "";

        var orderBy = plan.OrderBy.Count > 0
            ? "ORDER BY\n  " + string.Join(",\n  ",
                plan.OrderBy.Select(x => $"{CompileOrderExpression(x)} {x.Direction}"))
            : plan.Offset > 0
                ? "ORDER BY (SELECT NULL)"
                : "";

        var sql = $"""
        SELECT{top}
          {select}
        FROM {plan.TableExpressions[plan.BaseTableId]} {baseAlias}
        {joins}
        {where}
        {groupBy}
        {having}
        {orderBy}
        {paging};
        """;

        return new SqlCompilationResult
        {
            Sql = sql,
            Parameters = parameters
        };

        string CompileFilter(FilterItem filter)
        {
            return filter.Operator switch
            {
                "=" => $"{filter.Expression} = {AddParameter(filter.Value)}",
                "!=" => $"{filter.Expression} <> {AddParameter(filter.Value)}",
                ">" => $"{filter.Expression} > {AddParameter(filter.Value)}",
                "<" => $"{filter.Expression} < {AddParameter(filter.Value)}",
                ">=" => $"{filter.Expression} >= {AddParameter(filter.Value)}",
                "<=" => $"{filter.Expression} <= {AddParameter(filter.Value)}",
                "CONTAINS" => $"{filter.Expression} LIKE '%' + {AddParameter(filter.Value)} + '%'",
                "IN" => $"{filter.Expression} IN ({string.Join(", ", ToList(filter.Value).Select(AddParameter))})",
                "BETWEEN" => CompileBetween(filter),
                _ => throw new InvalidOperationException($"Unsupported filter operator: {filter.Operator}")
            };
        }

        string CompileBetween(FilterItem filter)
        {
            var values = ToList(filter.Value);
            if (values.Count != 2)
            {
                throw new InvalidOperationException("BETWEEN filter requires exactly two values.");
            }

            return $"{filter.Expression} BETWEEN {AddParameter(values[0])} AND {AddParameter(values[1])}";
        }

        string AddParameter(object? value)
        {
            var name = $"p{nextParameterIndex++}";
            parameters[name] = value;
            return $"@{name}";
        }
    }

    private static List<object?> ToList(object? value)
    {
        if (value is IEnumerable<object?> items && value is not string)
        {
            return items.ToList();
        }

        return value is null ? [] : [value];
    }

    private static string CompileOrderExpression(OrderItem item)
    {
        return item.IsAlias ? $"[{item.Expression}]" : item.Expression;
    }
}
