using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Semantic;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets/{datasetId}/calculated-objects")]
public sealed class CalculatedObjectsController(SemanticModelMutationService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        string datasetId,
        [FromBody] CreateCalculatedObjectRequest request,
        CancellationToken ct) =>
        Ok(await service.CreateCalculatedObjectAsync(datasetId, request, ct));
}
