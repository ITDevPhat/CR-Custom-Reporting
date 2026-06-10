using Report.Metadata.Models;
using Report.QueryEngine.Expressions.Binding;
using Report.QueryEngine.Expressions.Compilation;
using Report.QueryEngine.Expressions.Dependencies;
using Report.QueryEngine.Expressions.Parsing;
using Report.QueryEngine.Expressions.Tokenization;

namespace Report.QueryEngine.Expressions.Validation;

public sealed class SemanticExpressionValidationService(
    IExpressionTokenizer tokenizer,
    IExpressionParser parser,
    ExpressionSemanticBinder binder,
    ExpressionScopeResolver scopeResolver,
    ExpressionTypeInferenceService typeInference,
    AggregationValidationService aggregationValidator,
    ExpressionDependencyGraphService dependencyGraph,
    SemanticExpressionSqlCompiler sqlCompiler)
{
    public SemanticExpressionValidationResult Validate(
        SemanticModel model,
        string expression,
        string targetKind = "auto",
        string? candidateId = null)
    {
        var errors = new List<ExpressionValidationIssue>();
        try
        {
            var ast = parser.Parse(tokenizer.Tokenize(expression));
            var bound = binder.Bind(ast, model);
            var scope = scopeResolver.Resolve(ast);
            var detectedKind = scope == ExpressionScope.Aggregate ? "calculated_measure" : "calculated_column";

            if (!targetKind.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
                !targetKind.Equals(detectedKind, StringComparison.OrdinalIgnoreCase))
            {
                throw new ExpressionValidationException(
                    "AGGREGATE_SCOPE_CONFLICT",
                    $"Expression resolves to {detectedKind}, but target kind is {targetKind}.");
            }

            aggregationValidator.Validate(ast, scope, targetKind);
            dependencyGraph.ValidateNoCycles(model, candidateId, bound.Dependencies);
            var dataType = typeInference.Infer(ast, model);
            var sql = sqlCompiler.Compile(bound, model);

            return new SemanticExpressionValidationResult
            {
                Valid = true,
                DetectedKind = detectedKind,
                DetectedScope = scope,
                DataType = dataType,
                Dependencies = bound.Dependencies,
                SqlPreview = sql,
                BoundExpression = bound
            };
        }
        catch (ExpressionValidationException ex)
        {
            errors.Add(new ExpressionValidationIssue(ex.Code, ex.Message));
        }
        catch (ExpressionParseException ex)
        {
            errors.Add(new ExpressionValidationIssue("SYNTAX_ERROR", ex.Message));
        }
        catch (Exception ex)
        {
            errors.Add(new ExpressionValidationIssue("SEMANTIC_VALIDATION_ERROR", ex.Message));
        }

        return new SemanticExpressionValidationResult
        {
            Valid = false,
            Errors = errors
        };
    }
}
