using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikReportFactory : ITelerikReportFactory
{
    public Telerik.Reporting.Report CreateSqlBackedTableReport(
        CompiledReportQuery compiled,
        string connectionString,
        string? title)
    {
        var report = new Telerik.Reporting.Report
        {
            Name = "RuntimeSqlExportReport",
            PageSettings =
            {
                Landscape = compiled.ExpectedColumns.Count > 8
            }
        };

        var pageHeader = new Telerik.Reporting.PageHeaderSection
        {
            Height = Telerik.Reporting.Drawing.Unit.Cm(1.4)
        };

        pageHeader.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = string.IsNullOrWhiteSpace(title) ? "Report Export" : title,
            Size = new Telerik.Reporting.Drawing.SizeU(
                Telerik.Reporting.Drawing.Unit.Cm(20),
                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Style = { Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(11) } }
        });

        pageHeader.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            Location = new Telerik.Reporting.Drawing.PointU(
                Telerik.Reporting.Drawing.Unit.Cm(0),
                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Size = new Telerik.Reporting.Drawing.SizeU(
                Telerik.Reporting.Drawing.Unit.Cm(20),
                Telerik.Reporting.Drawing.Unit.Cm(0.5)),
            Style =
            {
                Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) },
                Color = System.Drawing.Color.DimGray
            }
        });

        report.Items.Add(pageHeader);

        var sqlDataSource = BuildSqlDataSource(compiled, connectionString);
        var table = BuildTable(compiled, sqlDataSource);

        var detail = new Telerik.Reporting.DetailSection
        {
            Height = Telerik.Reporting.Drawing.Unit.Cm(1)
        };

        detail.Items.Add(table);
        report.Items.Add(detail);

        return report;
    }

    private static Telerik.Reporting.SqlDataSource BuildSqlDataSource(
        CompiledReportQuery compiled,
        string connectionString)
    {
        var dataSource = new Telerik.Reporting.SqlDataSource
        {
            ConnectionString = connectionString,
            SelectCommand = compiled.Sql
        };

        foreach (var parameter in compiled.Parameters)
        {
            dataSource.Parameters.Add(new Telerik.Reporting.SqlDataSourceParameter(
                NormalizeParameterName(parameter.Key),
                ResolveDbType(parameter.Value),
                parameter.Value ?? DBNull.Value));
        }

        return dataSource;
    }

    private static Telerik.Reporting.Table BuildTable(
        CompiledReportQuery compiled,
        Telerik.Reporting.SqlDataSource dataSource)
    {
        var table = new Telerik.Reporting.Table
        {
            DataSource = dataSource,
            Location = new Telerik.Reporting.Drawing.PointU(
                Telerik.Reporting.Drawing.Unit.Cm(0),
                Telerik.Reporting.Drawing.Unit.Cm(0))
        };

        var columnCount = Math.Max(compiled.ExpectedColumns.Count, 1);
        var columnWidth = Telerik.Reporting.Drawing.Unit.Cm(Math.Max(2.4, 26.0 / columnCount));

        foreach (var _ in compiled.ExpectedColumns)
        {
            table.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(columnWidth));
            table.ColumnGroups.Add(new Telerik.Reporting.TableGroup());
        }

        table.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(
            Telerik.Reporting.Drawing.Unit.Cm(0.7)));

        table.RowGroups.Add(new Telerik.Reporting.TableGroup());

        for (var i = 0; i < compiled.ExpectedColumns.Count; i++)
        {
            var column = compiled.ExpectedColumns[i];

            table.ColumnGroups[i].ReportItem = new Telerik.Reporting.TextBox
            {
                Value = column.Name,
                Style =
                {
                    BackgroundColor = System.Drawing.Color.Gainsboro,
                    Font =
                    {
                        Bold = true,
                        Size = Telerik.Reporting.Drawing.Unit.Point(8)
                    },
                    BorderStyle =
                    {
                        Default = Telerik.Reporting.Drawing.BorderType.Solid
                    }
                }
            };

            table.Body.SetCellContent(0, i, new Telerik.Reporting.TextBox
            {
                Value = $"= Fields.{column.Name}",
                Style =
                {
                    Font =
                    {
                        Size = Telerik.Reporting.Drawing.Unit.Point(8)
                    },
                    BorderStyle =
                    {
                        Default = Telerik.Reporting.Drawing.BorderType.Solid
                    }
                }
            });
        }

        table.Size = new Telerik.Reporting.Drawing.SizeU(
            Telerik.Reporting.Drawing.Unit.Cm(columnWidth.Value * columnCount),
            Telerik.Reporting.Drawing.Unit.Cm(0.7));

        return table;
    }

    private static string NormalizeParameterName(string name)
    {
        return name.StartsWith("@", StringComparison.Ordinal)
            ? name
            : $"@{name}";
    }

    private static System.Data.DbType ResolveDbType(object? value)
    {
        return value switch
        {
            byte => System.Data.DbType.Byte,
            short => System.Data.DbType.Int16,
            int => System.Data.DbType.Int32,
            long => System.Data.DbType.Int64,
            float => System.Data.DbType.Single,
            double => System.Data.DbType.Double,
            decimal => System.Data.DbType.Decimal,
            bool => System.Data.DbType.Boolean,
            DateTime => System.Data.DbType.DateTime,
            DateTimeOffset => System.Data.DbType.DateTimeOffset,
            Guid => System.Data.DbType.Guid,
            _ => System.Data.DbType.String
        };
    }
}
