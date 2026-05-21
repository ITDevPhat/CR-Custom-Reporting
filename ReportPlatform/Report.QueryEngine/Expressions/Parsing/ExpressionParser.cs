using System.Globalization;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Tokenization;

namespace Report.QueryEngine.Expressions.Parsing;

public interface IExpressionParser
{
    ExpressionNode Parse(IReadOnlyList<ExpressionToken> tokens);
}

public sealed class ExpressionParser : IExpressionParser
{
    private IReadOnlyList<ExpressionToken> _tokens = [];
    private int _index;

    public ExpressionNode Parse(IReadOnlyList<ExpressionToken> tokens)
    {
        _tokens = tokens;
        _index = 0;
        var expression = ParseOr();
        Expect(ExpressionTokenType.End);
        return expression;
    }

    private ExpressionNode ParseOr()
    {
        var node = ParseAnd();
        while (MatchOperator("OR")) node = new BinaryExpressionNode(node, "OR", ParseAnd());
        return node;
    }

    private ExpressionNode ParseAnd()
    {
        var node = ParseComparison();
        while (MatchOperator("AND")) node = new BinaryExpressionNode(node, "AND", ParseComparison());
        return node;
    }

    private ExpressionNode ParseComparison()
    {
        var node = ParseAdditive();
        while (Current.Type == ExpressionTokenType.Operator && Current.Text is "=" or "!=" or "<>" or "<" or ">" or "<=" or ">=")
        {
            var op = Current.Text;
            Advance();
            node = new BinaryExpressionNode(node, op, ParseAdditive());
        }

        return node;
    }

    private ExpressionNode ParseAdditive()
    {
        var node = ParseMultiplicative();
        while (Current.Type == ExpressionTokenType.Operator && Current.Text is "+" or "-")
        {
            var op = Current.Text;
            Advance();
            node = new BinaryExpressionNode(node, op, ParseMultiplicative());
        }

        return node;
    }

    private ExpressionNode ParseMultiplicative()
    {
        var node = ParseUnary();
        while (Current.Type == ExpressionTokenType.Operator && Current.Text is "*" or "/")
        {
            var op = Current.Text;
            Advance();
            node = new BinaryExpressionNode(node, op, ParseUnary());
        }

        return node;
    }

    private ExpressionNode ParseUnary()
    {
        if (Current.Type == ExpressionTokenType.Operator && Current.Text is "-" or "NOT")
        {
            var op = Current.Text;
            Advance();
            return new UnaryExpressionNode(op, ParseUnary());
        }

        return ParsePrimary();
    }

    private ExpressionNode ParsePrimary()
    {
        var token = Current;
        switch (token.Type)
        {
            case ExpressionTokenType.FieldReference:
                Advance();
                return new FieldReferenceNode(token.Text);
            case ExpressionTokenType.MetricReference:
                Advance();
                return new MetricReferenceNode(token.Text);
            case ExpressionTokenType.NumberLiteral:
                Advance();
                if (!decimal.TryParse(token.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    throw new ExpressionParseException($"Invalid number literal '{token.Text}'.", token.Position);
                }
                return new NumberLiteralNode(value);
            case ExpressionTokenType.StringLiteral:
                Advance();
                return new StringLiteralNode(token.Text);
            case ExpressionTokenType.Identifier when token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase):
                Advance();
                return new BooleanLiteralNode(true);
            case ExpressionTokenType.Identifier when token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase):
                Advance();
                return new BooleanLiteralNode(false);
            case ExpressionTokenType.Identifier when token.Text.Equals("NULL", StringComparison.OrdinalIgnoreCase):
                Advance();
                return new NullLiteralNode();
            case ExpressionTokenType.FunctionName:
                return ParseFunctionCall();
            case ExpressionTokenType.OpenParen:
                Advance();
                var expression = ParseOr();
                Expect(ExpressionTokenType.CloseParen);
                return expression;
            default:
                throw new ExpressionParseException($"Unexpected token '{token.Text}'.", token.Position);
        }
    }

    private ExpressionNode ParseFunctionCall()
    {
        var name = Current.Text.ToUpperInvariant();
        Advance();
        Expect(ExpressionTokenType.OpenParen);
        var args = new List<ExpressionNode>();
        if (Current.Type != ExpressionTokenType.CloseParen)
        {
            do
            {
                args.Add(ParseOr());
            } while (Match(ExpressionTokenType.Comma));
        }

        Expect(ExpressionTokenType.CloseParen);
        return new FunctionCallNode(name, args);
    }

    private ExpressionToken Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

    private void Advance() => _index++;

    private bool Match(ExpressionTokenType type)
    {
        if (Current.Type != type) return false;
        Advance();
        return true;
    }

    private bool MatchOperator(string op)
    {
        if (Current.Type != ExpressionTokenType.Operator || !Current.Text.Equals(op, StringComparison.OrdinalIgnoreCase)) return false;
        Advance();
        return true;
    }

    private void Expect(ExpressionTokenType type)
    {
        if (!Match(type))
        {
            throw new ExpressionParseException($"Expected {type}, found '{Current.Text}'.", Current.Position);
        }
    }
}
