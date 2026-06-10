using Microsoft.AspNetCore.Mvc;
using Report.Contracts.Semantic;
using Report.Metadata.Stores;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportRegistry _reports;
    public ReportsController(IReportRegistry reports) => _reports = reports;

    [HttpPost]
    public IActionResult Create([FromBody] SaveReportDefinitionRequest request) => Ok(_reports.Save(request));

    [HttpGet("{reportId}")]
    public IActionResult Get(string reportId) => _reports.Find(reportId) is { } report ? Ok(report) : NotFound();

    [HttpPut("{reportId}")]
    public IActionResult Update(string reportId, [FromBody] SaveReportDefinitionRequest request) => Ok(_reports.Save(request, reportId));

    [HttpGet]
    public IActionResult List([FromQuery] string? datasetId) => Ok(_reports.List(datasetId));

    [HttpDelete("{reportId}")]
    public IActionResult Delete(string reportId) => _reports.Delete(reportId) ? NoContent() : NotFound();
}
