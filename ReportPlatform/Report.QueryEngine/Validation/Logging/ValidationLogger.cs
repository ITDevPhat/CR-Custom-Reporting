using Microsoft.Extensions.Logging;
using Report.Contracts.Validation;

namespace Report.QueryEngine.Validation.Logging;

public sealed class ValidationLogger(ILogger<ValidationLogger> logger)
{
    public void LogStage(ValidationResult result, string operation)
    {
        foreach (var issue in result.Errors.Concat(result.Warnings))
        {
            ValidationLogging.LogValidationIssue(logger, issue, result.Stage);
        }

        ValidationLogging.LogValidationComplete(logger, result, operation);
    }
}
