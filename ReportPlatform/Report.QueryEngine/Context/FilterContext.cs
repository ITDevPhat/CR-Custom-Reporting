using Report.QueryEngine.Binding;

namespace Report.QueryEngine.Context;

public sealed class FilterContext
{
    public List<ResolvedFilter> WhereFilters { get; init; } = [];
    public List<ResolvedFilter> HavingFilters { get; init; } = [];
}
