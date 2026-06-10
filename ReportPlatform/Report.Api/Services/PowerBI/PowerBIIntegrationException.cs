namespace Report.Api.Services.PowerBI;

public sealed class PowerBIIntegrationException : Exception
{
    public PowerBIIntegrationException(string code, string message, int statusCode = StatusCodes.Status400BadRequest, string? detail = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Detail = detail;
    }

    public string Code { get; }
    public int StatusCode { get; }
    public string? Detail { get; }
}
