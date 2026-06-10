using System.Data;
using Report.Contracts.Results;
using SystemDataColumn = System.Data.DataColumn;

namespace Report.Api.Rendering;

public static class QueryResultDataTableMapper
{
    public static DataTable Map(QueryResult result)
    {
        var table = new DataTable("ReportResult");

        var columnTypes = result.Columns
            .Select(column => InferType(column, result.Rows))
            .ToArray();

        for (var i = 0; i < result.Columns.Count; i++)
        {
            var systemDataColumn = new SystemDataColumn(result.Columns[i].Name, columnTypes[i])
            {
                AllowDBNull = true
            };
            table.Columns.Add(systemDataColumn);
        }

        foreach (var row in result.Rows)
        {
            var item = table.NewRow();
            for (var i = 0; i < result.Columns.Count; i++)
            {
                var colName = result.Columns[i].Name;
                item[colName] = CoerceValue(row.TryGetValue(colName, out var value) ? value : null, columnTypes[i]);
            }
            table.Rows.Add(item);
        }

        return table;
    }

    private static Type InferType(QueryColumn column, List<Dictionary<string, object?>> rows)
    {
        var nonNull = rows
            .Select(r => r.TryGetValue(column.Name, out var v) ? v : null)
            .FirstOrDefault(v => v is not null);

        if (nonNull is not null)
        {
            return nonNull.GetType();
        }

        return column.Type.ToLowerInvariant() switch
        {
            "int" or "smallint" or "tinyint" => typeof(int),
            "bigint" => typeof(long),
            "decimal" or "money" or "numeric" => typeof(decimal),
            "float" => typeof(double),
            "bit" => typeof(bool),
            "date" or "datetime" or "datetime2" => typeof(DateTime),
            "guid" or "uniqueidentifier" => typeof(Guid),
            "string" or "nvarchar" or "varchar" or "text" => typeof(string),
            _ => typeof(object),
        };
    }

    private static object CoerceValue(object? value, Type targetType)
    {
        if (value is null or DBNull)
        {
            return DBNull.Value;
        }

        if (targetType == typeof(decimal) && value is not decimal)
        {
            return Convert.ToDecimal(value);
        }

        if (targetType == typeof(DateTime) && value is DateTimeOffset dto)
        {
            return dto.UtcDateTime;
        }

        return value;
    }
}
