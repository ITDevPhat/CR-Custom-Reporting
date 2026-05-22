using Report.Contracts.Results;

namespace Report.Api.Rendering;

public interface ITelerikReportFactory
{
    global::Telerik.Reporting.Report CreateTableReport(QueryResult result, string? title);
}
