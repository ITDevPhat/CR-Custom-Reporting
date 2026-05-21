using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Ast;
using Report.QueryEngine.Expressions.Binding;
using Report.QueryEngine.Expressions.Compilation;
using Report.QueryEngine.Expressions.Dependencies;
using Report.QueryEngine.Expressions.Parsing;
using Report.QueryEngine.Expressions.Tokenization;
using Report.QueryEngine.Expressions.Validation;

namespace Report.QueryEngine.Tests;

public sealed class SemanticExpressionEngineTests
{
    [Fact]
    public void Tokenizer_ShouldClassifyFieldMetricFunctionAndLiterals()
    {
        var tokens = new ExpressionTokenizer().Tokenize("IF([metric.sales] > 1000, 'High', ROUND([factsales.salesamount], 2))");

        Assert.Contains(tokens, t => t.Type == ExpressionTokenType.MetricReference && t.Text == "metric.sales");
        Assert.Contains(tokens, t => t.Type == ExpressionTokenType.FieldReference && t.Text == "factsales.salesamount");
        Assert.Contains(tokens, t => t.Type == ExpressionTokenType.FunctionName && t.Text == "IF");
        Assert.Contains(tokens, t => t.Type == ExpressionTokenType.StringLiteral && t.Text == "High");
        Assert.Contains(tokens, t => t.Type == ExpressionTokenType.NumberLiteral && t.Text == "1000");
    }

    [Fact]
    public void Parser_ShouldRespectOperatorPrecedence()
    {
        var ast = Parse("[metric.sales] - [metric.cost] / [metric.sales]");

        var binary = Assert.IsType<BinaryExpressionNode>(ast);
        Assert.Equal("-", binary.Operator);
        Assert.IsType<MetricReferenceNode>(binary.Left);
        var right = Assert.IsType<BinaryExpressionNode>(binary.Right);
        Assert.Equal("/", right.Operator);
    }

    [Fact]
    public void Parser_ShouldParseRoundWithNestedParentheses()
    {
        var ast = Parse("ROUND(([metric.sales] - [metric.cost]) / [metric.sales], 4)");

        var call = Assert.IsType<FunctionCallNode>(ast);
        Assert.Equal("ROUND", call.FunctionName);
        Assert.Equal(2, call.Arguments.Count);
    }

    [Fact]
    public void Validation_ShouldRejectUnknownField()
    {
        var result = Services().Validator.Validate(Model(), "[factsales.nope] * 2");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == "UNKNOWN_FIELD_REFERENCE");
    }

    [Fact]
    public void Validation_ShouldRejectInvalidFunction()
    {
        var result = Services().Validator.Validate(Model(), "BOGUS([factsales.salesamount])");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_FUNCTION");
    }

    [Fact]
    public void Validation_ShouldRejectTypeMismatch()
    {
        var result = Services().Validator.Validate(Model(), "[dimcustomer.customername] * 2");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code is "INVALID_OPERATOR_TYPES" or "TYPE_MISMATCH");
    }

    [Fact]
    public void Validation_ShouldDetectRowExpressionAsCalculatedColumn()
    {
        var result = Services().Validator.Validate(Model(), "[factsales.unitprice] * [factsales.quantity]");

        Assert.True(result.Valid);
        Assert.Equal("calculated_column", result.DetectedKind);
        Assert.Equal(ExpressionScope.Row, result.DetectedScope);
    }

    [Fact]
    public void Validation_ShouldDetectAggregateExpressionAsCalculatedMeasure()
    {
        var result = Services().Validator.Validate(Model(), "SUM([factsales.salesamount])");

        Assert.True(result.Valid);
        Assert.Equal("calculated_measure", result.DetectedKind);
        Assert.Equal(ExpressionScope.Aggregate, result.DetectedScope);
    }

    [Fact]
    public void Validation_ShouldRejectMetricReferenceForCalculatedColumnTarget()
    {
        var result = Services().Validator.Validate(Model(), "[metric.total_sales] - [metric.total_profit]", "calculated_column");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == "AGGREGATE_SCOPE_CONFLICT");
    }

    [Fact]
    public void Validation_ShouldRejectNestedAggregates()
    {
        var result = Services().Validator.Validate(Model(), "SUM(AVG([factsales.salesamount]))");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == "AGGREGATE_SCOPE_CONFLICT");
    }

    [Fact]
    public void Validation_ShouldRejectBareRowFieldInCalculatedMeasure()
    {
        var result = Services().Validator.Validate(Model(), "[metric.total_sales] / [factsales.quantity]", "auto");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == "AGGREGATE_SCOPE_CONFLICT");
    }

    [Fact]
    public void Validation_ShouldAllowAggregatedFieldInCalculatedMeasure()
    {
        var result = Services().Validator.Validate(Model(), "[metric.total_sales] / SUM([factsales.quantity])", "auto");

        Assert.True(result.Valid);
        Assert.Equal("calculated_measure", result.DetectedKind);
    }

    [Fact]
    public void Validation_ShouldRejectCircularMetricDependency()
    {
        var model = Model();
        model.Metrics.Add(new SemanticMetric { MetricId = "metric.a", DatasetId = "sales", DisplayName = "A", Formula = "[metric.b]", BaseTableId = "FactSales" });
        model.Metrics.Add(new SemanticMetric { MetricId = "metric.b", DatasetId = "sales", DisplayName = "B", Formula = "[metric.a]", BaseTableId = "FactSales" });

        var result = Services().Validator.Validate(model, "[metric.b]", "calculated_measure", "metric.a");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Code == "CIRCULAR_DEPENDENCY");
    }

    [Fact]
    public void SqlCompiler_ShouldExpandMetricReferencesAndProtectDivision()
    {
        var model = Model();
        model.Metrics.Add(new SemanticMetric
        {
            MetricId = "metric.gross_profit",
            DatasetId = "sales",
            DisplayName = "GrossProfit",
            Formula = "[metric.total_sales] - SUM([factsales.profitamount])",
            BaseTableId = "FactSales"
        });

        var sql = Services().Compiler.CompileFormula("[metric.gross_profit] / [metric.total_sales]", model, new Dictionary<string, string> { ["FactSales"] = "f" });

        Assert.Contains("SUM(f.[SalesAmount])", sql);
        Assert.Contains("SUM(f.[ProfitAmount])", sql);
        Assert.Contains("NULLIF", sql);
    }

    [Fact]
    public void SqlCompiler_ShouldCompileIfCountDistinctAndRound()
    {
        var compiler = Services().Compiler;
        var model = Model();

        Assert.Contains("CASE WHEN", compiler.CompileFormula("IF([factsales.salesamount] > 1000, 'High', 'Low')", model));
        Assert.Contains("COUNT(DISTINCT", compiler.CompileFormula("COUNT_DISTINCT([factsales.orderid])", model));
        Assert.Contains("ROUND(", compiler.CompileFormula("ROUND(SUM([factsales.salesamount]), 2)", model));
    }

    private static ExpressionNode Parse(string expression)
    {
        var tokenizer = new ExpressionTokenizer();
        return new ExpressionParser().Parse(tokenizer.Tokenize(expression));
    }

    private static SemanticModel Model() => QueryEngineTestHarness.CreateSalesModel();

    private static ServicesBag Services()
    {
        var tokenizer = new ExpressionTokenizer();
        var parser = new ExpressionParser();
        var functions = new SemanticFunctionRegistry();
        var compiler = new SemanticExpressionSqlCompiler(tokenizer, parser, functions);
        var validator = new SemanticExpressionValidationService(
            tokenizer,
            parser,
            new ExpressionSemanticBinder(functions),
            new ExpressionScopeResolver(functions),
            new ExpressionTypeInferenceService(functions),
            new AggregationValidationService(functions),
            new ExpressionDependencyGraphService(),
            compiler);
        return new ServicesBag(validator, compiler);
    }

    private sealed record ServicesBag(SemanticExpressionValidationService Validator, SemanticExpressionSqlCompiler Compiler);
}
