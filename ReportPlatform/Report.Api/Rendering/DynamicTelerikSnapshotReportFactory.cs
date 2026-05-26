using System.Data;
using Report.Contracts.Artifacts;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikSnapshotReportFactory : ITelerikSnapshotReportFactory
{
    public Telerik.Reporting.Report CreateSnapshotBackedTableReport(
        DataTable dataTable,
        string? title,
        ReportExecutionRecord execution,
        ReportExecutionArtifactHeader? header = null)
    {
        var report = new Telerik.Reporting.Report();
        var columnCount = Math.Max(dataTable.Columns.Count, 1);
        var layout = ResolveLayout(columnCount);
        report.PageSettings.Landscape = layout.Landscape;

        var pageHeader = new Telerik.Reporting.PageHeaderSection { Height = Telerik.Reporting.Drawing.Unit.Cm(1.8) };
        pageHeader.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = string.IsNullOrWhiteSpace(title) ? (execution.ReportName ?? execution.ReportId) : title,
            Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(20), Telerik.Reporting.Drawing.Unit.Cm(0.7)),
            Style = { Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(10) } }
        });
        pageHeader.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = $"Execution: {execution.ExecutionId} | Rows: {dataTable.Rows.Count} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Artifact: {header?.ArtifactVersion ?? "n/a"}",
            Location = new Telerik.Reporting.Drawing.PointU(Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(27), Telerik.Reporting.Drawing.Unit.Cm(0.7)),
            Style = { Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) } }
        });
        report.Items.Add(pageHeader);

        var detail = new Telerik.Reporting.DetailSection { Height = Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm + 0.2) };
        detail.Items.Add(BuildTable(dataTable, layout));
        report.Items.Add(detail);
        return report;
    }

    private static Telerik.Reporting.Table BuildTable(DataTable dataTable, TableLayout layout)
    {
        var table = new Telerik.Reporting.Table
        {
            DataSource = dataTable,
            Location = new Telerik.Reporting.Drawing.PointU(Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0))
        };

        var columns = dataTable.Columns.Cast<DataColumn>().ToList();
        if (columns.Count == 0) columns.Add(new DataColumn("NoData"));
        var width = Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm / columns.Count);

        for (var i = 0; i < columns.Count; i++)
        {
            table.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(width));
            table.ColumnGroups.Add(new Telerik.Reporting.TableGroup
            {
                Name = $"cg{i}",
                ReportItem = new Telerik.Reporting.TextBox
                {
                    Value = columns[i].ColumnName,
                    Size = new Telerik.Reporting.Drawing.SizeU(width, Telerik.Reporting.Drawing.Unit.Cm(0.7)),
                    Style = { Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(layout.HeaderFontPt) } }
                }
            });
        }

        table.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm)));
        table.RowGroups.Add(new Telerik.Reporting.TableGroup { Groupings = { new Telerik.Reporting.Grouping(null) } });

        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            table.Body.SetCellContent(0, i, new Telerik.Reporting.TextBox
            {
                Value = $"= Fields.[{col.ColumnName.Replace("]", "]]")}]",
                Size = new Telerik.Reporting.Drawing.SizeU(width, Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm)),
                Style = { Font = { Size = Telerik.Reporting.Drawing.Unit.Point(layout.BodyFontPt) } }
            });
        }
        return table;
    }

    private sealed record TableLayout(bool Landscape, double PrintableWidthCm, double HeaderFontPt, double BodyFontPt, double RowHeightCm);
    private static TableLayout ResolveLayout(int columnCount)
        => columnCount >= 10 ? new(true, 27.0, 6, 6, 0.5)
        : columnCount >= 6 ? new(true, 27.0, 7, 7, 0.55)
        : new(false, 18.5, 8, 8, 0.6);
}
