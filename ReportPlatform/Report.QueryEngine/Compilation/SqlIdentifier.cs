using System.Text.RegularExpressions;
using Report.Metadata.Models;

namespace Report.QueryEngine.Compilation;

public static partial class SqlIdentifier
{
    public static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier is required.", nameof(identifier));
        }

        return $"[{identifier.Replace("]", "]]")}]";
    }

    public static string QuoteTable(SemanticModel model, string tableId)
    {
        var table = model.Tables.FirstOrDefault(t =>
            string.Equals(t.TableId, tableId, StringComparison.OrdinalIgnoreCase));

        if (table is not null &&
            !string.IsNullOrWhiteSpace(table.PhysicalSchema) &&
            !string.IsNullOrWhiteSpace(table.PhysicalTable))
        {
            return $"{Quote(table.PhysicalSchema)}.{Quote(table.PhysicalTable)}";
        }

        return tableId.Contains('.')
            ? string.Join(".", tableId.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(Quote))
            : Quote(tableId);
    }

    public static string QuoteColumn(string column)
    {
        return column.StartsWith("[", StringComparison.Ordinal) &&
            column.EndsWith("]", StringComparison.Ordinal)
                ? column
                : Quote(column);
    }

    public static string SafeAlias(string value)
    {
        var alias = AliasCleaner().Replace(value, "");
        return string.IsNullOrWhiteSpace(alias) ? "Field" : alias;
    }

    [GeneratedRegex("[^A-Za-z0-9_]")]
    private static partial Regex AliasCleaner();
}
