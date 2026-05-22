using Report.Contracts.Results;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikReportFactory : ITelerikReportFactory
{
    private const int MaxRenderableRows = 200;

    public Telerik.Reporting.Report CreateTableReport(QueryResult result, string? title)
    {
        var report = new Telerik.Reporting.Report
        {
            Name = "RuntimeSqlExportReport",
            PageSettings =
            {
                Landscape = compiled.ExpectedColumns.Count > 8,
            },
        };

        var pageHeader = new Telerik.Reporting.PageHeaderSection { Height = Telerik.Reporting.Drawing.Unit.Cm(1.4) };
        var titleBox = new Telerik.Reporting.TextBox
        {
            Value = string.IsNullOrWhiteSpace(title) ? "Report Export" : title,
            Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(20), Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Style = { Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(11) } },
        };
        var stamp = new Telerik.Reporting.TextBox
        {
            Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            Location = new Telerik.Reporting.Drawing.PointU(Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(20), Telerik.Reporting.Drawing.Unit.Cm(0.5)),
            Style = { Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) }, Color = System.Drawing.Color.DimGray },
        };
        pageHeader.Items.Add(titleBox);
        pageHeader.Items.Add(stamp);
        report.Items.Add(pageHeader);

        var detail = new Telerik.Reporting.DetailSection { Height = Telerik.Reporting.Drawing.Unit.Cm(24) };
        var panel = BuildGridPanel(result);
        detail.Items.Add(panel);
        report.Items.Add(detail);

        return report;
    }

    private static Telerik.Reporting.Panel BuildGridPanel(QueryResult result)
    {
        var panel = new Telerik.Reporting.Panel
        {
            Location = new Telerik.Reporting.Drawing.PointU(Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0)),
        };
        var columnCount = Math.Max(result.Columns.Count, 1);
        var renderRowCount = Math.Min(result.Rows.Count, MaxRenderableRows);
        var columnWidth = Telerik.Reporting.Drawing.Unit.Cm(Math.Max(2.3, 26.0 / columnCount));
        var rowHeight = Telerik.Reporting.Drawing.Unit.Cm(0.6);
        var headerHeight = Telerik.Reporting.Drawing.Unit.Cm(0.7);

        if (result.Columns.Count == 0 || result.Rows.Count == 0)
        {
            panel.Items.Add(new Telerik.Reporting.TextBox
            {
                Value = "No data available.",
                Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(8), rowHeight),
                Style = { Font = { Size = Telerik.Reporting.Drawing.Unit.Point(9) } },
            });
            panel.Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(8), rowHeight);
            return panel;
        }

        for (var colIndex = 0; colIndex < result.Columns.Count; colIndex++)
        {
            var x = Telerik.Reporting.Drawing.Unit.Cm(columnWidth.Value * colIndex);
            panel.Items.Add(new Telerik.Reporting.TextBox
            {
                Value = result.Columns[colIndex].Name,
                Location = new Telerik.Reporting.Drawing.PointU(x, Telerik.Reporting.Drawing.Unit.Cm(0)),
                Size = new Telerik.Reporting.Drawing.SizeU(columnWidth, headerHeight),
                Style =
                {
                    BackgroundColor = System.Drawing.Color.Gainsboro,
                    Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(8) },
                    BorderStyle = { Default = Telerik.Reporting.Drawing.BorderType.Solid },
                },
            });
        }

        for (var rowIndex = 0; rowIndex < renderRowCount; rowIndex++)
        {
            var y = Telerik.Reporting.Drawing.Unit.Cm(headerHeight.Value + (rowIndex * rowHeight.Value));
            var row = result.Rows[rowIndex];
            for (var colIndex = 0; colIndex < result.Columns.Count; colIndex++)
            {
                var columnName = result.Columns[colIndex].Name;
                var value = row.TryGetValue(columnName, out var raw) ? SanitizeValue(raw) : string.Empty;
                var x = Telerik.Reporting.Drawing.Unit.Cm(columnWidth.Value * colIndex);
                panel.Items.Add(new Telerik.Reporting.TextBox
                {
                    Value = value,
                    Location = new Telerik.Reporting.Drawing.PointU(x, y),
                    Size = new Telerik.Reporting.Drawing.SizeU(columnWidth, rowHeight),
                    Style =
                    {
                        Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) },
                        BorderStyle = { Default = Telerik.Reporting.Drawing.BorderType.Solid },
                    },
                });
            }
        }

        panel.Size = new Telerik.Reporting.Drawing.SizeU(
            Telerik.Reporting.Drawing.Unit.Cm(columnWidth.Value * columnCount),
            Telerik.Reporting.Drawing.Unit.Cm(headerHeight.Value + (renderRowCount * rowHeight.Value)));
        return panel;
    }

    private static string SanitizeValue(object? value)
    {
        return value?.ToString()?.Replace("\r", " ").Replace("\n", " ") ?? string.Empty;
    }
}
