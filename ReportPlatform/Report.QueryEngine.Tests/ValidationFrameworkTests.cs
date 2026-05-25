using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Report.Contracts.Requests;
using Report.Contracts.Results;
using Report.QueryEngine.Compilation;
using Report.QueryEngine.Validation;
using Report.QueryEngine.Validation.Logging;
using Report.QueryEngine.Validation.Stages;

namespace Report.QueryEngine.Tests;

public sealed class ValidationFrameworkTests
{
    [Fact]
    public void Stage1_InvalidField_ShouldReturnError()
    {
        var model = QueryEngineTestHarness.CreateSalesModel();
        var validator = new SemanticBindingValidator();
        var result = validator.Validate(new ValidationContext(QueryEngineTestHarness.Request(rows:["bad.field"]), model));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "FIELD_NOT_FOUND");
    }

    [Fact]
    public void Stage2_EmptyQuery_ShouldStopWithError()
    {
        var model = QueryEngineTestHarness.CreateSalesModel();
        var validator = new ContextBuildingValidator(Options.Create(new ValidationOptions()));
        var req = QueryEngineTestHarness.Request(rows:[], values:[]);
        var result = validator.Validate(new ValidationContext(req, model));
        result.Errors.Should().Contain(e => e.Code == "EMPTY_QUERY");
    }

    [Fact]
    public void Stage6_MissingParameter_ShouldReturnError()
    {
        var validator = new SqlCompilationValidator();
        var result = validator.Validate(new SqlCompilationResult { Sql = "select * from T where id = @p1", Parameters = [] });
        result.Errors.Should().Contain(e => e.Code == "MISSING_PARAMETER");
    }

    [Fact]
    public void Stage7_SchemaMismatch_ShouldReturnError()
    {
        var validator = new QueryExecutionValidator(Options.Create(new ValidationOptions()));
        var result = validator.Validate(new QueryResult { Columns = [], Rows = [new Dictionary<string, object?>()] });
        result.Errors.Should().Contain(e => e.Code == "SCHEMA_MISMATCH");
    }

    [Fact]
    public void ValidationLogger_ShouldNotThrow()
    {
        var logger = new ValidationLogger(new NullLogger<ValidationLogger>());
        var result = new Report.Contracts.Validation.ValidationResult { Stage = "TEST" };
        logger.LogStage(result, "unit");
    }
}
