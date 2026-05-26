using Microsoft.AspNetCore.Mvc;
using Report.Contracts.Artifacts;

namespace Report.Api.Controllers;

[ApiController]
[Route("api/report-executions")]
public sealed class ReportExecutionsController : ControllerBase
{
    private readonly IReportExecutionRepository _repository;
    private readonly IReportArtifactStore _artifactStore;

    public ReportExecutionsController(IReportExecutionRepository repository, IReportArtifactStore artifactStore)
    {
        _repository = repository;
        _artifactStore = artifactStore;
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

    [HttpGet("{executionId}/preview-reference")]
    public async Task<IActionResult> GetPreviewReference(string executionId, CancellationToken ct)
    {
        var execution = await _repository.GetAsync(executionId, ct);
        if (execution is null) return NotFound(new { message = "Execution not found." });
        if (!string.Equals(execution.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Execution is not completed." });
        }
        if (string.IsNullOrWhiteSpace(execution.ArtifactKey))
        {
            return BadRequest(new { message = "Artifact key is missing." });
        }
        if (!await _artifactStore.ExistsAsync(execution.ArtifactKey, ct))
        {
            await _repository.MarkArtifactMissingAsync(executionId, ct);
            return NotFound(new { message = "Artifact is not available for preview." });
        }

        return Ok(new
        {
            executionId = execution.ExecutionId,
            reportSource = $"execution:{execution.ExecutionId}",
            viewerUrl = $"/report-preview/{execution.ExecutionId}",
            status = execution.Status,
            artifactAvailable = true
        });
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
