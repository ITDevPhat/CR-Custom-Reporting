using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Binding;

namespace Report.QueryEngine.Expressions.Validation;

public sealed class ExpressionTypeInferenceService(SemanticFunctionRegistry functions)
{
    public string Infer(ExpressionNode ast, SemanticModel model) => InferNode(ast, model);

    private string InferNode(ExpressionNode node, SemanticModel model) => node switch
    {
        FieldReferenceNode field => NormalizeType(model.Fields.First(f => f.FieldId.Equals(field.FieldId, StringComparison.OrdinalIgnoreCase)).DataType),
        MetricReferenceNode metric => NormalizeType(model.Metrics.First(m => m.MetricId.Equals(metric.MetricId, StringComparison.OrdinalIgnoreCase)).DataType),
        NumberLiteralNode n => n.Value % 1 == 0 ? "integer" : "decimal",
        StringLiteralNode => "string",
        BooleanLiteralNode => "boolean",
        NullLiteralNode => "null",
        UnaryExpressionNode unary => InferUnary(unary, model),
        BinaryExpressionNode binary => InferBinary(binary, model),
        FunctionCallNode call => InferFunction(call, model),
        _ => "null"
    };

    private string InferUnary(UnaryExpressionNode unary, SemanticModel model)
    {
        var operand = InferNode(unary.Operand, model);
        if (unary.Operator.Equals("NOT", StringComparison.OrdinalIgnoreCase))
        {
            Require(operand == "boolean", "INVALID_OPERATOR_TYPES", "NOT requires a boolean operand.");
            return "boolean";
        }

        Require(IsNumeric(operand), "INVALID_OPERATOR_TYPES", $"Unary {unary.Operator} requires a numeric operand.");
        return operand;
    }

    private string InferBinary(BinaryExpressionNode binary, SemanticModel model)
    {
        var left = InferNode(binary.Left, model);
        var right = InferNode(binary.Right, model);
        var op = binary.Operator.ToUpperInvariant();

        if (op is "AND" or "OR")
        {
            Require((left == "boolean" || left == "null") && (right == "boolean" || right == "null"),
                "INVALID_OPERATOR_TYPES",
                $"{op} requires boolean operands.");
            return "boolean";
        }

        if (op is "=" or "!=" or "<>" or "<" or ">" or "<=" or ">=")
        {
            Require(AreComparable(left, right), "TYPE_MISMATCH", $"{left} and {right} cannot be compared.");
            return "boolean";
        }

        if (op == "+" && (left == "string" || right == "string"))
        {
            Require(left == "string" && right == "string", "TYPE_MISMATCH", "String concatenation requires both operands to be strings.");
            return "string";
        }

        Require(IsNumeric(left) && IsNumeric(right), "INVALID_OPERATOR_TYPES", $"{binary.Operator} requires numeric operands.");
        return op == "/" || left == "decimal" || right == "decimal" ? "decimal" : "integer";
    }

    private string InferFunction(FunctionCallNode call, SemanticModel model)
    {
        if (!functions.TryGet(call.FunctionName, out _))
        {
            throw new ExpressionValidationException("INVALID_FUNCTION", $"Invalid function: {call.FunctionName}.");
        }

        var args = call.Arguments.Select(arg => InferNode(arg, model)).ToList();
        switch (call.FunctionName)
        {
            case "SUM":
            case "AVG":
                Require(args.Count == 1 && IsNumeric(args[0]), "INVALID_FUNCTION_ARGUMENT", $"{call.FunctionName} requires one numeric field argument.");
                return "decimal";
            case "MIN":
            case "MAX":
                Require(args.Count == 1 && args[0] is "integer" or "decimal" or "datetime", "INVALID_FUNCTION_ARGUMENT", $"{call.FunctionName} requires a numeric or datetime argument.");
                return args[0] == "integer" ? "integer" : args[0];
            case "COUNT":
            case "COUNT_DISTINCT":
                return "integer";
            case "ROUND":
                Require(IsNumeric(args[0]) && args[1] == "integer", "INVALID_FUNCTION_ARGUMENT", "ROUND(value, decimals) requires numeric value and integer decimals.");
                return "decimal";
            case "ABS":
            case "CEILING":
            case "FLOOR":
                Require(IsNumeric(args[0]), "INVALID_FUNCTION_ARGUMENT", $"{call.FunctionName} requires a numeric argument.");
                return args[0] == "integer" && call.FunctionName == "ABS" ? "integer" : "decimal";
            case "NULLIF":
                Require(AreComparable(args[0], args[1]), "TYPE_MISMATCH", "NULLIF arguments must be comparable.");
                return args[0];
            case "COALESCE":
            case "ISNULL":
                return CommonType(args);
            case "IF":
                Require(args[0] == "boolean" || args[0] == "null", "INVALID_FUNCTION_ARGUMENT", "IF condition must be boolean.");
                return CommonType(args.Skip(1).ToList());
            case "YEAR":
            case "MONTH":
            case "DAY":
                Require(args[0] == "datetime", "INVALID_FUNCTION_ARGUMENT", $"{call.FunctionName} requires a datetime argument.");
                return "integer";
            case "CONCAT":
            case "LEFT":
            case "RIGHT":
            case "LEN":
            case "UPPER":
            case "LOWER":
                if (call.FunctionName == "LEN") return "integer";
                return "string";
            default:
                throw new ExpressionValidationException("INVALID_FUNCTION", $"Invalid function: {call.FunctionName}.");
        }
    }

    public static string NormalizeType(string dataType)
    {
        var t = dataType.ToLowerInvariant();
        if (t is "tinyint" or "smallint" or "int" or "bigint" or "integer") return "integer";
        if (t is "decimal" or "numeric" or "float" or "real" or "money") return "decimal";
        if (t.Contains("date") || t.Contains("time")) return "datetime";
        if (t is "bit" or "bool" or "boolean") return "boolean";
        if (t is "null") return "null";
        return "string";
    }

    private static string CommonType(IReadOnlyList<string> types)
    {
        var nonNull = types.Where(t => t != "null").Distinct().ToList();
        if (nonNull.Count == 0) return "null";
        if (nonNull.All(IsNumeric)) return nonNull.Contains("decimal") ? "decimal" : "integer";
        if (nonNull.Count == 1) return nonNull[0];
        throw new ExpressionValidationException("TYPE_MISMATCH", $"Incompatible result types: {string.Join(", ", nonNull)}.");
    }

    private static bool AreComparable(string left, string right) =>
        left == "null" || right == "null" || left == right || IsNumeric(left) && IsNumeric(right);

    private static bool IsNumeric(string type) => type is "integer" or "decimal";

    private static void Require(bool condition, string code, string message)
    {
        if (!condition) throw new ExpressionValidationException(code, message);
    }
}
