using Microsoft.Extensions.Options;
using Report.Contracts.Validation;

namespace Report.QueryEngine.Validation.Stages;

public sealed class ContextBuildingValidator(IOptions<ValidationOptions> options) : IValidationStage<ValidationContext>
{
    public string Stage => ValidationStages.Stage2ContextBuilding;

    public ValidationResult Validate(ValidationContext input)
    {
        var result = new ValidationResult { Stage = Stage };
        if (!input.Request.Rows.Any() && !input.Request.Values.Any()) result.Errors.Add(new ValidationIssue{Code="EMPTY_QUERY",Message="Query must contain at least one row or value",Severity=ValidationSeverity.Error});
        if (input.Request.Values.Any() && !input.Request.Rows.Any()) result.Warnings.Add(new ValidationIssue{Code="AGGREGATION_WITHOUT_DIMENSION",Message="Aggregated query without dimensions",Severity=ValidationSeverity.Warning});
        if (input.Request.Limit < 1) result.Errors.Add(new ValidationIssue{Code="LIMIT_TOO_LOW",Message="Limit must be >= 1",Severity=ValidationSeverity.Error});
        if (input.Request.Limit > options.Value.MaxLimit) result.Errors.Add(new ValidationIssue{Code="LIMIT_TOO_HIGH",Message="Limit exceeds max",Severity=ValidationSeverity.Error});
        if (input.Request.Offset < 0) result.Errors.Add(new ValidationIssue{Code="OFFSET_NEGATIVE",Message="Offset cannot be negative",Severity=ValidationSeverity.Error});
        return result;
    }
}
