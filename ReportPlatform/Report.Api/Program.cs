using Report.Api.Middleware;
using Report.Api.Services;
using Report.Api.Rendering;
using Report.Api.Swagger;
using Report.Infrastructure.Connections;
using Report.Infrastructure.Execution;
using Report.Metadata.Connections;
using Report.Metadata.Stores;
using Report.QueryEngine.Binding;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Context;
using Report.QueryEngine.Execution;
using Report.QueryEngine.Expressions.Binding;
using Report.QueryEngine.Expressions.Compilation;
using Report.QueryEngine.Expressions.Dependencies;
using Report.QueryEngine.Expressions.Parsing;
using Report.QueryEngine.Expressions.Tokenization;
using Report.QueryEngine.Expressions.Validation;
using Report.QueryEngine.Measures;
using Report.QueryEngine.Planning;
using Report.QueryEngine.Relationships;
using Report.QueryEngine.Services;
using Report.QueryEngine.Validation;
using Report.QueryEngine.Validation.Stages;
using Report.QueryEngine.Validation.Logging;
using Report.QueryEngine.Artifacts;
using Report.Infrastructure.Artifacts;
using Report.Infrastructure.Persistence;
using Report.Contracts.Artifacts;




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.RequestBodyFilter<VisualQueryRequestExampleFilter>();
});

var allowedFrontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://127.0.0.1:3000" };
var allowLocalDevFrontend = builder.Environment.IsDevelopment();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (allowedFrontendOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!allowLocalDevFrontend ||
                    !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var isLocalHost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
                var isHttp = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

                return isLocalHost && isHttp && uri.Port >= 3000 && uri.Port <= 3009;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IConnectionRegistry, InMemoryConnectionRegistry>();
builder.Services.AddSingleton<IDatasetRegistry, InMemoryDatasetRegistry>();
builder.Services.AddSingleton<IReportRegistry, InMemoryReportRegistry>();
builder.Services.AddSingleton<ISemanticModelStore, InMemorySemanticModelStore>();

builder.Services.AddScoped<ReportArtifactBuilder>();
builder.Services.AddScoped<ReportArtifactLoader>();
builder.Services.AddScoped<QueryFingerprintService>();
builder.Services.AddScoped<TelerikArtifactRenderService>();
builder.Services.AddScoped<IArtifactReportRenderer, TelerikArtifactReportRenderer>();
builder.Services.AddScoped<IReportSnapshotExportProcessor, DataFirstSnapshotProcessor>();
builder.Services.AddScoped<IReportSnapshotExportProcessor, TelerikSnapshotRenderProcessor>();
builder.Services.AddScoped<SnapshotExportRouter>();

var storageMode = builder.Configuration["ReportArtifacts:StorageMode"] ?? "Local";
builder.Services.Configure<ReportQueryServiceOptions>(options =>
{
    options.StorageMode = storageMode;
});

string? localArtifactRoot = null;
if (!string.Equals(storageMode, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    var configuredRoot = builder.Configuration["ReportArtifacts:Local:RootPath"]
        ?? Path.Combine("..", "ReportArtifacts");

    localArtifactRoot = Path.IsPathRooted(configuredRoot)
        ? configuredRoot
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, configuredRoot));
}

var executionStoreMode = builder.Configuration["ReportExecutionStore:Mode"] ?? "SqlServer";
var executionStoreConnectionString = builder.Configuration["ReportExecutionStore:ConnectionString"] ?? "";
if (string.Equals(executionStoreMode, "SqlServer", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(executionStoreConnectionString))
{
    builder.Services.AddSingleton<IReportExecutionRepository>(sp =>
        new SqlServerReportExecutionRepository(
            executionStoreConnectionString,
            sp.GetRequiredService<ILogger<SqlServerReportExecutionRepository>>()));
}
else
{
    builder.Services.AddSingleton<IReportExecutionRepository>(_ =>
    {
        var memory = new InMemoryReportExecutionRepository();
        return localArtifactRoot is null
            ? memory
            : new ArtifactBackedReportExecutionRepository(memory, localArtifactRoot);
    });
    builder.Logging.AddFilter("ReportExecutionStore", LogLevel.Warning);
}

if (string.Equals(storageMode, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IReportArtifactStore, InMemoryReportArtifactStore>();
}
else
{
    var artifactRoot = localArtifactRoot
        ?? throw new InvalidOperationException("Local artifact root was not configured.");
    Directory.CreateDirectory(artifactRoot);

    builder.Services.AddSingleton<IReportArtifactStore>(_ =>
        new LocalReportArtifactStore(artifactRoot));
}

builder.Services.AddScoped<SqlServerSchemaDiscoveryService>();
builder.Services.AddScoped<SemanticMetadataGenerator>();
builder.Services.AddScoped<MetadataConsistencyValidator>();
builder.Services.AddScoped<DatasetRelationshipService>();
builder.Services.AddScoped<SemanticModelMutationService>();
builder.Services.AddScoped<IExpressionTokenizer, ExpressionTokenizer>();
builder.Services.AddScoped<IExpressionParser, ExpressionParser>();
builder.Services.AddScoped<SemanticFunctionRegistry>();
builder.Services.AddScoped<ExpressionSemanticBinder>();
builder.Services.AddScoped<ExpressionScopeResolver>();
builder.Services.AddScoped<ExpressionTypeInferenceService>();
builder.Services.AddScoped<AggregationValidationService>();
builder.Services.AddScoped<ExpressionDependencyGraphService>();
builder.Services.AddScoped<SemanticExpressionSqlCompiler>();
builder.Services.AddScoped<SemanticExpressionValidationService>();
builder.Services.AddScoped<SemanticModelBinder>();
builder.Services.AddScoped<EvaluationContextBuilder>();
builder.Services.AddScoped<MeasureExpansionEngine>();
builder.Services.AddScoped<RelationshipTraversalEngine>();
builder.Services.AddScoped<LogicalPlanBuilder>();
builder.Services.AddScoped<SqlCompiler>();
builder.Services.AddScoped<IQueryExecutor, SqlServerQueryExecutor>();
builder.Services.AddScoped<ReportQueryService>();
builder.Services.AddScoped<GrainValidationService>();
builder.Services.Configure<ValidationOptions>(builder.Configuration.GetSection("Validation"));

builder.Services.AddScoped<ValidationLogger>();
builder.Services.AddScoped<SemanticBindingValidator>();
builder.Services.AddScoped<ContextBuildingValidator>();
builder.Services.AddScoped<MeasureExpansionValidator>();
builder.Services.AddScoped<RelationshipTraversalValidator>();
builder.Services.AddScoped<LogicalPlanValidator>();
builder.Services.AddScoped<SqlCompilationValidator>();
builder.Services.AddScoped<QueryExecutionValidator>();

builder.Services.AddScoped<DatasetMetadataService>();
builder.Services.AddScoped<ExpressionValidationService>();
builder.Services.AddScoped<ITelerikReportFactory, DynamicTelerikReportFactory>();
builder.Services.AddScoped<IReportConnectionStringResolver, ReportConnectionStringResolver>();
builder.Services.AddScoped<IReportRenderService, TelerikReportRenderService>();


var app = builder.Build();

if (string.Equals(executionStoreMode, "SqlServer", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(executionStoreConnectionString) &&
    builder.Configuration.GetValue("ReportExecutionStore:AutoCreateSchema", true))
{
    var repository = (SqlServerReportExecutionRepository)app.Services.GetRequiredService<IReportExecutionRepository>();
    await repository.EnsureSchemaAsync(CancellationToken.None);
}
else if (string.Equals(executionStoreMode, "SqlServer", StringComparison.OrdinalIgnoreCase) &&
    string.IsNullOrWhiteSpace(executionStoreConnectionString))
{
    app.Logger.LogWarning("ReportExecutionStore is configured for SqlServer but ConnectionString is empty. Using in-memory report execution repository.");
}

app.UseMiddleware<ApiExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");

app.MapControllers();

app.Run();
