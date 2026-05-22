using FluentAssertions;
using Report.Api.Rendering;
using Report.Contracts.Exports;
using Report.Contracts.Results;

namespace Report.QueryEngine.Tests;

public class TelerikExportRenderingTests
{
    [Fact]
    public void DynamicFactory_BuildsSqlDataSourceBackedTable()
    {
        var compiled = new CompiledReportQuery
        {
            ConnectionId = "conn_002",
            Sql = "SELECT f.[CustomerKey] AS [CustomerKey] FROM [dbo].[FactInternetSales] f WHERE f.[CustomerKey] >= @p0;",
            Parameters = new Dictionary<string, object?>
            {
                ["p0"] = 100
            },
            ExpectedColumns =
            [
                new QueryColumn { Name = "CustomerKey", Type = "int" }
            ]
        };

        var factory = new DynamicTelerikReportFactory();
        var report = factory.CreateSqlBackedTableReport(compiled, "Server=.;Database=Db;User Id=u;Password=p;", "Sales Export");

        var detail = report.Items.OfType<Telerik.Reporting.DetailSection>().Single();
        var table = detail.Items.OfType<Telerik.Reporting.Table>().Single();
        table.DataSource.Should().BeAssignableTo<Telerik.Reporting.SqlDataSource>();

        var dataSource = (Telerik.Reporting.SqlDataSource)table.DataSource!;
        dataSource.SelectCommand.Should().Be(compiled.Sql);
        dataSource.Parameters.Count.Should().Be(1);
        dataSource.Parameters[0].Name.Should().Be("p0");

        var bodyCell = (Telerik.Reporting.TextBox)table.Body.GetCellContent(0, 0);
        bodyCell.Value.Should().Be("= Fields.CustomerKey");
    }
}
