# 📍 COMPLETE FLOW TRACE: SQL Compiler and Final SQL Script Generation

## 🎬 END-TO-END JOURNEY: Builder Selection → Final SQL Text → Database Execution

```
Frontend Report Builder / Visual Query UI
    ↓
buildVisualQueryRequest()
    ↓
POST /api/query/execute
    ↓
QueryController.Execute()
    ↓
ReportQueryService.ExecuteAsync()
    ├─ Load SemanticModel
    ├─ Validate request shape
    ├─ SemanticModelBinder.Bind()
    ├─ EvaluationContextBuilder.Build()
    ├─ MeasureExpansionEngine.Expand()
    ├─ RelationshipTraversalEngine.Build()
    ├─ LogicalPlanBuilder.Build()
    └─ SqlCompiler.Compile()
          ↓
      SqlCompilationResult
      {
        Sql = "SELECT ... FROM ... WHERE ... GROUP BY ... HAVING ... ORDER BY ...",
        Parameters = { p0: ..., p1: ... }
      }
          ↓
SqlServerQueryExecutor.ExecuteAsync()
    ↓
SQL Server returns rows
    ↓
Artifact/export pipeline can persist or download the result
```

---

# 🔍 WHAT THIS FILE EXPLAINS

This document focuses on the **final SQL compiler flow**: how user selections from the frontend become the actual SQL script string sent to SQL Server.

The important idea:

> `SqlCompiler` does **not** decide business meaning. It renders an already-built `LogicalQueryPlan` into SQL text and parameter values.

So the final SQL is produced by two layers working together:

| Layer | Responsibility | Example |
|---|---|---|
| **Planner layer** | Decides *what* should be selected, grouped, joined, filtered, sorted | Dimension filters go to `WHERE`; metric filters go to `HAVING`; selected dimensions go to `GROUP BY` when measures exist |
| **Compiler layer** | Converts the plan into SQL text and `@p0`, `@p1`, ... parameters | `FilterItem(Expression="c.[Country]", Operator="=", Value="USA")` → `c.[Country] = @p0` |

---

# 🧭 HIGH-LEVEL SQL CLAUSE ORDER

The final compiled SQL follows this shape:

```sql
SELECT [TOP]
  <select expression> AS [<safe alias>],
  <measure expression> AS [<safe alias>]
FROM <base table expression> <base alias>
<JOIN clauses>
WHERE
  <dimension filter 1>
  AND <dimension filter 2>
GROUP BY
  <dimension expression 1>,
  <dimension expression 2>
HAVING
  <metric filter 1>
ORDER BY
  <sort expression or select alias> ASC|DESC
OFFSET <n> ROWS FETCH NEXT <limit> ROWS ONLY
```

Not every clause is always present:

| Clause | Appears when |
|---|---|
| `TOP` | `Limit > 0`, `Offset == 0`, and no `ORDER BY` |
| `JOIN` | Relationship traversal found required related tables |
| `WHERE` | There are dimension / field filters |
| `GROUP BY` | The query has measures and selected dimensions |
| `HAVING` | There are metric / measure filters |
| `ORDER BY` | User requested sorting, or `OFFSET` needs a fallback order |
| `OFFSET FETCH` | `Limit > 0` and compiler cannot use `TOP` |

---

# 🎯 PHASE 1: FRONTEND BUILDS THE QUERY REQUEST

## File

`data-report-builder/lib/build-visual-query-request.ts`

## Purpose

The frontend takes UI state and sends a `VisualQueryRequest` to the backend. This request is **not SQL yet**. It is a semantic request using field IDs, metric IDs, filters, sort, and paging.

## Example UI Selection

User selects:

- Rows: `Customer.Country`
- Values: `SUM(Sales.Amount)`
- Filter: `Customer.Country = "USA"`
- Measure filter: `SUM(Sales.Amount) > 1000`
- Sort: `SUM(Sales.Amount) DESC`
- Limit: `100`

## Example Request Shape

```json
{
  "datasetId": "sales-dataset",
  "rows": [
    "field.dimcustomer.country"
  ],
  "values": [
    "metric.sum_factorders_amount"
  ],
  "filters": [
    {
      "field": "field.dimcustomer.country",
      "operator": "=",
      "value": "USA"
    },
    {
      "field": "metric.sum_factorders_amount",
      "operator": ">",
      "value": 1000
    }
  ],
  "sort": [
    {
      "field": "metric.sum_factorders_amount",
      "direction": "DESC"
    }
  ],
  "limit": 100,
  "offset": 0
}
```

## Frontend Decisions That Affect SQL

| Frontend part | Backend SQL impact |
|---|---|
| `rows[]` | Becomes dimension `SELECT` items; may become `GROUP BY` items |
| `values[]` | Becomes metric / aggregate `SELECT` items |
| `filters[]` on fields | Becomes `WHERE` filters |
| `filters[]` on metrics | Becomes `HAVING` filters |
| `sort[]` | Becomes `ORDER BY` |
| `limit` / `offset` | Becomes `TOP` or `OFFSET FETCH` |
| Runtime aggregation selection | Can create a runtime metric ID that backend resolves into aggregate SQL |

---

# 🌐 PHASE 2: BACKEND ENTRY POINT

## File

`ReportPlatform/Report.Api/Controllers/QueryController.cs`

## Purpose

The controller receives the request and delegates all real work to the query service.

```csharp
[HttpPost("execute")]
public async Task<IActionResult> Execute([FromBody] VisualQueryRequest request, CancellationToken ct)
{
    var result = await _service.ExecuteAsync(request, ct);
    return Ok(result);
}
```

## Input

```csharp
VisualQueryRequest request
```

## Output

```csharp
VisualQueryResult
```

At this phase there is still no final SQL string produced by the controller. The controller only forwards the request.

---

# ⚙️ PHASE 3: ORCHESTRATION IN REPORT QUERY SERVICE

## File

`ReportPlatform/Report.QueryEngine/Services/ReportQueryService.cs`

## Purpose

`ReportQueryService.ExecuteAsync()` is the main backend pipeline. It transforms frontend semantic intent into executable SQL.

## Typical Internal Flow

```csharp
public async Task<ComprehensiveQueryResponse> ExecuteAsync(VisualQueryRequest request, CancellationToken ct)
{
    // 1. Create execution record/fingerprint and load semantic model
    var model = await _modelStore.LoadAsync(request.DatasetId, ct);
    var context = new ValidationContext(request, model);

    // 2. Run semantic binding/context validation stages
    var st1 = Run(_stage1.Validate(context));
    var st2 = Run(_stage2.Validate(context));

    // 3. Bind request IDs to semantic fields/metrics, then build evaluation context
    var bound = _binder.Bind(request, model);
    var eval = _contextBuilder.Build(bound);

    // 4. Expand measures, build joins, build logical plan
    var measures = _measureEngine.Expand(eval, model);
    var joins = _relationshipEngine.Build(eval, measures, model);
    var logical = _planBuilder.Build(eval, measures, joins, model);

    // 5. Compile final SQL and validate compilation
    var sql = _sqlCompiler.Compile(logical);
    var st6 = Run(_stage6.Validate(sql));

    // 6. Execute SQL, build artifact SQL, save artifact, return response
    var expected = logical.Select.Select(s => new QueryColumn { Name = s.Alias }).ToList();
    var queryResult = await _queryExecutor.ExecuteAsync(request.ConnectionId, sql, expected, ct);
    return Build(...);
}
```

## Key Point

`SqlCompiler.Compile()` is late in the pipeline. Before it runs, these decisions are already finished:

- Which physical tables are required.
- Which table is the base table.
- Which relationships become joins.
- Which expressions appear in `SELECT`.
- Which filters are `WHERE` vs `HAVING`.
- Which dimensions must be included in `GROUP BY`.
- Which aliases are safe SQL aliases.

---

# 🧩 PHASE 4: SEMANTIC BINDING BEFORE SQL EXISTS

## File

`ReportPlatform/Report.QueryEngine/Binding/SemanticModelBinder.cs`

## Purpose

The binder converts frontend IDs into typed semantic objects.

## Input

```json
{
  "rows": ["field.dimcustomer.country"],
  "values": ["metric.sum_factorders_amount"],
  "filters": [
    { "field": "field.dimcustomer.country", "operator": "=", "value": "USA" },
    { "field": "metric.sum_factorders_amount", "operator": ">", "value": 1000 }
  ]
}
```

## Processing

The binder:

1. Resolves row field IDs to `SemanticField` objects.
2. Resolves value IDs to `SemanticMetric` objects or runtime aggregate metrics.
3. Resolves filter field IDs.
4. Determines filter target type:
   - Field target → `TargetType = "dimension"`
   - Metric target → `TargetType = "metric"`
5. Validates filter operator compatibility:
   - `CONTAINS` only makes sense for strings.
   - `>`, `<`, `>=`, `<=`, `BETWEEN` are for numeric/date-like values.
   - `IN` is list-based.
6. Resolves sorts to dimension or metric targets.

## Output Example

```csharp
BoundVisualQuery
{
    GroupFields = [Country field],
    Metrics = [Sum Amount metric],
    Filters = [
        BoundFilter
        {
            TargetId = "field.dimcustomer.country",
            TargetType = "dimension",
            SqlExpression = "DimCustomer.[Country]",
            Operator = "=",
            Value = "USA"
        },
        BoundFilter
        {
            TargetId = "metric.sum_factorders_amount",
            TargetType = "metric",
            Operator = ">",
            Value = 1000
        }
    ]
}
```

## Why This Matters for the Compiler

The compiler does not know if `Country` is a field or `SUM(Amount)` is a metric. It only receives `plan.Where` and `plan.Having`. The binder and context builder create that separation before compilation.

---

# 🧠 PHASE 5: WHERE VS HAVING DECISION

## File

`ReportPlatform/Report.QueryEngine/Context/EvaluationContextBuilder.cs`

## Rule

```text
Dimension / field filters → WHERE
Metric / measure filters   → HAVING
```

## Example

Frontend sends:

```json
{
  "filters": [
    { "field": "field.dimcustomer.country", "operator": "=", "value": "USA" },
    { "field": "metric.sum_factorders_amount", "operator": ">", "value": 1000 }
  ]
}
```

Context builder separates them:

```csharp
EvaluationContext
{
    WhereFilters = [Country = "USA"],
    HavingFilters = [SUM Amount > 1000]
}
```

## SQL Result

```sql
WHERE
  c.[Country] = @p0
GROUP BY
  c.[Country]
HAVING
  SUM(f.[Amount]) > @p1
```

## Important

A calculated measure filter also goes to `HAVING`, because it is a metric expression. A calculated column filter goes to `WHERE`, because it is a row-level/dimension expression.

---

# 🧮 PHASE 6: MEASURE AND CALCULATED EXPRESSION EXPANSION

## Files

- `ReportPlatform/Report.QueryEngine/Measures/MeasureExpansionEngine.cs`
- `ReportPlatform/Report.QueryEngine/Compilation/SemanticExpressionCompiler.cs`
- `ReportPlatform/Report.QueryEngine/Expressions/Compilation/SemanticExpressionSqlCompiler.cs`

## Purpose

Before the final SQL compiler runs, selected measures are expanded into SQL expressions.

## Normal Measure Example

Semantic metric:

```json
{
  "id": "metric.sum_factorders_amount",
  "baseTableId": "FactOrders",
  "formula": "SUM([FactOrders].[Amount])"
}
```

Expanded measure:

```csharp
ExpandedMeasure
{
    MetricId = "metric.sum_factorders_amount",
    Alias = "TotalAmount",
    BaseTableId = "FactOrders",
    SqlExpression = "SUM(FactOrders.[Amount])"
}
```

Later, `LogicalPlanBuilder` applies table aliases:

```sql
SUM(f.[Amount])
```

## Calculated Measure Example

User creates:

```text
Profit Margin = SUM(Profit) / SUM(Revenue)
```

The expression compiler can produce:

```sql
SUM(FactSales.[Profit]) / NULLIF(SUM(FactSales.[Revenue]), 0)
```

Then the plan builder applies aliases:

```sql
SUM(f.[Profit]) / NULLIF(SUM(f.[Revenue]), 0)
```

## Calculated Column Example

User creates:

```text
Customer Label = CustomerName + ' - ' + Country
```

A selected calculated column may become a dimension select expression:

```sql
CONCAT(c.[CustomerName], ' - ', c.[Country]) AS [CustomerLabel]
```

If selected together with a measure, the same calculated expression must also be repeated in `GROUP BY`:

```sql
GROUP BY
  CONCAT(c.[CustomerName], ' - ', c.[Country])
```

---

# 🔗 PHASE 7: RELATIONSHIP TRAVERSAL BUILDS JOIN INTENT

## File

`ReportPlatform/Report.QueryEngine/Relationships/RelationshipTraversalEngine.cs`

## Purpose

The query may need fields and measures from different tables. The relationship engine chooses a base table and join paths.

## Example Requirement

Selected:

- Dimension: `DimCustomer.Country`
- Measure: `SUM(FactOrders.Amount)`

Required tables:

```text
DimCustomer
FactOrders
```

Relationship:

```text
FactOrders.CustomerId → DimCustomer.CustomerId
```

Join plan result:

```csharp
JoinPlan
{
    BaseTableId = "FactOrders",
    Joins = [
        JoinStep
        {
            FromTableId = "FactOrders",
            ToTableId = "DimCustomer",
            FromColumn = "CustomerId",
            ToColumn = "CustomerId",
            JoinType = "LEFT"
        }
    ]
}
```

## Why This Matters for SQL Compiler

The compiler only sees this as a `JoinItem` already in the logical plan:

```csharp
JoinItem
{
    JoinType = "LEFT",
    TableId = "DimCustomer",
    Alias = "c",
    Condition = "f.[CustomerId] = c.[CustomerId]"
}
```

The compiler renders it directly:

```sql
LEFT JOIN [dbo].[DimCustomer] c ON f.[CustomerId] = c.[CustomerId]
```

---

# 🏗️ PHASE 8: LOGICAL PLAN BUILDER CREATES THE COMPILER INPUT

## File

`ReportPlatform/Report.QueryEngine/Planning/LogicalPlanBuilder.cs`

## Purpose

The logical plan builder converts semantic query context into a `LogicalQueryPlan`. This is the direct input to `SqlCompiler`.

## Compiler Input Type

`ReportPlatform/Report.QueryEngine/Planning/LogicalQueryPlan.cs`

```csharp
public sealed class LogicalQueryPlan
{
    public required string BaseTableId { get; init; }
    public required IReadOnlyDictionary<string, string> TableExpressions { get; init; }
    public required IReadOnlyDictionary<string, string> Aliases { get; init; }
    public required IReadOnlyList<SelectItem> Select { get; init; }
    public required IReadOnlyList<JoinItem> Joins { get; init; }
    public required IReadOnlyList<FilterItem> Where { get; init; }
    public required IReadOnlyList<FilterItem> Having { get; init; }
    public required IReadOnlyList<string> GroupBy { get; init; }
    public required IReadOnlyList<OrderItem> OrderBy { get; init; }
    public required int Limit { get; init; }
    public required int Offset { get; init; }
}
```

## What Each Property Means

| Property | Meaning | Final SQL location |
|---|---|---|
| `BaseTableId` | Physical/semantic table used after `FROM` | `FROM <table> <alias>` |
| `TableExpressions` | Table ID → quoted physical table expression | `FROM`, `JOIN` |
| `Aliases` | Table ID → short SQL alias | `f`, `c`, `d`, ... |
| `Select` | Final expressions and output aliases | `SELECT expr AS [Alias]` |
| `Joins` | Join type, table, alias, condition | `LEFT JOIN ... ON ...` |
| `Where` | Dimension filters | `WHERE ...` |
| `Having` | Metric filters | `HAVING ...` |
| `GroupBy` | Dimension expressions used for grouping | `GROUP BY ...` |
| `OrderBy` | Sort expressions or select aliases | `ORDER BY ...` |
| `Limit` | Max rows | `TOP` or `FETCH NEXT` |
| `Offset` | Rows to skip | `OFFSET` |

## Example Logical Plan

```csharp
LogicalQueryPlan
{
    BaseTableId = "FactOrders",
    TableExpressions = new Dictionary<string, string>
    {
        ["FactOrders"] = "[dbo].[FactOrders]",
        ["DimCustomer"] = "[dbo].[DimCustomer]"
    },
    Aliases = new Dictionary<string, string>
    {
        ["FactOrders"] = "f",
        ["DimCustomer"] = "c"
    },
    Select = [
        new SelectItem("c.[Country]", "Country", "dimension"),
        new SelectItem("SUM(f.[Amount])", "TotalAmount", "metric")
    ],
    Joins = [
        new JoinItem("LEFT", "DimCustomer", "c", "f.[CustomerId] = c.[CustomerId]")
    ],
    Where = [
        new FilterItem("c.[Country]", "=", "USA")
    ],
    GroupBy = ["c.[Country]"],
    Having = [
        new FilterItem("SUM(f.[Amount])", ">", 1000)
    ],
    OrderBy = [
        new OrderItem("TotalAmount", true, "DESC")
    ],
    Limit = 100,
    Offset = 0
}
```

This is the exact kind of object the compiler knows how to render.

---

# 🧱 PHASE 9: SQL IDENTIFIER QUOTING AND SAFE ALIASES

## File

`ReportPlatform/Report.QueryEngine/Compilation/SqlIdentifier.cs`

## Purpose

Before or during planning, table names, column names, and aliases are made safe.

## `Quote()`

Input:

```text
Customer Name
```

Output:

```sql
[Customer Name]
```

If the identifier contains `]`, it is escaped as `]]`.

## `QuoteTable()`

If the semantic table has physical schema and physical table name:

```text
Schema = dbo
Table = FactOrders
```

Output:

```sql
[dbo].[FactOrders]
```

If the table ID contains dots, each part is quoted.

## `QuoteColumn()`

Input:

```text
Amount
```

Output:

```sql
[Amount]
```

If already bracketed, it is left unchanged.

## `SafeAlias()`

Aliases remove unsafe characters. For example:

| Display name | Safe alias |
|---|---|
| `Total Amount` | `TotalAmount` |
| `Customer.Country` | `CustomerCountry` |
| `Profit %` | `Profit` |
| empty/null | `Field` |

## Why Alias Safety Matters

The compiler renders:

```csharp
$"{x.Expression} AS [{x.Alias}]"
```

So if the planner gives alias `TotalAmount`, SQL becomes:

```sql
SUM(f.[Amount]) AS [TotalAmount]
```

---

# 🧾 PHASE 10: SQL COMPILER ENTRY POINT

## File

`ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs`

## Main Function

```csharp
public SqlCompilationResult Compile(LogicalQueryPlan plan)
```

## Input

```csharp
LogicalQueryPlan plan
```

## Output

```csharp
SqlCompilationResult
{
    Sql = "SELECT ...",
    Parameters = new Dictionary<string, object?>
    {
        ["p0"] = "USA",
        ["p1"] = 1000
    }
}
```

## Important

`SqlCompiler` is intentionally simple. It does not:

- Resolve semantic fields.
- Resolve metric IDs.
- Discover relationships.
- Validate whether a filter is dimension or metric.
- Decide whether a measure can be selected.
- Decide whether a calculated expression is valid.

Those are upstream responsibilities.

---

# 🔬 PHASE 11: INSIDE `SqlCompiler.Compile()` STEP BY STEP

## 11A: Create Parameter Store

```csharp
var parameters = new Dictionary<string, object?>();
var nextParameterIndex = 0;
```

Every filter value becomes a parameter:

```text
p0, p1, p2, ...
```

SQL uses:

```sql
@p0, @p1, @p2
```

Parameter dictionary stores:

```csharp
{
    ["p0"] = "USA",
    ["p1"] = 1000
}
```

## 11B: Find Base Alias

```csharp
var baseAlias = plan.Aliases[plan.BaseTableId];
```

Example:

```text
BaseTableId = FactOrders
Aliases[FactOrders] = f
```

Used in final SQL:

```sql
FROM [dbo].[FactOrders] f
```

## 11C: Decide Whether `TOP` Can Be Used

```csharp
var usesTopForLimit = plan.Limit > 0 && plan.Offset == 0 && plan.OrderBy.Count == 0;
var top = usesTopForLimit ? $" TOP ({plan.Limit})" : string.Empty;
```

`TOP` is used only for the simplest limit case:

```sql
SELECT TOP (100)
  ...
```

If there is sorting or offset paging, compiler uses `OFFSET FETCH` instead.

## 11D: Compile `SELECT`

```csharp
var select = string.Join(",\n  ", plan.Select.Select(x => $"{x.Expression} AS [{x.Alias}]"));
```

Input:

```csharp
Select = [
    new SelectItem("c.[Country]", "Country", "dimension"),
    new SelectItem("SUM(f.[Amount])", "TotalAmount", "metric")
]
```

Output:

```sql
c.[Country] AS [Country],
SUM(f.[Amount]) AS [TotalAmount]
```

## 11E: Compile `JOIN`

```csharp
var joins = string.Join("\n", plan.Joins.Select(j =>
    $"{j.JoinType} JOIN {plan.TableExpressions[j.TableId]} {j.Alias} ON {j.Condition}"));
```

Input:

```csharp
JoinItem("LEFT", "DimCustomer", "c", "f.[CustomerId] = c.[CustomerId]")
```

Output:

```sql
LEFT JOIN [dbo].[DimCustomer] c ON f.[CustomerId] = c.[CustomerId]
```

## 11F: Compile `WHERE`

```csharp
var where = plan.Where.Count > 0
    ? "WHERE\n  " + string.Join("\n  AND ", plan.Where.Select(CompileFilter))
    : string.Empty;
```

Input:

```csharp
Where = [
    new FilterItem("c.[Country]", "=", "USA"),
    new FilterItem("c.[Region]", "IN", new[] { "West", "East" })
]
```

Output:

```sql
WHERE
  c.[Country] = @p0
  AND c.[Region] IN (@p1, @p2)
```

Parameters:

```csharp
{
    ["p0"] = "USA",
    ["p1"] = "West",
    ["p2"] = "East"
}
```

## 11G: Compile `GROUP BY`

```csharp
var groupBy = plan.GroupBy.Count > 0
    ? "GROUP BY\n  " + string.Join(",\n  ", plan.GroupBy)
    : string.Empty;
```

Input:

```csharp
GroupBy = ["c.[Country]", "d.[Year]"]
```

Output:

```sql
GROUP BY
  c.[Country],
  d.[Year]
```

The compiler does not decide if grouping is required. It only renders the `GroupBy` list it receives.

## 11H: Compile `HAVING`

```csharp
var having = plan.Having.Count > 0
    ? "HAVING\n  " + string.Join("\n  AND ", plan.Having.Select(CompileFilter))
    : string.Empty;
```

Input:

```csharp
Having = [
    new FilterItem("SUM(f.[Amount])", ">", 1000)
]
```

Output:

```sql
HAVING
  SUM(f.[Amount]) > @p3
```

The parameter number depends on how many `WHERE` parameters were created first.

## 11I: Compile Paging

```csharp
var paging = plan.Limit > 0 && !usesTopForLimit
    ? $"OFFSET {plan.Offset} ROWS FETCH NEXT {plan.Limit} ROWS ONLY"
    : string.Empty;
```

Examples:

| Plan | SQL |
|---|---|
| `Limit=100`, `Offset=0`, no sort | `TOP (100)` |
| `Limit=100`, `Offset=0`, has sort | `OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY` |
| `Limit=100`, `Offset=200` | `OFFSET 200 ROWS FETCH NEXT 100 ROWS ONLY` |

## 11J: Compile `ORDER BY`

```csharp
var orderBy = plan.OrderBy.Count > 0
    ? "ORDER BY\n  " + string.Join(",\n  ", plan.OrderBy.Select(x => $"{CompileOrderExpression(x)} {x.Direction}"))
    : plan.Offset > 0
        ? "ORDER BY (SELECT NULL)"
        : string.Empty;
```

If sorting by selected alias:

```csharp
new OrderItem("TotalAmount", true, "DESC")
```

Compiler output:

```sql
ORDER BY
  [TotalAmount] DESC
```

If sorting by a raw expression:

```csharp
new OrderItem("c.[Country]", false, "ASC")
```

Compiler output:

```sql
ORDER BY
  c.[Country] ASC
```

If `Offset > 0` but no user sort exists, SQL Server still requires an `ORDER BY` for `OFFSET`, so compiler emits:

```sql
ORDER BY (SELECT NULL)
OFFSET 200 ROWS FETCH NEXT 100 ROWS ONLY
```

## 11K: Assemble Final SQL String

```csharp
var sql = $@"SELECT{top}
  {select}
FROM {plan.TableExpressions[plan.BaseTableId]} {baseAlias}
{joins}
{where}
{groupBy}
{having}
{orderBy}
{paging}".Trim();
```

All previously compiled clause strings are concatenated into the final SQL script.

## 11L: Return Result

```csharp
return new SqlCompilationResult
{
    Sql = sql,
    Parameters = parameters
};
```

---

# 🧪 PHASE 12: FILTER COMPILATION DETAILS

## Function

`CompileFilter(FilterItem filter)` inside `SqlCompiler.Compile()`

## Supported Operators

| Operator | SQL output pattern | Example |
|---|---|---|
| `=` | `<expr> = @pN` | `c.[Country] = @p0` |
| `!=` | `<expr> <> @pN` | `c.[Status] <> @p1` |
| `>` | `<expr> > @pN` | `SUM(f.[Amount]) > @p2` |
| `<` | `<expr> < @pN` | `f.[Amount] < @p3` |
| `>=` | `<expr> >= @pN` | `d.[Year] >= @p4` |
| `<=` | `<expr> <= @pN` | `d.[Year] <= @p5` |
| `CONTAINS` | `<expr> LIKE '%' + @pN + '%'` | `c.[Name] LIKE '%' + @p6 + '%'` |
| `IN` | `<expr> IN (@pN, @pN+1, ...)` | `c.[Region] IN (@p7, @p8)` |
| `BETWEEN` | `<expr> BETWEEN @pN AND @pN+1` | `d.[Date] BETWEEN @p9 AND @p10` |

## Normal Comparison Example

Input:

```csharp
new FilterItem("c.[Country]", "=", "USA")
```

Compiler calls:

```csharp
AddParameter("USA")
```

Parameter added:

```csharp
parameters["p0"] = "USA";
```

SQL fragment:

```sql
c.[Country] = @p0
```

## `CONTAINS` Example

Input:

```csharp
new FilterItem("c.[CustomerName]", "CONTAINS", "john")
```

Output:

```sql
c.[CustomerName] LIKE '%' + @p0 + '%'
```

Parameters:

```csharp
{ "p0": "john" }
```

## `IN` Example

Input:

```csharp
new FilterItem("c.[Region]", "IN", new[] { "West", "East" })
```

Output:

```sql
c.[Region] IN (@p0, @p1)
```

Parameters:

```csharp
{
    "p0": "West",
    "p1": "East"
}
```

## `BETWEEN` Example

Input:

```csharp
new FilterItem("d.[OrderDate]", "BETWEEN", new[] { "2026-01-01", "2026-12-31" })
```

Output:

```sql
d.[OrderDate] BETWEEN @p0 AND @p1
```

Parameters:

```csharp
{
    "p0": "2026-01-01",
    "p1": "2026-12-31"
}
```

## Important Filter Rule

Multiple filters are always joined with `AND` in the compiler:

```sql
WHERE
  filter1
  AND filter2
  AND filter3
```

There is no final-stage compiler logic for `OR` groups. If `OR` support is needed, the logical plan and compiler contract would need to change.

---

# 🧾 PHASE 13: FINAL SQL EXAMPLE - DIMENSION + MEASURE + WHERE + HAVING

## Input Visual Query Request

```json
{
  "datasetId": "sales-dataset",
  "rows": ["field.dimcustomer.country"],
  "values": ["metric.sum_factorders_amount"],
  "filters": [
    {
      "field": "field.dimcustomer.country",
      "operator": "=",
      "value": "USA"
    },
    {
      "field": "metric.sum_factorders_amount",
      "operator": ">",
      "value": 1000
    }
  ],
  "sort": [
    {
      "field": "metric.sum_factorders_amount",
      "direction": "DESC"
    }
  ],
  "limit": 100,
  "offset": 0
}
```

## Logical Plan Given to Compiler

```csharp
LogicalQueryPlan
{
    BaseTableId = "FactOrders",
    TableExpressions = {
        ["FactOrders"] = "[dbo].[FactOrders]",
        ["DimCustomer"] = "[dbo].[DimCustomer]"
    },
    Aliases = {
        ["FactOrders"] = "f",
        ["DimCustomer"] = "c"
    },
    Select = [
        SelectItem("c.[Country]", "Country", "dimension"),
        SelectItem("SUM(f.[Amount])", "TotalAmount", "metric")
    ],
    Joins = [
        JoinItem("LEFT", "DimCustomer", "c", "f.[CustomerId] = c.[CustomerId]")
    ],
    Where = [
        FilterItem("c.[Country]", "=", "USA")
    ],
    GroupBy = [
        "c.[Country]"
    ],
    Having = [
        FilterItem("SUM(f.[Amount])", ">", 1000)
    ],
    OrderBy = [
        OrderItem("TotalAmount", true, "DESC")
    ],
    Limit = 100,
    Offset = 0
}
```

## Final SQL

```sql
SELECT
  c.[Country] AS [Country],
  SUM(f.[Amount]) AS [TotalAmount]
FROM [dbo].[FactOrders] f
LEFT JOIN [dbo].[DimCustomer] c ON f.[CustomerId] = c.[CustomerId]
WHERE
  c.[Country] = @p0
GROUP BY
  c.[Country]
HAVING
  SUM(f.[Amount]) > @p1
ORDER BY
  [TotalAmount] DESC
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY
```

## Parameters

```json
{
  "p0": "USA",
  "p1": 1000
}
```

## Why No `TOP` Here?

Even though `Limit = 100` and `Offset = 0`, there is an `ORDER BY`. The compiler only uses `TOP` when there is no `ORDER BY`. Because a sort exists, it uses:

```sql
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY
```

---

# 🧾 PHASE 14: FINAL SQL EXAMPLE - SIMPLE LIMIT USES TOP

## Logical Plan

```csharp
LogicalQueryPlan
{
    BaseTableId = "DimCustomer",
    TableExpressions = { ["DimCustomer"] = "[dbo].[DimCustomer]" },
    Aliases = { ["DimCustomer"] = "c" },
    Select = [
        SelectItem("c.[CustomerName]", "CustomerName", "dimension")
    ],
    Joins = [],
    Where = [],
    GroupBy = [],
    Having = [],
    OrderBy = [],
    Limit = 50,
    Offset = 0
}
```

## Final SQL

```sql
SELECT TOP (50)
  c.[CustomerName] AS [CustomerName]
FROM [dbo].[DimCustomer] c
```

## Parameters

```json
{}
```

---

# 🧾 PHASE 15: FINAL SQL EXAMPLE - OFFSET WITHOUT SORT

## Logical Plan

```csharp
LogicalQueryPlan
{
    BaseTableId = "DimCustomer",
    TableExpressions = { ["DimCustomer"] = "[dbo].[DimCustomer]" },
    Aliases = { ["DimCustomer"] = "c" },
    Select = [
        SelectItem("c.[CustomerName]", "CustomerName", "dimension")
    ],
    Joins = [],
    Where = [],
    GroupBy = [],
    Having = [],
    OrderBy = [],
    Limit = 50,
    Offset = 100
}
```

## Final SQL

```sql
SELECT
  c.[CustomerName] AS [CustomerName]
FROM [dbo].[DimCustomer] c
ORDER BY (SELECT NULL)
OFFSET 100 ROWS FETCH NEXT 50 ROWS ONLY
```

## Why `ORDER BY (SELECT NULL)`?

SQL Server requires `ORDER BY` when using `OFFSET`. If the user did not provide sorting but offset paging is required, the compiler creates a neutral placeholder order.

---

# 🧾 PHASE 16: FINAL SQL EXAMPLE - CALCULATED COLUMN + CALCULATED MEASURE

## User-Defined Objects

Calculated column:

```text
CustomerLabel = CustomerName + ' - ' + Country
```

Calculated measure:

```text
ProfitMargin = SUM(Profit) / SUM(Revenue)
```

## Query Request

```json
{
  "rows": ["field.dimcustomer.customer_label"],
  "values": ["metric.profit_margin"],
  "filters": [
    {
      "field": "field.dimcustomer.customer_label",
      "operator": "CONTAINS",
      "value": "USA"
    },
    {
      "field": "metric.profit_margin",
      "operator": ">=",
      "value": 0.25
    }
  ],
  "sort": [
    {
      "field": "metric.profit_margin",
      "direction": "DESC"
    }
  ],
  "limit": 25,
  "offset": 0
}
```

## Logical Plan Shape

```csharp
Select = [
    SelectItem("CONCAT(c.[CustomerName], ' - ', c.[Country])", "CustomerLabel", "dimension"),
    SelectItem("SUM(f.[Profit]) / NULLIF(SUM(f.[Revenue]), 0)", "ProfitMargin", "metric")
]
Where = [
    FilterItem("CONCAT(c.[CustomerName], ' - ', c.[Country])", "CONTAINS", "USA")
]
GroupBy = [
    "CONCAT(c.[CustomerName], ' - ', c.[Country])"
]
Having = [
    FilterItem("SUM(f.[Profit]) / NULLIF(SUM(f.[Revenue]), 0)", ">=", 0.25)
]
OrderBy = [
    OrderItem("ProfitMargin", true, "DESC")
]
```

## Final SQL

```sql
SELECT
  CONCAT(c.[CustomerName], ' - ', c.[Country]) AS [CustomerLabel],
  SUM(f.[Profit]) / NULLIF(SUM(f.[Revenue]), 0) AS [ProfitMargin]
FROM [dbo].[FactSales] f
LEFT JOIN [dbo].[DimCustomer] c ON f.[CustomerId] = c.[CustomerId]
WHERE
  CONCAT(c.[CustomerName], ' - ', c.[Country]) LIKE '%' + @p0 + '%'
GROUP BY
  CONCAT(c.[CustomerName], ' - ', c.[Country])
HAVING
  SUM(f.[Profit]) / NULLIF(SUM(f.[Revenue]), 0) >= @p1
ORDER BY
  [ProfitMargin] DESC
OFFSET 0 ROWS FETCH NEXT 25 ROWS ONLY
```

## Parameters

```json
{
  "p0": "USA",
  "p1": 0.25
}
```

## Key Lesson

Calculated column SQL is row-level, so it appears in:

- `SELECT`
- `WHERE` if filtered
- `GROUP BY` if selected with measures
- `ORDER BY` if sorted as a dimension and not using select alias

Calculated measure SQL is aggregate-level, so it appears in:

- `SELECT`
- `HAVING` if filtered
- `ORDER BY` usually by select alias

---

# 🧾 PHASE 17: FINAL SQL EXAMPLE - FILTER OPERATOR MATRIX

## Input Filters

```csharp
Where = [
    FilterItem("c.[Country]", "=", "USA"),
    FilterItem("c.[Status]", "!=", "Inactive"),
    FilterItem("c.[CustomerName]", "CONTAINS", "smith"),
    FilterItem("c.[Region]", "IN", new[] { "West", "East" }),
    FilterItem("d.[OrderDate]", "BETWEEN", new[] { "2026-01-01", "2026-12-31" })
]
```

## Final WHERE Clause

```sql
WHERE
  c.[Country] = @p0
  AND c.[Status] <> @p1
  AND c.[CustomerName] LIKE '%' + @p2 + '%'
  AND c.[Region] IN (@p3, @p4)
  AND d.[OrderDate] BETWEEN @p5 AND @p6
```

## Parameters

```json
{
  "p0": "USA",
  "p1": "Inactive",
  "p2": "smith",
  "p3": "West",
  "p4": "East",
  "p5": "2026-01-01",
  "p6": "2026-12-31"
}
```

---

# 🚀 PHASE 18: SQL EXECUTION AFTER COMPILATION

## File

`ReportPlatform/Report.Infrastructure/Execution/SqlServerQueryExecutor.cs`

## Purpose

After `SqlCompiler` returns SQL text and parameters, the executor sends it to SQL Server.

## Input

```csharp
SqlCompilationResult
{
    Sql = "SELECT ... WHERE c.[Country] = @p0",
    Parameters = { ["p0"] = "USA" }
}
```

## Processing

The executor:

1. Finds the correct database connection for the dataset/model.
2. Creates a Dapper parameter object.
3. Adds each `SqlCompilationResult.Parameters` entry.
4. Executes the SQL query.
5. Returns result rows.

## Output

```csharp
IReadOnlyList<IDictionary<string, object?>> rows
```

Example rows:

```json
[
  { "Country": "USA", "TotalAmount": 15200.50 },
  { "Country": "Canada", "TotalAmount": 8200.75 }
]
```

---

# 📦 PHASE 19: OUTPUT BACK TO FRONTEND AND EXPORT PIPELINE

The compiled SQL result can be used in two ways:

## Preview / UI Query

```text
SQL Server rows
    ↓
VisualQueryResult
    ↓
Frontend table/chart preview
```

## Execution / Export Flow

```text
SQL Server rows
    ↓
Report execution artifact
    ↓
CSV/Telerik export pipeline
    ↓
Browser downloads file
```

The SQL compiler itself does not create files. It only produces the SQL text and parameters used to fetch the rows that later become preview data or export artifacts.

---

# 🔄 COMPLETE FLOW SUMMARY

```
┌─────────────────────────────────────┐
│ 1. FRONTEND QUERY BUILDER           │
│    rows, values, filters, sort      │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 2. VisualQueryRequest               │
│    semantic IDs, no SQL yet          │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 3. QueryController.Execute()         │
│    forwards request to service       │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 4. ReportQueryService.ExecuteAsync() │
│    orchestrates full query pipeline  │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 5. SemanticModelBinder.Bind()        │
│    field IDs → fields                │
│    metric IDs → metrics              │
│    filters typed as dimension/metric │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 6. EvaluationContextBuilder.Build()  │
│    dimension filters → WHERE         │
│    metric filters → HAVING           │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 7. MeasureExpansionEngine.Expand()   │
│    metric formulas → SQL aggregate   │
│    expressions                       │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 8. RelationshipTraversalEngine.Build │
│    required tables → join plan       │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 9. LogicalPlanBuilder.Build()        │
│    creates LogicalQueryPlan          │
│    SELECT/JOIN/WHERE/GROUP/HAVING    │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 10. SqlCompiler.Compile()            │
│     renders final SQL text           │
│     creates @p0, @p1 parameters      │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 11. SqlServerQueryExecutor           │
│     executes SQL against database    │
└─────────────────────────────────────┘
                ↓
┌─────────────────────────────────────┐
│ 12. Rows returned to frontend/export │
└─────────────────────────────────────┘
```

---

# 📋 FILE MAPPING TABLE

| Phase | File | Class / Function | Input | Output |
|---|---|---|---|---|
| Frontend request | `data-report-builder/lib/build-visual-query-request.ts` | `buildVisualQueryRequest()` | UI selections | `VisualQueryRequest` |
| API entry | `ReportPlatform/Report.Api/Controllers/QueryController.cs` | `Execute()` | `VisualQueryRequest` | Service call / `Ok(result)` |
| Orchestration | `ReportPlatform/Report.QueryEngine/Services/ReportQueryService.cs` | `ExecuteAsync()` | Request + dataset ID | Query result |
| Binding | `ReportPlatform/Report.QueryEngine/Binding/SemanticModelBinder.cs` | `Bind()` | Semantic model + request | `BoundVisualQuery` |
| Context | `ReportPlatform/Report.QueryEngine/Context/EvaluationContextBuilder.cs` | `Build()` | Bound query | `EvaluationContext` |
| Measure SQL | `ReportPlatform/Report.QueryEngine/Measures/MeasureExpansionEngine.cs` | `Expand()` | Metrics | `ExpandedMeasure` SQL expressions |
| Expression SQL | `ReportPlatform/Report.QueryEngine/Compilation/SemanticExpressionCompiler.cs` | expression compile methods | AST / semantic expressions | SQL expression fragments |
| Relationships | `ReportPlatform/Report.QueryEngine/Relationships/RelationshipTraversalEngine.cs` | `Build()` | Required tables | Join plan |
| Logical plan | `ReportPlatform/Report.QueryEngine/Planning/LogicalPlanBuilder.cs` | `Build()` | Context + measures + joins | `LogicalQueryPlan` |
| Plan contract | `ReportPlatform/Report.QueryEngine/Planning/LogicalQueryPlan.cs` | plan records | Clause-ready data | Compiler input |
| Identifier safety | `ReportPlatform/Report.QueryEngine/Compilation/SqlIdentifier.cs` | `Quote*()`, `SafeAlias()` | Raw names | Safe SQL identifiers |
| Final compiler | `ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs` | `Compile()` | `LogicalQueryPlan` | `SqlCompilationResult` |
| Execution | `ReportPlatform/Report.Infrastructure/Execution/SqlServerQueryExecutor.cs` | `ExecuteAsync()` | SQL + parameters | Rows |

---

# 🎯 KEY DECISION POINTS

| Decision | Made by | Compiler role |
|---|---|---|
| Field filter or metric filter? | Binder + context builder | Renders `plan.Where` or `plan.Having` only |
| Which table is base table? | Relationship traversal | Uses `plan.BaseTableId` |
| Which joins are needed? | Relationship traversal + plan builder | Renders `plan.Joins` |
| Which dimensions are grouped? | Logical plan builder | Renders `plan.GroupBy` |
| How is a measure calculated? | Measure expansion / semantic expression compiler | Renders final measure expression from `plan.Select` and `plan.Having` |
| Should `TOP` be used? | `SqlCompiler` | Uses `TOP` only when limit exists, no offset, no order |
| Should `OFFSET FETCH` be used? | `SqlCompiler` | Uses when limit exists and `TOP` is not valid |
| How are values parameterized? | `SqlCompiler` | Creates `@p0`, `@p1`, ... |
| How are aliases rendered? | Planner creates safe alias; compiler brackets it | `expr AS [Alias]` |

---

# 💡 IMPORTANT NOTES AND LIMITATIONS

1. **Compiler is not semantic-aware**
   It renders the plan. It does not know whether an expression is a column, calculated column, normal measure, or calculated measure.

2. **WHERE/HAVING separation happens before compilation**
   If a metric filter appears in `WHERE`, that is a planner/binder bug, not a compiler decision.

3. **All filters are ANDed**
   `SqlCompiler` joins all `WHERE` filters with `AND` and all `HAVING` filters with `AND`.

4. **Parameter names reset per compile**
   Every `Compile()` call starts at `p0`. Parameter order follows render order: `WHERE` filters first, then `HAVING` filters.

5. **`IN` and `BETWEEN` are expanded to multiple parameters**
   `IN` creates one parameter per item. `BETWEEN` requires exactly two values.

6. **SELECT aliases are always bracketed**
   Example: `SUM(f.[Amount]) AS [TotalAmount]`.

7. **Sort can use alias or raw expression**
   `OrderItem.IsAlias = true` renders `[Alias]`; otherwise the compiler renders the expression directly.

8. **`TOP` and `OFFSET FETCH` are mutually exclusive**
   `TOP` is used only for no-sort/no-offset limits. Sorting or offset uses `OFFSET FETCH`.

9. **`ORDER BY (SELECT NULL)` is a paging fallback**
   It appears only when offset paging needs an order but no user sort exists.

10. **The `ParameterBindings` property on `LogicalQueryPlan` is not the final parameter dictionary**
    The compiler creates its own `SqlCompilationResult.Parameters` from `FilterItem.Value` while rendering filters.

11. **Blank clauses are omitted by using empty strings**
    The final interpolated SQL is trimmed. Some internal line gaps can appear when optional clauses are empty, but the SQL remains valid.

12. **Unsupported operators fail at compile time if they reach the compiler**
    The binder should reject invalid operators earlier, but `SqlCompiler` also throws for unsupported filter operators.

---

# ✅ MENTAL MODEL

Think of the backend query pipeline like this:

```text
Frontend request = what user wants
Semantic binder = what those IDs mean
Evaluation context = where filters belong
Measure engine = how metrics become aggregate SQL
Relationship engine = how tables connect
Logical plan builder = SQL clause blueprint
SqlCompiler = string renderer + parameterizer
Executor = sends final SQL to SQL Server
```

The final SQL script is not created in one place from scratch. It is built progressively, but the exact final string is assembled in:

```text
ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs
```
