using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public interface ITelerikReportFactory
{
    Telerik.Reporting.Report CreateSqlBackedTableReport(CompiledReportQuery compiled, string connectionString, string? title);
}
