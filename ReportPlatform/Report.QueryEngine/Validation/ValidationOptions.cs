namespace Report.QueryEngine.Validation;

public sealed class ValidationOptions
{
    public int MaxJoins { get; set; } = 10;
    public int WarnJoins { get; set; } = 5;
    public int MaxLimit { get; set; } = 1000;
    public int MaxResultRows { get; set; } = 100_000;
    public long MaxResultBytes { get; set; } = 50 * 1024 * 1024;
}
