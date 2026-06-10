using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Binding;
using Report.QueryEngine.Expressions.Compilation;
using Report.QueryEngine.Expressions.Parsing;
using Report.QueryEngine.Expressions.Tokenization;
using Report.QueryEngine.Expressions.Validation;

namespace Report.QueryEngine.Compilation;

public static class SemanticExpressionCompiler
{
    public static string CompileMetricFormula(string formula, SemanticModel model) =>
        CompileMetricFormula(formula, model, null);

    public static string CompileDerivedExpression(string expression, SemanticModel model) =>
        CreateCompiler().CompileFormula(NormalizeLegacyCaseExpression(expression), model);

    public static string CompileFormula(string expression, SemanticModel model, IReadOnlyDictionary<string, string> aliases) =>
        CreateCompiler().CompileFormula(expression, model, aliases);

    public static string CompileMetricFormula(string formula, SemanticModel model, IReadOnlyDictionary<string, string>? aliases)
    {
        var tokenizer = new ExpressionTokenizer();
        var parser = new ExpressionParser();
        var functions = new SemanticFunctionRegistry();
        var ast = parser.Parse(tokenizer.Tokenize(formula));
        new AggregationValidationService(functions).Validate(ast, ExpressionScope.Aggregate, "calculated_measure");
        return new SemanticExpressionSqlCompiler(tokenizer, parser, functions).CompileFormula(formula, model, aliases);
    }

    private static SemanticExpressionSqlCompiler CreateCompiler()
    {
        var tokenizer = new ExpressionTokenizer();
        var parser = new ExpressionParser();
        return new SemanticExpressionSqlCompiler(tokenizer, parser, new SemanticFunctionRegistry());
    }

    private static string NormalizeLegacyCaseExpression(string expression)
    {
        var text = expression.Trim();
        if (!text.StartsWith("CASE WHEN ", StringComparison.OrdinalIgnoreCase) ||
            !text.EndsWith(" END", StringComparison.OrdinalIgnoreCase))
        {
            return expression;
        }

        var body = text[10..^4];
        var thenIndex = IndexOfWord(body, "THEN");
        var elseIndex = IndexOfWord(body, "ELSE");
        if (thenIndex < 0 || elseIndex < 0 || elseIndex < thenIndex) return expression;

        var condition = body[..thenIndex].Trim();
        var trueValue = body[(thenIndex + 4)..elseIndex].Trim();
        var falseValue = body[(elseIndex + 4)..].Trim();
        return $"IF({condition}, {trueValue}, {falseValue})";
    }

    private static int IndexOfWord(string text, string word)
    {
        for (var i = 0; i <= text.Length - word.Length; i++)
        {
            if (!text.AsSpan(i, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase)) continue;
            var before = i == 0 || char.IsWhiteSpace(text[i - 1]);
            var after = i + word.Length == text.Length || char.IsWhiteSpace(text[i + word.Length]);
            if (before && after) return i;
        }
        return -1;
    }
}
