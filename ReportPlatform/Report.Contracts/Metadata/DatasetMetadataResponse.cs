namespace Report.Contracts.Metadata;

public sealed class DatasetMetadataResponse
{
    public string DatasetId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public List<MetadataTableDto> Tables { get; init; } = [];
    public List<MetadataMetricDto> Metrics { get; init; } = [];
    public List<MetadataRelationshipDto> Relationships { get; init; } = [];
}

public sealed class MetadataTableDto
{
    public string TableId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string TableType { get; init; } = "";
    public string Grain { get; init; } = "";
    public List<MetadataFieldDto> Fields { get; init; } = [];
}

public sealed class MetadataFieldDto
{
    public string FieldId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string TableId { get; init; } = "";
    public string PhysicalSchema { get; init; } = "";
    public string PhysicalTable { get; init; } = "";
    public string PhysicalColumn { get; init; } = "";
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
    public string DataType { get; init; } = "";
    public string SqlDataType { get; init; } = "";
    public int? CharacterMaximumLength { get; init; }
    public byte? NumericPrecision { get; init; }
    public int? NumericScale { get; init; }
    public short? DatetimePrecision { get; init; }
    public string Role { get; init; } = "";
    public string Grain { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public string DefaultAggregation { get; init; } = "";
    public string Format { get; init; } = "";
    public string? Expression { get; init; }
    public string? BaseTableId { get; init; }
    public bool IsDerived { get; init; }
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
    public string ClassificationReason { get; init; } = "";
}

public sealed class MetadataMetricDto
{
    public string MetricId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string BaseTableId { get; init; } = "";
    public string Formula { get; init; } = "";
    public string AggregationBehavior { get; init; } = "";
    public string DataType { get; init; } = "";
    public string Format { get; init; } = "";
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
}

public class MetadataRelationshipDto
{
    public string RelationshipId { get; init; } = "";
    public string DatasetId { get; init; } = "";
    public string FromTableId { get; init; } = "";
    public string FromColumn { get; init; } = "";
    public string ToTableId { get; init; } = "";
    public string ToColumn { get; init; } = "";
    public string JoinType { get; init; } = "";
    public string Cardinality { get; init; } = "";
    public string CrossFilterDirection { get; init; } = "";
    public bool IsActive { get; init; }
    public bool IsPrimary { get; init; }
    public string Source { get; init; } = "";
    public decimal Confidence { get; init; }
    public string Status { get; init; } = "";
    public string? Warning { get; init; }
}
