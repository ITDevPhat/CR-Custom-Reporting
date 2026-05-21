using FluentAssertions;
using Report.Contracts.Requests;
using Report.QueryEngine.Validation;
using static Report.QueryEngine.Tests.QueryEngineTestHarness;

namespace Report.QueryEngine.Tests;

public sealed class SqlGenerationTests
{
    private readonly QueryEngineTestHarness _harness = new();

    [Fact]
    public void DimensionOnly_ShouldGenerateRawDimensionSql()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"]));

        result.LogicalPlan.BaseTableId.Should().Be("DimCustomer");
        AssertSqlContains(result.Sql.Sql, "FROM DimCustomer c");
        AssertSqlContains(result.Sql.Sql, "c.CustomerName AS CustomerName");
        AssertSqlDoesNotContain(result.Sql.Sql, "GROUP BY");
        AssertSqlDoesNotContain(result.Sql.Sql, "FactSales");
        AssertSqlDoesNotContain(result.Sql.Sql, "SUM(");
    }

    [Fact]
    public void RawNumericFieldInRows_ShouldNotSum()
    {
        var result = _harness.Compile(Request(rows: ["factsales.quantity"], limit: 50));

        AssertSqlContains(result.Sql.Sql, "SELECT TOP (50) f.Quantity AS Quantity FROM FactSales f");
        AssertSqlDoesNotContain(result.Sql.Sql, "SUM(f.Quantity)");
        AssertSqlDoesNotContain(result.Sql.Sql, "GROUP BY");
    }

    [Fact]
    public void MeasureCandidateDroppedToValues_ShouldUseMetric()
    {
        var result = _harness.Compile(Request(values: ["metric.sum_factsales_quantity"], limit: 50));

        AssertSqlContains(result.Sql.Sql, "SELECT TOP (50) SUM(f.Quantity) AS SumQuantity FROM FactSales f");
        AssertSqlDoesNotContain(result.Sql.Sql, "GROUP BY");
    }

    [Fact]
    public void MetricOnly_ShouldGenerateAggregateWithoutGroupBy()
    {
        var result = _harness.Compile(Request(values: ["metric.total_sales"]));

        result.LogicalPlan.BaseTableId.Should().Be("FactSales");
        AssertSqlContains(result.Sql.Sql, "FROM FactSales f");
        AssertSqlContains(result.Sql.Sql, "SUM(f.SalesAmount) AS TotalSales");
        AssertSqlDoesNotContain(result.Sql.Sql, "GROUP BY");
    }

    [Fact]
    public void NoFilter_ShouldNotEmitWhere()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"], values: ["metric.total_sales"]));

        AssertSqlDoesNotContain(result.Sql.Sql, "WHERE");
    }

    [Fact]
    public void NoSortWithLimitOnly_ShouldUseTopWithoutOrderByFallback()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"], limit: 50));

        AssertSqlContains(result.Sql.Sql, "SELECT TOP (50) c.CustomerName AS CustomerName FROM DimCustomer c");
        AssertSqlDoesNotContain(result.Sql.Sql, "ORDER BY");
        AssertSqlDoesNotContain(result.Sql.Sql, "OFFSET");
        AssertSqlDoesNotContain(result.Sql.Sql, "FETCH NEXT");
    }

    [Fact]
    public void NoMetric_ShouldNotEmitGroupBy()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername", "factsales.quantity"]));

        AssertSqlContains(result.Sql.Sql, "c.CustomerName AS CustomerName");
        AssertSqlContains(result.Sql.Sql, "f.Quantity AS Quantity");
        AssertSqlDoesNotContain(result.Sql.Sql, "GROUP BY");
        AssertSqlDoesNotContain(result.Sql.Sql, "SUM(f.Quantity)");
    }

    [Fact]
    public void DimensionMetric_ShouldJoinDimensionAndGroup()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"], values: ["metric.total_sales"]));

        result.LogicalPlan.BaseTableId.Should().Be("FactSales");
        AssertSqlContains(result.Sql.Sql, "FROM FactSales f");
        AssertSqlContains(result.Sql.Sql, "INNER JOIN DimCustomer c ON f.CustomerKey = c.CustomerKey");
        AssertSqlContains(result.Sql.Sql, "c.CustomerName AS CustomerName");
        AssertSqlContains(result.Sql.Sql, "SUM(f.SalesAmount) AS TotalSales");
        AssertSqlContains(result.Sql.Sql, "GROUP BY c.CustomerName");
    }

    [Fact]
    public void DimensionMetric_ShouldRequireGroupBy()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"], values: ["metric.sum_factsales_quantity"]));

        AssertSqlContains(result.Sql.Sql, "SUM(f.Quantity) AS SumQuantity");
        AssertSqlContains(result.Sql.Sql, "GROUP BY c.CustomerName");
    }

    [Fact]
    public void MultiDimensionMetric_ShouldJoinAllRequiredDimensions()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername", "dimdate.yearnumber", "dimproduct.category"],
            values: ["metric.total_sales"]));

        AssertSqlContains(result.Sql.Sql, "FROM FactSales f");
        AssertSqlContains(result.Sql.Sql, "INNER JOIN DimCustomer c ON f.CustomerKey = c.CustomerKey");
        AssertSqlContains(result.Sql.Sql, "INNER JOIN DimDate d ON f.OrderDateKey = d.DateKey");
        AssertSqlContains(result.Sql.Sql, "INNER JOIN DimProduct p ON f.ProductKey = p.ProductKey");
        AssertSqlContains(result.Sql.Sql, "c.CustomerName AS CustomerName");
        AssertSqlContains(result.Sql.Sql, "d.YearNumber AS Year");
        AssertSqlContains(result.Sql.Sql, "p.Category AS Category");
        AssertSqlContains(result.Sql.Sql, "SUM(f.SalesAmount) AS TotalSales");
        AssertSqlContains(result.Sql.Sql, "GROUP BY c.CustomerName, d.YearNumber, p.Category");
    }

    [Fact]
    public void WhereFilterDimension_ShouldJoinFilterTableAndParameterizeWhere()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "dimdate.yearnumber", Operator = "=", Value = 2025, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "INNER JOIN DimDate d ON f.OrderDateKey = d.DateKey");
        AssertSqlContains(result.Sql.Sql, "WHERE d.YearNumber = @p0");
        AssertSqlContains(result.Sql.Sql, "GROUP BY c.CustomerName");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be(2025);
    }

    [Fact]
    public void WhereFilterContains_ShouldUseSqlServerLikePattern()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "dimcustomer.customername", Operator = "CONTAINS", Value = "Corp", Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "WHERE c.CustomerName LIKE '%' + @p0 + '%'");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be("Corp");
    }

    [Fact]
    public void WhereFilterIn_ShouldParameterizeEachValueInOrder()
    {
        var result = _harness.Compile(Request(
            rows: ["dimdate.yearnumber"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "dimdate.yearnumber", Operator = "IN", Value = new object[] { 2024, 2025 }, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "WHERE d.YearNumber IN (@p0, @p1)");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be(2024);
        result.Sql.Parameters.Should().ContainKey("p1").WhoseValue.Should().Be(2025);
    }

    [Fact]
    public void WhereFilterBetween_ShouldParameterizeBoundsInOrder()
    {
        var result = _harness.Compile(Request(
            rows: ["dimdate.yearnumber"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "dimdate.yearnumber", Operator = "BETWEEN", Value = new object[] { 2020, 2025 }, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "WHERE d.YearNumber BETWEEN @p0 AND @p1");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be(2020);
        result.Sql.Parameters.Should().ContainKey("p1").WhoseValue.Should().Be(2025);
    }

    [Fact]
    public void HavingFilterMetric_ShouldCompileMetricFilterToHaving()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "metric.total_sales", Operator = ">", Value = 10000, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "HAVING SUM(f.SalesAmount) > @p0");
        AssertSqlDoesNotContain(result.Sql.Sql, "WHERE SUM(f.SalesAmount) > @p0");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be(10000);
    }

    [Fact]
    public void RawFilter_ShouldCompileToWhere()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "factsales.quantity", Operator = ">=", Value = 10, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "WHERE f.Quantity >= @p0");
        AssertSqlDoesNotContain(result.Sql.Sql, "HAVING f.Quantity >= @p0");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be(10);
    }

    [Fact]
    public void MetricFilter_ShouldCompileToHaving()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.sum_factsales_quantity"],
            filters:
            [
                new FilterRequest { Field = "metric.sum_factsales_quantity", Operator = ">=", Value = 10, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "HAVING SUM(f.Quantity) >= @p0");
        AssertSqlDoesNotContain(result.Sql.Sql, "WHERE SUM(f.Quantity) >= @p0");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be(10);
    }

    [Fact]
    public void SortByMetricAndDimension_ShouldOrderBySelectedAliases()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.total_sales"],
            sort:
            [
                new SortRequest { Field = "metric.total_sales", Direction = "DESC" },
                new SortRequest { Field = "dimcustomer.customername", Direction = "ASC" }
            ]));

        AssertSqlContains(result.Sql.Sql, "ORDER BY TotalSales DESC, CustomerName ASC");
    }

    [Fact]
    public void RatioMetricProfitMargin_ShouldProtectDivisionWithNullIf()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"], values: ["metric.profit_margin"]));

        AssertSqlContains(result.Sql.Sql, "SUM(f.ProfitAmount) / NULLIF(SUM(f.SalesAmount), 0) AS ProfitMargin");
        AssertSqlDoesNotContain(result.Sql.Sql, "SUM(f.ProfitAmount) / SUM(f.SalesAmount)");
        AssertSqlContains(result.Sql.Sql, "GROUP BY c.CustomerName");
    }

    [Fact]
    public void CountDistinctMetric_ShouldCompileCountDistinct()
    {
        var result = _harness.Compile(Request(rows: ["dimcustomer.customername"], values: ["metric.order_count"]));

        AssertSqlContains(result.Sql.Sql, "COUNT(DISTINCT f.OrderID) AS OrderCount");
    }

    [Fact]
    public void DerivedFieldGrouping_ShouldSelectAndGroupByExpression()
    {
        var result = _harness.Compile(Request(rows: ["derived.discount_band"], values: ["metric.total_sales"]));

        AssertSqlContains(result.Sql.Sql, "CASE WHEN f.Discount > 0.2 THEN 'High' ELSE 'Low' END AS DiscountBand");
        AssertSqlContains(result.Sql.Sql, "GROUP BY CASE WHEN f.Discount > 0.2 THEN 'High' ELSE 'Low' END");
        AssertSqlDoesNotContain(result.Sql.Sql, "GROUP BY DiscountBand");
    }

    [Fact]
    public void DerivedFieldFilter_ShouldUseDerivedExpressionInWhere()
    {
        var result = _harness.Compile(Request(
            rows: ["derived.discount_band"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "derived.discount_band", Operator = "=", Value = "High", Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "WHERE CASE WHEN f.Discount > 0.2 THEN 'High' ELSE 'Low' END = @p0");
        AssertSqlContains(result.Sql.Sql, "GROUP BY CASE WHEN f.Discount > 0.2 THEN 'High' ELSE 'Low' END");
        result.Sql.Parameters.Should().ContainKey("p0").WhoseValue.Should().Be("High");
    }

    [Fact]
    public void UnknownField_ShouldFailWithClearSemanticError()
    {
        var act = () => _harness.Compile(Request(rows: ["not.exists"], values: ["metric.total_sales"]));

        act.Should().Throw<SemanticQueryValidationException>()
            .Where(ex => ex.Errors.Values.SelectMany(errors => errors).Any(error => error.Contains("Unknown field id", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void UnknownMetric_ShouldFailWithClearSemanticError()
    {
        var act = () => _harness.Compile(Request(rows: ["dimcustomer.customername"], values: ["metric.not_exists"]));

        act.Should().Throw<SemanticQueryValidationException>()
            .Where(ex => ex.Errors.Values.SelectMany(errors => errors).Any(error => error.Contains("Unknown metric id", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MissingRelationship_ShouldFailWithClearSemanticError()
    {
        var harness = new QueryEngineTestHarness(model =>
        {
            model.Relationships.RemoveAll(r => r.ToTableId == "DimProduct");
            return model;
        });

        var act = () => harness.Compile(Request(rows: ["dimproduct.category"], values: ["metric.total_sales"]));

        act.Should().Throw<SemanticQueryValidationException>()
            .Where(ex => ex.Errors.Values.SelectMany(errors => errors).Any(error => error.Contains("No relationship", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void StableParameterOrder_ShouldFollowWhereThenHavingFilterOrder()
    {
        var result = _harness.Compile(Request(
            rows: ["dimcustomer.customername"],
            values: ["metric.total_sales"],
            filters:
            [
                new FilterRequest { Field = "dimdate.yearnumber", Operator = "=", Value = 2025, Scope = "visual" },
                new FilterRequest { Field = "dimcustomer.customername", Operator = "CONTAINS", Value = "Corp", Scope = "visual" },
                new FilterRequest { Field = "metric.total_sales", Operator = ">", Value = 10000, Scope = "visual" }
            ]));

        AssertSqlContains(result.Sql.Sql, "WHERE d.YearNumber = @p0 AND c.CustomerName LIKE '%' + @p1 + '%'");
        AssertSqlContains(result.Sql.Sql, "HAVING SUM(f.SalesAmount) > @p2");
        result.Sql.Parameters.Should().ContainInOrder(
            new KeyValuePair<string, object?>("p0", 2025),
            new KeyValuePair<string, object?>("p1", "Corp"),
            new KeyValuePair<string, object?>("p2", 10000));
    }
}
