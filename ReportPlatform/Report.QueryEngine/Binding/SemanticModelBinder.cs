using System.Text.Json;
using Report.Contracts.Requests;
using Report.Metadata.Models;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Validation;

namespace Report.QueryEngine.Binding;

public sealed class SemanticModelBinder
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "=", "!=", ">", "<", ">=", "<=", "IN", "BETWEEN", "CONTAINS"
    };

    public BoundSemanticQuery Bind(VisualQueryRequest request, SemanticModel model)
    {
        var errors = new Dictionary<string, string[]>();
        var fieldIds = model.Fields.Select(f => f.FieldId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metricIds = model.Metrics.Select(m => m.MetricId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidRows = request.Rows
            .Where(id => !fieldIds.Contains(id) || IsUnavailableField(model, id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidRows.Length > 0)
        {
            errors["rows"] =
            [
                $"Unknown field id(s): {string.Join(", ", invalidRows)}. Valid field ids: {string.Join(", ", fieldIds.Order())}."
            ];
        }

        var resolvedValues = ResolveMetrics(request.Values, model, errors);
        var invalidValues = request.Values
            .Where(id => resolvedValues.All(metric => !metric.MetricId.Equals(id, StringComparison.OrdinalIgnoreCase)) &&
                !errors.ContainsKey($"values.{id}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidValues.Length > 0)
        {
            errors["values"] =
            [
                $"Unknown metric id(s): {string.Join(", ", invalidValues)}. Valid metric ids: {string.Join(", ", metricIds.Order())}."
            ];
        }

        var resolvedFilters = ResolveFilters(request.Filters, model, errors);
        var resolvedSort = ResolveSort(request.Sort, model, errors);

        if (errors.Count > 0)
        {
            throw new SemanticQueryValidationException(errors);
        }

        var rows = request.Rows
            .Select(id => model.Fields.First(f => string.Equals(f.FieldId, id, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new BoundSemanticQuery
        {
            Rows = rows,
            Values = resolvedValues,
            Filters = resolvedFilters,
            Sort = resolvedSort,
            Limit = request.Limit,
            Offset = request.Offset
        };
    }

    private static List<ResolvedSort> ResolveSort(
        List<SortRequest> sort,
        SemanticModel model,
        Dictionary<string, string[]> errors)
    {
        var resolved = new List<ResolvedSort>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < sort.Count; index++)
        {
            var item = sort[index];
            var key = $"sort[{index}]";
            var direction = item.Direction.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(item.Field))
            {
                AddError(errors, key, "Sort field is required.");
                continue;
            }

            if (!seen.Add(item.Field))
            {
                AddError(errors, key, $"Duplicate sort field: {item.Field}.");
                continue;
            }

            if (direction is not ("ASC" or "DESC"))
            {
                AddError(errors, key, "Sort direction must be ASC or DESC.");
                continue;
            }

            var field = model.Fields.FirstOrDefault(f => string.Equals(f.FieldId, item.Field, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            {
                if (field.IsHidden)
                {
                    AddError(errors, key, $"Field '{item.Field}' is hidden.");
                    continue;
                }

                resolved.Add(new ResolvedSort
                {
                    FieldId = field.FieldId,
                    TargetType = "dimension",
                    PhysicalExpression = field.IsDerived && field.Expression is not null
                        ? SemanticExpressionCompiler.CompileDerivedExpression(field.Expression, model)
                        : $"{field.TableId}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}",
                    Alias = SqlIdentifier.SafeAlias(field.DisplayName),
                    Direction = direction
                });
                continue;
            }

            var metric = ResolveMetric(item.Field, model, out var metricError);
            if (metric is not null)
            {
                if (metric.IsHidden || !metric.IsDraggable)
                {
                    AddError(errors, key, $"Metric '{item.Field}' is hidden or not draggable.");
                    continue;
                }

                resolved.Add(new ResolvedSort
                {
                    FieldId = metric.MetricId,
                    TargetType = "metric",
                    Alias = SqlIdentifier.SafeAlias(metric.DisplayName),
                    Direction = direction
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(metricError))
            {
                AddError(errors, key, metricError);
                continue;
            }

            AddError(errors, key, $"Unknown sort field or metric id: {item.Field}.");
        }

        return resolved;
    }

    private static List<ResolvedFilter> ResolveFilters(
        List<FilterRequest> filters,
        SemanticModel model,
        Dictionary<string, string[]> errors)
    {
        var resolved = new List<ResolvedFilter>();

        for (var index = 0; index < filters.Count; index++)
        {
            var filter = filters[index];
            var key = $"filters[{index}]";
            var op = filter.Operator.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(filter.Field))
            {
                AddError(errors, key, "Filter field is required.");
                continue;
            }

            if (!SupportedOperators.Contains(op))
            {
                AddError(errors, key, $"Unsupported operator '{filter.Operator}'.");
                continue;
            }

            var field = model.Fields.FirstOrDefault(f => string.Equals(f.FieldId, filter.Field, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            {
                if (field.IsHidden)
                {
                    AddError(errors, key, $"Filter field '{filter.Field}' is hidden.");
                    continue;
                }

                var normalizedValue = NormalizeAndValidateValue(filter.Value, op, field.DataType, key, errors);
                if (errors.ContainsKey(key))
                {
                    continue;
                }

                resolved.Add(new ResolvedFilter
                {
                    FieldId = field.FieldId,
                    PhysicalTable = field.PhysicalTable,
                    PhysicalColumn = field.PhysicalColumn,
                    PhysicalExpression = field.IsDerived && field.Expression is not null
                        ? SemanticExpressionCompiler.CompileDerivedExpression(field.Expression, model)
                        : $"{field.TableId}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}",
                    DataType = field.DataType,
                    Operator = op,
                    Value = normalizedValue,
                    TargetType = "dimension"
                });
                continue;
            }

            var metric = ResolveMetric(filter.Field, model, out var metricError);
            if (metric is not null)
            {
                if (metric.IsHidden || !metric.IsDraggable)
                {
                    AddError(errors, key, $"Filter metric '{filter.Field}' is hidden or not draggable.");
                    continue;
                }

                var normalizedValue = NormalizeAndValidateValue(filter.Value, op, "decimal", key, errors);
                if (errors.ContainsKey(key))
                {
                    continue;
                }

                resolved.Add(new ResolvedFilter
                {
                    FieldId = metric.MetricId,
                    DataType = "decimal",
                    Operator = op,
                    Value = normalizedValue,
                    TargetType = "metric"
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(metricError))
            {
                AddError(errors, key, metricError);
                continue;
            }

            AddError(errors, key, $"Unknown filter field or metric id: {filter.Field}.");
        }

        return resolved;
    }

    private static object? NormalizeAndValidateValue(
        object? rawValue,
        string op,
        string dataType,
        string key,
        Dictionary<string, string[]> errors)
    {
        if (!IsOperatorAllowed(dataType, op))
        {
            AddError(errors, key, $"Operator '{op}' is not allowed for data type '{dataType}'.");
            return null;
        }

        if (op == "IN")
        {
            var values = ToValueList(rawValue);
            if (values.Count == 0)
            {
                AddError(errors, key, "IN value must contain at least one item.");
            }

            return values;
        }

        if (op == "BETWEEN")
        {
            var values = ToValueList(rawValue);
            if (values.Count != 2)
            {
                AddError(errors, key, "BETWEEN value must contain exactly two items.");
            }

            return values;
        }

        var value = NormalizeJsonValue(rawValue);
        if (value is null)
        {
            AddError(errors, key, "Filter value is required.");
        }

        return value;
    }

    private static bool IsOperatorAllowed(string dataType, string op)
    {
        var normalizedType = dataType.ToLowerInvariant();
        var isString = normalizedType is "nvarchar" or "varchar" or "char" or "nchar" or "text";
        var isDate = normalizedType.Contains("date") || normalizedType.Contains("time");
        var isNumber = normalizedType is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money";

        if (op == "CONTAINS")
        {
            return isString;
        }

        if (op is ">" or "<" or ">=" or "<=" or "BETWEEN")
        {
            return isNumber || isDate;
        }

        return isString || isNumber || isDate;
    }

    private static bool IsUnavailableField(SemanticModel model, string fieldId)
    {
        var field = model.Fields.FirstOrDefault(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
        return field is null || field.IsHidden;
    }

    private static bool IsUnavailableMetric(SemanticModel model, string metricId)
    {
        var metric = ResolveMetric(metricId, model, out _);
        return metric is null || metric.IsHidden || !metric.IsDraggable;
    }

    private static List<SemanticMetric> ResolveMetrics(
        List<string> metricIds,
        SemanticModel model,
        Dictionary<string, string[]> errors)
    {
        var resolved = new List<SemanticMetric>();

        foreach (var metricId in metricIds)
        {
            var metric = ResolveMetric(metricId, model, out var error);
            if (metric is not null && !metric.IsHidden && metric.IsDraggable)
            {
                resolved.Add(metric);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                AddError(errors, $"values.{metricId}", error);
            }
        }

        return resolved;
    }

    private static SemanticMetric? ResolveMetric(
        string metricId,
        SemanticModel model,
        out string? error)
    {
        error = null;
        var metric = model.Metrics.FirstOrDefault(m =>
            string.Equals(m.MetricId, metricId, StringComparison.OrdinalIgnoreCase));

        if (metric is not null)
        {
            return metric;
        }

        if (RuntimeAggregateMetricFactory.TryCreate(metricId, model, out var runtimeMetric, out error))
        {
            return runtimeMetric;
        }

        return null;
    }

    private static List<object?> ToValueList(object? rawValue)
    {
        var value = NormalizeJsonValue(rawValue);

        if (value is IEnumerable<object?> items && value is not string)
        {
            return items.ToList();
        }

        if (value is string text)
        {
            return text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Cast<object?>()
                .ToList();
        }

        return value is null ? [] : [value];
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement json)
        {
            return value;
        }

        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number when json.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when json.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when json.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => json.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToList(),
            JsonValueKind.Null => null,
            _ => json.ToString()
        };
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string error)
    {
        errors[key] = errors.TryGetValue(key, out var existing)
            ? existing.Concat([error]).ToArray()
            : [error];
    }
}
