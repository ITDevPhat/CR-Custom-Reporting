using Report.Contracts.Semantic;
using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Validation;

namespace Report.Api.Services;

public sealed class ExpressionValidationService(SemanticExpressionValidationService validator)
{
    public ExpressionValidationResponse Validate(SemanticModel model, ExpressionValidationRequest request)
    {
        var result = validator.Validate(model, request.Expression, request.TargetKind);
        return new ExpressionValidationResponse
        {
            Valid = result.Valid,
            DetectedKind = result.DetectedKind,
            DetectedScope = result.DetectedScope.ToString(),
            DataType = result.DataType,
            ReturnType = result.DataType,
            Dependencies = result.Dependencies,
            SqlPreview = result.SqlPreview,
            CompiledSqlPreview = result.SqlPreview ?? "",
            Errors = result.Errors.Select(e => new ExpressionValidationMessage { Code = e.Code, Message = e.Message }).ToList(),
            Warnings = result.Warnings.Select(e => new ExpressionValidationMessage { Code = e.Code, Message = e.Message }).ToList()
        };
    }
}
