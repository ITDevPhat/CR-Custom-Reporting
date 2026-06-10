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
            if (string.IsNullOrWhiteSpace(measure.MetricId))
            {
                result.Errors.Add(new ValidationIssue
                {
                    Code = "METRIC_ID_EMPTY",
                    Message = "Expanded metric id is empty.",
                    Target = measure.MetricId ?? string.Empty
                });
            }
        }

        return result;
    }
}