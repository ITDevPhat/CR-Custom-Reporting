using Report.Contracts.Validation;
using Report.QueryEngine.Measures;

namespace Report.QueryEngine.Validation.Stages;

public sealed class MeasureExpansionValidator : IValidationStage<IReadOnlyList<ExpandedMeasure>>
{
    public string Stage => ValidationStages.Stage3MeasureExpansion;

    public ValidationResult Validate(IReadOnlyList<ExpandedMeasure> input)
    {
        var result = new ValidationResult { Stage = Stage };
        foreach (var measure in input)
        {
            if (string.IsNullOrWhiteSpace(measure.FormulaSql))
                result.Errors.Add(new ValidationIssue{Code="FORMULA_SYNTAX_ERROR",Message=$"Metric '{measure.MetricId}' formula is empty",Target=measure.MetricId});
            if (measure.AggregationBehavior.Equals("non_additive", StringComparison.OrdinalIgnoreCase))
                result.Warnings.Add(new ValidationIssue{Code="NON_ADDITIVE_METRIC",Message=$"Metric '{measure.MetricId}' is non additive",Target=measure.MetricId,Severity=ValidationSeverity.Warning});
        }
        return result;
    }
}
