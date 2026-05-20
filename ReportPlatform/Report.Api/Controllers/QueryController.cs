using Microsoft.AspNetCore.Mvc;
using Report.Contracts.Requests;
using Report.QueryEngine.Services;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/query")]
public sealed class QueryController : ControllerBase
{
    private readonly ReportQueryService _service;

    public QueryController(ReportQueryService service)
    {
        _service = service;
    }

    [HttpPost("compile")]
    public async Task<IActionResult> Compile(
        [FromBody] VisualQueryRequest request,
        CancellationToken ct)
    {
        var result = await _service.CompileAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] VisualQueryRequest request,
        CancellationToken ct)
    {
        var result = await _service.ExecuteAsync(request, ct);
        return Ok(result);
    }
}
