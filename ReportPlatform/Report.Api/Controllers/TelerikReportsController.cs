

namespace Report.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Telerik.Reporting.Services;
using Telerik.Reporting.Services.AspNetCore;
[Route("api/reports")]
public sealed class TelerikReportsController : ReportsControllerBase
{
    public TelerikReportsController(IReportServiceConfiguration reportServiceConfiguration)
        : base(reportServiceConfiguration)
    {
    }
}
