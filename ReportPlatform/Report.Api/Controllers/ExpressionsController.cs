using Microsoft.AspNetCore.Mvc;
using Report.Api.Services;
using Report.Contracts.Semantic;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/datasets/{datasetId}/expressions")]
public sealed class ExpressionsController : ControllerBase
{
    private readonly SemanticModelMutationService _mutationService;
    private readonly ExpressionValidationService _validator;
    public ExpressionsController(SemanticModelMutationService mutationService, ExpressionValidationService validator)
    {
        _mutationService = mutationService;
        _validator = validator;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(string datasetId, [FromBody] ExpressionValidationRequest request, CancellationToken ct)
    {
        var model = await _mutationService.LoadAsync(datasetId, ct);
        return Ok(_validator.Validate(model, request));
    }
}
