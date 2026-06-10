using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Report.Contracts.Requests;

namespace Report.QueryEngine.Artifacts;

public sealed class QueryFingerprintService
{
    public string Compute(VisualQueryRequest request, string semanticModelVersion)
    {
        var normalized = new
        {
            request.DatasetId,
            Rows = request.Rows.OrderBy(x => x).ToArray(),
            Values = request.Values.OrderBy(x => x).ToArray(),
            Filters = request.Filters.Select(f => new { f.Field, Operator = f.Operator, Value = JsonSerializer.Serialize(f.Value), f.Scope }).OrderBy(x => x.Field).ThenBy(x => x.Operator).ToArray(),
            Sort = request.Sort.Select(s => new { s.Field, Direction = s.Direction }).OrderBy(x => x.Field).ThenBy(x => x.Direction).ToArray(),
            request.Limit,
            request.Offset,
            semanticModelVersion
        };

        var json = JsonSerializer.Serialize(normalized);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"sha256-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
