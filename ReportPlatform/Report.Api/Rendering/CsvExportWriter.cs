using System.Globalization;
using System.Text;
using Report.Contracts.Results;

namespace Report.Api.Rendering;

public static class CsvExportWriter
{
    public static byte[] Write(QueryResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(
            ",",
            result.Columns.Select(c => Escape(c.Name))));

        foreach (var row in result.Rows)
        {
            var values = result.Columns.Select(column =>
            {
                row.TryGetValue(column.Name, out var value);
                return Escape(FormatValue(value));
            });

            sb.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DBNull => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Escape(string value)
    {
        var mustQuote =
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\r') ||
            value.Contains('\n');

        var escaped = value.Replace("\"", "\"\"");

        return mustQuote ? $"\"{escaped}\"" : escaped;
    }
}