using System.Collections.Concurrent;
using Report.Contracts.Semantic;

namespace Report.Metadata.Stores;

public sealed class InMemoryReportRegistry : IReportRegistry
{
    private readonly ConcurrentDictionary<string, ReportDefinition> _reports = new();
    private int _nextId;

    public ReportDefinition Save(SaveReportDefinitionRequest request, string? reportId = null)
    {
        var id = string.IsNullOrWhiteSpace(reportId) ? $"rpt_{Interlocked.Increment(ref _nextId):000}" : reportId;
        var now = DateTimeOffset.UtcNow;
        var existing = Find(id);
        var report = new ReportDefinition
        {
            ReportId = id,
            DatasetId = request.DatasetId,
            ConnectionId = request.ConnectionId,
            Title = request.Title,
            Description = request.Description,
            VisualType = request.VisualType,
            Rows = request.Rows,
            Columns = request.Columns,
            Values = request.Values,
            Filters = request.Filters,
            Sort = request.Sort,
            Limit = request.Limit,
            Offset = request.Offset,
            Layout = request.Layout,
            SemanticModelVersion = request.SemanticModelVersion,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };
        _reports[id] = report;
        return report;
    }

    public ReportDefinition? Find(string reportId) => _reports.TryGetValue(reportId, out var report) ? report : null;
    public List<ReportDefinition> List(string? datasetId) => _reports.Values.Where(r => string.IsNullOrWhiteSpace(datasetId) || r.DatasetId == datasetId).OrderByDescending(r => r.UpdatedAt).ToList();
    public bool Delete(string reportId) => _reports.TryRemove(reportId, out _);
}
