using System.Text.RegularExpressions;
using Report.Contracts.Validation;
using Report.QueryEngine.Compilation;

namespace Report.QueryEngine.Validation.Stages;

public sealed partial class SqlCompilationValidator : IValidationStage<SqlCompilationResult>
{
    public string Stage => ValidationStages.Stage6SqlCompilation;

    public ValidationResult Validate(SqlCompilationResult input)
    {
        var result = new ValidationResult { Stage = Stage };
        if (string.IsNullOrWhiteSpace(input.Sql)) result.Errors.Add(new ValidationIssue{Code="SQL_SYNTAX_ERROR",Message="SQL is empty"});
        if (input.Sql.Count(c => c == '(') != input.Sql.Count(c => c == ')')) result.Errors.Add(new ValidationIssue{Code="MISMATCHED_BRACKETS",Message="SQL bracket mismatch"});
        var placeholders = ParamRegex().Matches(input.Sql).Select(m => m.Value.TrimStart('@')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var ph in placeholders.Where(p => !input.Parameters.ContainsKey(p))) result.Errors.Add(new ValidationIssue{Code="MISSING_PARAMETER",Message=$"Missing parameter {ph}",Target=ph});
        foreach (var p in input.Parameters.Keys.Where(p => !placeholders.Contains(p))) result.Warnings.Add(new ValidationIssue{Code="UNUSED_PARAMETER",Message=$"Unused parameter {p}",Target=p,Severity=ValidationSeverity.Warning});
        return result;
    }

    [GeneratedRegex("@[A-Za-z0-9_]+")]
    private static partial Regex ParamRegex();
}
