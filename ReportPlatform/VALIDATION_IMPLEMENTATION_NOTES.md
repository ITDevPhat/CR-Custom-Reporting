# 7-Stage Validation Framework Implementation Notes

## Files changed
- Added validation contracts in `Report.Contracts/Validation/ValidationContracts.cs`.
- Added validation options/context/interfaces and stage validators under `Report.QueryEngine/Validation`.
- Added centralized structured logger `ValidationLogger`.
- Refactored `ReportQueryService` to execute through staged validation and return `ComprehensiveQueryResponse`.
- Updated API DI registration in `Report.Api/Program.cs`.
- Added validation-focused tests in `Report.QueryEngine.Tests/ValidationFrameworkTests.cs`.

## Architecture summary
- Introduced zero-exception validation contracts (`ValidationResult`, `ValidationIssue`) and response contract (`ComprehensiveQueryResponse`).
- Implemented stage validators (1..7) as composable services.
- Integrated stage execution in query pipeline with short-circuit on blocking errors.
- Runtime exceptions remain for unrecoverable runtime failures (e.g., export attempted on invalid query).

## Validation flow
1. Stage 1 semantic binding validation over request/model ids and sort semantics.
2. Stage 2 context constraints (empty query, limit/offset bounds).
3. Stage 3 measure expansion checks + non-additive warnings.
4. Stage 4 join-path complexity/safety checks.
5. Stage 5 logical plan alias consistency checks.
6. Stage 6 SQL checks (basic syntax/parameter alignment).
7. Stage 7 execution result checks (schema/size constraints).

## Known assumptions
- Existing binder/planner/compiler still enforce some invariants.
- Stage coverage currently prioritizes representative rules and non-throwing behavior.
- `CompileAsync` is intentionally kept non-breaking by returning a lightweight compatibility payload.

## How to run tests
From `ReportPlatform/`:
- `dotnet restore`
- `dotnet build`
- `dotnet test`
