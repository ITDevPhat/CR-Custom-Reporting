using Report.Contracts.Metadata;

namespace Report.Contracts.Relationships;

public sealed class RelationshipDto : MetadataRelationshipDto;

public class CreateRelationshipRequest
{
    public string FromTableId { get; init; } = "";
    public string FromColumn { get; init; } = "";
    public string ToTableId { get; init; } = "";
    public string ToColumn { get; init; } = "";
    public string Cardinality { get; init; } = "N:1";
    public string JoinType { get; init; } = "INNER";
    public string CrossFilterDirection { get; init; } = "single";
    public bool IsActive { get; init; } = true;
    public bool IsPrimary { get; init; } = true;
}

public sealed class UpdateRelationshipRequest : CreateRelationshipRequest
{
    public string RelationshipId { get; init; } = "";
}

public sealed class AutodetectRelationshipsRequest
{
    public string DatasetId { get; init; } = "";
    public string Mode { get; init; } = "safe";
    public bool IncludeExisting { get; init; }
}

public sealed class AutodetectRelationshipsResponse
{
    public List<RelationshipDto> Relationships { get; init; } = [];
    public AutodetectRelationshipsSummary Summary { get; init; } = new();
}

public sealed class AutodetectRelationshipsSummary
{
    public int Detected { get; init; }
    public int DatabaseForeignKeys { get; init; }
    public int InferredByName { get; init; }
    public int SkippedExisting { get; init; }
    public List<string> Warnings { get; init; } = [];
}
