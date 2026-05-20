using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Semantic;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets/{datasetId}/metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly SemanticModelMutationService _service;
    public MetricsController(SemanticModelMutationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(string datasetId, CancellationToken ct) => Ok(await _service.GetMetricsAsync(datasetId, ct));

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(string datasetId, [FromBody] MetricRequest request, CancellationToken ct) => Ok(await _service.ValidateMetricAsync(datasetId, request, ct));

    [HttpPost]
    public async Task<IActionResult> Create(string datasetId, [FromBody] MetricRequest request, CancellationToken ct) => Ok(await _service.CreateMetricAsync(datasetId, request, ct));

    [HttpPut("{metricId}")]
    public async Task<IActionResult> Update(string datasetId, string metricId, [FromBody] MetricRequest request, CancellationToken ct) => Ok(await _service.UpdateMetricAsync(datasetId, metricId, request, ct));

    [HttpDelete("{metricId}")]
    public async Task<IActionResult> Delete(string datasetId, string metricId, CancellationToken ct)
    {
        await _service.DeleteMetricAsync(datasetId, metricId, ct);
        return NoContent();
    }
}
