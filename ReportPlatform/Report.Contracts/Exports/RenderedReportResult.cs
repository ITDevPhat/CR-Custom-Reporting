namespace Report.Contracts.Exports;

public sealed class RenderedReportResult
{
    public string FileName { get; init; } = "";
    public string ContentType { get; init; } = "";
    public byte[] Bytes { get; init; } = [];
}
