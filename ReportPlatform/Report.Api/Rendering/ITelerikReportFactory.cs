using Report.Contracts.Results;
using Telerik.Reporting;

namespace Report.Api.Rendering;

public interface ITelerikReportFactory
{
    Report CreateTableReport(QueryResult result, string? title);
}
