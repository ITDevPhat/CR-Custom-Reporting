using Report.Contracts.Validation;
using Report.QueryEngine.Planning;

namespace Report.QueryEngine.Validation.Stages;

public sealed class LogicalPlanValidator : IValidationStage<LogicalQueryPlan>
{
    public string Stage => ValidationStages.Stage5LogicalPlanBuilding;

    public ValidationResult Validate(LogicalQueryPlan input)
    {
        var result = new ValidationResult { Stage = Stage };
        var dup = input.Select.GroupBy(s => s.Alias, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) result.Errors.Add(new ValidationIssue{Code="DUPLICATE_COLUMN_ALIAS",Message=$"Duplicate alias '{dup.Key}'",Target=dup.Key});
        return result;
    }
}
