using Report.QueryEngine.Binding;

namespace Report.QueryEngine.Context;

public sealed class EvaluationContextBuilder
{
    public EvaluationContext Build(BoundSemanticQuery bound)
    {
        return new EvaluationContext
        {
            GroupFields = bound.Rows,
            Measures = bound.Values,
            Filters = new FilterContext
            {
                WhereFilters = bound.Filters
                    .Where(f => f.TargetType == "dimension")
                    .ToList(),
                HavingFilters = bound.Filters
                    .Where(f => f.TargetType == "metric")
                    .ToList()
            },
            Sort = bound.Sort,
            Limit = bound.Limit,
            Offset = bound.Offset
        };
    }
}
