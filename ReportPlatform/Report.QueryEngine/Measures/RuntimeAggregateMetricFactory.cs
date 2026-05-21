using System.Text.RegularExpressions;
using Report.Metadata.Models;

namespace Report.QueryEngine.Measures;

public static partial class RuntimeAggregateMetricFactory
{
    private static readonly string[] Aggregations = ["SUM", "AVG", "MIN", "MAX", "COUNT", "COUNT_DISTINCT"];

    public static bool TryCreate(
        string metricId,
        SemanticModel model,
        out SemanticMetric metric,
        out string? error)
    {
        metric = new SemanticMetric();
        error = null;

        foreach (var field in model.Fields.Where(field => !field.IsHidden))
        {
            foreach (var aggregation in Aggregations)
            {
                if (!metricId.Equals(BuildMetricId(field, aggregation), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsAggregationAllowed(aggregation, field, out error))
                {
                    return false;
                }

                metric = new SemanticMetric
                {
                    DatasetId = model.DatasetId,
                    MetricId = BuildMetricId(field, aggregation),
                    DisplayName = BuildDisplayName(field.DisplayName, aggregation),
                    Formula = $"{aggregation}([{field.FieldId}])",
                    BaseTableId = field.TableId,
                    AggregationBehavior = aggregation is "SUM" or "COUNT" ? "additive" : "non_additive",
                    DataType = aggregation.StartsWith("COUNT", StringComparison.OrdinalIgnoreCase)
                        ? "integer"
                        : field.DataType,
                    Format = aggregation.StartsWith("COUNT", StringComparison.OrdinalIgnoreCase)
                        ? "integer"
                        : field.Format,
                    IsHidden = false,
                    IsDraggable = true
                };
                return true;
            }
        }

        return false;
    }

    public static string BuildMetricId(SemanticField field, string aggregation)
    {
        return $"metric.{aggregation.ToLowerInvariant()}_{NormalizeId(field.TableId)}_{NormalizeId(field.PhysicalColumn)}";
    }

    public static string BuildDisplayName(string fieldName, string aggregation)
    {
        return aggregation switch
        {
            "SUM" => $"Sum {fieldName}",
            "AVG" => $"Average {fieldName}",
            "MIN" => $"Min {fieldName}",
            "MAX" => $"Max {fieldName}",
            "COUNT" => $"Count {fieldName}",
            "COUNT_DISTINCT" => $"Distinct {fieldName} Count",
            _ => $"{aggregation} {fieldName}"
        };
    }

    public static bool IsAggregationAllowed(string aggregation, SemanticField field, out string? error)
    {
        error = null;

        if (aggregation is "SUM" or "AVG" && !IsNumeric(field.DataType))
        {
            error = $"{aggregation} is invalid for field '{field.FieldId}' with data type '{field.DataType}'.";
            return false;
        }

        if (aggregation is "MIN" or "MAX" && IsUnsupportedComparableText(field.DataType))
        {
            error = $"{aggregation} is invalid for field '{field.FieldId}' with data type '{field.DataType}'.";
            return false;
        }

        return true;
    }

    private static bool IsNumeric(string dataType)
    {
        return dataType.ToLowerInvariant() is "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney";
    }

    private static bool IsUnsupportedComparableText(string dataType)
    {
        return dataType.ToLowerInvariant() is "text" or "ntext" or "image" or "xml";
    }

    private static string NormalizeId(string value)
    {
        return IdCleaner().Replace(value.ToLowerInvariant(), "_").Trim('_');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex IdCleaner();
}
