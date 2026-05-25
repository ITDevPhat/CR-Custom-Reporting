using System.Data;
using System.Globalization;
using System.Text;
using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public sealed class TelerikArtifactReportRenderer : IArtifactReportRenderer
{
    private readonly ILogger<TelerikArtifactReportRenderer> _logger;

    public TelerikArtifactReportRenderer(ILogger<TelerikArtifactReportRenderer> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> RenderAsync(string format, string templateId, DataTable dataTable, CancellationToken ct)
    {
        var normalized = Normalize(format);
        ct.ThrowIfCancellationRequested();

        var report = BuildReport(templateId, dataTable);
        var source = new Telerik.Reporting.InstanceReportSource
        {
            ReportDocument = report
        };

        try
        {
            var processor = new Telerik.Reporting.Processing.ReportProcessor();
            var result = processor.RenderReport(
                normalized,
                source,
                new System.Collections.Hashtable())
                ?? throw new ReportExportException($"Telerik renderer returned null output for '{normalized}'.");

            var bytes = result.DocumentBytes
                ?? throw new ReportExportException($"Telerik renderer produced null document bytes for '{normalized}'.");

            ValidatePayload(normalized, bytes);

            _logger.LogInformation(
                "Telerik artifact render completed. Format={Format} TemplateId={TemplateId} Rows={Rows} Columns={Columns} Bytes={Bytes} Magic={Magic}",
                normalized,
                templateId,
                dataTable.Rows.Count,
                dataTable.Columns.Count,
                bytes.Length,
                GetMagicBytes(bytes));

            return Task.FromResult(bytes);
        }
        catch (ReportExportException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ReportExportException(
                $"Telerik artifact render failed for format '{normalized}': {ex.Message}",
                500,
                ex);
        }
    }

    private static Telerik.Reporting.Report BuildReport(string templateId, DataTable dataTable)
    {
        var columnCount = Math.Max(dataTable.Columns.Count, 1);
        var layout = ResolveLayout(columnCount);

        var report = new Telerik.Reporting.Report
        {
            Name = "ArtifactSnapshotExportReport",
            PageSettings =
            {
                Landscape = layout.Landscape,
                Margins =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Cm(0.5),
                    Right = Telerik.Reporting.Drawing.Unit.Cm(0.5),
                    Top = Telerik.Reporting.Drawing.Unit.Cm(0.7),
                    Bottom = Telerik.Reporting.Drawing.Unit.Cm(0.7)
                }
            }
        };

        report.Items.Add(BuildHeader(templateId, dataTable));

        var detail = new Telerik.Reporting.DetailSection
        {
            Height = Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm + 0.1)
        };

        detail.Items.Add(BuildTable(dataTable, layout));
        report.Items.Add(detail);

        return report;
    }

    private static Telerik.Reporting.PageHeaderSection BuildHeader(string templateId, DataTable dataTable)
    {
        var header = new Telerik.Reporting.PageHeaderSection
        {
            Height = Telerik.Reporting.Drawing.Unit.Cm(1.4)
        };

        header.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = string.IsNullOrWhiteSpace(templateId) ? "Report Export" : templateId,
            Size = new Telerik.Reporting.Drawing.SizeU(
                Telerik.Reporting.Drawing.Unit.Cm(20),
                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Style =
            {
                Font = { Bold = true, Size = Telerik.Reporting.Drawing.Unit.Point(11) },
                Padding =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Point(2),
                    Right = Telerik.Reporting.Drawing.Unit.Point(2)
                }
            }
        });

        header.Items.Add(new Telerik.Reporting.TextBox
        {
            Value = $"Rows: {dataTable.Rows.Count} | Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            Location = new Telerik.Reporting.Drawing.PointU(
                Telerik.Reporting.Drawing.Unit.Cm(0),
                Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            Size = new Telerik.Reporting.Drawing.SizeU(
                Telerik.Reporting.Drawing.Unit.Cm(20),
                Telerik.Reporting.Drawing.Unit.Cm(0.5)),
            Style =
            {
                Font = { Size = Telerik.Reporting.Drawing.Unit.Point(8) },
                Color = System.Drawing.Color.DimGray,
                Padding =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Point(2),
                    Right = Telerik.Reporting.Drawing.Unit.Point(2)
                }
            }
        });

        return header;
    }

    private static Telerik.Reporting.Table BuildTable(DataTable dataTable, TableLayout layout)
    {
        var table = new Telerik.Reporting.Table
        {
            DataSource = dataTable,
            Location = new Telerik.Reporting.Drawing.PointU(
                Telerik.Reporting.Drawing.Unit.Cm(0),
                Telerik.Reporting.Drawing.Unit.Cm(0))
        };

        var columns = dataTable.Columns.Cast<DataColumn>().ToList();
        if (columns.Count == 0)
        {
            columns.Add(new DataColumn("NoData"));
        }

        var columnWidth = Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm / columns.Count);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            table.Body.Columns.Add(new Telerik.Reporting.TableBodyColumn(columnWidth));
            table.ColumnGroups.Add(new Telerik.Reporting.TableGroup
            {
                Name = $"columnGroup{i}",
                ReportItem = BuildHeaderCell(column.ColumnName, columnWidth, layout)
            });
        }

        table.Body.Rows.Add(new Telerik.Reporting.TableBodyRow(
            Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm)));
        table.RowGroups.Add(new Telerik.Reporting.TableGroup
        {
            Name = "detailGroup",
            Groupings = { new Telerik.Reporting.Grouping(null) }
        });

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            table.Body.SetCellContent(0, i, BuildBodyCell(column, columnWidth, layout));
        }

        table.Size = new Telerik.Reporting.Drawing.SizeU(
            Telerik.Reporting.Drawing.Unit.Cm(layout.PrintableWidthCm),
            Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm + 0.7));

        return table;
    }

    private static Telerik.Reporting.TextBox BuildHeaderCell(
        string columnName,
        Telerik.Reporting.Drawing.Unit width,
        TableLayout layout) =>
        new()
        {
            Value = columnName,
            Size = new Telerik.Reporting.Drawing.SizeU(
                width,
                Telerik.Reporting.Drawing.Unit.Cm(0.7)),
            Style =
            {
                BackgroundColor = System.Drawing.Color.FromArgb(230, 236, 244),
                Font =
                {
                    Bold = true,
                    Size = Telerik.Reporting.Drawing.Unit.Point(layout.HeaderFontPt)
                },
                BorderStyle =
                {
                    Default = Telerik.Reporting.Drawing.BorderType.Solid
                },
                BorderColor =
                {
                    Default = System.Drawing.Color.FromArgb(180, 190, 204)
                },
                Padding =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Point(2),
                    Right = Telerik.Reporting.Drawing.Unit.Point(2)
                },
                VerticalAlign = Telerik.Reporting.Drawing.VerticalAlign.Middle
            }
        };

    private static Telerik.Reporting.TextBox BuildBodyCell(
        DataColumn column,
        Telerik.Reporting.Drawing.Unit width,
        TableLayout layout)
    {
        var expression = column.DataType == typeof(DateTime) || column.DataType == typeof(DateTimeOffset)
            ? $"= Format('{{0:yyyy-MM-dd HH:mm:ss}}', Fields.[{EscapeFieldName(column.ColumnName)}])"
            : $"= Fields.[{EscapeFieldName(column.ColumnName)}]";

        return new Telerik.Reporting.TextBox
        {
            Value = expression,
            Size = new Telerik.Reporting.Drawing.SizeU(
                width,
                Telerik.Reporting.Drawing.Unit.Cm(layout.RowHeightCm)),
            Style =
            {
                Font =
                {
                    Size = Telerik.Reporting.Drawing.Unit.Point(layout.BodyFontPt)
                },
                BorderStyle =
                {
                    Default = Telerik.Reporting.Drawing.BorderType.Solid
                },
                BorderColor =
                {
                    Default = System.Drawing.Color.FromArgb(215, 221, 230)
                },
                Padding =
                {
                    Left = Telerik.Reporting.Drawing.Unit.Point(2),
                    Right = Telerik.Reporting.Drawing.Unit.Point(2)
                },
                VerticalAlign = Telerik.Reporting.Drawing.VerticalAlign.Middle
            }
        };
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
            return new TableLayout(true, 27.0, 6, 6, 0.5);
        }

        if (columnCount >= 6)
        {
            return new TableLayout(true, 27.0, 7, 7, 0.55);
        }

        return new TableLayout(false, 18.5, 8, 8, 0.6);
    }

    private static string EscapeFieldName(string name) => name.Replace("]", "]]");

    private static string Normalize(string format)
    {
        var normalized = format.Trim().ToUpperInvariant();
        return normalized switch
        {
            "PDF" => "PDF",
            "XLSX" => "XLSX",
            "DOCX" => "DOCX",
            _ => throw new ReportExportException(
                $"Unsupported Telerik export format '{format}'. Supported formats: PDF, XLSX, DOCX.",
                400)
        };
    }

    private static void ValidatePayload(string format, byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ReportExportException($"Telerik export payload for '{format}' is empty.");
        }

        if (format == "PDF")
        {
            var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4));
            if (bytes.Length <= 100 || !header.StartsWith("%PDF", StringComparison.Ordinal))
            {
                throw new ReportExportException("Telerik rendered invalid PDF bytes.");
            }
        }

        if (format is "XLSX" or "DOCX" &&
            (bytes.Length <= 100 || bytes[0] != 0x50 || bytes[1] != 0x4B))
        {
            throw new ReportExportException($"Telerik rendered invalid {format} bytes.");
        }
    }

    private static string GetMagicBytes(byte[] bytes)
    {
        return string.Join(
            '-',
            bytes.Take(8).Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
