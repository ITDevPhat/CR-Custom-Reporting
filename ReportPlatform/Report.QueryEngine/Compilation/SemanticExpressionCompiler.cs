using System.Text.RegularExpressions;
using Report.Metadata.Models;

namespace Report.QueryEngine.Compilation;

public static partial class SemanticExpressionCompiler
{
    public static string CompileMetricFormula(string formula, SemanticModel model)
    {
        var expression = AggregateRef().Replace(formula, match =>
        {
            var fn = match.Groups[1].Value.ToUpperInvariant();
            var fieldId = match.Groups[2].Value;
            var field = model.Fields.First(f => f.FieldId.Equals(fieldId, StringComparison.OrdinalIgnoreCase));
            var tableExpr = $"{field.TableId}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}";
            return fn == "COUNT_DISTINCT"
                ? $"COUNT(DISTINCT {tableExpr})"
                : $"{fn}({tableExpr})";
        });

        return DivisionExpression().Replace(expression, match =>
        {
            var left = match.Groups[1].Value.Trim();
            var right = match.Groups[2].Value.Trim();
            if (right.StartsWith("NULLIF(", StringComparison.OrdinalIgnoreCase)) return $"{left} / {right}";
            if (decimal.TryParse(right, out var constant) && constant != 0) return $"{left} / {right}";
            return $"{left} / NULLIF({right}, 0)";
        });
    }

    public static string CompileDerivedExpression(string expression, SemanticModel model)
    {
        return FieldRef().Replace(expression, match =>
        {
            var field = model.Fields.First(f => f.FieldId.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            return $"{field.TableId}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}";
        });
    }

    [GeneratedRegex(@"\b(SUM|COUNT|COUNT_DISTINCT|AVG|MIN|MAX)\s*\(\s*\[([^\]]+)\]\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex AggregateRef();

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex FieldRef();

    [GeneratedRegex(@"(.+?)\s+/\s+(.+)")]
    private static partial Regex DivisionExpression();
}
