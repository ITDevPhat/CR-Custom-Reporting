using Microsoft.AspNetCore.Mvc;
using Telerik.Reporting.Processing;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/telerik-health")]
public sealed class TelerikHealthController : ControllerBase
{
    [HttpGet("formats")]
    public IActionResult Formats()
    {
        var formats = ReportProcessor.ListRenderingExtensions()
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();

        return Ok(new
        {
            formats,
            csv = "CSV (custom writer, non-Telerik)"
        });
    }

    [HttpGet("pdf")]
    public IActionResult Pdf()
    {
        var available = ReportProcessor.ListRenderingExtensions().Any(x => string.Equals(x.Name, "PDF", StringComparison.OrdinalIgnoreCase));
        return available ? Ok(new { available = true }) : StatusCode(500, new { available = false, message = "Telerik rendering extension 'PDF' is not available." });
    }
}
