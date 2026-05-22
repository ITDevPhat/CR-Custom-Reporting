using Microsoft.AspNetCore.Mvc;
using Report.Api.Rendering;
using Report.Contracts.Exports;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/report-exports")]
public sealed class ReportExportsController : ControllerBase
{
    private readonly IReportRenderService _reportRenderService;

    public ReportExportsController(IReportRenderService reportRenderService)
    {
        _reportRenderService = reportRenderService;
    }

    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] RenderReportRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _reportRenderService.RenderAsync(request, ct);
            return File(result.Bytes, result.ContentType, result.FileName);
        }
        catch (ReportExportException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
