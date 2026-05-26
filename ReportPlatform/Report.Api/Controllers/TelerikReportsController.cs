using Telerik.Reporting.Services;
using Telerik.Reporting.Services.AspNetCore;

namespace Report.Api.Controllers;

[Route("api/reports")]
public sealed class TelerikReportsController : ReportsControllerBase
{
    public TelerikReportsController(IReportServiceConfiguration reportServiceConfiguration)
        : base(reportServiceConfiguration)
    {
    }
}
