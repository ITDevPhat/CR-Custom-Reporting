using Microsoft.Extensions.Options;
using Report.Contracts.Results;
using Report.Contracts.Validation;

namespace Report.QueryEngine.Validation.Stages;

public sealed class QueryExecutionValidator(IOptions<ValidationOptions> options) : IValidationStage<QueryResult>
{
    public string Stage => ValidationStages.Stage7QueryExecution;

    public ValidationResult Validate(QueryResult input)
    {
        var result = new ValidationResult { Stage = Stage };
        if (input.Columns.Count == 0 && input.Rows.Count > 0)
            result.Errors.Add(new ValidationIssue{Code="SCHEMA_MISMATCH",Message="Rows returned with no columns"});
        if (input.Rows.Count > options.Value.MaxResultRows)
            result.Errors.Add(new ValidationIssue{Code="RESULT_TOO_LARGE",Message="Result row count exceeds limit"});
        return result;
    }
}
