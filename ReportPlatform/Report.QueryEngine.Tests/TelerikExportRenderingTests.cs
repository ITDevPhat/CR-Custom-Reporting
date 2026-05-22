using System.Data;
using FluentAssertions;
using Report.Api.Rendering;
using Report.Contracts.Results;
using Telerik.Reporting;

namespace Report.QueryEngine.Tests;

public class TelerikExportRenderingTests
{
    [Fact]
    public void QueryResultDataTableMapper_PreservesAliasOrder_AndValues()
    {
        var result = new QueryResult
        {
            Columns =
            [
                new QueryColumn { Name = "EnglishProductName", Type = "nvarchar" },
                new QueryColumn { Name = "TotalSalesAmount", Type = "decimal" },
                new QueryColumn { Name = "SaleDate", Type = "datetime2" }
            ],
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["EnglishProductName"] = "Road-150 Red, 62",
                    ["TotalSalesAmount"] = 1200.45d,
                    ["SaleDate"] = new DateTimeOffset(new DateTime(2024, 1, 20), TimeSpan.Zero)
                }
            ]
        };

        var table = QueryResultDataTableMapper.Map(result);

        table.Columns.Cast<DataColumn>().Select(c => c.ColumnName)
            .Should().Equal("EnglishProductName", "TotalSalesAmount", "SaleDate");
        table.Rows.Count.Should().Be(1);
        table.Rows[0]["TotalSalesAmount"].Should().BeOfType<decimal>();
        table.Rows[0]["SaleDate"].Should().BeOfType<DateTime>();
    }

    [Fact]
    public void DynamicFactory_UsesFieldDotNotation_AndBindsRows()
    {
        var result = new QueryResult
        {
            Columns =
            [
                new QueryColumn { Name = "English Product Name", Type = "nvarchar" },
                new QueryColumn { Name = "Total-Sales", Type = "decimal" }
            ],
            Rows =
            [
                new Dictionary<string, object?>
                {
                    ["English Product Name"] = "HL Mountain Tire",
                    ["Total-Sales"] = 2500m
                }
            ]
        };

        var factory = new DynamicTelerikReportFactory();
        var report = factory.CreateTableReport(result, "Sales Export");

        var detail = report.Items.OfType<DetailSection>().Single();
        var telerikTable = detail.Items.OfType<Table>().Single();
        var bodyCell = (TextBox)telerikTable.Body.GetCellContent(0, 0);

        bodyCell.Value.Should().Be("= Fields.English_Product_Name");
        telerikTable.DataSource.Should().BeAssignableTo<DataTable>();
        ((DataTable)telerikTable.DataSource!).Rows.Count.Should().Be(1);
    }

    [Fact]
    public void DynamicFactory_RendersNoDataMessage_WhenDatasetIsEmpty()
    {
        var result = new QueryResult
        {
            Columns = [new QueryColumn { Name = "EnglishProductName", Type = "nvarchar" }],
            Rows = []
        };

        var factory = new DynamicTelerikReportFactory();
        var report = factory.CreateTableReport(result, "Sales Export");

        var detail = report.Items.OfType<DetailSection>().Single();
        var telerikTable = detail.Items.OfType<Table>().Single();
        var bodyCell = (TextBox)telerikTable.Body.GetCellContent(0, 0);
        bodyCell.Value.Should().Be("No data available.");
    }
}
