using System.Data;
using Report.Contracts.Results;

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
            table.Columns.Add(result.Columns[i].Name, columnTypes[i]);
        }

        foreach (var row in result.Rows)
        {
            var item = table.NewRow();
            for (var i = 0; i < result.Columns.Count; i++)
            {
                var colName = result.Columns[i].Name;
                item[colName] = row.TryGetValue(colName, out var value) && value is not null ? value : DBNull.Value;
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
}
