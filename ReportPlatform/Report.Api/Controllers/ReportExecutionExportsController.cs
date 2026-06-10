using Microsoft.AspNetCore.Mvc;
using Report.Api.Rendering;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/report-executions")]
public sealed class ReportExecutionExportsController : ControllerBase
{
    private readonly IExecutionReportExportService _exportService;

    public ReportExecutionExportsController(IExecutionReportExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet("{executionId}/export/{format}")]
    public async Task<IActionResult> Export(string executionId, string format, CancellationToken ct)
    {
        try
        {
            var result = await _exportService.ExportAsync(executionId, format, ct);
            return File(result.Bytes, result.ContentType, result.FileName);
        }
        catch (ReportExportException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
