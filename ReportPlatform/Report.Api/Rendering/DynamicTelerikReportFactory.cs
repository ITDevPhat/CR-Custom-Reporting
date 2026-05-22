using System.Data;
using Telerik.Reporting;
using Telerik.Reporting.Drawing;
using Report.Contracts.Results;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikReportFactory : ITelerikReportFactory
{
    public Report CreateTableReport(QueryResult result, string? title)
    {
        var data = QueryResultDataTableMapper.Map(result);
        var report = new Report
        {
            Name = "RuntimeExportReport",
            PageSettings =
            {
                Landscape = result.Columns.Count > 8,
            },
        };

        var pageHeader = new PageHeaderSection { Height = Unit.Cm(1.4) };
        var titleBox = new Telerik.Reporting.TextBox
        {
            Value = string.IsNullOrWhiteSpace(title) ? "Report Export" : title,
            Size = new SizeU(Unit.Cm(20), Unit.Cm(0.8)),
            Style = { Font = { Bold = true, Size = Unit.Point(11) } },
        };
        var stamp = new Telerik.Reporting.TextBox
        {
            Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            Location = new PointU(Unit.Cm(0), Unit.Cm(0.8)),
            Size = new SizeU(Unit.Cm(20), Unit.Cm(0.5)),
            Style = { Font = { Size = Unit.Point(8) }, Color = System.Drawing.Color.DimGray },
        };
        pageHeader.Items.Add(titleBox);
        pageHeader.Items.Add(stamp);
        report.Items.Add(pageHeader);

        var detail = new DetailSection { Height = Unit.Cm(15) };
        var table = BuildTable(data);
        detail.Items.Add(table);
        report.Items.Add(detail);

        return report;
    }

    private static Table BuildTable(DataTable data)
    {
        var table = new Table
        {
            DataSource = data,
            Location = new PointU(Unit.Cm(0), Unit.Cm(0)),
        };

        var columnWidth = Unit.Cm(Math.Max(2.4, 26.0 / Math.Max(data.Columns.Count, 1)));
        foreach (DataColumn col in data.Columns)
        {
            table.Body.Columns.Add(new TableBodyColumn(columnWidth));
            table.ColumnGroups.Add(new TableGroup());
        }

        table.Body.Rows.Add(new TableBodyRow(Unit.Cm(0.7)));
        table.RowGroups.Add(new TableGroup());

        for (var i = 0; i < data.Columns.Count; i++)
        {
            var col = data.Columns[i];

            var headerText = new Telerik.Reporting.TextBox
            {
                Value = col.ColumnName,
                Style =
                {
                    BackgroundColor = System.Drawing.Color.Gainsboro,
                    Font = { Bold = true, Size = Unit.Point(9) },
                    BorderStyle = { Default = BorderType.Solid },
                },
            };
            table.ColumnGroups[i].ReportItem = headerText;

            var fieldExpr = $"= Fields.[{col.ColumnName}]";
            var bodyText = new Telerik.Reporting.TextBox
            {
                Value = fieldExpr,
                Style =
                {
                    Font = { Size = Unit.Point(8) },
                    BorderStyle = { Default = BorderType.Solid },
                },
            };

            table.Body.SetCellContent(0, i, bodyText);
        }

        table.Size = new SizeU(Unit.Cm(columnWidth.Value * Math.Max(data.Columns.Count, 1)), Unit.Cm(0.7));
        return table;
    }
}
