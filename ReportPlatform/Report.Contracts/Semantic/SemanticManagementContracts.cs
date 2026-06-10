using Report.Contracts.Requests;

namespace Report.Contracts.Semantic;

public sealed class UpdateFieldRequest
{
    public string DisplayName { get; init; } = "";
    public string Role { get; init; } = "";
    public string SemanticType { get; init; } = "";
    public string DefaultAggregation { get; init; } = "none";
    public string Format { get; init; } = "general";
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
    public string Grain { get; init; } = "";
}

public sealed class MetricRequest
{
    public string DisplayName { get; init; } = "";
    public string Formula { get; init; } = "";
    public string BaseTableId { get; init; } = "";
    public string AggregationBehavior { get; init; } = "additive";
    public string DataType { get; init; } = "decimal";
    public string Format { get; init; } = "general";
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
}

public sealed class DerivedFieldRequest
{
    public string DisplayName { get; init; } = "";
    public string BaseTableId { get; init; } = "";
    public string Expression { get; init; } = "";
    public string DataType { get; init; } = "nvarchar";
    public string SemanticType { get; init; } = "category";
    public string Format { get; init; } = "general";
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
}

public sealed class ValidationResponse
{
    public bool Valid { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<ValidationMessage> Details { get; init; } = [];
}

public sealed class GrainValidationResult
{
    public bool Valid { get; init; } = true;
    public List<ValidationMessage> Errors { get; init; } = [];
    public List<ValidationMessage> Warnings { get; init; } = [];
}

public sealed class ValidationMessage
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class ReportDefinition
{
    public string ReportId { get; init; } = "";
    public string DatasetId { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string VisualType { get; init; } = "table";
    public List<string> Rows { get; init; } = [];
    public List<string> Columns { get; init; } = [];
    public List<string> Values { get; init; } = [];
    public List<FilterRequest> Filters { get; init; } = [];
    public List<SortRequest> Sort { get; init; } = [];
    public int Limit { get; init; } = 100;
    public int Offset { get; init; }
    public Dictionary<string, object?> Layout { get; init; } = [];
    public string SemanticModelVersion { get; init; } = "v1";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class SaveReportDefinitionRequest
{
    public string DatasetId { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string VisualType { get; init; } = "table";
    public List<string> Rows { get; init; } = [];
    public List<string> Columns { get; init; } = [];
    public List<string> Values { get; init; } = [];
    public List<FilterRequest> Filters { get; init; } = [];
    public List<SortRequest> Sort { get; init; } = [];
    public int Limit { get; init; } = 100;
    public int Offset { get; init; }
    public Dictionary<string, object?> Layout { get; init; } = [];
    public string SemanticModelVersion { get; init; } = "v1";
}
