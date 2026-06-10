using System.Data;
using Report.Contracts.Artifacts;

namespace Report.Api.Rendering;

public interface ITelerikSnapshotReportFactory
{
    Telerik.Reporting.Report CreateSnapshotBackedTableReport(
        DataTable dataTable,
        string? title,
        ReportExecutionRecord execution,
        ReportExecutionArtifactHeader? header = null,
        SnapshotReportBuildOptions? options = null);
}

public sealed record SnapshotReportBuildOptions(
    int? MaxRows = null,
    string Mode = "Preview");
