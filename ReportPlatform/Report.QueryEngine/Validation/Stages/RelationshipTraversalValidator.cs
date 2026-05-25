using Microsoft.Extensions.Options;
using Report.Contracts.Validation;
using Report.QueryEngine.Relationships;

namespace Report.QueryEngine.Validation.Stages;

public sealed class RelationshipTraversalValidator(IOptions<ValidationOptions> options) : IValidationStage<JoinPlan>
{
    public string Stage => ValidationStages.Stage4RelationshipTraversal;

    public ValidationResult Validate(JoinPlan input)
    {
        var result = new ValidationResult { Stage = Stage };
        if (input.Joins.Count > options.Value.MaxJoins)
            result.Errors.Add(new ValidationIssue{Code="TOO_MANY_JOINS",Message="Join count exceeds limit",Target="joins"});
        else if (input.Joins.Count > options.Value.WarnJoins)
            result.Warnings.Add(new ValidationIssue{Code="QUERY_TOO_COMPLEX",Message="Join count is high",Target="joins",Severity=ValidationSeverity.Warning});
        if (input.Joins.Any(j => j.FromTableId.Equals(j.ToTableId, StringComparison.OrdinalIgnoreCase)))
            result.Errors.Add(new ValidationIssue{Code="SELF_JOIN_DETECTED",Message="Self join detected",Target="joins"});
        return result;
    }
}
