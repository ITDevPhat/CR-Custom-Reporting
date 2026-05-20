namespace Report.QueryEngine.Validation;

public sealed class SemanticQueryValidationException : Exception
{
    public SemanticQueryValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("The visual query request contains invalid semantic references.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
