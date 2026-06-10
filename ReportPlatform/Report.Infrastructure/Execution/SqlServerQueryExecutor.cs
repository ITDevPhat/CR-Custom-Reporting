using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Report.Contracts.Results;
using Report.Infrastructure.Connections;
using Report.Metadata.Connections;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Execution;

namespace Report.Infrastructure.Execution;

public sealed class SqlServerQueryExecutor : IQueryExecutor
{
    private readonly IConfiguration _configuration;
    private readonly IConnectionRegistry _connectionRegistry;

    public SqlServerQueryExecutor(
        IConfiguration configuration,
        IConnectionRegistry connectionRegistry)
    {
        _configuration = configuration;
        _connectionRegistry = connectionRegistry;
    }

    public async Task<QueryResult> ExecuteAsync(
        string connectionId,
        SqlCompilationResult compilation,
        IReadOnlyList<QueryColumn> expectedColumns,
        CancellationToken ct)
    {
        var connectionString = ResolveConnectionString(connectionId);
        var parameters = ToDynamicParameters(compilation.Parameters);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            var command = new CommandDefinition(
                compilation.Sql,
                parameters,
                commandType: CommandType.Text,
                cancellationToken: ct);

            var resultRows = await connection.QueryAsync(command);
            stopwatch.Stop();

            var rows = resultRows
                .Select(row => ((IDictionary<string, object>)row)
                    .ToDictionary(
                        column => column.Key,
                        column => NormalizeDbValue(column.Value)))
                .ToList();

            var columns = expectedColumns.Count > 0
                ? expectedColumns.ToList()
                : InferColumns(rows);

            return new QueryResult
            {
                Columns = columns,
                Rows = rows,
                Metadata = new QueryExecutionMetadata
                {
                    RowCount = rows.Count,
                    ExecutionMs = stopwatch.ElapsedMilliseconds,
                    Sql = compilation.Sql,
                    Parameters = compilation.Parameters
                }
            };
        }
        catch (Exception ex) when (ex is not QueryExecutionException)
        {
            stopwatch.Stop();
            throw new QueryExecutionException(
                $"SQL Server query execution failed: {ex.Message}",
                compilation.Sql,
                ex);
        }
    }

    private string ResolveConnectionString(string connectionId)
    {
        var connectionString =
            _configuration.GetSection("ReportConnections")[connectionId] ??
            _configuration.GetConnectionString(connectionId);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var connection = _connectionRegistry.Find(connectionId);
            if (connection is not null)
            {
                return SqlServerConnectionFactory.BuildConnectionString(connection);
            }

            throw new QueryExecutionException(
                $"Connection string not found for connectionId '{connectionId}'.",
                "");
        }

        return connectionString;
    }

    private static DynamicParameters ToDynamicParameters(Dictionary<string, object?> parameters)
    {
        var dynamicParameters = new DynamicParameters();

        foreach (var parameter in parameters)
        {
            dynamicParameters.Add(parameter.Key, parameter.Value);
        }

        return dynamicParameters;
    }

    private static object? NormalizeDbValue(object? value)
    {
        return value is DBNull ? null : value;
    }

    private static List<QueryColumn> InferColumns(List<Dictionary<string, object?>> rows)
    {
        var firstRow = rows.FirstOrDefault();
        if (firstRow is null)
        {
            return [];
        }

        return firstRow
            .Select(column => new QueryColumn
            {
                Name = column.Key,
                Type = InferType(column.Value)
            })
            .ToList();
    }

    private static string InferType(object? value)
    {
        return value switch
        {
            null => "string",
            byte or short or int or long => "number",
            float or double or decimal => "decimal",
            DateOnly or DateTime or DateTimeOffset => "date",
            bool => "boolean",
            _ => "string"
        };
    }
}
