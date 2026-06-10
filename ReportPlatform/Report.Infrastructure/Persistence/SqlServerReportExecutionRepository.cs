using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Report.Contracts.Artifacts;

namespace Report.Infrastructure.Persistence;

public sealed class SqlServerReportExecutionRepository : IReportExecutionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerReportExecutionRepository> _logger;

    public SqlServerReportExecutionRepository(
        string connectionString,
        ILogger<SqlServerReportExecutionRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task UpsertAsync(ReportExecutionRecord record, CancellationToken ct)
    {
        const string sql = """
MERGE dbo.ReportExecutions AS target
USING (SELECT @ExecutionId AS ExecutionId) AS source
ON target.ExecutionId = source.ExecutionId
WHEN MATCHED THEN UPDATE SET
    ReportId = @ReportId,
    ReportName = @ReportName,
    TemplateId = @TemplateId,
    Status = @Status,
    RowCount = @RowCount,
    ArtifactKey = @ArtifactKey,
    ArtifactAvailable = @ArtifactAvailable,
    StorageMode = @StorageMode,
    QueryFingerprint = @QueryFingerprint,
    SemanticModelVersion = @SemanticModelVersion,
    CompiledSql = @CompiledSql,
    CreatedAtUtc = @CreatedAtUtc,
    StartedAtUtc = @StartedAtUtc,
    CompletedAtUtc = @CompletedAtUtc,
    FailedAtUtc = @FailedAtUtc,
    DurationMs = @DurationMs,
    ErrorMessage = @ErrorMessage
WHEN NOT MATCHED THEN INSERT (
    ExecutionId, ReportId, ReportName, TemplateId, Status, RowCount, ArtifactKey,
    ArtifactAvailable, StorageMode, QueryFingerprint, SemanticModelVersion, CompiledSql,
    CreatedAtUtc, StartedAtUtc, CompletedAtUtc, FailedAtUtc, DurationMs, ErrorMessage)
VALUES (
    @ExecutionId, @ReportId, @ReportName, @TemplateId, @Status, @RowCount, @ArtifactKey,
    @ArtifactAvailable, @StorageMode, @QueryFingerprint, @SemanticModelVersion, @CompiledSql,
    @CreatedAtUtc, @StartedAtUtc, @CompletedAtUtc, @FailedAtUtc, @DurationMs, @ErrorMessage);
""";

        await ExecuteAsync(sql, record, ct);
    }

    public async Task<ReportExecutionRecord?> GetAsync(string executionId, CancellationToken ct)
    {
        const string sql = """
SELECT ExecutionId, ReportId, ReportName, TemplateId, Status, RowCount, ArtifactKey,
       ArtifactAvailable, StorageMode, QueryFingerprint, SemanticModelVersion, CompiledSql,
       CreatedAtUtc, StartedAtUtc, CompletedAtUtc, FailedAtUtc, DurationMs, ErrorMessage
FROM dbo.ReportExecutions
WHERE ExecutionId = @executionId;
""";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<ReportExecutionRecord>(
            new CommandDefinition(sql, new { executionId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ReportExecutionRecord>> ListAsync(ReportExecutionListQuery query, CancellationToken ct)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            where.Add("Status = @Status");
            parameters.Add("Status", query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.StorageMode))
        {
            where.Add("StorageMode = @StorageMode");
            parameters.Add("StorageMode", query.StorageMode);
        }

        if (query.FromUtc is not null)
        {
            where.Add("CreatedAtUtc >= @FromUtc");
            parameters.Add("FromUtc", query.FromUtc.Value);
        }

        if (query.ToUtc is not null)
        {
            where.Add("CreatedAtUtc <= @ToUtc");
            parameters.Add("ToUtc", query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("(ExecutionId LIKE @Search OR ReportId LIKE @Search OR ReportName LIKE @Search)");
            parameters.Add("Search", $"%{query.Search.Trim()}%");
        }

        parameters.Add("Offset", Math.Max(0, query.Offset));
        parameters.Add("Limit", query.Limit is > 0 and <= 500 ? query.Limit : 100);

        var sql = $"""
SELECT ExecutionId, ReportId, ReportName, TemplateId, Status, RowCount, ArtifactKey,
       ArtifactAvailable, StorageMode, QueryFingerprint, SemanticModelVersion, CompiledSql,
       CreatedAtUtc, StartedAtUtc, CompletedAtUtc, FailedAtUtc, DurationMs, ErrorMessage
FROM dbo.ReportExecutions
{(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
ORDER BY CreatedAtUtc DESC
OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;
""";

        await using var connection = new SqlConnection(_connectionString);
        var records = await connection.QueryAsync<ReportExecutionRecord>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));
        return records.ToList();
    }

    public Task MarkProcessingAsync(string executionId, DateTime startedAtUtc, CancellationToken ct)
        => ExecuteAsync(
            "UPDATE dbo.ReportExecutions SET Status = 'Processing', StartedAtUtc = @startedAtUtc WHERE ExecutionId = @executionId;",
            new { executionId, startedAtUtc },
            ct);

    public Task MarkCompletedAsync(string executionId, int rowCount, string artifactKey, long durationMs, CancellationToken ct)
        => ExecuteAsync(
            """
UPDATE dbo.ReportExecutions
SET Status = 'Completed',
    RowCount = @rowCount,
    ArtifactKey = @artifactKey,
    ArtifactAvailable = 1,
    CompletedAtUtc = SYSUTCDATETIME(),
    DurationMs = @durationMs,
    ErrorMessage = NULL
WHERE ExecutionId = @executionId;
""",
            new { executionId, rowCount, artifactKey, durationMs },
            ct);

    public Task MarkFailedAsync(string executionId, string errorMessage, CancellationToken ct)
        => ExecuteAsync(
            """
UPDATE dbo.ReportExecutions
SET Status = 'Failed',
    FailedAtUtc = SYSUTCDATETIME(),
    ErrorMessage = @errorMessage
WHERE ExecutionId = @executionId;
""",
            new { executionId, errorMessage },
            ct);

    public Task MarkArtifactMissingAsync(string executionId, CancellationToken ct)
        => ExecuteAsync(
            """
UPDATE dbo.ReportExecutions
SET Status = 'ArtifactMissing',
    ArtifactAvailable = 0,
    ErrorMessage = 'Artifact file is missing.'
WHERE ExecutionId = @executionId;
""",
            new { executionId },
            ct);

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        var script = SqlScripts.CreateReportExecutions;
        await ExecuteAsync(script, new { }, ct);
        _logger.LogInformation("ReportExecutions schema is ready.");
    }

    private async Task ExecuteAsync(string sql, object parameters, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, commandType: CommandType.Text, cancellationToken: ct));
    }
}

internal static class SqlScripts
{
    public const string CreateReportExecutions = """
IF OBJECT_ID(N'dbo.ReportExecutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportExecutions
    (
        ExecutionId nvarchar(64) NOT NULL CONSTRAINT PK_ReportExecutions PRIMARY KEY,
        ReportId nvarchar(128) NOT NULL,
        ReportName nvarchar(256) NULL,
        TemplateId nvarchar(128) NULL,
        Status nvarchar(32) NOT NULL,
        RowCount int NULL,
        ArtifactKey nvarchar(1024) NULL,
        ArtifactAvailable bit NOT NULL CONSTRAINT DF_ReportExecutions_ArtifactAvailable DEFAULT 0,
        StorageMode nvarchar(32) NOT NULL,
        QueryFingerprint nvarchar(128) NULL,
        SemanticModelVersion nvarchar(64) NULL,
        CompiledSql nvarchar(max) NULL,
        CreatedAtUtc datetime2 NOT NULL,
        StartedAtUtc datetime2 NULL,
        CompletedAtUtc datetime2 NULL,
        FailedAtUtc datetime2 NULL,
        DurationMs bigint NULL,
        ErrorMessage nvarchar(max) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_CreatedAtUtc ON dbo.ReportExecutions (CreatedAtUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_Status' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_Status ON dbo.ReportExecutions (Status);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_QueryFingerprint' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_QueryFingerprint ON dbo.ReportExecutions (QueryFingerprint);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReportExecutions_ReportId' AND object_id = OBJECT_ID(N'dbo.ReportExecutions'))
    CREATE INDEX IX_ReportExecutions_ReportId ON dbo.ReportExecutions (ReportId);
""";
}
