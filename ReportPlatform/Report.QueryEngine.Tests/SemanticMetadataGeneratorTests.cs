using FluentAssertions;
using Report.Api.Services;
using Report.Contracts.Connections;

namespace Report.QueryEngine.Tests;

public sealed class SemanticMetadataGeneratorTests
{
    [Fact]
    public void DimDateNumericDateParts_ShouldBeDraggableDimensions()
    {
        var model = GenerateSuperstoreLikeModel();

        foreach (var column in new[] { "YearNumber", "MonthNumber", "QuarterNumber", "DayNumber" })
        {
            var field = FieldByPhysicalColumn(model, "DimDate", column);
            field.Role.Should().Be("dimension");
            field.IsDraggable.Should().BeTrue();
            field.IsHidden.Should().BeFalse();
            field.DefaultAggregation.Should().Be("none");
            field.SemanticType.Should().Be("number");
        }
    }

    [Fact]
    public void SqlDataType_ShouldBePreservedSeparatelyFromRole()
    {
        var model = GenerateSuperstoreLikeModel();

        var year = FieldByPhysicalColumn(model, "DimDate", "YearNumber");
        year.DataType.Should().Be("smallint");
        year.SqlDataType.Should().Be("smallint");
        year.Role.Should().Be("dimension");
        year.IsDraggable.Should().BeTrue();

        var month = FieldByPhysicalColumn(model, "DimDate", "MonthNumber");
        month.DataType.Should().Be("tinyint");
        month.SqlDataType.Should().Be("tinyint");
        month.Role.Should().Be("dimension");

        var sales = FieldByPhysicalColumn(model, "FactSales", "SalesAmount");
        sales.DataType.Should().Be("decimal");
        sales.SqlDataType.Should().Be("decimal");
        sales.Role.Should().Be("measure_candidate");

        var customerName = FieldByPhysicalColumn(model, "DimCustomer", "CustomerName");
        customerName.DataType.Should().Be("nvarchar");
        customerName.SqlDataType.Should().Be("nvarchar");
        customerName.Role.Should().Be("dimension");

        var customerKey = FieldByPhysicalColumn(model, "FactSales", "CustomerKey");
        customerKey.DataType.Should().Be("int");
        customerKey.SqlDataType.Should().Be("int");
        customerKey.Role.Should().Be("key");
    }

    [Fact]
    public void FactSalesAdditiveNumericColumns_ShouldBecomeMeasureCandidatesWithAllAggregateMetrics()
    {
        var model = GenerateSuperstoreLikeModel();

        foreach (var column in new[] { "SalesAmount", "ProfitAmount", "Quantity" })
        {
            var field = FieldByPhysicalColumn(model, "FactSales", column);
            field.Role.Should().Be("measure_candidate");
            field.DefaultAggregation.Should().Be("SUM");

            foreach (var aggregation in new[] { "SUM", "AVG", "MIN", "MAX", "COUNT", "COUNT_DISTINCT" })
            {
                var metric = model.Metrics.Should().ContainSingle(m =>
                    m.MetricId == $"metric.{aggregation.ToLowerInvariant()}_factsales_{Normalize(column)}").Subject;
                metric.Formula.Should().Be($"{aggregation}([{field.FieldId}])");
                metric.IsDraggable.Should().BeTrue();
                metric.IsHidden.Should().BeFalse();
            }
        }
    }

    [Fact]
    public void FactSalesDiscount_ShouldBecomeMeasureCandidateWithAverageAndCountMetrics()
    {
        var model = GenerateSuperstoreLikeModel();

        var field = FieldByPhysicalColumn(model, "FactSales", "Discount");
        field.Role.Should().Be("measure_candidate");
        field.DefaultAggregation.Should().Be("AVG");

        var avgMetric = model.Metrics.Should().ContainSingle(m => m.MetricId == "metric.avg_factsales_discount").Subject;
        avgMetric.DisplayName.Should().Be("Average Discount");
        avgMetric.Formula.Should().Be($"AVG([{field.FieldId}])");

        var distinctMetric = model.Metrics.Should().ContainSingle(m => m.MetricId == "metric.count_distinct_factsales_discount").Subject;
        distinctMetric.DisplayName.Should().Be("Distinct Discount Count");
    }

    [Fact]
    public void BusinessIdentifiers_ShouldRemainDraggableDimensions()
    {
        var model = GenerateSuperstoreLikeModel();

        foreach (var item in new[] { ("DimCustomer", "CustomerID"), ("DimProduct", "ProductID"), ("FactSales", "OrderID"), ("DimCustomer", "PostalCode") })
        {
            var field = FieldByPhysicalColumn(model, item.Item1, item.Item2);
            field.Role.Should().Be("dimension");
            field.SemanticType.Should().Be("identifier");
            field.DefaultAggregation.Should().Be("none");
            field.IsHidden.Should().BeFalse();
            field.IsDraggable.Should().BeTrue();
        }
    }

    [Fact]
    public void NonRelationshipKeyNamedColumn_ShouldNotBecomeTechnicalKey()
    {
        var model = GenerateSuperstoreLikeModel();

        var field = FieldByPhysicalColumn(model, "FactSales", "SomethingKey");

        field.Role.Should().Be("dimension");
        field.SemanticType.Should().Be("identifier");
        field.IsDraggable.Should().BeTrue();
        field.ClassificationReason.Should().Contain("Business identifier");
    }

    [Fact]
    public void KeyColumns_ShouldBeIdentifiersAndDraggableAsRawFields()
    {
        var model = GenerateSuperstoreLikeModel();

        foreach (var item in new[] { ("DimDate", "DateKey"), ("FactSales", "CustomerKey"), ("FactSales", "ProductKey") })
        {
            var field = FieldByPhysicalColumn(model, item.Item1, item.Item2);
            field.Role.Should().Be("key");
            field.SemanticType.Should().Be("identifier");
            field.DefaultAggregation.Should().Be("none");
            field.IsDraggable.Should().BeTrue();
        }
    }

    [Fact]
    public void ForeignKeyColumns_ShouldBeTechnicalKeysFromSqlServerMetadata()
    {
        var model = GenerateSuperstoreLikeModel();

        var customerKey = FieldByPhysicalColumn(model, "FactSales", "CustomerKey");

        customerKey.IsPrimaryKey.Should().BeFalse();
        customerKey.IsForeignKey.Should().BeTrue();
        customerKey.ParticipatesInRelationship.Should().BeTrue();
        customerKey.Role.Should().Be("key");
        customerKey.IsDraggable.Should().BeTrue();
        customerKey.ClassificationReason.Should().Be("SQL Server foreign key");
    }

    [Fact]
    public void SelectedTables_ShouldRegisterEveryDiscoveredColumn()
    {
        var discovered = CreateDiscoveredSchema();
        var selectedTables = SelectedTables();
        var model = new SemanticMetadataGenerator().Generate("SuperstoreDW", discovered, selectedTables);
        var validator = new MetadataConsistencyValidator();

        var result = validator.Validate(discovered.Tables, selectedTables, model);

        result.Warnings.Should().BeEmpty();
        result.Consistency.Should().OnlyContain(item => item.PhysicalColumnCount == item.RegisteredFieldCount);
        foreach (var table in discovered.Tables)
        {
            var tableId = table.Schema == "dbo" ? table.Table : $"{table.Schema}.{table.Table}";
            model.Fields.Count(field => field.TableId == tableId).Should().Be(table.Columns.Count);
        }
    }

    [Fact]
    public async Task MetadataEndpoint_ShouldNestFieldsAndPreserveOrdinalPosition()
    {
        var model = GenerateSuperstoreLikeModel();
        var store = new TestSemanticModelStore(model);
        var service = new Report.Api.Services.DatasetMetadataService(store);

        var metadata = await service.GetMetadataAsync("dataset_superstore", CancellationToken.None);
        var date = metadata.Tables.Should().ContainSingle(table => table.TableId == "DimDate").Subject;

        date.Fields.Should().HaveCount(8);
        date.Fields.Select(field => field.PhysicalColumn).Should().Equal(
            "DateKey",
            "FullDate",
            "DayNumber",
            "MonthNumber",
            "MonthName",
            "QuarterNumber",
            "YearNumber",
            "FiscalYear");
        date.Fields.Single(field => field.PhysicalColumn == "YearNumber").OrdinalPosition.Should().Be(7);
        date.Fields.Single(field => field.PhysicalColumn == "YearNumber").SqlDataType.Should().Be("smallint");
    }

    private static Report.Metadata.Models.SemanticModel GenerateSuperstoreLikeModel()
    {
        return new SemanticMetadataGenerator().Generate("SuperstoreDW", CreateDiscoveredSchema(), SelectedTables());
    }

    private static Report.Metadata.Models.SemanticField FieldByPhysicalColumn(
        Report.Metadata.Models.SemanticModel model,
        string tableId,
        string physicalColumn)
    {
        return model.Fields.Should().ContainSingle(field =>
            field.TableId == tableId &&
            field.PhysicalColumn == physicalColumn).Subject;
    }

    private static DiscoverSchemaResponse CreateDiscoveredSchema()
    {
        return new DiscoverSchemaResponse
        {
            Database = "SuperstoreDW",
            Tables =
            [
                new TableDto
                {
                    Schema = "dbo",
                    Table = "DimCustomer",
                    TableType = "BASE TABLE",
                    Columns =
                    [
                        Column("dbo", "DimCustomer", "CustomerKey", "int", 1, isPrimaryKey: true),
                        Column("dbo", "DimCustomer", "CustomerID", "nvarchar", 2),
                        Column("dbo", "DimCustomer", "CustomerName", "nvarchar", 3),
                        Column("dbo", "DimCustomer", "PostalCode", "nvarchar", 4)
                    ]
                },
                new TableDto
                {
                    Schema = "dbo",
                    Table = "DimProduct",
                    TableType = "BASE TABLE",
                    Columns =
                    [
                        Column("dbo", "DimProduct", "ProductKey", "int", 1, isPrimaryKey: true),
                        Column("dbo", "DimProduct", "ProductID", "nvarchar", 2),
                        Column("dbo", "DimProduct", "Category", "nvarchar", 3)
                    ]
                },
                new TableDto
                {
                    Schema = "dbo",
                    Table = "DimDate",
                    TableType = "BASE TABLE",
                    Columns =
                    [
                        Column("dbo", "DimDate", "DateKey", "int", 1, isPrimaryKey: true),
                        Column("dbo", "DimDate", "FullDate", "date", 2),
                        Column("dbo", "DimDate", "DayNumber", "tinyint", 3),
                        Column("dbo", "DimDate", "MonthNumber", "tinyint", 4),
                        Column("dbo", "DimDate", "MonthName", "nvarchar", 5),
                        Column("dbo", "DimDate", "QuarterNumber", "tinyint", 6),
                        Column("dbo", "DimDate", "YearNumber", "smallint", 7),
                        Column("dbo", "DimDate", "FiscalYear", "smallint", 8)
                    ]
                },
                new TableDto
                {
                    Schema = "dbo",
                    Table = "FactSales",
                    TableType = "BASE TABLE",
                    Columns =
                    [
                        Column("dbo", "FactSales", "SalesKey", "int", 1, isPrimaryKey: true),
                        Column("dbo", "FactSales", "OrderID", "nvarchar", 2),
                        Column("dbo", "FactSales", "OrderDateKey", "int", 3, isForeignKey: true),
                        Column("dbo", "FactSales", "CustomerKey", "int", 4, isForeignKey: true),
                        Column("dbo", "FactSales", "ProductKey", "int", 5, isForeignKey: true),
                        Column("dbo", "FactSales", "SalesAmount", "decimal", 6),
                        Column("dbo", "FactSales", "ProfitAmount", "decimal", 7),
                        Column("dbo", "FactSales", "Quantity", "int", 8),
                        Column("dbo", "FactSales", "Discount", "decimal", 9),
                        Column("dbo", "FactSales", "SomethingKey", "int", 10)
                    ]
                }
            ]
        };
    }

    private static List<SelectedTableDto> SelectedTables()
    {
        return
        [
            new SelectedTableDto { Schema = "dbo", Table = "DimDate" },
            new SelectedTableDto { Schema = "dbo", Table = "DimCustomer" },
            new SelectedTableDto { Schema = "dbo", Table = "DimProduct" },
            new SelectedTableDto { Schema = "dbo", Table = "FactSales" }
        ];
    }

    private static ColumnDto Column(
        string schema,
        string table,
        string column,
        string dataType,
        int ordinal,
        bool isPrimaryKey = false,
        bool isForeignKey = false)
    {
        return new ColumnDto
        {
            Schema = schema,
            Table = table,
            Column = column,
            DataType = dataType,
            SqlDataType = dataType,
            CharacterMaximumLength = dataType == "nvarchar" ? 255 : null,
            NumericPrecision = dataType is "decimal" ? (byte)18 : null,
            NumericScale = dataType is "decimal" ? 2 : null,
            DatetimePrecision = dataType.Contains("date", StringComparison.OrdinalIgnoreCase) ? (short)7 : null,
            OrdinalPosition = ordinal,
            IsNullable = false,
            IsPrimaryKey = isPrimaryKey,
            IsForeignKey = isForeignKey,
            ParticipatesInRelationship = isPrimaryKey || isForeignKey
        };
    }

    private static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        return new string(chars).Trim('_');
    }

    private sealed class TestSemanticModelStore : Report.Metadata.Stores.ISemanticModelStore
    {
        private readonly Report.Metadata.Models.SemanticModel _model;

        public TestSemanticModelStore(Report.Metadata.Models.SemanticModel model)
        {
            _model = new Report.Metadata.Models.SemanticModel
            {
                DatasetId = "dataset_superstore",
                DisplayName = model.DisplayName,
                ConnectionId = "conn_test",
                Tables = model.Tables,
                Fields = model.Fields.Select(field => new Report.Metadata.Models.SemanticField
                {
                    FieldId = field.FieldId,
                    DatasetId = "dataset_superstore",
                    TableId = field.TableId,
                    PhysicalSchema = field.PhysicalSchema,
                    PhysicalTable = field.PhysicalTable,
                    PhysicalColumn = field.PhysicalColumn,
                    OrdinalPosition = field.OrdinalPosition,
                    IsNullable = field.IsNullable,
                    IsPrimaryKey = field.IsPrimaryKey,
                    IsForeignKey = field.IsForeignKey,
                    ParticipatesInRelationship = field.ParticipatesInRelationship,
                    IsUnique = field.IsUnique,
                    ReferencedSchema = field.ReferencedSchema,
                    ReferencedTable = field.ReferencedTable,
                    ReferencedColumn = field.ReferencedColumn,
                    ForeignKeyName = field.ForeignKeyName,
                    DisplayName = field.DisplayName,
                    DataType = field.DataType,
                    SqlDataType = field.SqlDataType,
                    CharacterMaximumLength = field.CharacterMaximumLength,
                    NumericPrecision = field.NumericPrecision,
                    NumericScale = field.NumericScale,
                    DatetimePrecision = field.DatetimePrecision,
                    Role = field.Role,
                    Grain = field.Grain,
                    SemanticType = field.SemanticType,
                    DefaultAggregation = field.DefaultAggregation,
                    Format = field.Format,
                    IsHidden = field.IsHidden,
                    IsDraggable = field.IsDraggable,
                    ClassificationReason = field.ClassificationReason
                }).ToList(),
                Metrics = model.Metrics,
                Relationships = model.Relationships
            };
        }

        public Task<Report.Metadata.Models.SemanticModel> LoadAsync(string datasetId, CancellationToken ct)
        {
            return Task.FromResult(_model);
        }
    }
}
