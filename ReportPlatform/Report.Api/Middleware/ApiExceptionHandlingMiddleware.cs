using Microsoft.AspNetCore.Mvc;
using Report.Contracts.Results;
using Report.QueryEngine.Execution;
using Report.QueryEngine.Validation;

namespace Report.Api.Middleware;

public sealed class ApiExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ApiExceptionHandlingMiddleware(
        RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SemanticQueryValidationException ex)
        {
            await WriteValidationProblemAsync(context, ex);
        }
        catch (QueryExecutionException ex)
        {
            var error = new QueryExecutionError
            {
                Message = ex.Message,
                Sql = ex.Sql
            };

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(error);
        }
    }

    private static async Task WriteValidationProblemAsync(
        HttpContext context,
        SemanticQueryValidationException exception)
    {
        var problem = new QueryExecutionError
        {
            ErrorCode = exception.Errors.ContainsKey("errorCode")
                ? exception.Errors["errorCode"].FirstOrDefault() ?? "INVALID_QUERY_REQUEST"
                : "INVALID_QUERY_REQUEST",
            Message = "Invalid visual query request",
            Details = exception.Errors
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
