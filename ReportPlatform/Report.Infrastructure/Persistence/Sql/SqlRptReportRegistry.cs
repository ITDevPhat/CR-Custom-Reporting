using System.Text.Json;
using Dapper;
using Report.Contracts.Requests;
using Report.Contracts.Semantic;
using Report.Metadata.Stores;

namespace Report.Infrastructure.Persistence.Sql;

public sealed class SqlRptReportRegistry : IReportRegistry
{
    private readonly IRptCatalogConnectionFactory _connectionFactory;
    private readonly RptAuditService _audit;

    public SqlRptReportRegistry(IRptCatalogConnectionFactory connectionFactory, RptAuditService audit)
    {
        _connectionFactory = connectionFactory;
        _audit = audit;
    }

    public ReportDefinition Save(SaveReportDefinitionRequest request, string? reportId = null)
    {
        var id = string.IsNullOrWhiteSpace(reportId) ? $"rpt_{Guid.NewGuid():N}" : reportId;
        var report = SaveAsync(request, id, CancellationToken.None).GetAwaiter().GetResult();
        return report;
    }

    public ReportDefinition? Find(string reportId)
        => FindAsync(reportId, CancellationToken.None).GetAwaiter().GetResult();

    public List<ReportDefinition> List(string? datasetId)
        => ListAsync(datasetId, CancellationToken.None).GetAwaiter().GetResult();

    public bool Delete(string reportId)
        => DeleteAsync(reportId, CancellationToken.None).GetAwaiter().GetResult();

    private async Task<ReportDefinition> SaveAsync(SaveReportDefinitionRequest request, string reportId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE rpt.ReportDefinitions AS target
            USING (SELECT @ReportId AS ReportId) AS source
                ON target.ReportId = source.ReportId
            WHEN MATCHED THEN UPDATE SET
                DatasetId = @DatasetId,
                ConnectionId = @ConnectionId,
                Title = @Title,
                Description = @Description,
                VisualType = @VisualType,
                RowsJson = @RowsJson,
                ColumnsJson = @ColumnsJson,
                ValuesJson = @ValuesJson,
                FiltersJson = @FiltersJson,
                SortJson = @SortJson,
                LimitRows = @LimitRows,
                OffsetRows = @OffsetRows,
                LayoutJson = @LayoutJson,
                SemanticModelVersion = @SemanticModelVersion,
                IsActive = 1,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (ReportId, DatasetId, ConnectionId, Title, Description, VisualType, RowsJson, ColumnsJson,
                 ValuesJson, FiltersJson, SortJson, LimitRows, OffsetRows, LayoutJson, SemanticModelVersion,
                 IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@ReportId, @DatasetId, @ConnectionId, @Title, @Description, @VisualType, @RowsJson, @ColumnsJson,
                 @ValuesJson, @FiltersJson, @SortJson, @LimitRows, @OffsetRows, @LayoutJson, @SemanticModelVersion,
                 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            new
            {
                ReportId = reportId,
                request.DatasetId,
                request.ConnectionId,
                request.Title,
                request.Description,
                request.VisualType,
                RowsJson = JsonSerializer.Serialize(request.Rows),
                ColumnsJson = JsonSerializer.Serialize(request.Columns),
                ValuesJson = JsonSerializer.Serialize(request.Values),
                FiltersJson = JsonSerializer.Serialize(request.Filters),
                SortJson = JsonSerializer.Serialize(request.Sort),
                LimitRows = request.Limit,
                OffsetRows = request.Offset,
                LayoutJson = JsonSerializer.Serialize(request.Layout),
                request.SemanticModelVersion
            },
            cancellationToken: ct));

        await _audit.WriteAsync(request.DatasetId, "rpt.ReportDefinitions", reportId, "upsert", null, ct: ct);
        return (await FindAsync(reportId, ct))!;
    }

    private async Task<ReportDefinition?> FindAsync(string reportId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync(new CommandDefinition(
            """
            SELECT TOP (1) *
            FROM rpt.ReportDefinitions
            WHERE ReportId = @ReportId AND IsActive = 1;
            """,
            new { ReportId = reportId },
            cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    private async Task<List<ReportDefinition>> ListAsync(string? datasetId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        var rows = await connection.QueryAsync(new CommandDefinition(
            """
            SELECT *
            FROM rpt.ReportDefinitions
            WHERE IsActive = 1 AND (@DatasetId IS NULL OR DatasetId = @DatasetId)
            ORDER BY UpdatedAtUtc DESC;
            """,
            new { DatasetId = string.IsNullOrWhiteSpace(datasetId) ? null : datasetId },
            cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    private async Task<bool> DeleteAsync(string reportId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE rpt.ReportDefinitions
            SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE ReportId = @ReportId AND IsActive = 1;
            """,
            new { ReportId = reportId },
            cancellationToken: ct));
        if (affected > 0)
        {
            await _audit.WriteAsync(null, "rpt.ReportDefinitions", reportId, "delete", null, ct: ct);
        }
        return affected > 0;
    }

    private static ReportDefinition Map(dynamic row) => new()
    {
        ReportId = Row.GetString(row, "ReportId"),
        DatasetId = Row.GetString(row, "DatasetId"),
        ConnectionId = Row.GetString(row, "ConnectionId"),
        Title = Row.GetString(row, "Title"),
        Description = Row.GetString(row, "Description"),
        VisualType = Row.GetString(row, "VisualType", "table"),
        Rows = Deserialize<List<string>>(Row.GetString(row, "RowsJson", "[]")),
        Columns = Deserialize<List<string>>(Row.GetString(row, "ColumnsJson", "[]")),
        Values = Deserialize<List<string>>(Row.GetString(row, "ValuesJson", "[]")),
        Filters = Deserialize<List<FilterRequest>>(Row.GetString(row, "FiltersJson", "[]")),
        Sort = Deserialize<List<SortRequest>>(Row.GetString(row, "SortJson", "[]")),
        Limit = Row.GetInt(row, "LimitRows", 100),
        Offset = Row.GetInt(row, "OffsetRows"),
        Layout = Deserialize<Dictionary<string, object?>>(Row.GetString(row, "LayoutJson", "{}")),
        SemanticModelVersion = Row.GetString(row, "SemanticModelVersion", "v1"),
        CreatedAt = ToDateTimeOffset(Row.Get(row, "CreatedAtUtc")),
        UpdatedAt = ToDateTimeOffset(Row.Get(row, "UpdatedAtUtc"))
    };

    private static T Deserialize<T>(string json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        try { return JsonSerializer.Deserialize<T>(json) ?? new T(); }
        catch (JsonException) { return new T(); }
    }

    private static DateTimeOffset ToDateTimeOffset(object? value)
        => value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => DateTimeOffset.UtcNow
        };
}
