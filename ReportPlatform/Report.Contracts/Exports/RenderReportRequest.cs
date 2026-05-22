using System.ComponentModel.DataAnnotations;
using Report.Contracts.Requests;

namespace Report.Contracts.Exports;

public sealed class RenderReportRequest
{
    [Required]
    public string Format { get; init; } = "PDF";

    public string? ReportTitle { get; init; }

    [Required]
    public VisualQueryRequest Query { get; init; } = new();
}
