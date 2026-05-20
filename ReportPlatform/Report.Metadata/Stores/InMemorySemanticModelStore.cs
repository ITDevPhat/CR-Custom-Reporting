using Report.Metadata.Models;

namespace Report.Metadata.Stores;

public sealed class InMemorySemanticModelStore : ISemanticModelStore
{
    private readonly IDatasetRegistry _datasetRegistry;

    public InMemorySemanticModelStore(IDatasetRegistry datasetRegistry)
    {
        _datasetRegistry = datasetRegistry;
    }

    public Task<SemanticModel> LoadAsync(string datasetId, CancellationToken ct)
    {
        var registeredDataset = _datasetRegistry.Find(datasetId);
        if (registeredDataset is not null)
        {
            return Task.FromResult(registeredDataset.Model);
        }

        var model = new SemanticModel
        {
            DatasetId = datasetId,
            DisplayName = "Sales Dataset",
            ConnectionId = "conn_001",
            Tables =
            [
                new() { TableId = "DimCustomer", DisplayName = "Customer", TableType = "dimension", Grain = "customer", PhysicalSchema = "dbo", PhysicalTable = "DimCustomer" },
                new() { TableId = "DimDate", DisplayName = "Date", TableType = "dimension", Grain = "date", PhysicalSchema = "dbo", PhysicalTable = "DimDate" },
                new() { TableId = "FactSales", DisplayName = "Sales", TableType = "fact", Grain = "sales_transaction", PhysicalSchema = "dbo", PhysicalTable = "FactSales" }
            ],
            Fields =
            [
                new() { FieldId = "customer.name", DatasetId = datasetId, TableId = "DimCustomer", PhysicalTable = "DimCustomer", PhysicalColumn = "CustomerName", DisplayName = "Customer Name", DataType = "nvarchar", Role = "dimension", Grain = "customer" },
                new() { FieldId = "date.year", DatasetId = datasetId, TableId = "DimDate", PhysicalTable = "DimDate", PhysicalColumn = "YearNumber", DisplayName = "Year", DataType = "smallint", Role = "dimension", Grain = "date" },
                new() { FieldId = "sales.amount", DatasetId = datasetId, TableId = "FactSales", PhysicalTable = "FactSales", PhysicalColumn = "SalesAmount", DisplayName = "Sales Amount", DataType = "decimal", Role = "measure_candidate", Grain = "sales_transaction", SemanticType = "currency", DefaultAggregation = "SUM", Format = "currency", IsDraggable = true }
            ],
            Metrics =
            [
                new() { MetricId = "total_sales", DatasetId = datasetId, DisplayName = "Total Sales", Formula = "SUM([sales.amount])", BaseTableId = "FactSales", AggregationBehavior = "additive", DataType = "decimal", Format = "currency", IsDraggable = true }
            ],
            Relationships =
            [
                new() { RelationshipId = "rel_sales_customer", DatasetId = datasetId, FromTableId = "FactSales", FromColumn = "CustomerKey", ToTableId = "DimCustomer", ToColumn = "CustomerKey", JoinType = "INNER", Cardinality = "N:1", CrossFilterDirection = "single", IsActive = true, IsPrimary = true, Source = "inferred", Confidence = 0.95m, Status = "active", Warning = "Inferred relationship. Please verify before production use." },
                new() { RelationshipId = "rel_sales_date", DatasetId = datasetId, FromTableId = "FactSales", FromColumn = "OrderDateKey", ToTableId = "DimDate", ToColumn = "DateKey", JoinType = "INNER", Cardinality = "N:1", CrossFilterDirection = "single", IsActive = true, IsPrimary = true, Source = "inferred", Confidence = 0.85m, Status = "active", Warning = "Inferred relationship. Please verify before production use." }
            ]
        };

        _datasetRegistry.SaveExisting(datasetId, model.DisplayName, model.ConnectionId, model);
        return Task.FromResult(model);
    }
}
