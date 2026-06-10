namespace Report.Api.Rendering;

public sealed class ReportExportException : Exception
{
    public int StatusCode { get; }

    public ReportExportException(string message, int statusCode = 500, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
