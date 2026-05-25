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
                Landscape = ResolveLayout(compiled.ExpectedColumns.Count).Landscape,
                Margins =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Cm(0.5),
                    Right = Telerik.Reporting.Drawing.Unit.Cm(0.5),
                    Top = Telerik.Reporting.Drawing.Unit.Cm(0.7),
                    Bottom = Telerik.Reporting.Drawing.Unit.Cm(0.7)
                }
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
            Height = Telerik.Reporting.Drawing.Unit.Cm(1.6)
        };

        detail.Items.Add(table);
        report.Items.Add(detail);

        return report;
    }
    private sealed record TableLayout(
    bool Landscape,
    double PrintableWidthCm,
    double HeaderFontPt,
    double BodyFontPt,
    double RowHeightCm);

    private static TableLayout ResolveLayout(int columnCount)
    {
        if (columnCount >= 10)
        {
            return new TableLayout(
                Landscape: true,
                PrintableWidthCm: 27.0,
                HeaderFontPt: 6,
                BodyFontPt: 6,
                RowHeightCm: 0.5);
        }

        if (columnCount >= 6)
        {
            return new TableLayout(
                Landscape: true,
                PrintableWidthCm: 27.0,
                HeaderFontPt: 7,
                BodyFontPt: 7,
                RowHeightCm: 0.55);
        }

        return new TableLayout(
            Landscape: false,
            PrintableWidthCm: 18.5,
            HeaderFontPt: 8,
            BodyFontPt: 8,
            RowHeightCm: 0.6);
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
        var layout = ResolveLayout(columnCount);

        var columnWidthCm = layout.PrintableWidthCm / columnCount;
        var columnWidth = Telerik.Reporting.Drawing.Unit.Cm(columnWidthCm);

        foreach (var column in compiled.ExpectedColumns)
        {
            table.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(columnWidth));

            var columnGroup = new Telerik.Reporting.TableGroup
            {
                Name = $"cg_{column.Name}"
            };

            columnGroup.ReportItem = new Telerik.Reporting.TextBox
            {
                Value = column.Name,
                Size = new Telerik.Reporting.Drawing.SizeU(
                    columnWidth,
                    Telerik.Reporting.Drawing.Unit.Cm(0.7)),
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
                },
                Padding =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Point(2),
                    Right = Telerik.Reporting.Drawing.Unit.Point(2)
                }
            }
            };

            table.ColumnGroups.Add(columnGroup);
        }

        table.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(
            Telerik.Reporting.Drawing.Unit.Cm(0.6)));

        var detailGroup = new Telerik.Reporting.TableGroup
        {
            Name = "detail"
        };

        detailGroup.Groupings.Add(new Telerik.Reporting.Grouping(null));
        table.RowGroups.Add(detailGroup);

        for (var i = 0; i < compiled.ExpectedColumns.Count; i++)
        {
            var column = compiled.ExpectedColumns[i];

            table.Body.SetCellContent(0, i, new Telerik.Reporting.TextBox
            {
                Value = $"=Fields.{column.Name}",
                Size = new Telerik.Reporting.Drawing.SizeU(
                    columnWidth,
                    Telerik.Reporting.Drawing.Unit.Cm(0.6)),
                Style =
            {
                Font =
                {
                    Size = Telerik.Reporting.Drawing.Unit.Point(7)
                },
                BorderStyle =
                {
                    Default = Telerik.Reporting.Drawing.BorderType.Solid
                },
                Padding =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Point(2),
                    Right = Telerik.Reporting.Drawing.Unit.Point(2)
                }
            }
            });
        }

        table.Size = new Telerik.Reporting.Drawing.SizeU(
            Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm),
            Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm * 2));

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
