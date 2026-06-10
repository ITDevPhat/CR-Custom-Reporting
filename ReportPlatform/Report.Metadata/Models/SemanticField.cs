namespace Report.Metadata.Models;

public sealed class SemanticField
{
    public string FieldId { get; init; } = "";
    public string DatasetId { get; init; } = "";
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
    public string DisplayName { get; init; } = "";
    public string DataType { get; init; } = "";
    public string SqlDataType { get; init; } = "";
    public int? CharacterMaximumLength { get; init; }
    public byte? NumericPrecision { get; init; }
    public int? NumericScale { get; init; }
    public short? DatetimePrecision { get; init; }
    public string Role { get; init; } = ""; // dimension | key | measure_candidate
    public string Grain { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public string DefaultAggregation { get; init; } = "none";
    public string Format { get; init; } = "general";
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
    public string ClassificationReason { get; init; } = "";
    public string? Expression { get; init; }
    public string? BaseTableId { get; init; }
    public bool IsDerived { get; init; }
}
