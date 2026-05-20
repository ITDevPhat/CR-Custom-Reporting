using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Semantic;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets/{datasetId}/derived-fields")]
public sealed class DerivedFieldsController : ControllerBase
{
    private readonly SemanticModelMutationService _service;
    public DerivedFieldsController(SemanticModelMutationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(string datasetId, CancellationToken ct) => Ok(await _service.GetDerivedFieldsAsync(datasetId, ct));

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(string datasetId, [FromBody] DerivedFieldRequest request, CancellationToken ct) => Ok(await _service.ValidateDerivedAsync(datasetId, request, ct));

    [HttpPost]
    public async Task<IActionResult> Create(string datasetId, [FromBody] DerivedFieldRequest request, CancellationToken ct) => Ok(await _service.CreateDerivedAsync(datasetId, request, ct));

    [HttpDelete("{fieldId}")]
    public async Task<IActionResult> Delete(string datasetId, string fieldId, CancellationToken ct)
    {
        await _service.DeleteDerivedAsync(datasetId, fieldId, ct);
        return NoContent();
    }
}
