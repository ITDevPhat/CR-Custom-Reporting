namespace Report.QueryEngine.Expressions.Tokenization;

public enum ExpressionTokenType
{
    Identifier,
    FieldReference,
    MetricReference,
    FunctionName,
    NumberLiteral,
    StringLiteral,
    Operator,
    OpenParen,
    CloseParen,
    Comma,
    End
}

public sealed record ExpressionToken(ExpressionTokenType Type, string Text, int Position);

public interface IExpressionTokenizer
{
    IReadOnlyList<ExpressionToken> Tokenize(string expression);
}

public sealed class ExpressionTokenizer : IExpressionTokenizer
{
    private static readonly HashSet<string> WordOperators = new(StringComparer.OrdinalIgnoreCase) { "AND", "OR", "NOT" };

    public IReadOnlyList<ExpressionToken> Tokenize(string expression)
    {
        var tokens = new List<ExpressionToken>();
        var i = 0;

        while (i < expression.Length)
        {
            var c = expression[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '[')
            {
                var start = i++;
                while (i < expression.Length && expression[i] != ']') i++;
                if (i >= expression.Length) throw new ExpressionParseException("Missing closing bracket for reference.", start);
                var reference = expression[(start + 1)..i].Trim();
                i++;
                tokens.Add(new ExpressionToken(
                    reference.StartsWith("metric.", StringComparison.OrdinalIgnoreCase)
                        ? ExpressionTokenType.MetricReference
                        : ExpressionTokenType.FieldReference,
                    reference,
                    start));
                continue;
            }

            if (c is '\'' or '"')
            {
                var quote = c;
                var start = i++;
                var value = "";
                while (i < expression.Length)
                {
                    if (expression[i] == quote)
                    {
                        if (i + 1 < expression.Length && expression[i + 1] == quote)
                        {
                            value += quote;
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    value += expression[i++];
                }

                if (i >= expression.Length) throw new ExpressionParseException("Missing closing quote for string literal.", start);
                i++;
                tokens.Add(new ExpressionToken(ExpressionTokenType.StringLiteral, value, start));
                continue;
            }

            if (char.IsDigit(c) || c == '.' && i + 1 < expression.Length && char.IsDigit(expression[i + 1]))
            {
                var start = i;
                i++;
                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.')) i++;
                tokens.Add(new ExpressionToken(ExpressionTokenType.NumberLiteral, expression[start..i], start));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                i++;
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_')) i++;
                var text = expression[start..i];
                if (text.Equals("CASE", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ExpressionParseException("CASE syntax is not supported yet. Use IF(condition, trueValue, falseValue).", start);
                }

                var type = WordOperators.Contains(text)
                    ? ExpressionTokenType.Operator
                    : PeekNonWhitespace(expression, i) == '('
                        ? ExpressionTokenType.FunctionName
                        : ExpressionTokenType.Identifier;
                tokens.Add(new ExpressionToken(type, text.ToUpperInvariant(), start));
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new ExpressionToken(ExpressionTokenType.OpenParen, "(", i++));
                continue;
            }

            if (c == ')')
            {
                tokens.Add(new ExpressionToken(ExpressionTokenType.CloseParen, ")", i++));
                continue;
            }

            if (c == ',')
            {
                tokens.Add(new ExpressionToken(ExpressionTokenType.Comma, ",", i++));
                continue;
            }

            var two = i + 1 < expression.Length ? expression.Substring(i, 2) : "";
            if (two is ">=" or "<=" or "!=" or "<>")
            {
                tokens.Add(new ExpressionToken(ExpressionTokenType.Operator, two, i));
                i += 2;
                continue;
            }

            if ("+-*/=<>".Contains(c))
            {
                tokens.Add(new ExpressionToken(ExpressionTokenType.Operator, c.ToString(), i++));
                continue;
            }

            throw new ExpressionParseException($"Unexpected character '{c}'.", i);
        }

        tokens.Add(new ExpressionToken(ExpressionTokenType.End, "", expression.Length));
        return tokens;
    }

    private static char PeekNonWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        return index < text.Length ? text[index] : '\0';
    }
}

public sealed class ExpressionParseException(string message, int position) : Exception($"{message} Position: {position}.")
{
    public int Position { get; } = position;
}
