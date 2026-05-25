using Microsoft.Extensions.Logging;
using Report.Contracts.Validation;

namespace Report.QueryEngine.Validation.Logging;

public static class ValidationLogging
{
    public const string LogTemplate = "[{Stage}] {Operation} | Result: {Result} | Issues: E={ErrorCount}, W={WarningCount} | Duration: {DurationMs}ms | Details: {Details}";

    public static void LogValidationComplete(ILogger logger, ValidationResult result, string operation)
    {
        var details = result.Errors.Count > 0
            ? string.Join("; ", result.Errors.Select(e => e.Code))
            : string.Empty;

        logger.LogInformation(
            LogTemplate,
            result.Stage,
            operation,
            result.IsValid ? "PASS" : "FAIL",
            result.Errors.Count,
            result.Warnings.Count,
            result.ValidationDurationMs,
            details);
    }

    public static void LogValidationIssue(ILogger logger, ValidationIssue issue, string stage)
    {
        if (issue.Severity == ValidationSeverity.Error)
            logger.LogError("[{Stage}] {Code}: {Message} | Target: {Target} | Fix: {Fix}", stage, issue.Code, issue.Message, issue.Target ?? "N/A", issue.SuggestedFix ?? "N/A");
        else if (issue.Severity == ValidationSeverity.Warning)
            logger.LogWarning("[{Stage}] {Code}: {Message} | Target: {Target} | Fix: {Fix}", stage, issue.Code, issue.Message, issue.Target ?? "N/A", issue.SuggestedFix ?? "N/A");
        else
            logger.LogInformation("[{Stage}] {Code}: {Message} | Target: {Target} | Fix: {Fix}", stage, issue.Code, issue.Message, issue.Target ?? "N/A", issue.SuggestedFix ?? "N/A");
    }
}
