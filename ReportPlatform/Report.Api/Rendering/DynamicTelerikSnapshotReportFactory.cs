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
        ReportExecutionArtifactHeader? header = null)
    {
        var normalizedTable = NormalizeTable(dataTable);

        var columnCount = Math.Max(normalizedTable.Columns.Count, 1);
        var layout = ResolveLayout(columnCount);

        _logger.LogInformation(
            "Building snapshot preview report. ExecutionId={ExecutionId} Rows={Rows} Columns={Columns}",
            execution.ExecutionId,
            normalizedTable.Rows.Count,
            normalizedTable.Columns.Count);

        var report = new Telerik.Reporting.Report
        {
            Name = "RuntimeSnapshotPreviewReport",
            DataSource = normalizedTable,
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
            layout));

        report.Items.Add(BuildDetailSection(
            normalizedTable,
            layout));
        report.Items.Add(BuildColumnHeaderSection(
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
        TableLayout layout)
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
                $"Execution: {execution.ExecutionId} | Rows: {table.Rows.Count} | Columns: {table.Columns.Count} | Artifact: {header?.ArtifactVersion ?? "n/a"} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
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

    private static DetailSection BuildDetailSection(
        DataTable table,
        TableLayout layout)
    {
        var section = new DetailSection
        {
            Height = Unit.Cm(layout.RowHeightCm),
            CanGrow = true,
            CanShrink = true,
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

        var columnWidthCm = layout.PrintableWidthCm / table.Columns.Count;

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];

            section.Items.Add(new TextBox
            {
                Value = $"= Fields.{column.ColumnName}",
                Location = new PointU(
                    Unit.Cm(i * columnWidthCm),
                    Unit.Cm(0)),
                Size = new SizeU(
                    Unit.Cm(columnWidthCm),
                    Unit.Cm(layout.RowHeightCm)),
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
//using System.Data;
//using Microsoft.Extensions.Logging;
//using Report.Contracts.Artifacts;

//namespace Report.Api.Rendering;

//public sealed class DynamicTelerikSnapshotReportFactory : ITelerikSnapshotReportFactory
//{
//    private readonly ILogger<DynamicTelerikSnapshotReportFactory> _logger;

//    public DynamicTelerikSnapshotReportFactory(
//        ILogger<DynamicTelerikSnapshotReportFactory> logger)
//    {
//        _logger = logger;
//    }

//    public Telerik.Reporting.Report CreateSnapshotBackedTableReport(
//        DataTable dataTable,
//        string? title,
//        ReportExecutionRecord execution,
//        ReportExecutionArtifactHeader? header = null)
//    {
//        var normalized = NormalizeDataTable(dataTable);

//        var columnCount = Math.Max(normalized.Table.Columns.Count, 1);
//        var layout = ResolveLayout(columnCount);

//        _logger.LogInformation(
//            "Building snapshot Telerik report. ExecutionId={ExecutionId} ReportId={ReportId} Rows={Rows} Columns={Columns} Landscape={Landscape} PrintableWidthCm={PrintableWidthCm} FirstColumns={FirstColumns}",
//            execution.ExecutionId,
//            execution.ReportId,
//            normalized.Table.Rows.Count,
//            normalized.Table.Columns.Count,
//            layout.Landscape,
//            layout.PrintableWidthCm,
//            string.Join(", ", normalized.Columns.Take(5).Select(c => $"{c.OriginalName}->{c.SafeName}")));

//        var report = new Telerik.Reporting.Report
//        {
//            Name = "RuntimeSnapshotPreviewReport",
//            PageSettings =
//            {
//                Landscape = layout.Landscape,
//                Margins =
//                {
//                    Left = Telerik.Reporting.Drawing.Unit.Cm(0.5),
//                    Right = Telerik.Reporting.Drawing.Unit.Cm(0.5),
//                    Top = Telerik.Reporting.Drawing.Unit.Cm(0.7),
//                    Bottom = Telerik.Reporting.Drawing.Unit.Cm(0.7)
//                }
//            }
//        };

//        report.Items.Add(BuildPageHeader(
//            normalized.Table,
//            title,
//            execution,
//            header,
//            layout));

//        var detail = new Telerik.Reporting.DetailSection
//        {
//            Height = Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm * 2.2)
//        };

//        if (normalized.Table.Columns.Count == 0)
//        {
//            detail.Items.Add(BuildMessageTextBox("No columns found in snapshot artifact."));
//        }
//        else if (normalized.Table.Rows.Count == 0)
//        {
//            detail.Items.Add(BuildMessageTextBox(
//                $"Snapshot contains {normalized.Table.Columns.Count} columns but 0 rows."));
//        }
//        else
//        {
//            detail.Items.Add(BuildTable(normalized.Table, normalized.Columns, layout));
//        }

//        report.Items.Add(detail);

//        _logger.LogInformation(
//            "Snapshot Telerik report built. ExecutionId={ExecutionId} ReportItems={ReportItems} DetailItems={DetailItems}",
//            execution.ExecutionId,
//            report.Items.Count,
//            detail.Items.Count);

//        return report;
//    }

//    private static Telerik.Reporting.PageHeaderSection BuildPageHeader(
//        DataTable dataTable,
//        string? title,
//        ReportExecutionRecord execution,
//        ReportExecutionArtifactHeader? header,
//        TableLayout layout)
//    {
//        var pageHeader = new Telerik.Reporting.PageHeaderSection
//        {
//            Height = Telerik.Reporting.Drawing.Unit.Cm(1.4)
//        };

//        pageHeader.Items.Add(new Telerik.Reporting.TextBox
//        {
//            Value = string.IsNullOrWhiteSpace(title)
//                ? execution.ReportName ?? execution.ReportId
//                : title,
//            Size = new Telerik.Reporting.Drawing.SizeU(
//                Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm),
//                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
//            CanGrow = false,
//            CanShrink = true,
//            Style =
//            {
//                Font =
//                {
//                    Bold = true,
//                    Size = Telerik.Reporting.Drawing.Unit.Point(11)
//                }
//            }
//        });

//        pageHeader.Items.Add(new Telerik.Reporting.TextBox
//        {
//            Value =
//                $"Execution: {execution.ExecutionId} | Rows: {dataTable.Rows.Count} | Columns: {dataTable.Columns.Count} | Artifact: {header?.ArtifactVersion ?? "n/a"} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
//            Location = new Telerik.Reporting.Drawing.PointU(
//                Telerik.Reporting.Drawing.Unit.Cm(0),
//                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
//            Size = new Telerik.Reporting.Drawing.SizeU(
//                Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm),
//                Telerik.Reporting.Drawing.Unit.Cm(0.5)),
//            CanGrow = false,
//            CanShrink = true,
//            Style =
//            {
//                Font =
//                {
//                    Size = Telerik.Reporting.Drawing.Unit.Point(8)
//                },
//                Color = System.Drawing.Color.DimGray
//            }
//        });

//        return pageHeader;
//    }

//    private static Telerik.Reporting.Table BuildTable(
//        DataTable dataTable,
//        IReadOnlyList<ColumnBinding> columns,
//        TableLayout layout)
//    {
//        var table = new Telerik.Reporting.Table
//        {
//            DataSource = dataTable,
//            Location = new Telerik.Reporting.Drawing.PointU(
//                Telerik.Reporting.Drawing.Unit.Cm(0),
//                Telerik.Reporting.Drawing.Unit.Cm(0))
//        };

//        var columnCount = Math.Max(columns.Count, 1);
//        var columnWidthCm = layout.PrintableWidthCm / columnCount;
//        var columnWidth = Telerik.Reporting.Drawing.Unit.Cm(columnWidthCm);

//        foreach (var column in columns)
//        {
//            table.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(columnWidth));

//            var columnGroup = new Telerik.Reporting.TableGroup
//            {
//                Name = $"cg_{column.SafeName}"
//            };

//            columnGroup.ReportItem = new Telerik.Reporting.TextBox
//            {
//                Value = column.OriginalName,
//                Size = new Telerik.Reporting.Drawing.SizeU(
//                    columnWidth,
//                    Telerik.Reporting.Drawing.Unit.Cm(0.7)),
//                CanGrow = false,
//                CanShrink = true,
//                Style =
//                {
//                    BackgroundColor = System.Drawing.Color.Gainsboro,
//                    Font =
//                    {
//                        Bold = true,
//                        Size = Telerik.Reporting.Drawing.Unit.Point(layout.HeaderFontPt)
//                    },
//                    BorderStyle =
//                    {
//                        Default = Telerik.Reporting.Drawing.BorderType.Solid
//                    },
//                    Padding =
//                    {
//                        Left = Telerik.Reporting.Drawing.Unit.Point(1),
//                        Right = Telerik.Reporting.Drawing.Unit.Point(1),
//                        Top = Telerik.Reporting.Drawing.Unit.Point(1),
//                        Bottom = Telerik.Reporting.Drawing.Unit.Point(1)
//                    }
//                }
//            };

//            table.ColumnGroups.Add(columnGroup);
//        }

//        table.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(
//            Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm)));

//        var detailGroup = new Telerik.Reporting.TableGroup
//        {
//            Name = "detail"
//        };

//        detailGroup.Groupings.Add(new Telerik.Reporting.Grouping(null));
//        table.RowGroups.Add(detailGroup);

//        for (var i = 0; i < columns.Count; i++)
//        {
//            var column = columns[i];

//            table.Body.SetCellContent(0, i, new Telerik.Reporting.TextBox
//            {
//                Value = $"=Fields.{column.SafeName}",
//                Size = new Telerik.Reporting.Drawing.SizeU(
//                    columnWidth,
//                    Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm)),
//                CanGrow = false,
//                CanShrink = true,
//                Style =
//                {
//                    Font =
//                    {
//                        Size = Telerik.Reporting.Drawing.Unit.Point(layout.BodyFontPt)
//                    },
//                    BorderStyle =
//                    {
//                        Default = Telerik.Reporting.Drawing.BorderType.Solid
//                    },
//                    Padding =
//                    {
//                        Left = Telerik.Reporting.Drawing.Unit.Point(1),
//                        Right = Telerik.Reporting.Drawing.Unit.Point(1),
//                        Top = Telerik.Reporting.Drawing.Unit.Point(1),
//                        Bottom = Telerik.Reporting.Drawing.Unit.Point(1)
//                    }
//                }
//            });
//        }

//        table.Size = new Telerik.Reporting.Drawing.SizeU(
//            Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm),
//            Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm * 2));

//        return table;
//    }

//    private static Telerik.Reporting.TextBox BuildMessageTextBox(string message)
//    {
//        return new Telerik.Reporting.TextBox
//        {
//            Value = message,
//            Size = new Telerik.Reporting.Drawing.SizeU(
//                Telerik.Reporting.Drawing.Unit.Cm(20),
//                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
//            CanGrow = false,
//            CanShrink = true,
//            Style =
//            {
//                Font =
//                {
//                    Bold = true,
//                    Size = Telerik.Reporting.Drawing.Unit.Point(9)
//                },
//                Color = System.Drawing.Color.DarkRed
//            }
//        };
//    }

//    private static NormalizedTable NormalizeDataTable(DataTable source)
//    {
//        var normalized = new DataTable(source.TableName);
//        var columns = new List<ColumnBinding>();

//        for (var i = 0; i < source.Columns.Count; i++)
//        {
//            var sourceColumn = source.Columns[i];

//            var safeName = MakeSafeColumnName(sourceColumn.ColumnName, i);
//            var type = Nullable.GetUnderlyingType(sourceColumn.DataType) ?? sourceColumn.DataType;

//            if (type == typeof(DBNull))
//            {
//                type = typeof(string);
//            }

//            normalized.Columns.Add(safeName, type);

//            columns.Add(new ColumnBinding(
//                OriginalName: sourceColumn.ColumnName,
//                SafeName: safeName));
//        }

//        foreach (DataRow sourceRow in source.Rows)
//        {
//            var row = normalized.NewRow();

//            for (var i = 0; i < source.Columns.Count; i++)
//            {
//                var value = sourceRow[i];
//                row[i] = value == null ? DBNull.Value : value;
//            }

//            normalized.Rows.Add(row);
//        }

//        return new NormalizedTable(normalized, columns);
//    }

//    private static string MakeSafeColumnName(string columnName, int index)
//    {
//        var normalized = new string(
//            columnName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

//        if (string.IsNullOrWhiteSpace(normalized))
//        {
//            normalized = $"Column_{index}";
//        }

//        if (char.IsDigit(normalized[0]))
//        {
//            normalized = $"C_{normalized}";
//        }

//        return $"{normalized}_{index}";
//    }

//    private sealed record NormalizedTable(
//        DataTable Table,
//        IReadOnlyList<ColumnBinding> Columns);

//    private sealed record ColumnBinding(
//        string OriginalName,
//        string SafeName);

//    private sealed record TableLayout(
//        bool Landscape,
//        double PrintableWidthCm,
//        double HeaderFontPt,
//        double BodyFontPt,
//        double RowHeightCm);

//    private static TableLayout ResolveLayout(int columnCount)
//    {
//        if (columnCount >= 12)
//        {
//            return new TableLayout(
//                Landscape: true,
//                PrintableWidthCm: 27.0,
//                HeaderFontPt: 5,
//                BodyFontPt: 5,
//                RowHeightCm: 0.45);
//        }

//        if (columnCount >= 10)
//        {
//            return new TableLayout(
//                Landscape: true,
//                PrintableWidthCm: 27.0,
//                HeaderFontPt: 6,
//                BodyFontPt: 6,
//                RowHeightCm: 0.5);
//        }

//        if (columnCount >= 6)
//        {
//            return new TableLayout(
//                Landscape: true,
//                PrintableWidthCm: 27.0,
//                HeaderFontPt: 7,
//                BodyFontPt: 7,
//                RowHeightCm: 0.55);
//        }

//        return new TableLayout(
//            Landscape: false,
//            PrintableWidthCm: 18.5,
//            HeaderFontPt: 8,
//            BodyFontPt: 8,
//            RowHeightCm: 0.6);
//    }
//}
