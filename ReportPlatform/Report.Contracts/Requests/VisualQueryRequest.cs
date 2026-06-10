using System.ComponentModel.DataAnnotations;

namespace Report.Contracts.Requests;

public sealed class VisualQueryRequest
{
    public string ConnectionId { get; init; } = "";

    [Required]
    [MinLength(1)]
    public string DatasetId { get; init; } = "";

    public string ReportId { get; init; } = "";
    public string VisualType { get; init; } = "table";

    public List<string> Rows { get; init; } = [];
    public List<string> Columns { get; init; } = [];
    public List<string> Values { get; init; } = [];
    public List<FilterRequest> Filters { get; init; } = [];
    public List<SortRequest> Sort { get; init; } = [];

    [Range(0, 1000)]
    public int Limit { get; init; } = 100;

    [Range(0, int.MaxValue)]
    public int Offset { get; init; } = 0;
}

public sealed class FilterRequest
{
    public string Field { get; init; } = "";
    public string Operator { get; init; } = "";
    public object? Value { get; init; }
    public string Scope { get; init; } = "visual";
}

public sealed class SortRequest
{
    public string Field { get; init; } = "";
    public string Direction { get; init; } = "ASC";
}
