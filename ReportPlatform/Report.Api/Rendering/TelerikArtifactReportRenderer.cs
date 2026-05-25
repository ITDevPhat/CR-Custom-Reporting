using System.Data;
using Report.Contracts.Exports;

namespace Report.Api.Rendering;

public sealed class TelerikArtifactReportRenderer : IArtifactReportRenderer
{
    public Task<byte[]> RenderAsync(string format, string templateId, DataTable dataTable, CancellationToken ct)
    {
        if (format == "CSV")
        {
            var lines = new List<string> { string.Join(',', dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName)) };
            foreach (DataRow r in dataTable.Rows) lines.Add(string.Join(',', r.ItemArray.Select(v => v?.ToString())));
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        }
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes($"fake-{format}-{templateId}-{dataTable.Rows.Count}"));
    }
}
