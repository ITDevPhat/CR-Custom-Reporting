using System.Text;
using Report.Contracts.Results;

namespace Report.Api.Rendering;

public static class CsvExportWriter
{
    public static byte[] Write(QueryResult result)
    {
        var sb = new StringBuilder();
        var columns = result.Columns.Select(c => c.Name).ToArray();

        sb.AppendLine(string.Join(',', columns.Select(Escape)));

        foreach (var row in result.Rows)
        {
            var values = columns
                .Select(col => row.TryGetValue(col, out var value) ? value : null)
                .Select(FormatValue);

            sb.AppendLine(string.Join(',', values));
        }

        var csv = sb.ToString();
        var bom = Encoding.UTF8.GetPreamble();
        var payload = Encoding.UTF8.GetBytes(csv);
        return [.. bom, .. payload];
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return Escape(value switch
        {
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            _ => value.ToString() ?? string.Empty
        });
    }

    private static string Escape(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        if (escaped.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }
}
