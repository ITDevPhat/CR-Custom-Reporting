using System.Text.RegularExpressions;
using FluentAssertions;
using Report.Contracts.Requests;
using Report.Metadata.Models;
using Report.QueryEngine.Binding;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Planning;
using Report.QueryEngine.Relationships;

namespace Report.QueryEngine.Tests;

public sealed class QueryEngineTestHarness
{
    private readonly Func<SemanticModel, SemanticModel>? _configureModel;

    public QueryEngineTestHarness(Func<SemanticModel, SemanticModel>? configureModel = null)
    {
        _configureModel = configureModel;
    }

    public QueryEngineCompilation Compile(VisualQueryRequest request)
    {
        var model = _configureModel?.Invoke(CreateSalesModel()) ?? CreateSalesModel();

        var bound = new SemanticModelBinder().Bind(request, model);
        var context = new EvaluationContextBuilder().Build(bound);
        var measures = new MeasureExpansionEngine().Expand(context, model);
        var joinPlan = new RelationshipTraversalEngine().Build(context, measures, model);
        var logicalPlan = new LogicalPlanBuilder().Build(context, measures, joinPlan, model);
        var sql = new SqlCompiler().Compile(logicalPlan);

        return new QueryEngineCompilation(bound, context, measures, joinPlan, logicalPlan, sql);
    }

    public static string NormalizeSql(string sql)
    {
        var normalized = sql
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("[", "", StringComparison.Ordinal)
            .Replace("]", "", StringComparison.Ordinal)
            .Replace("dbo.", "", StringComparison.OrdinalIgnoreCase);

        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    public static void AssertSqlContains(string actual, string expected)
    {
        NormalizeSql(actual).Should().Contain(NormalizeSql(expected));
    }

    public static void AssertSqlDoesNotContain(string actual, string unexpected)
    {
        NormalizeSql(actual).Should().NotContain(NormalizeSql(unexpected));
    }

    public static VisualQueryRequest Request(
        string[]? rows = null,
        string[]? values = null,
        FilterRequest[]? filters = null,
        SortRequest[]? sort = null,
        int limit = 100,
        int offset = 0)
    {
        return new VisualQueryRequest
        {
            ConnectionId = "conn_test",
            DatasetId = "sales",
            ReportId = "rpt_test",
            VisualType = "table",
            Rows = rows?.ToList() ?? [],
            Values = values?.ToList() ?? [],
            Filters = filters?.ToList() ?? [],
            Sort = sort?.ToList() ?? [],
            Limit = limit,
            Offset = offset
        };
    }

    public static SemanticModel CreateSalesModel()
    {
        return new SemanticModel
        {
            DatasetId = "sales",
            DisplayName = "Sales",
            ConnectionId = "conn_test",
            Tables =
            [
                Table("FactSales", "Sales", "fact", "sales_transaction"),
                Table("DimCustomer", "Customer", "dimension", "customer"),
                Table("DimDate", "Date", "dimension", "date"),
                Table("DimProduct", "Product", "dimension", "product")
            ],
            Fields =
            [
                Field("dimcustomer.customername", "DimCustomer", "CustomerName", "CustomerName", "nvarchar", "dimension", "customer"),
                Field("dimdate.yearnumber", "DimDate", "YearNumber", "Year", "int", "dimension", "date"),
                Field("dimproduct.category", "DimProduct", "Category", "Category", "nvarchar", "dimension", "product"),
                Field("factsales.salesamount", "FactSales", "SalesAmount", "Sales Amount", "decimal", "measure_candidate", "sales_transaction"),
                Field("factsales.profitamount", "FactSales", "ProfitAmount", "Profit Amount", "decimal", "measure_candidate", "sales_transaction"),
                Field("factsales.quantity", "FactSales", "Quantity", "Quantity", "int", "measure_candidate", "sales_transaction"),
                Field("factsales.orderid", "FactSales", "OrderID", "Order ID", "nvarchar", "dimension", "sales_transaction"),
                Field("factsales.discount", "FactSales", "Discount", "Discount", "decimal", "measure_candidate", "sales_transaction"),
                new()
                {
                    DatasetId = "sales",
                    FieldId = "derived.discount_band",
                    TableId = "FactSales",
                    PhysicalTable = "FactSales",
                    PhysicalColumn = "DiscountBand",
                    DisplayName = "DiscountBand",
                    DataType = "nvarchar",
                    Role = "derived_field",
                    Grain = "sales_transaction",
                    SemanticType = "category",
                    Format = "general",
                    IsHidden = false,
                    IsDraggable = true,
                    IsDerived = true,
                    BaseTableId = "FactSales",
                    Expression = "CASE WHEN [factsales.discount] > 0.2 THEN 'High' ELSE 'Low' END"
                }
            ],
            Metrics =
            [
                Metric("metric.total_sales", "TotalSales", "SUM([factsales.salesamount])", "FactSales", "additive"),
                Metric("metric.total_profit", "TotalProfit", "SUM([factsales.profitamount])", "FactSales", "additive"),
                Metric("metric.sum_factsales_quantity", "SumQuantity", "SUM([factsales.quantity])", "FactSales", "additive"),
                Metric("metric.profit_margin", "ProfitMargin", "SUM([factsales.profitamount]) / SUM([factsales.salesamount])", "FactSales", "ratio"),
                Metric("metric.order_count", "OrderCount", "COUNT_DISTINCT([factsales.orderid])", "FactSales", "non_additive")
            ],
            Relationships =
            [
                Relationship("rel_customer", "FactSales", "CustomerKey", "DimCustomer", "CustomerKey"),
                Relationship("rel_date", "FactSales", "OrderDateKey", "DimDate", "DateKey"),
                Relationship("rel_product", "FactSales", "ProductKey", "DimProduct", "ProductKey")
            ]
        };
    }

    private static SemanticTable Table(string tableId, string displayName, string tableType, string grain)
    {
        return new SemanticTable
        {
            TableId = tableId,
            DisplayName = displayName,
            TableType = tableType,
            Grain = grain,
            PhysicalSchema = "dbo",
            PhysicalTable = tableId
        };
    }

    private static SemanticField Field(
        string fieldId,
        string tableId,
        string physicalColumn,
        string displayName,
        string dataType,
        string role,
        string grain)
    {
        return new SemanticField
        {
            DatasetId = "sales",
            FieldId = fieldId,
            TableId = tableId,
            PhysicalTable = tableId,
            PhysicalColumn = physicalColumn,
            DisplayName = displayName,
            DataType = dataType,
            Role = role,
            Grain = grain,
            SemanticType = role == "dimension" ? "category" : "number",
            DefaultAggregation = "none",
            Format = "general",
            IsHidden = false,
            IsDraggable = true
        };
    }

    private static SemanticMetric Metric(
        string metricId,
        string displayName,
        string formula,
        string baseTableId,
        string aggregationBehavior)
    {
        return new SemanticMetric
        {
            DatasetId = "sales",
            MetricId = metricId,
            DisplayName = displayName,
            Formula = formula,
            BaseTableId = baseTableId,
            AggregationBehavior = aggregationBehavior,
            DataType = aggregationBehavior == "ratio" ? "percentage" : "decimal",
            Format = aggregationBehavior == "ratio" ? "percentage" : "general",
            IsHidden = false,
            IsDraggable = true
        };
    }

    private static SemanticRelationship Relationship(
        string relationshipId,
        string fromTableId,
        string fromColumn,
        string toTableId,
        string toColumn)
    {
        return new SemanticRelationship
        {
            DatasetId = "sales",
            RelationshipId = relationshipId,
            FromTableId = fromTableId,
            FromColumn = fromColumn,
            ToTableId = toTableId,
            ToColumn = toColumn,
            JoinType = "INNER",
            Cardinality = "N:1",
            CrossFilterDirection = "single",
            IsActive = true,
            IsPrimary = true,
            Source = "database_fk",
            Confidence = 1.0m,
            Status = "active"
        };
    }
}

public sealed record QueryEngineCompilation(
    BoundSemanticQuery Bound,
    EvaluationContext Context,
    List<ExpandedMeasure> Measures,
    JoinPlan JoinPlan,
    LogicalQueryPlan LogicalPlan,
    SqlCompilationResult Sql);
