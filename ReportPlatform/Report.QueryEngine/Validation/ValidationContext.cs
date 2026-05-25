using Report.Contracts.Requests;
using Report.Metadata.Models;

namespace Report.QueryEngine.Validation;

public sealed class ValidationContext(
    VisualQueryRequest request,
    SemanticModel model)
{
    public VisualQueryRequest Request { get; } = request;
    public SemanticModel Model { get; } = model;
}
