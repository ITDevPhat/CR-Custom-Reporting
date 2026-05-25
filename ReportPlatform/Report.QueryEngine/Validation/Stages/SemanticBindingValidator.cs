using Report.Contracts.Validation;

namespace Report.QueryEngine.Validation.Stages;

public sealed class SemanticBindingValidator : IValidationStage<ValidationContext>
{
    public string Stage => ValidationStages.Stage1SemanticBinding;

    public ValidationResult Validate(ValidationContext input)
    {
        var result = new ValidationResult { Stage = Stage };
        var fields = input.Model.Fields.ToDictionary(f => f.FieldId, StringComparer.OrdinalIgnoreCase);
        var metrics = input.Model.Metrics.ToDictionary(m => m.MetricId, StringComparer.OrdinalIgnoreCase);

        foreach (var row in input.Request.Rows)
        {
            if (!fields.TryGetValue(row, out var field)) result.Errors.Add(I("FIELD_NOT_FOUND", $"Field '{row}' not found", row));
            else if (field.IsHidden) result.Errors.Add(I("FIELD_HIDDEN", $"Field '{row}' is hidden", row));
        }
        foreach (var value in input.Request.Values)
        {
            if (!metrics.ContainsKey(value) && !value.StartsWith("metric.", StringComparison.OrdinalIgnoreCase))
                result.Errors.Add(I("METRIC_NOT_FOUND", $"Metric '{value}' not found", value));
        }
        var selected = input.Request.Rows.Concat(input.Request.Values).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sort in input.Request.Sort)
        {
            if (!selected.Contains(sort.Field)) result.Errors.Add(I("SORT_FIELD_NOT_SELECTED", $"Sort field '{sort.Field}' is not selected", sort.Field));
            if (!sortFields.Add(sort.Field)) result.Errors.Add(I("DUPLICATE_SORT", $"Duplicate sort for '{sort.Field}'", sort.Field));
            if (sort.Direction is not ("ASC" or "DESC")) result.Errors.Add(I("INVALID_SORT_DIRECTION", $"Invalid sort direction '{sort.Direction}'", sort.Field));
        }
        return result;
    }

    private static ValidationIssue I(string c, string m, string t, ValidationSeverity s = ValidationSeverity.Error) => new() { Code = c, Message = m, Target = t, Severity = s };
}
