using System.Globalization;
using Report.Contracts.Semantic;
using Report.Metadata.Models;

namespace Report.Api.Services;

public sealed class ExpressionValidationService
{
    private static readonly HashSet<string> AggregateFunctions = ["SUM", "AVG", "MIN", "MAX", "COUNT", "COUNT_DISTINCT"];
    private static readonly HashSet<string> SupportedFunctions = ["ABS", "ROUND", "CEILING", "FLOOR", "NULLIF", "COALESCE", "ISNULL", "IF", "CONCAT", "LEFT", "RIGHT", "LEN", "UPPER", "LOWER", "YEAR", "MONTH", "DAY", "DATEADD", "DATEDIFF", "SUM", "AVG", "MIN", "MAX", "COUNT", "COUNT_DISTINCT"];

    public ExpressionValidationResponse Validate(SemanticModel model, ExpressionValidationRequest request)
    {
        try
        {
            var parser = new ExprParser(request.Expression);
            var ast = parser.ParseExpression();
            var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasMetric = false;
            var hasAggregate = false;
            string Compile(Node n)
            {
                switch (n)
                {
                    case RefNode r:
                        deps.Add(r.Ref);
                        if (r.Ref.StartsWith("metric.", StringComparison.OrdinalIgnoreCase))
                        {
                            hasMetric = true;
                            var metric = model.Metrics.FirstOrDefault(m => m.MetricId.Equals(r.Ref, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Unknown metric reference: {r.Ref}");
                            return $"({metric.Formula})";
                        }
                        var field = model.Fields.FirstOrDefault(f => f.FieldId.Equals(r.Ref, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Unknown field reference: {r.Ref}");
                        return $"{field.TableId}.[{field.PhysicalColumn}]";
                    case NumNode x: return x.Value;
                    case StrNode s: return $"'{s.Value.Replace("'", "''")}'";
                    case UnaryNode u: return $"{u.Op}({Compile(u.Inner)})";
                    case BinaryNode b:
                        var l = Compile(b.Left); var r = Compile(b.Right);
                        return b.Op == "/" ? $"({l} / NULLIF({r}, 0))" : $"({l} {b.Op} {r})";
                    case FuncNode f:
                        if (!SupportedFunctions.Contains(f.Name)) throw new InvalidOperationException($"Unknown function: {f.Name}");
                        if (AggregateFunctions.Contains(f.Name)) hasAggregate = true;
                        if (f.Name == "IF" && f.Args.Count == 3) return $"(CASE WHEN {Compile(f.Args[0])} THEN {Compile(f.Args[1])} ELSE {Compile(f.Args[2])} END)";
                        if (f.Name == "COUNT_DISTINCT" && f.Args.Count == 1) return $"COUNT(DISTINCT {Compile(f.Args[0])})";
                        return $"{f.Name}({string.Join(", ", f.Args.Select(Compile))})";
                    default: throw new InvalidOperationException("Unsupported node.");
                }
            }

            var sql = Compile(ast);
            var kind = (hasMetric || hasAggregate) ? "calculated_measure" : "calculated_column";
            return new ExpressionValidationResponse { Valid = true, DetectedKind = kind, ReturnType = "decimal", Dependencies = deps.ToList(), CompiledSqlPreview = sql };
        }
        catch (Exception ex)
        {
            return new ExpressionValidationResponse { Valid = false, Errors = [ex.Message] };
        }
    }

    private abstract record Node;
    private sealed record RefNode(string Ref) : Node;
    private sealed record NumNode(string Value) : Node;
    private sealed record StrNode(string Value) : Node;
    private sealed record UnaryNode(string Op, Node Inner) : Node;
    private sealed record BinaryNode(string Op, Node Left, Node Right) : Node;
    private sealed record FuncNode(string Name, List<Node> Args) : Node;

    private sealed class ExprParser(string text)
    {
        private int _i;
        public Node ParseExpression() => ParseOr();
        private Node ParseOr() { var n = ParseAnd(); while (MatchWord("OR")) n = new BinaryNode("OR", n, ParseAnd()); return n; }
        private Node ParseAnd() { var n = ParseCmp(); while (MatchWord("AND")) n = new BinaryNode("AND", n, ParseCmp()); return n; }
        private Node ParseCmp() { var n = ParseAdd(); while (true) { var op = MatchAny(">=", "<=", "!=", "=", ">", "<"); if (op is null) break; n = new BinaryNode(op, n, ParseAdd()); } return n; }
        private Node ParseAdd() { var n = ParseMul(); while (true) { var op = MatchAny("+", "-"); if (op is null) break; n = new BinaryNode(op, n, ParseMul()); } return n; }
        private Node ParseMul() { var n = ParseUnary(); while (true) { var op = MatchAny("*", "/"); if (op is null) break; n = new BinaryNode(op, n, ParseUnary()); } return n; }
        private Node ParseUnary() { if (MatchAny("+", "-", "NOT") is string op) return new UnaryNode(op, ParseUnary()); return ParsePrimary(); }
        private Node ParsePrimary()
        {
            Skip();
            if (Peek() == '[') { _i++; var s = ReadUntil(']'); return new RefNode(s); }
            if (Peek() == '\'') { _i++; var s = ReadUntil('\''); return new StrNode(s); }
            if (char.IsDigit(Peek())) { var n = ReadWhile(c => char.IsDigit(c) || c == '.'); return new NumNode(n); }
            if (Peek() == '(') { _i++; var n = ParseExpression(); Expect(")"); return n; }
            var ident = ReadWhile(c => char.IsLetter(c) || c == '_').ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(ident)) throw new InvalidOperationException("Unexpected token.");
            if (ident == "CASE") throw new InvalidOperationException("CASE keyword parsing is not yet supported in Formula Editor.");
            Expect("(");
            var args = new List<Node>();
            if (!Check(")")) { do { args.Add(ParseExpression()); } while (MatchAny(",") is not null); }
            Expect(")");
            return new FuncNode(ident, args);
        }
        private char Peek() => _i >= text.Length ? '\0' : text[_i];
        private void Skip() { while (_i < text.Length && char.IsWhiteSpace(text[_i])) _i++; }
        private bool Check(string s) { Skip(); return text.AsSpan(_i).StartsWith(s, StringComparison.OrdinalIgnoreCase); }
        private void Expect(string s) { if (MatchAny(s) is null) throw new InvalidOperationException($"Expected '{s}'."); }
        private bool MatchWord(string w) { Skip(); if (!text.AsSpan(_i).StartsWith(w, StringComparison.OrdinalIgnoreCase)) return false; _i += w.Length; return true; }
        private string? MatchAny(params string[] ops) { Skip(); foreach (var op in ops.OrderByDescending(x => x.Length)) { if (text.AsSpan(_i).StartsWith(op, StringComparison.OrdinalIgnoreCase)) { _i += op.Length; return op.ToUpperInvariant(); } } return null; }
        private string ReadUntil(char end) { var start = _i; while (_i < text.Length && text[_i] != end) _i++; if (_i >= text.Length) throw new InvalidOperationException($"Missing '{end}'."); var v = text[start.._i]; _i++; return v; }
        private string ReadWhile(Func<char, bool> p) { var start = _i; while (_i < text.Length && p(text[_i])) _i++; return text[start.._i]; }
    }
}
