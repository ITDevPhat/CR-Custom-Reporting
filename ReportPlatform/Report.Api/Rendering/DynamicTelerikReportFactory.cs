using System.Data;
using Telerik.Reporting;
using Telerik.Reporting.Drawing;
using Report.Contracts.Results;
using System.Text.RegularExpressions;
using TelerikReport = Telerik.Reporting.Report;
using TelerikTable = Telerik.Reporting.Table;
using SystemDataColumn = System.Data.DataColumn;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikReportFactory : ITelerikReportFactory
{
    public TelerikReport CreateTableReport(QueryResult result, string? title)
    {
        var data = QueryResultDataTableMapper.Map(result);
        var (safeData, aliases) = BuildTelerikSafeData(data);
        var report = new TelerikReport
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
        var table = BuildTable(safeData, aliases);
        detail.Items.Add(table);
        report.Items.Add(detail);

        return report;
    }

    private static TelerikTable BuildTable(DataTable data, IReadOnlyDictionary<string, string> aliases)
    {
        if (data.Rows.Count == 0)
        {
            return BuildNoDataTable(data.Columns.Count);
        }

        var table = new TelerikTable
        {
            DataSource = data,
            Location = new PointU(Unit.Cm(0), Unit.Cm(0)),
        };

        var columnWidth = Unit.Cm(Math.Max(2.4, 26.0 / Math.Max(data.Columns.Count, 1)));
        foreach (SystemDataColumn col in data.Columns)
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
                Value = aliases[col.ColumnName],
                Style =
                {
                    BackgroundColor = System.Drawing.Color.Gainsboro,
                    Font = { Bold = true, Size = Unit.Point(9) },
                    BorderStyle = { Default = BorderType.Solid },
                },
            };
            table.ColumnGroups[i].ReportItem = headerText;

            var fieldExpr = $"= Fields.{col.ColumnName}";
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

    private static TelerikTable BuildNoDataTable(int columnCount)
    {
        var table = new TelerikTable
        {
            Location = new PointU(Unit.Cm(0), Unit.Cm(0)),
        };

        table.Body.Columns.Add(new TableBodyColumn(Unit.Cm(Math.Max(4.0, columnCount * 2.0))));
        table.ColumnGroups.Add(new TableGroup());
        table.Body.Rows.Add(new TableBodyRow(Unit.Cm(0.7)));
        table.RowGroups.Add(new TableGroup());

        table.ColumnGroups[0].ReportItem = new Telerik.Reporting.TextBox
        {
            Value = "Status",
            Style = { BackgroundColor = System.Drawing.Color.Gainsboro, Font = { Bold = true } }
        };

        table.Body.SetCellContent(0, 0, new Telerik.Reporting.TextBox { Value = "No data available." });
        table.Size = new SizeU(Unit.Cm(Math.Max(4.0, columnCount * 2.0)), Unit.Cm(0.7));
        return table;
    }

    private static (DataTable SafeData, IReadOnlyDictionary<string, string> Aliases) BuildTelerikSafeData(DataTable source)
    {
        var safe = new DataTable(source.TableName);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SystemDataColumn column in source.Columns)
        {
            var safeName = ToUniqueSafeName(column.ColumnName, used);
            aliases[safeName] = column.ColumnName;
            safe.Columns.Add(new SystemDataColumn(safeName, column.DataType) { AllowDBNull = true });
        }

        foreach (DataRow row in source.Rows)
        {
            var newRow = safe.NewRow();
            for (var i = 0; i < source.Columns.Count; i++)
            {
                newRow[i] = row[i];
            }
            safe.Rows.Add(newRow);
        }

        return (safe, aliases);
    }

    private static string ToUniqueSafeName(string original, ISet<string> used)
    {
        var normalized = Regex.Replace(original, "[^A-Za-z0-9_]", "_");
        if (string.IsNullOrWhiteSpace(normalized) || char.IsDigit(normalized[0]))
        {
            normalized = $"Col_{normalized}";
        }

        var candidate = normalized;
        var suffix = 1;
        while (!used.Add(candidate))
        {
            candidate = $"{normalized}_{suffix++}";
        }

        return candidate;
    }
}
