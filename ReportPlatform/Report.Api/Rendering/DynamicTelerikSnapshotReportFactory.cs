using System.Data;
using Microsoft.Extensions.Logging;
using Report.Contracts.Artifacts;
using Telerik.Reporting;
using Telerik.Reporting.Drawing;

namespace Report.Api.Rendering;

public sealed class DynamicTelerikSnapshotReportFactory : ITelerikSnapshotReportFactory
{
    private readonly ILogger<DynamicTelerikSnapshotReportFactory> _logger;

    public DynamicTelerikSnapshotReportFactory(
        ILogger<DynamicTelerikSnapshotReportFactory> logger)
    {
        _logger = logger;
    }

    public Telerik.Reporting.Report CreateSnapshotBackedTableReport(
        DataTable dataTable,
        string? title,
        ReportExecutionRecord execution,
        ReportExecutionArtifactHeader? header = null,
        SnapshotReportBuildOptions? options = null)
    {
        var sourceRowCount = dataTable.Rows.Count;
        var reportData = ApplyRowLimit(dataTable, options?.MaxRows);
        var normalizedTable = NormalizeTable(reportData);

        var columnCount = Math.Max(normalizedTable.Columns.Count, 1);
        var layout = ResolveLayout(columnCount);

        _logger.LogInformation(
            "Building snapshot report. ExecutionId={ExecutionId} Mode={Mode} Rows={Rows} SourceRows={SourceRows} Columns={Columns}",
            execution.ExecutionId,
            options?.Mode ?? "Preview",
            normalizedTable.Rows.Count,
            sourceRowCount,
            normalizedTable.Columns.Count);

        var report = new Telerik.Reporting.Report
        {
            Name = "RuntimeSnapshotPreviewReport",
            Width = Unit.Cm(layout.PrintableWidthCm),
            PageSettings =
            {
                Landscape = layout.Landscape,
                
                Margins =
                {
                    Left = Unit.Cm(0.4),
                    Right = Unit.Cm(0.4),
                    Top = Unit.Cm(0.5),
                    Bottom = Unit.Cm(0.5)
                }
            }
        };

        report.Items.Add(BuildPageHeader(
            normalizedTable,
            title,
            execution,
            header,
            layout,
            sourceRowCount,
            options));

        report.Items.Add(BuildDetailSection(
            normalizedTable,
            layout));

        report.Items.Add(BuildPageFooter());

        return report;
    }

    private static GroupHeaderSection BuildColumnHeaderSection(
        DataTable table,
        TableLayout layout)
    {
        var section = new GroupHeaderSection
        {
            Height = Unit.Cm(layout.HeaderHeightCm),
            PrintOnEveryPage = true
        };

        var columnWidthCm = layout.PrintableWidthCm / table.Columns.Count;
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            section.Items.Add(new TextBox
            {
                Value = column.ColumnName,
                Location = new PointU(Unit.Cm(i * columnWidthCm), Unit.Cm(0)),
                Size = new SizeU(Unit.Cm(columnWidthCm), Unit.Cm(layout.HeaderHeightCm)),
                CanGrow = false,
                Style =
                {
                    BackgroundColor = System.Drawing.Color.Gainsboro,
                    Font =
                    {
                        Bold = true,
                        Size = Unit.Point(layout.HeaderFontPt)
                    },
                    BorderStyle = { Default = BorderType.Solid },
                    VerticalAlign = VerticalAlign.Middle,
                    Padding =
                    {
                        Left = Unit.Point(2),
                        Right = Unit.Point(2)
                    }
                }
            });
        }

        return section;
    }

    private static PageHeaderSection BuildPageHeader(
        DataTable table,
        string? title,
        ReportExecutionRecord execution,
        ReportExecutionArtifactHeader? header,
        TableLayout layout,
        int sourceRowCount,
        SnapshotReportBuildOptions? options)
    {
        var section = new PageHeaderSection
        {
            Height = Unit.Cm(1.4)
        };

        section.Items.Add(new TextBox
        {
            Value = string.IsNullOrWhiteSpace(title)
                ? execution.ReportName ?? execution.ReportId
                : title,
            Location = new PointU(Unit.Cm(0), Unit.Cm(0)),
            Size = new SizeU(
                Unit.Cm(layout.PrintableWidthCm),
                Unit.Cm(0.6)),
            Style =
            {
                Font =
                {
                    Bold = true,
                    Size = Unit.Point(11)
                }
            }
        });

        section.Items.Add(new TextBox
        {
            Value =
                BuildSubtitle(table, execution, header, sourceRowCount, options),
            Location = new PointU(Unit.Cm(0), Unit.Cm(0.7)),
            Size = new SizeU(
                Unit.Cm(layout.PrintableWidthCm),
                Unit.Cm(0.5)),
            Style =
            {
                Font =
                {
                    Size = Unit.Point(7)
                },
                Color = System.Drawing.Color.DimGray
            }
        });

        return section;
    }

    private static string BuildSubtitle(
        DataTable table,
        ReportExecutionRecord execution,
        ReportExecutionArtifactHeader? header,
        int sourceRowCount,
        SnapshotReportBuildOptions? options)
    {
        var mode = options?.Mode ?? "Preview";
        var rows = table.Rows.Count == sourceRowCount
            ? table.Rows.Count.ToString()
            : $"{table.Rows.Count} preview rows of {sourceRowCount}";

        return
            $"Execution: {execution.ExecutionId} | Mode: {mode} | Rows: {rows} | Columns: {table.Columns.Count} | Artifact: {header?.ArtifactVersion ?? "n/a"} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
    }

    private static DetailSection BuildDetailSection(
        DataTable table,
        TableLayout layout)
    {
        var section = new DetailSection
        {
            Height = Unit.Cm(layout.RowHeightCm + layout.HeaderHeightCm + 0.1),
            KeepTogether = false
        };

        if (table.Columns.Count == 0)
        {
            section.Items.Add(new TextBox
            {
                Value = "No columns found in snapshot.",
                Size = new SizeU(Unit.Cm(10), Unit.Cm(0.6))
            });

            return section;
        }

        if (table.Rows.Count == 0)
        {
            section.Items.Add(new TextBox
            {
                Value = $"Snapshot contains {table.Columns.Count} columns but no rows.",
                Size = new SizeU(Unit.Cm(layout.PrintableWidthCm), Unit.Cm(0.7)),
                CanShrink = true,
                Style =
                {
                    Font =
                    {
                        Bold = true,
                        Size = Unit.Point(9)
                    }
                }
            });

            return section;
        }

        section.Items.Add(BuildTable(table, layout));
        return section;
    }

    private static Telerik.Reporting.Table BuildTable(
        DataTable table,
        TableLayout layout)
    {
        var reportTable = new Telerik.Reporting.Table
        {
            DataSource = table,
            Location = new PointU(Unit.Cm(0), Unit.Cm(0))
        };

        var columns = table.Columns.Cast<System.Data.DataColumn>().ToList();
        var columnWidth = Unit.Cm(layout.PrintableWidthCm / columns.Count);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            reportTable.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(columnWidth));
            reportTable.ColumnGroups.Add(new TableGroup
            {
                Name = $"columnGroup{i}",
                ReportItem = BuildHeaderCell(column.ColumnName, columnWidth, layout)
            });
        }

        reportTable.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(Unit.Cm(layout.RowHeightCm)));
        reportTable.RowGroups.Add(new TableGroup
        {
            Name = "detailGroup",
            Groupings = { new Grouping(null) }
        });

        for (var i = 0; i < columns.Count; i++)
        {
            reportTable.Body.SetCellContent(0, i, BuildBodyCell(columns[i], columnWidth, layout));
        }

        reportTable.Size = new SizeU(
            Unit.Cm(layout.PrintableWidthCm),
            Unit.Cm(layout.RowHeightCm + layout.HeaderHeightCm));

        return reportTable;
    }

    private static TextBox BuildHeaderCell(
        string columnName,
        Unit width,
        TableLayout layout) =>
        new()
        {
            Value = columnName,
            Size = new SizeU(width, Unit.Cm(layout.HeaderHeightCm)),
            CanGrow = false,
            CanShrink = true,
            Style =
            {
                BackgroundColor = System.Drawing.Color.FromArgb(230, 236, 244),
                Font =
                {
                    Bold = true,
                    Size = Unit.Point(layout.HeaderFontPt)
                },
                BorderStyle =
                {
                    Default = BorderType.Solid
                },
                BorderColor =
                {
                    Default = System.Drawing.Color.FromArgb(180, 190, 204)
                },
                Padding =
                {
                    Left = Unit.Point(2),
                    Right = Unit.Point(2)
                },
                VerticalAlign = VerticalAlign.Middle
            }
        };

    private static TextBox BuildBodyCell(
        System.Data.DataColumn column,
        Unit width,
        TableLayout layout) =>
        new()
        {
            Value = $"= Fields.[{EscapeFieldName(column.ColumnName)}]",
            Size = new SizeU(width, Unit.Cm(layout.RowHeightCm)),
            CanGrow = true,
            CanShrink = true,
            Style =
            {
                Font =
                {
                    Size = Unit.Point(layout.BodyFontPt)
                },
                BorderStyle =
                {
                    Default = BorderType.Solid
                },
                BorderColor =
                {
                    Default = System.Drawing.Color.FromArgb(215, 221, 230)
                },
                Padding =
                {
                    Left = Unit.Point(2),
                    Right = Unit.Point(2)
                },
                VerticalAlign = VerticalAlign.Middle
            }
        };

    private static string EscapeFieldName(string name) => name.Replace("]", "]]");

    private static PageFooterSection BuildPageFooter()
    {
        var footer = new PageFooterSection
        {
            Height = Unit.Cm(0.5)
        };

        footer.Items.Add(new TextBox
        {
            Value = "= PageNumber + ' / ' + PageCount",
            Size = new SizeU(Unit.Cm(5), Unit.Cm(0.4)),
            Style =
            {
                TextAlign = HorizontalAlign.Right,
                Font =
                {
                    Size = Unit.Point(7)
                }
            },
            Location = new PointU(Unit.Cm(22), Unit.Cm(0))
        });

        return footer;
    }

    private static DataTable NormalizeTable(DataTable source)
    {
        var normalized = new DataTable();

        foreach (System.Data.DataColumn column in source.Columns)
        {
            var normalizedName = NormalizeColumnName(column.ColumnName);

            if (normalized.Columns.Contains(normalizedName))
            {
                normalizedName = $"{normalizedName}_{normalized.Columns.Count}";
            }

            normalized.Columns.Add(
                normalizedName,
                typeof(string));
        }

        foreach (System.Data.DataRow row in source.Rows)
        {
            var newRow = normalized.NewRow();

            for (var i = 0; i < source.Columns.Count; i++)
            {
                var value = row[i];

                newRow[i] = value == DBNull.Value
                    ? string.Empty
                    : Convert.ToString(value);
            }

            normalized.Rows.Add(newRow);
        }

        return normalized;
    }

    private static DataTable ApplyRowLimit(DataTable source, int? maxRows)
    {
        if (maxRows is not > 0 || source.Rows.Count <= maxRows.Value)
        {
            return source;
        }

        var limited = source.Clone();
        for (var i = 0; i < maxRows.Value; i++)
        {
            limited.ImportRow(source.Rows[i]);
        }

        return limited;
    }

    private static string NormalizeColumnName(string columnName)
    {
        var chars = columnName
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        var normalized = new string(chars);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Column";
        }

        normalized = normalized.Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Column";
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = $"C_{normalized}";
        }

        return normalized;
    }

    private sealed record TableLayout(
        bool Landscape,
        double PrintableWidthCm,
        double HeaderFontPt,
        double BodyFontPt,
        double HeaderHeightCm,
        double RowHeightCm);

    private static TableLayout ResolveLayout(int columnCount)
    {
        if (columnCount >= 12)
        {
            return new TableLayout(
                Landscape: true,
                PrintableWidthCm: 27.0,
                HeaderFontPt: 5,
                BodyFontPt: 5,
                HeaderHeightCm: 0.45,
                RowHeightCm: 0.45);
        }

        if (columnCount >= 6)
        {
            return new TableLayout(
                Landscape: true,
                PrintableWidthCm: 27.0,
                HeaderFontPt: 6,
                BodyFontPt: 6,
                HeaderHeightCm: 0.5,
                RowHeightCm: 0.5);
        }

        return new TableLayout(
            Landscape: false,
            PrintableWidthCm: 18.5,
            HeaderFontPt: 8,
            BodyFontPt: 8,
            HeaderHeightCm: 0.6,
            RowHeightCm: 0.55);
    }
}
