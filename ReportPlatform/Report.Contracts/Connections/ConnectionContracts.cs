using Report.Contracts.Metadata;

namespace Report.Contracts.Connections;

public class CreateConnectionRequest
{
    public string Provider { get; init; } = "sqlserver";
    public string Server { get; init; } = "";
    public string Database { get; init; } = "";
    public string AuthenticationType { get; init; } = "sql";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public bool TrustServerCertificate { get; init; } = true;
    public bool Encrypt { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;
}

public sealed class TestConnectionRequest : CreateConnectionRequest;

public sealed class ConnectionDto
{
    public string ConnectionId { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Server { get; init; } = "";
    public string Database { get; init; } = "";
    public string AuthenticationType { get; init; } = "";
    public bool TrustServerCertificate { get; init; }
    public bool Encrypt { get; init; }
    public int CommandTimeoutSeconds { get; init; }
}

public sealed class ConnectionTestResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public ConnectionDto? Connection { get; init; }
}

public sealed class DatabaseDto
{
    public string Name { get; init; } = "";
}

public sealed class TableDto
{
    public string Schema { get; init; } = "";
    public string Table { get; init; } = "";
    public string TableType { get; init; } = "";
    public List<ColumnDto> Columns { get; init; } = [];
}

public sealed class ColumnDto
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
    public bool IsPrimaryKey { get; init; }
    public bool IsForeignKey { get; init; }
    public bool ParticipatesInRelationship { get; init; }
    public bool IsUnique { get; init; }
    public string ReferencedSchema { get; init; } = "";
    public string ReferencedTable { get; init; } = "";
    public string ReferencedColumn { get; init; } = "";
    public string ForeignKeyName { get; init; } = "";
}

public sealed class RelationshipDiscoveryDto
{
    public string ForeignKeyName { get; init; } = "";
    public string FromSchema { get; init; } = "";
    public string FromTable { get; init; } = "";
    public string FromColumn { get; init; } = "";
    public string ToSchema { get; init; } = "";
    public string ToTable { get; init; } = "";
    public string ToColumn { get; init; } = "";
}

public sealed class DiscoverSchemaResponse
{
    public string Database { get; init; } = "";
    public List<TableDto> Tables { get; init; } = [];
    public List<RelationshipDiscoveryDto> Relationships { get; init; } = [];
}

public sealed class PreviewTableRequest
{
    public CreateConnectionRequest Connection { get; init; } = new();
    public string Schema { get; init; } = "";
    public string Table { get; init; } = "";
    public int Limit { get; init; } = 20;
}

public sealed class TablePreviewResponse
{
    public string Schema { get; init; } = "";
    public string Table { get; init; } = "";
    public List<ColumnDto> Columns { get; init; } = [];
    public List<Dictionary<string, object?>> Rows { get; init; } = [];
}

public sealed class RegisterDatasetRequest
{
    public string DatasetName { get; init; } = "";
    public CreateConnectionRequest Connection { get; init; } = new();
    public List<SelectedTableDto> SelectedTables { get; init; } = [];
}

public sealed class SelectedTableDto
{
    public string Schema { get; init; } = "";
    public string Table { get; init; } = "";
}

public sealed class RegisterDatasetResponse
{
    public string DatasetId { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public DatasetMetadataResponse Metadata { get; init; } = new();
    public List<string> Warnings { get; init; } = [];
    public List<MetadataConsistencyDto> Consistency { get; init; } = [];
    public List<MetadataFieldDebugDto> DebugFields { get; init; } = [];
}

public sealed class MetadataConsistencyDto
{
    public string TableId { get; init; } = "";
    public int PhysicalColumnCount { get; init; }
    public int RegisteredFieldCount { get; init; }
    public List<string> MissingColumns { get; init; } = [];
}

public sealed class MetadataFieldDebugDto
{
    public string FieldId { get; init; } = "";
    public string PhysicalColumn { get; init; } = "";
    public string SqlDataType { get; init; } = "";
    public string Role { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public bool IsPrimaryKey { get; init; }
    public bool IsForeignKey { get; init; }
    public bool ParticipatesInRelationship { get; init; }
    public bool IsDraggable { get; init; }
    public string ClassificationReason { get; init; } = "";
}
