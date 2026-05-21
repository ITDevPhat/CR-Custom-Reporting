using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Relationships;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets/{datasetId}/relationships")]
public sealed class RelationshipsController : ControllerBase
{
    private readonly DatasetRelationshipService _service;

    public RelationshipsController(DatasetRelationshipService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(string datasetId, CancellationToken ct) =>
        Ok(await _service.ListAsync(datasetId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(string datasetId, [FromBody] CreateRelationshipRequest request, CancellationToken ct) =>
        Ok(await _service.CreateAsync(datasetId, request, ct));

    [HttpPut("{relationshipId}")]
    public async Task<IActionResult> Update(string datasetId, string relationshipId, [FromBody] UpdateRelationshipRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(datasetId, relationshipId, request, ct));

    [HttpDelete("{relationshipId}")]
    public async Task<IActionResult> Delete(string datasetId, string relationshipId, CancellationToken ct)
    {
        await _service.DeleteAsync(datasetId, relationshipId, ct);
        return NoContent();
    }

    [HttpPost("autodetect")]
    public async Task<IActionResult> Autodetect(string datasetId, [FromBody] AutodetectRelationshipsRequest request, CancellationToken ct) =>
        Ok(await _service.AutodetectAsync(datasetId, request, ct));

    [HttpPost("{relationshipId}/activate")]
    public async Task<IActionResult> Activate(string datasetId, string relationshipId, CancellationToken ct) =>
        Ok(await _service.ActivateAsync(datasetId, relationshipId, ct));
}
