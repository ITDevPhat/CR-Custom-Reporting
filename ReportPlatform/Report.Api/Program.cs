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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.RequestBodyFilter<VisualQueryRequestExampleFilter>();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<IConnectionRegistry, InMemoryConnectionRegistry>();
builder.Services.AddSingleton<IDatasetRegistry, InMemoryDatasetRegistry>();
builder.Services.AddSingleton<IReportRegistry, InMemoryReportRegistry>();
builder.Services.AddSingleton<ISemanticModelStore, InMemorySemanticModelStore>();

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

app.UseMiddleware<ApiExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");

app.MapControllers();

app.Run();
