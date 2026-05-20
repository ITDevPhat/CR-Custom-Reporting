using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Report.Contracts.Connections;
using Report.Metadata.Connections;
using Report.QueryEngine.Compilation;

namespace Report.Infrastructure.Connections;

public sealed class SqlServerSchemaDiscoveryService
{
    public async Task TestConnectionAsync(ConnectionDefinition definition, CancellationToken ct)
    {
        await using var connection = new SqlConnection(SqlServerConnectionFactory.BuildConnectionString(definition));
        await connection.OpenAsync(ct);
    }

    public async Task<List<DatabaseDto>> GetDatabasesAsync(ConnectionDefinition definition, CancellationToken ct)
    {
        await using var connection = new SqlConnection(SqlServerConnectionFactory.BuildConnectionString(definition, "master"));
        var command = new CommandDefinition(
            "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name;",
            cancellationToken: ct);
        var names = await connection.QueryAsync<string>(command);
        return names.Select(name => new DatabaseDto { Name = name }).ToList();
    }

    public async Task<DiscoverSchemaResponse> DiscoverAsync(ConnectionDefinition definition, CancellationToken ct)
    {
        await using var connection = new SqlConnection(SqlServerConnectionFactory.BuildConnectionString(definition));

        var tables = (await connection.QueryAsync<TableRow>(new CommandDefinition("""
            SELECT s.name AS [Schema], t.name AS [Table], 'BASE TABLE' AS TableType
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            ORDER BY s.name, t.name;
            """, cancellationToken: ct))).ToList();

        var columns = (await connection.QueryAsync<ColumnRow>(new CommandDefinition("""
            SELECT
              s.name AS [Schema],
              t.name AS [Table],
              c.name AS [Column],
              ty.name AS DataType,
              ty.name AS SqlDataType,
              c.column_id AS OrdinalPosition,
              c.is_nullable AS IsNullable,
              c.max_length AS CharacterMaximumLength,
              c.precision AS NumericPrecision,
              c.scale AS NumericScale,
              CONVERT(smallint, CASE WHEN ty.name IN ('date', 'time', 'datetime2', 'datetimeoffset') THEN c.scale ELSE NULL END) AS DatetimePrecision
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.columns c ON t.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            ORDER BY s.name, t.name, c.column_id;
            """, cancellationToken: ct))).ToList();

        var primaryKeys = (await connection.QueryAsync<KeyRow>(new CommandDefinition("""
            SELECT
              s.name AS [Schema],
              t.name AS [Table],
              c.name AS [Column],
              kc.name AS ConstraintName
            FROM sys.key_constraints kc
            INNER JOIN sys.index_columns ic
              ON kc.parent_object_id = ic.object_id
             AND kc.unique_index_id = ic.index_id
            INNER JOIN sys.columns c
              ON ic.object_id = c.object_id
             AND ic.column_id = c.column_id
            INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE kc.type = 'PK';
            """, cancellationToken: ct))).ToHashSet(KeyRowComparer.Instance);

        var uniqueColumns = (await connection.QueryAsync<KeyRow>(new CommandDefinition("""
            SELECT s.name AS [Schema], t.name AS [Table], c.name AS [Column]
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic
              ON i.object_id = ic.object_id
             AND i.index_id = ic.index_id
            INNER JOIN sys.columns c
              ON ic.object_id = c.object_id
             AND ic.column_id = c.column_id
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE i.is_unique = 1;
            """, cancellationToken: ct))).ToHashSet(KeyRowComparer.Instance);

        var relationships = (await connection.QueryAsync<RelationshipDiscoveryDto>(new CommandDefinition("""
            SELECT
              fk.name AS ForeignKeyName,
              parent_schema.name AS FromSchema,
              parent_table.name AS FromTable,
              parent_column.name AS FromColumn,
              referenced_schema.name AS ToSchema,
              referenced_table.name AS ToTable,
              referenced_column.name AS ToColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables parent_table ON fkc.parent_object_id = parent_table.object_id
            INNER JOIN sys.schemas parent_schema ON parent_table.schema_id = parent_schema.schema_id
            INNER JOIN sys.columns parent_column
              ON fkc.parent_object_id = parent_column.object_id
             AND fkc.parent_column_id = parent_column.column_id
            INNER JOIN sys.tables referenced_table ON fkc.referenced_object_id = referenced_table.object_id
            INNER JOIN sys.schemas referenced_schema ON referenced_table.schema_id = referenced_schema.schema_id
            INNER JOIN sys.columns referenced_column
              ON fkc.referenced_object_id = referenced_column.object_id
             AND fkc.referenced_column_id = referenced_column.column_id
            ORDER BY FromSchema, FromTable, FromColumn;
            """, cancellationToken: ct))).ToList();

        var foreignKeys = relationships
            .Select(r => new KeyRow { Schema = r.FromSchema, Table = r.FromTable, Column = r.FromColumn })
            .ToHashSet(KeyRowComparer.Instance);

        var relationshipColumns = relationships
            .SelectMany(r => new[]
            {
                new KeyRow { Schema = r.FromSchema, Table = r.FromTable, Column = r.FromColumn },
                new KeyRow { Schema = r.ToSchema, Table = r.ToTable, Column = r.ToColumn }
            })
            .ToHashSet(KeyRowComparer.Instance);

        var tableDtos = tables.Select(table => new TableDto
        {
            Schema = table.Schema,
            Table = table.Table,
            TableType = table.TableType,
            Columns = columns
                .Where(column => column.Schema == table.Schema && column.Table == table.Table)
                .Select(column => new ColumnDto
                {
                    Schema = column.Schema,
                    Table = column.Table,
                    Column = column.Column,
                    DataType = column.DataType,
                    SqlDataType = string.IsNullOrWhiteSpace(column.SqlDataType) ? column.DataType : column.SqlDataType,
                    CharacterMaximumLength = column.CharacterMaximumLength,
                    NumericPrecision = column.NumericPrecision,
                    NumericScale = column.NumericScale,
                    DatetimePrecision = column.DatetimePrecision,
                    OrdinalPosition = column.OrdinalPosition,
                    IsNullable = column.IsNullable,
                    IsPrimaryKey = primaryKeys.Contains(new KeyRow { Schema = column.Schema, Table = column.Table, Column = column.Column }),
                    IsForeignKey = foreignKeys.Contains(new KeyRow { Schema = column.Schema, Table = column.Table, Column = column.Column }),
                    ParticipatesInRelationship = relationshipColumns.Contains(new KeyRow { Schema = column.Schema, Table = column.Table, Column = column.Column }),
                    IsUnique = uniqueColumns.Contains(new KeyRow { Schema = column.Schema, Table = column.Table, Column = column.Column }),
                    ReferencedSchema = relationships.FirstOrDefault(r => r.FromSchema.Equals(column.Schema, StringComparison.OrdinalIgnoreCase) && r.FromTable.Equals(column.Table, StringComparison.OrdinalIgnoreCase) && r.FromColumn.Equals(column.Column, StringComparison.OrdinalIgnoreCase))?.ToSchema ?? "",
                    ReferencedTable = relationships.FirstOrDefault(r => r.FromSchema.Equals(column.Schema, StringComparison.OrdinalIgnoreCase) && r.FromTable.Equals(column.Table, StringComparison.OrdinalIgnoreCase) && r.FromColumn.Equals(column.Column, StringComparison.OrdinalIgnoreCase))?.ToTable ?? "",
                    ReferencedColumn = relationships.FirstOrDefault(r => r.FromSchema.Equals(column.Schema, StringComparison.OrdinalIgnoreCase) && r.FromTable.Equals(column.Table, StringComparison.OrdinalIgnoreCase) && r.FromColumn.Equals(column.Column, StringComparison.OrdinalIgnoreCase))?.ToColumn ?? "",
                    ForeignKeyName = relationships.FirstOrDefault(r => r.FromSchema.Equals(column.Schema, StringComparison.OrdinalIgnoreCase) && r.FromTable.Equals(column.Table, StringComparison.OrdinalIgnoreCase) && r.FromColumn.Equals(column.Column, StringComparison.OrdinalIgnoreCase))?.ForeignKeyName ?? ""
                })
                .ToList()
        }).ToList();

        return new DiscoverSchemaResponse
        {
            Database = definition.Database,
            Tables = tableDtos,
            Relationships = relationships
        };
    }

    public async Task<TablePreviewResponse> PreviewTableAsync(
        ConnectionDefinition definition,
        string schema,
        string table,
        int limit,
        CancellationToken ct)
    {
        var discovered = await DiscoverAsync(definition, ct);
        var tableDto = discovered.Tables.FirstOrDefault(t =>
            t.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) &&
            t.Table.Equals(table, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Table '{schema}.{table}' was not found.");

        var safeLimit = Math.Clamp(limit, 1, 100);
        var sql = $"SELECT TOP ({safeLimit}) * FROM {SqlIdentifier.Quote(schema)}.{SqlIdentifier.Quote(table)};";

        await using var connection = new SqlConnection(SqlServerConnectionFactory.BuildConnectionString(definition));
        var rows = await connection.QueryAsync(new CommandDefinition(sql, commandType: CommandType.Text, cancellationToken: ct));

        return new TablePreviewResponse
        {
            Schema = schema,
            Table = table,
            Columns = tableDto.Columns,
            Rows = rows
                .Select(row => ((IDictionary<string, object>)row)
                    .ToDictionary(column => column.Key, column => column.Value is DBNull ? null : column.Value))
                .ToList()
        };
    }

    private sealed class TableRow
    {
        public string Schema { get; init; } = "";
        public string Table { get; init; } = "";
        public string TableType { get; init; } = "";
    }

    private sealed class ColumnRow
    {
        public string Schema { get; init; } = "";
        public string Table { get; init; } = "";
        public string Column { get; init; } = "";
        public string DataType { get; init; } = "";
        public string SqlDataType { get; init; } = "";
        public int? CharacterMaximumLength { get; init; }
        public byte? NumericPrecision { get; init; }
        public int? NumericScale { get; init; }
        public short? DatetimePrecision { get; init; }
        public int OrdinalPosition { get; init; }
        public bool IsNullable { get; init; }
    }

    private sealed class KeyRow
    {
        public string Schema { get; init; } = "";
        public string Table { get; init; } = "";
        public string Column { get; init; } = "";
    }

    private sealed class KeyRowComparer : IEqualityComparer<KeyRow>
    {
        public static readonly KeyRowComparer Instance = new();

        public bool Equals(KeyRow? x, KeyRow? y)
        {
            return x is not null && y is not null &&
                x.Schema.Equals(y.Schema, StringComparison.OrdinalIgnoreCase) &&
                x.Table.Equals(y.Table, StringComparison.OrdinalIgnoreCase) &&
                x.Column.Equals(y.Column, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(KeyRow obj)
        {
            return HashCode.Combine(
                obj.Schema.ToLowerInvariant(),
                obj.Table.ToLowerInvariant(),
                obj.Column.ToLowerInvariant());
        }
    }
}
