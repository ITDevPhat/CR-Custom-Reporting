using Report.Metadata.Models;
using Report.QueryEngine.Binding;

namespace Report.QueryEngine.Context;

public sealed class EvaluationContext
{
    public List<SemanticField> GroupFields { get; init; } = [];
    public List<SemanticMetric> Measures { get; init; } = [];
    public FilterContext Filters { get; init; } = new();
    public List<ResolvedSort> Sort { get; init; } = [];
    public int Limit { get; init; }
    public int Offset { get; init; }
}
