namespace Report.Contracts.Validation;

public enum ValidationSeverity { Info, Warning, Error }

public static class ValidationStages
{
    public const string Stage1SemanticBinding = "STAGE_1_SEMANTIC_BINDING";
    public const string Stage2ContextBuilding = "STAGE_2_CONTEXT_BUILDING";
    public const string Stage3MeasureExpansion = "STAGE_3_MEASURE_EXPANSION";
    public const string Stage4RelationshipTraversal = "STAGE_4_RELATIONSHIP_TRAVERSAL";
    public const string Stage5LogicalPlanBuilding = "STAGE_5_LOGICAL_PLAN_BUILDING";
    public const string Stage6SqlCompilation = "STAGE_6_SQL_COMPILATION";
    public const string Stage7QueryExecution = "STAGE_7_QUERY_EXECUTION";
}

public sealed class ValidationIssue
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Target { get; init; } = "";
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;
    public string SuggestedFix { get; init; } = "";
    public Dictionary<string, object?> Details { get; init; } = [];
}

public sealed class ValidationResult
{
    public string Stage { get; init; } = "";
    public Dictionary<string, object?> Context { get; init; } = [];
    public long ValidationDurationMs { get; set; }
    public List<ValidationIssue> Errors { get; init; } = [];
    public List<ValidationIssue> Warnings { get; init; } = [];
    public bool IsValid => Errors.Count == 0;
}

public sealed class CompilationResult
{
    public bool Success { get; init; }
    public string Sql { get; init; } = "";
    public Dictionary<string, object?> Parameters { get; init; } = [];
}

public sealed class ColumnMetadata
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "string";
}

public sealed class ExecutionMetadata
{
    public long TotalDurationMs { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public string ExecutedAt { get; init; } = DateTime.UtcNow.ToString("O");
    public List<string> ExecutedStages { get; init; } = [];
}

public sealed class ComprehensiveQueryResponse
{
    public bool Success { get; init; }
    public List<ColumnMetadata> Columns { get; init; } = [];
    public List<Dictionary<string, object?>> Data { get; init; } = [];
    public CompilationResult? Compilation { get; init; }
    public ExecutionMetadata Metadata { get; init; } = new();
    public List<ValidationResult> ValidationResults { get; init; } = [];
}
