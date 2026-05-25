using Microsoft.AspNetCore.Mvc;
using Report.Api.Rendering;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/report-executions")]
public sealed class ReportExecutionExportsController : ControllerBase
{
    private readonly TelerikArtifactRenderService _service;
    public ReportExecutionExportsController(TelerikArtifactRenderService service){_service=service;}
    [HttpGet("{executionId}/export/{format}")]
    public async Task<IActionResult> Export(string executionId, string format, CancellationToken ct)
    {
        var result = await _service.RenderByExecutionAsync(executionId, format, ct);
        if (!result.Ok) return StatusCode(result.Status, new { code = result.Code, message = result.Error });
        return File(result.Result!.Bytes, result.Result.ContentType, result.Result.FileName);
    }
}
