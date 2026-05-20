using Report.Contracts.Requests;
using Report.Metadata.Models;

namespace Report.QueryEngine.Binding;

public sealed class BoundSemanticQuery
{
    public List<SemanticField> Rows { get; init; } = [];
    public List<SemanticMetric> Values { get; init; } = [];
    public List<ResolvedFilter> Filters { get; init; } = [];
    public List<ResolvedSort> Sort { get; init; } = [];
    public int Limit { get; init; }
    public int Offset { get; init; }
}
