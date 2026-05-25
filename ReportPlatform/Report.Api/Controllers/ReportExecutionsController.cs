using Microsoft.AspNetCore.Mvc;
using Report.Contracts.Artifacts;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/report-executions")]
public sealed class ReportExecutionsController : ControllerBase
{
    private readonly IReportExecutionRepository _repository;

    public ReportExecutionsController(IReportExecutionRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? storageMode,
        [FromQuery] string? search,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken ct)
    {
        var executions = await _repository.ListAsync(new ReportExecutionListQuery
        {
            Status = status,
            StorageMode = storageMode,
            Search = search,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Limit = limit ?? 100,
            Offset = offset ?? 0
        }, ct);

        return Ok(new
        {
            executions = executions.Select(ToSummary),
            isMockData = false
        });
    }

    [HttpGet("{executionId}")]
    public async Task<IActionResult> Get(string executionId, CancellationToken ct)
    {
        var execution = await _repository.GetAsync(executionId, ct);
        return execution is null ? NotFound(new { message = "Execution not found." }) : Ok(ToDetail(execution));
    }

    private static object ToSummary(ReportExecutionRecord record) => new
    {
        record.ExecutionId,
        record.ReportId,
        record.ReportName,
        record.TemplateId,
        record.Status,
        record.RowCount,
        record.ArtifactKey,
        record.ArtifactAvailable,
        record.StorageMode,
        record.CreatedAtUtc,
        record.CompletedAtUtc,
        record.DurationMs,
        record.ErrorMessage,
        record.QueryFingerprint,
        record.SemanticModelVersion
    };

    private static object ToDetail(ReportExecutionRecord record) => new
    {
        record.ExecutionId,
        record.ReportId,
        record.ReportName,
        record.TemplateId,
        record.Status,
        record.RowCount,
        record.ArtifactKey,
        record.ArtifactAvailable,
        record.StorageMode,
        record.QueryFingerprint,
        record.SemanticModelVersion,
        record.CompiledSql,
        record.CreatedAtUtc,
        record.StartedAtUtc,
        record.CompletedAtUtc,
        record.FailedAtUtc,
        record.DurationMs,
        record.ErrorMessage
    };
}
