using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Semantic;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets/{datasetId}/fields")]
public sealed class FieldsController : ControllerBase
{
    private readonly SemanticModelMutationService _service;
    public FieldsController(SemanticModelMutationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(string datasetId, CancellationToken ct) => Ok(await _service.GetFieldsAsync(datasetId, ct));

    [HttpPut("{fieldId}")]
    public async Task<IActionResult> Update(string datasetId, string fieldId, [FromBody] UpdateFieldRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateFieldAsync(datasetId, fieldId, request, ct));
}
