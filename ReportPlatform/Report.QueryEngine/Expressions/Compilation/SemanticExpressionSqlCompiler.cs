using Report.Metadata.Models;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Binding;
using Report.QueryEngine.Expressions.Parsing;
using Report.QueryEngine.Expressions.Tokenization;
using Report.QueryEngine.Expressions.Validation;

namespace Report.QueryEngine.Expressions.Compilation;

public sealed class SemanticExpressionSqlCompiler(
    IExpressionTokenizer tokenizer,
    IExpressionParser parser,
    SemanticFunctionRegistry functions)
{
    public string Compile(BoundExpression expression, SemanticModel model, IReadOnlyDictionary<string, string>? aliases = null) =>
        TrimOuterParentheses(CompileNode(expression.Ast, model, aliases ?? new Dictionary<string, string>(), []));

    public string CompileFormula(string formula, SemanticModel model, IReadOnlyDictionary<string, string>? aliases = null)
    {
        var ast = parser.Parse(tokenizer.Tokenize(formula));
        return TrimOuterParentheses(CompileNode(ast, model, aliases ?? new Dictionary<string, string>(), []));
    }

    private string CompileNode(ExpressionNode node, SemanticModel model, IReadOnlyDictionary<string, string> aliases, HashSet<string> metricStack)
    {
        switch (node)
        {
            case FieldReferenceNode fieldRef:
                var field = model.Fields.First(f => f.FieldId.Equals(fieldRef.FieldId, StringComparison.OrdinalIgnoreCase));
                var qualifier = aliases.TryGetValue(field.TableId, out var alias) ? alias : field.TableId;
                if (field.IsDerived && field.Expression is not null)
                {
                    return $"({CompileFormula(field.Expression, model, aliases)})";
                }
                return $"{qualifier}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}";
            case MetricReferenceNode metricRef:
                if (!metricStack.Add(metricRef.MetricId))
                {
                    throw new ExpressionValidationException("CIRCULAR_DEPENDENCY", $"Circular metric dependency detected at {metricRef.MetricId}.");
                }
                var metric = model.Metrics.First(m => m.MetricId.Equals(metricRef.MetricId, StringComparison.OrdinalIgnoreCase));
                var compiled = CompileNode(parser.Parse(tokenizer.Tokenize(metric.Formula)), model, aliases, metricStack);
                metricStack.Remove(metricRef.MetricId);
                return $"({compiled})";
            case NumberLiteralNode number:
                return number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case StringLiteralNode text:
                return $"'{text.Value.Replace("'", "''")}'";
            case BooleanLiteralNode boolean:
                return boolean.Value ? "1" : "0";
            case NullLiteralNode:
                return "NULL";
            case UnaryExpressionNode unary:
                return unary.Operator.Equals("NOT", StringComparison.OrdinalIgnoreCase)
                    ? $"(NOT {CompileNode(unary.Operand, model, aliases, metricStack)})"
                    : $"({unary.Operator}{CompileNode(unary.Operand, model, aliases, metricStack)})";
            case BinaryExpressionNode binary:
                var left = CompileNode(binary.Left, model, aliases, metricStack);
                var right = CompileNode(binary.Right, model, aliases, metricStack);
                var op = binary.Operator == "!=" ? "<>" : binary.Operator;
                return op == "/"
                    ? $"({left} / {SafeDenominator(right)})"
                    : $"({left} {op} {right})";
            case FunctionCallNode call:
                return CompileFunction(call, model, aliases, metricStack);
            default:
                throw new InvalidOperationException($"Unsupported expression node {node.GetType().Name}.");
        }
    }

    private string CompileFunction(FunctionCallNode call, SemanticModel model, IReadOnlyDictionary<string, string> aliases, HashSet<string> metricStack)
    {
        if (!functions.TryGet(call.FunctionName, out _))
        {
            throw new ExpressionValidationException("INVALID_FUNCTION", $"Invalid function: {call.FunctionName}.");
        }

        var args = call.Arguments.Select(arg => CompileNode(arg, model, aliases, metricStack)).ToList();
        return call.FunctionName switch
        {
            "IF" => $"(CASE WHEN {TrimOuterParentheses(args[0])} THEN {args[1]} ELSE {args[2]} END)",
            "COUNT_DISTINCT" => $"COUNT(DISTINCT {args[0]})",
            "ISNULL" => $"ISNULL({args[0]}, {args[1]})",
            _ => $"{call.FunctionName}({string.Join(", ", args)})"
        };
    }

    private static string SafeDenominator(string right)
    {
        var trimmed = TrimOuterParentheses(right.Trim());
        if (trimmed.StartsWith("NULLIF(", StringComparison.OrdinalIgnoreCase)) return right;
        if (decimal.TryParse(trimmed, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var constant) && constant != 0) return right;
        return $"NULLIF({right}, 0)";
    }

    private static string TrimOuterParentheses(string value)
    {
        while (IsWrappedByOuterParentheses(value))
        {
            value = value[1..^1].Trim();
        }
        return value;
    }

    private static bool IsWrappedByOuterParentheses(string value)
    {
        value = value.Trim();
        if (value.Length <= 1 || value[0] != '(' || value[^1] != ')') return false;

        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '(') depth++;
            if (value[i] == ')') depth--;
            if (depth == 0 && i < value.Length - 1)
            {
                return false;
            }
        }

        return depth == 0;
    }
}
