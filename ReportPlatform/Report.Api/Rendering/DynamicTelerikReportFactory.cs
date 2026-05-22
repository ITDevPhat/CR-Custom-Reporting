using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikReportFactory : ITelerikReportFactory
{
    public Telerik.Reporting.Report CreateSqlBackedTableReport(CompiledReportQuery compiled, string connectionString, string? title)
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
        pageHeader.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = string.IsNullOrWhiteSpace(title) ? "Report Export" : title,
            Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(20), Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Style = { Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(11) } },
        });
        pageHeader.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            Location = new Telerik.Reporting.Drawing.PointU(Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Size = new Telerik.Reporting.Drawing.SizeU(Telerik.Reporting.Drawing.Unit.Cm(20), Telerik.Reporting.Drawing.Unit.Cm(0.5)),
            Style = { Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) }, Color = System.Drawing.Color.DimGray },
        });
        report.Items.Add(pageHeader);

        var sqlDataSource = new Telerik.Reporting.SqlDataSource
        {
            ConnectionString = connectionString,
            SelectCommand = compiled.Sql,
            Name = "ExportSqlDataSource",
        };

        foreach (var parameter in compiled.Parameters)
        {
            var parameterName = parameter.Key.StartsWith("@", StringComparison.Ordinal) ? parameter.Key[1..] : parameter.Key;
            sqlDataSource.Parameters.Add(new Telerik.Reporting.SqlDataSourceParameter(parameterName, InferDbType(parameter.Value), parameter.Value));
        }

        var detail = new Telerik.Reporting.DetailSection { Height = Telerik.Reporting.Drawing.Unit.Cm(20) };
        detail.Items.Add(BuildTable(sqlDataSource, compiled));
        report.Items.Add(detail);

        return report;
    }

    private static Telerik.Reporting.Table BuildTable(Telerik.Reporting.SqlDataSource dataSource, CompiledReportQuery compiled)
    {
        var table = new Telerik.Reporting.Table
        {
            DataSource = dataSource,
            Location = new Telerik.Reporting.Drawing.PointU(Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0)),
        };

        var columns = compiled.ExpectedColumns;
        var columnWidth = Telerik.Reporting.Drawing.Unit.Cm(Math.Max(2.4, 26.0 / Math.Max(columns.Count, 1)));

        foreach (var column in columns)
        {
            table.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(columnWidth));
            table.ColumnGroups.Add(new Telerik.Reporting.TableGroup());
        }

        table.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(Telerik.Reporting.Drawing.Unit.Cm(0.7)));
        table.RowGroups.Add(new Telerik.Reporting.TableGroup());

        for (var i = 0; i < columns.Count; i++)
        {
            table.ColumnGroups[i].ReportItem = new Telerik.Reporting.TextBox
            {
                Value = columns[i].Name,
                Style =
                {
                    BackgroundColor = System.Drawing.Color.Gainsboro,
                    Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(9) },
                    BorderStyle = { Default = Telerik.Reporting.Drawing.BorderType.Solid },
                },
            };

            table.Body.SetCellContent(0, i, new Telerik.Reporting.TextBox
            {
                Value = $"= Fields.{columns[i].Name}",
                Style =
                {
                    Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) },
                    BorderStyle = { Default = Telerik.Reporting.Drawing.BorderType.Solid },
                },
            });
        }

        table.Size = new Telerik.Reporting.Drawing.SizeU(
            Telerik.Reporting.Drawing.Unit.Cm(columnWidth.Value * Math.Max(columns.Count, 1)),
            Telerik.Reporting.Drawing.Unit.Cm(0.7));

        return table;
    }

    private static System.Data.DbType InferDbType(object? value)
    {
        return value switch
        {
            null => System.Data.DbType.Object,
            int => System.Data.DbType.Int32,
            long => System.Data.DbType.Int64,
            short => System.Data.DbType.Int16,
            decimal => System.Data.DbType.Decimal,
            float => System.Data.DbType.Single,
            double => System.Data.DbType.Double,
            bool => System.Data.DbType.Boolean,
            DateTime => System.Data.DbType.DateTime,
            DateTimeOffset => System.Data.DbType.DateTimeOffset,
            Guid => System.Data.DbType.Guid,
            _ => System.Data.DbType.String,
        };
    }
}
