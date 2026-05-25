using Report.Contracts.Validation;

namespace Report.QueryEngine.Validation;

public interface IValidationStage<in TInput>
{
    string Stage { get; }
    ValidationResult Validate(TInput input);
}
