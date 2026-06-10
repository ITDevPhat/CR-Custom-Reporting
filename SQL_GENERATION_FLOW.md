# 📍 COMPLETE FLOW TRACE: Workload → SQL Generation Pipeline

## 🎬 END-TO-END JOURNEY: Selected Fields/Measures/Filters/Sorts → Final SQL

```text
Frontend Report Builder
    ↓
buildVisualQueryRequest()
    ├─ selected dimension/calculated columns → rows[]
    ├─ selected semantic/calculated/runtime measures → values[]
    ├─ report filters → filters[]
    ├─ sort rules → sort[]
    └─ preview limit/offset → limit/offset
    ↓
POST /api/query/execute
    ↓
QueryController.Execute()
    ↓
ReportQueryService.ExecuteAsync()
    ├─ Load SemanticModel by datasetId
    ├─ Validation stage 1/2
    ├─ SemanticModelBinder.Bind()
    │   ├─ rows[] → SemanticField list
    │   ├─ values[] → SemanticMetric list or runtime aggregate metric
    │   ├─ field filters → ResolvedFilter TargetType=dimension
    │   ├─ metric filters → ResolvedFilter TargetType=metric
    │   └─ sort[] → ResolvedSort dimension/metric
    ├─ EvaluationContextBuilder.Build()
    │   ├─ dimension filters → WHERE filters
    │   └─ metric filters → HAVING filters
    ├─ MeasureExpansionEngine.Expand()
    │   └─ metric formula → aggregate SQL expression
    ├─ RelationshipTraversalEngine.Build()
    │   └─ selected tables → JoinPlan
    ├─ LogicalPlanBuilder.Build()
    │   ├─ SELECT expressions + AS aliases
    │   ├─ JOIN items
    │   ├─ WHERE filter items
    │   ├─ GROUP BY expressions
    │   ├─ HAVING filter items
    │   └─ ORDER BY items
    └─ SqlCompiler.Compile()
        ├─ SELECT / FROM / JOIN
        ├─ WHERE with parameters
        ├─ GROUP BY
        ├─ HAVING with parameters
        ├─ ORDER BY
        └─ TOP or OFFSET/FETCH
    ↓
SqlCompilationResult { Sql, Parameters }
    ↓
SqlServerQueryExecutor.ExecuteAsync()
    ↓
QueryResult + artifact for export
```

---

# 🔍 PHASE-BY-PHASE BREAKDOWN

## 🎯 PHASE 1: FRONTEND BUILDS `VisualQueryRequest`

**File**: `data-report-builder/lib/build-visual-query-request.ts`

The frontend converts UI report state into the backend request:

```ts
return {
  connectionId: state.connectionId,
  datasetId: state.datasetId,
  reportId: state.reportId,
  visualType: state.visualType ?? 'table',
  rows,
  columns: [],
  values,
  filters,
  sort,
  limit: Math.min(Math.max(state.limit ?? 100, 1), 1000),
  offset: Math.max(state.offset ?? 0, 0),
}
```

## 1A: Rows = Dimension / Calculated Column Fields

```ts
const rows = selected
  .filter((field) =>
    (!field.aggregation || field.placement !== 'values') &&
    (field.kind === 'field' || field.kind === 'derived') &&
    field.role !== 'metric')
  .map((field) => field.id)
```

**Goes to SQL**:

- `SELECT` as dimension expression with `AS [Alias]`.
- `GROUP BY` if at least one measure exists.
- Can be used in `WHERE` if filter targets this field.
- Can be used in `ORDER BY` by alias if selected.

## 1B: Values = Semantic / Calculated / Runtime Measures

```ts
const values = selected.flatMap((field) => {
  if (field.kind === 'metric' || field.role === 'metric') return [field.id]
  if (field.placement === 'values' && field.aggregation) {
    return [buildRuntimeMetricId(field, lookup)]
  }
  return []
})
```

**Goes to SQL**:

- `SELECT` as aggregate expression with `AS [Alias]`.
- Metric filters go to `HAVING`.
- Metric sorts go to `ORDER BY [Alias]`.

## 1C: Runtime Aggregate Metric IDs

If user drags a regular field into values and chooses `SUM`, `AVG`, etc., frontend builds a runtime metric ID:

```ts
return `metric.${field.aggregation!.toLowerCase()}_${normalizeMetricIdPart(tableId)}_${normalizeMetricIdPart(physicalColumn)}`
```

Example:

```text
field: factsales.salesamount
aggregation: SUM
runtime metric id: metric.sum_FactSales_SalesAmount normalized to metric.sum_factsales_salesamount
```

Backend can create a temporary `SemanticMetric` from this ID even if it is not stored in `model.Metrics`.

---

# 📨 PHASE 2: BACKEND API ENTRY

**File**: `ReportPlatform/Report.Api/Controllers/QueryController.cs`

```csharp
[HttpPost("execute")]
public async Task<IActionResult> Execute(
    [FromBody] VisualQueryRequest request,
    CancellationToken ct)
{
    var result = await _service.ExecuteAsync(request, ct);
    return Ok(result);
}
```

**Input**:

```json
{
  "connectionId": "conn_002",
  "datasetId": "dataset_sales_001",
  "reportId": "rpt_001",
  "visualType": "table",
  "rows": ["dimcustomer.customername"],
  "columns": [],
  "values": ["metric.sum_factsales_salesamount"],
  "filters": [
    { "field": "dimdate.calendaryear", "operator": ">=", "value": 2023 },
    { "field": "metric.sum_factsales_salesamount", "operator": ">", "value": 10000 }
  ],
  "sort": [
    { "field": "metric.sum_factsales_salesamount", "direction": "DESC" }
  ],
  "limit": 100,
  "offset": 0
}
```

---

# ⚙️ PHASE 3: REPORT QUERY SERVICE ORCHESTRATION

**File**: `ReportPlatform/Report.QueryEngine/Services/ReportQueryService.cs`

Core pipeline:

```csharp
var model = await _modelStore.LoadAsync(request.DatasetId, ct);
var context = new ValidationContext(request, model);

var bound = _binder.Bind(request, model);
var eval = _contextBuilder.Build(bound);
var measures = _measureEngine.Expand(eval, model);
var joins = _relationshipEngine.Build(eval, measures, model);
var logical = _planBuilder.Build(eval, measures, joins, model);
var sql = _sqlCompiler.Compile(logical);
```

Each component transforms the request one level lower:

```text
VisualQueryRequest
  → BoundSemanticQuery
  → EvaluationContext
  → ExpandedMeasure[]
  → JoinPlan
  → LogicalQueryPlan
  → SqlCompilationResult
```

---

# 🧩 PHASE 4: SEMANTIC BINDING

**File**: `ReportPlatform/Report.QueryEngine/Binding/SemanticModelBinder.cs`

`SemanticModelBinder.Bind()` validates and resolves semantic IDs into model objects or resolved runtime objects.

## 4A: Rows Resolve to `SemanticField`

```csharp
var invalidRows = request.Rows
    .Where(id => !fieldIds.Contains(id) || IsUnavailableField(model, id))
```

Then:

```csharp
var rows = request.Rows
    .Select(id => model.Fields.First(f => string.Equals(f.FieldId, id, ...)))
    .ToList();
```

**Input**:

```json
"rows": ["dimcustomer.customername"]
```

**Output**:

```csharp
Rows = [ SemanticField { FieldId = "dimcustomer.customername", TableId = "DimCustomer", PhysicalColumn = "CustomerName" } ]
```

## 4B: Values Resolve to `SemanticMetric` or Runtime Metric

```csharp
var resolvedValues = ResolveMetrics(request.Values, model, errors);
```

`ResolveMetric()` checks saved metrics first:

```csharp
var metric = model.Metrics.FirstOrDefault(m => m.MetricId == metricId);
```

If not found, it tries runtime aggregate metric creation:

```csharp
RuntimeAggregateMetricFactory.TryCreate(metricId, model, out var runtimeMetric, out error)
```

### Runtime Metric Factory

**File**: `ReportPlatform/Report.QueryEngine/Measures/RuntimeAggregateMetricFactory.cs`

```csharp
metric = new SemanticMetric
{
    MetricId = BuildMetricId(field, aggregation),
    DisplayName = BuildDisplayName(field.DisplayName, aggregation),
    Formula = $"{aggregation}([{field.FieldId}])",
    BaseTableId = field.TableId,
    AggregationBehavior = aggregation is "SUM" or "COUNT" ? "additive" : "non_additive",
    DataType = aggregation.StartsWith("COUNT") ? "integer" : field.DataType,
    Format = aggregation.StartsWith("COUNT") ? "integer" : field.Format,
    IsHidden = false,
    IsDraggable = true
};
```

Allowed runtime aggregations:

```text
SUM, AVG, MIN, MAX, COUNT, COUNT_DISTINCT
```

Rules:

- `SUM` and `AVG` require numeric field data type.
- `MIN` and `MAX` reject unsupported comparable text types: `text`, `ntext`, `image`, `xml`.
- `COUNT` and `COUNT_DISTINCT` can target any visible field.

## 4C: Filters Resolve to Dimension or Metric Filters

### Field Filter → Dimension Filter → WHERE

```csharp
var field = model.Fields.FirstOrDefault(f => f.FieldId == filter.Field);
if (field is not null)
{
    resolved.Add(new ResolvedFilter
    {
        FieldId = field.FieldId,
        PhysicalTable = field.PhysicalTable,
        PhysicalColumn = field.PhysicalColumn,
        PhysicalExpression = field.IsDerived && field.Expression is not null
            ? SemanticExpressionCompiler.CompileDerivedExpression(field.Expression, model)
            : $"{field.TableId}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}",
        Operator = op,
        Value = normalizedValue,
        TargetType = "dimension"
    });
}
```

A dimension filter later becomes a `WHERE` predicate.

### Metric Filter → Metric Filter → HAVING

```csharp
var metric = ResolveMetric(filter.Field, model, out var metricError);
if (metric is not null)
{
    resolved.Add(new ResolvedFilter
    {
        FieldId = metric.MetricId,
        DataType = "decimal",
        Operator = op,
        Value = normalizedValue,
        TargetType = "metric"
    });
}
```

A metric filter later becomes a `HAVING` predicate.

## 4D: Operator Validation

Supported operators:

```text
=, !=, >, <, >=, <=, IN, BETWEEN, CONTAINS
```

Operator/data type rules:

| Operator | Allowed Types |
|---|---|
| `CONTAINS` | string only |
| `>`, `<`, `>=`, `<=`, `BETWEEN` | number or date/time |
| `=`, `!=`, `IN` | string, number, date/time |

## 4E: Filter Value Normalization

- JSON string becomes string.
- JSON number becomes `int`, `long`, or `decimal` where possible.
- JSON boolean becomes boolean.
- JSON array becomes `List<object?>`.
- `IN` accepts list or comma-separated string.
- `BETWEEN` requires exactly two values.

## 4F: Sort Resolves to Dimension or Metric Sort

Field sort:

```csharp
ResolvedSort
{
    FieldId = field.FieldId,
    TargetType = "dimension",
    PhysicalExpression = field expression or physical column expression,
    Alias = SafeAlias(field.DisplayName),
    Direction = "ASC" or "DESC"
}
```

Metric sort:

```csharp
ResolvedSort
{
    FieldId = metric.MetricId,
    TargetType = "metric",
    Alias = SafeAlias(metric.DisplayName),
    Direction = "ASC" or "DESC"
}
```

Duplicate sort fields are rejected, and only `ASC`/`DESC` directions are accepted.

---

# 🧠 PHASE 5: EVALUATION CONTEXT SPLITS WHERE VS HAVING

**File**: `ReportPlatform/Report.QueryEngine/Context/EvaluationContextBuilder.cs`

```csharp
Filters = new FilterContext
{
    WhereFilters = bound.Filters
        .Where(f => f.TargetType == "dimension")
        .ToList(),
    HavingFilters = bound.Filters
        .Where(f => f.TargetType == "metric")
        .ToList()
}
```

This is the key decision:

| Filter target | TargetType | SQL clause |
|---|---|---|
| field/dimension/calculated column | `dimension` | `WHERE` |
| metric/calculated measure/runtime aggregate | `metric` | `HAVING` |

Why?

- `WHERE` filters rows before aggregation.
- `HAVING` filters aggregate groups after `GROUP BY` and aggregate computation.

---

# 📊 PHASE 6: MEASURE EXPANSION

**File**: `ReportPlatform/Report.QueryEngine/Measures/MeasureExpansionEngine.cs`

```csharp
sqlExpression = SemanticExpressionCompiler.CompileMetricFormula(metric.Formula, model);
```

Input metric:

```csharp
SemanticMetric
{
  MetricId = "metric.sum_factsales_salesamount",
  DisplayName = "Total Sales Amount",
  Formula = "SUM([factsales.salesamount])",
  BaseTableId = "FactSales"
}
```

Output expanded measure:

```csharp
ExpandedMeasure
{
  MetricId = "metric.sum_factsales_salesamount",
  Alias = "Total Sales Amount",
  BaseTableId = "FactSales",
  SqlExpression = "SUM(FactSales.[SalesAmount])"
}
```

For calculated measures, formulas can reference other metrics and expressions are recursively compiled by the semantic expression compiler.

Example:

```text
Formula: ROUND([metric.gross_margin] / [metric.total_sales], 4)
```

Compiled shape:

```sql
ROUND((SUM(FactSales.[SalesAmount]) - SUM(FactSales.[Cost])) / NULLIF(SUM(FactSales.[SalesAmount]), 0), 4)
```

---

# 🔗 PHASE 7: RELATIONSHIP TRAVERSAL BUILDS JOIN PLAN

**File**: `ReportPlatform/Report.QueryEngine/Relationships/RelationshipTraversalEngine.cs`

The engine gathers all tables required by:

```csharp
var requestedTables = context.GroupFields.Select(f => f.TableId)
    .Concat(measures.Select(m => m.BaseTableId))
    .Concat(context.Filters.WhereFilters.Select(f => f.PhysicalTable))
    .Distinct()
    .ToList();
```

Then it chooses a base table and finds relationship paths to every required table.

Example:

```text
rows: DimCustomer.CustomerName
values: metric.sum_factsales_salesamount (base table FactSales)
where filter: DimDate.CalendarYear

requested tables:
  DimCustomer
  FactSales
  DimDate

base table:
  FactSales

joins:
  FactSales.CustomerKey -> DimCustomer.CustomerKey
  FactSales.OrderDateKey -> DimDate.DateKey
```

Output:

```csharp
JoinPlan
{
  BaseTableId = "FactSales",
  Joins = [ ... ]
}
```

---

# 🧱 PHASE 8: LOGICAL PLAN BUILDER

**File**: `ReportPlatform/Report.QueryEngine/Planning/LogicalPlanBuilder.cs`

The logical plan is the full SQL workload before string rendering.

## 8A: Determine All Tables and Aliases

```csharp
var allTables = new[] { baseTable }
    .Concat(context.GroupFields.Select(f => f.TableId))
    .Concat(context.Filters.WhereFilters.Select(f => f.PhysicalTable))
    .Concat(measures.Select(m => m.BaseTableId))
    .Concat(joinPlan.Joins.Select(j => j.FromTableId))
    .Concat(joinPlan.Joins.Select(j => j.ToTableId))
    .Distinct()
    .ToList();

var aliases = AssignAliases(allTables);
```

Alias rules:

| TableId | Preferred Alias |
|---|---|
| starts with `Fact` | `f` |
| `DimCustomer` | `c` |
| `DimDate` | `d` |
| `DimProduct` | `p` |
| other | first lower-case letter |
| collision | `t{count}` |

## 8B: SELECT Dimension Fields With `AS`

```csharp
foreach (var field in context.GroupFields)
{
    select.Add(new SelectItem
    {
        Expression = field.IsDerived && field.Expression is not null
            ? ApplyAliases(CompileDerivedExpression(field.Expression), aliases)
            : $"{aliases[field.TableId]}.{QuoteColumn(field.PhysicalColumn)}",
        Alias = SafeAlias(field.DisplayName),
        Role = "dimension"
    });
}
```

Example normal field:

```sql
c.[CustomerName] AS [Customer Name]
```

Example calculated column:

```sql
(f.[SalesAmount] - f.[TotalProductCost]) AS [Gross Margin]
```

## 8C: SELECT Measures With `AS`

```csharp
foreach (var measure in measures)
{
    var expr = ApplyAliases(measure.SqlExpression, aliases);
    aliasedMeasures[measure.MetricId] = expr;

    select.Add(new SelectItem
    {
        Expression = expr,
        Alias = measure.Alias,
        Role = "metric"
    });
}
```

Example saved metric:

```sql
SUM(f.[SalesAmount]) AS [Total Sales Amount]
```

Example calculated measure:

```sql
ROUND(SUM(f.[Profit]) / NULLIF(SUM(f.[SalesAmount]), 0), 4) AS [Profit Margin %]
```

## 8D: JOIN Items

```csharp
var joins = joinPlan.Joins.Select(j => new JoinItem
{
    JoinType = j.JoinType,
    TableId = j.ToTableId,
    Alias = aliases[j.ToTableId],
    Condition = $"{aliases[j.FromTableId]}.{QuoteColumn(j.FromColumn)} = {aliases[j.ToTableId]}.{QuoteColumn(j.ToColumn)}"
}).ToList();
```

Example:

```sql
INNER JOIN [dbo].[DimCustomer] c ON f.[CustomerKey] = c.[CustomerKey]
```

## 8E: GROUP BY Rules

```csharp
var groupBy = measures.Count > 0 && context.GroupFields.Count > 0
    ? context.GroupFields.Select(f => field expression).ToList()
    : [];
```

Rule:

| Query Shape | GROUP BY? |
|---|---|
| dimensions + measures | yes, group by every selected dimension expression |
| measures only | no group by; one aggregate row |
| dimensions only | no group by; raw selected rows |

Examples:

```sql
GROUP BY
  c.[CustomerName],
  d.[CalendarYear]
```

Calculated column group by repeats the expression, not the alias:

```sql
GROUP BY
  (f.[SalesAmount] - f.[TotalProductCost])
```

## 8F: WHERE Filter Items

```csharp
Where = context.Filters.WhereFilters
    .Select(filter => BuildDimensionFilter(filter, aliases))
    .ToList()
```

Dimension filter expression:

```csharp
Expression = string.IsNullOrWhiteSpace(filter.PhysicalExpression)
    ? $"{alias}.{QuoteColumn(filter.PhysicalColumn)}"
    : ApplyAliases(filter.PhysicalExpression, aliases)
```

Examples:

```sql
WHERE
  d.[CalendarYear] >= @p0
```

Calculated column filter:

```sql
WHERE
  (f.[SalesAmount] - f.[TotalProductCost]) > @p0
```

## 8G: HAVING Filter Items

```csharp
Having = context.Filters.HavingFilters
    .Select(filter => BuildMetricFilter(filter, aliasedMeasures))
    .ToList()
```

Metric filters must target a selected metric:

```csharp
if (!aliasedMeasures.TryGetValue(filter.FieldId, out var expression))
{
    throw new SemanticQueryValidationException(...
        "Metric filter '{id}' must target a selected metric.")
}
```

Why selected metric only?

Because the builder uses the expanded selected metric expression map. If a metric is in a filter but not selected in `values[]`, current planner refuses to build `HAVING` for it.

Example:

```sql
HAVING
  SUM(f.[SalesAmount]) > @p1
```

Calculated measure HAVING:

```sql
HAVING
  ROUND(SUM(f.[Profit]) / NULLIF(SUM(f.[SalesAmount]), 0), 4) >= @p1
```

## 8H: ORDER BY Rules

```csharp
OrderBy = context.Sort.Select(sort => BuildOrderItem(sort, context, aliases)).ToList()
```

Metric sort:

```csharp
Expression = sort.Alias
IsAlias = true
```

Selected dimension sort:

```csharp
Expression = sort.Alias
IsAlias = true
```

Unselected dimension sort:

```csharp
Expression = ApplyAliases(sort.PhysicalExpression, aliases)
IsAlias = false
```

Examples:

```sql
ORDER BY
  [Total Sales Amount] DESC
```

```sql
ORDER BY
  d.[CalendarYear] ASC
```

---

# 🧾 PHASE 9: SQL COMPILER STRING RENDERING

**File**: `ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs`

## 9A: SELECT + AS Aliases

```csharp
var select = string.Join(",\n  ",
    plan.Select.Select(x => $"{x.Expression} AS [{x.Alias}]"));
```

Every select item is rendered as:

```sql
{Expression} AS [{Alias}]
```

Examples:

```sql
c.[CustomerName] AS [Customer Name]
SUM(f.[SalesAmount]) AS [Total Sales Amount]
```

## 9B: FROM and JOIN

```csharp
FROM {plan.TableExpressions[plan.BaseTableId]} {baseAlias}
{joins}
```

Join rendering:

```csharp
$"{j.JoinType} JOIN {plan.TableExpressions[j.TableId]} {j.Alias} ON {j.Condition}"
```

Example:

```sql
FROM [dbo].[FactSales] f
INNER JOIN [dbo].[DimCustomer] c ON f.[CustomerKey] = c.[CustomerKey]
```

## 9C: WHERE

```csharp
var where = plan.Where.Count > 0
    ? "WHERE\n  " + string.Join("\n  AND ", plan.Where.Select(CompileFilter))
    : "";
```

All dimension filters are joined with `AND`.

## 9D: GROUP BY

```csharp
var groupBy = plan.GroupBy.Count > 0
    ? "GROUP BY\n  " + string.Join(",\n  ", plan.GroupBy)
    : "";
```

## 9E: HAVING

```csharp
var having = plan.Having.Count > 0
    ? "HAVING\n  " + string.Join("\n  AND ", plan.Having.Select(CompileFilter))
    : "";
```

All metric filters are joined with `AND`.

## 9F: ORDER BY

```csharp
var orderBy = plan.OrderBy.Count > 0
    ? "ORDER BY\n  " + string.Join(",\n  ",
        plan.OrderBy.Select(x => $"{CompileOrderExpression(x)} {x.Direction}"))
    : plan.Offset > 0
        ? "ORDER BY (SELECT NULL)"
        : "";
```

Alias order expression:

```csharp
item.IsAlias ? $"[{item.Expression}]" : item.Expression
```

Examples:

```sql
ORDER BY
  [Total Sales Amount] DESC
```

or:

```sql
ORDER BY
  d.[CalendarYear] ASC
```

If `offset > 0` and no explicit sort exists, SQL Server requires an order by, so compiler emits:

```sql
ORDER BY (SELECT NULL)
```

## 9G: TOP vs OFFSET/FETCH Limit

```csharp
var usesTopForLimit = plan.Limit > 0 && plan.Offset == 0 && plan.OrderBy.Count == 0;
var top = usesTopForLimit ? $" TOP ({plan.Limit})" : "";
```

If no offset and no order:

```sql
SELECT TOP (100)
```

Otherwise:

```sql
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY
```

---

# 🔐 PHASE 10: FILTER OPERATOR SQL + PARAMETERS

**File**: `ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs`

Filters are parameterized:

```csharp
string AddParameter(object? value)
{
    var name = $"p{nextParameterIndex++}";
    parameters[name] = value;
    return $"@{name}";
}
```

## Operator Rendering

| Operator | SQL Output |
|---|---|
| `=` | `{expr} = @p0` |
| `!=` | `{expr} <> @p0` |
| `>` | `{expr} > @p0` |
| `<` | `{expr} < @p0` |
| `>=` | `{expr} >= @p0` |
| `<=` | `{expr} <= @p0` |
| `CONTAINS` | `{expr} LIKE '%' + @p0 + '%'` |
| `IN` | `{expr} IN (@p0, @p1, ...)` |
| `BETWEEN` | `{expr} BETWEEN @p0 AND @p1` |

## Example WHERE Parameters

Input filter:

```json
{ "field": "dimdate.calendaryear", "operator": ">=", "value": 2023 }
```

SQL:

```sql
WHERE
  d.[CalendarYear] >= @p0
```

Parameters:

```json
{ "p0": 2023 }
```

## Example HAVING Parameters

Input filter:

```json
{ "field": "metric.sum_factsales_salesamount", "operator": ">", "value": 10000 }
```

SQL:

```sql
HAVING
  SUM(f.[SalesAmount]) > @p1
```

Parameters:

```json
{ "p1": 10000 }
```

---

# 🧪 FULL EXAMPLE A: DIMENSION + MEASURE + WHERE + HAVING + SORT

## Input VisualQueryRequest

```json
{
  "connectionId": "conn_002",
  "datasetId": "dataset_sales_001",
  "reportId": "rpt_001",
  "visualType": "table",
  "rows": [
    "dimcustomer.customername",
    "dimdate.calendaryear"
  ],
  "columns": [],
  "values": [
    "metric.sum_factsales_salesamount"
  ],
  "filters": [
    { "field": "dimdate.calendaryear", "operator": ">=", "value": 2023 },
    { "field": "metric.sum_factsales_salesamount", "operator": ">", "value": 10000 }
  ],
  "sort": [
    { "field": "metric.sum_factsales_salesamount", "direction": "DESC" }
  ],
  "limit": 100,
  "offset": 0
}
```

## Binding Output

```text
Rows:
  dimcustomer.customername → SemanticField(DimCustomer.CustomerName)
  dimdate.calendaryear → SemanticField(DimDate.CalendarYear)

Values:
  metric.sum_factsales_salesamount → SemanticMetric Formula SUM([factsales.salesamount])

Filters:
  dimdate.calendaryear → TargetType dimension → WHERE
  metric.sum_factsales_salesamount → TargetType metric → HAVING

Sort:
  metric.sum_factsales_salesamount → metric alias sort
```

## Logical Plan Shape

```text
BaseTableId:
  FactSales

Select:
  c.[CustomerName] AS [Customer Name]
  d.[CalendarYear] AS [Calendar Year]
  SUM(f.[SalesAmount]) AS [Total Sales Amount]

Joins:
  FactSales.CustomerKey → DimCustomer.CustomerKey
  FactSales.OrderDateKey → DimDate.DateKey

Where:
  d.[CalendarYear] >= @p0

GroupBy:
  c.[CustomerName]
  d.[CalendarYear]

Having:
  SUM(f.[SalesAmount]) > @p1

OrderBy:
  [Total Sales Amount] DESC
```

## Final SQL Shape

```sql
SELECT
  c.[CustomerName] AS [Customer Name],
  d.[CalendarYear] AS [Calendar Year],
  SUM(f.[SalesAmount]) AS [Total Sales Amount]
FROM [dbo].[FactSales] f
INNER JOIN [dbo].[DimCustomer] c ON f.[CustomerKey] = c.[CustomerKey]
INNER JOIN [dbo].[DimDate] d ON f.[OrderDateKey] = d.[DateKey]
WHERE
  d.[CalendarYear] >= @p0
GROUP BY
  c.[CustomerName],
  d.[CalendarYear]
HAVING
  SUM(f.[SalesAmount]) > @p1
ORDER BY
  [Total Sales Amount] DESC
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY;
```

## Parameters

```json
{
  "p0": 2023,
  "p1": 10000
}
```

---

# 🧪 FULL EXAMPLE B: CALCULATED COLUMN + CALCULATED MEASURE FILTER

## Semantic Objects

Calculated column:

```text
fieldId: factsales.gross_margin_bucket
expression: IF([factsales.salesamount] - [factsales.totalproductcost] > 0, 'Profit', 'Loss')
```

Calculated measure:

```text
metricId: metric.gross_margin_pct
formula: ROUND(([metric.sum_factsales_salesamount] - [metric.sum_factsales_totalproductcost]) / [metric.sum_factsales_salesamount], 4)
```

## Input Request

```json
{
  "rows": ["factsales.gross_margin_bucket"],
  "values": ["metric.gross_margin_pct"],
  "filters": [
    { "field": "factsales.gross_margin_bucket", "operator": "=", "value": "Profit" },
    { "field": "metric.gross_margin_pct", "operator": ">=", "value": 0.3 }
  ],
  "sort": [
    { "field": "factsales.gross_margin_bucket", "direction": "ASC" },
    { "field": "metric.gross_margin_pct", "direction": "DESC" }
  ],
  "limit": 50,
  "offset": 0
}
```

## Clause Placement

| Item | SQL Clause | Why |
|---|---|---|
| calculated column in rows | `SELECT`, `GROUP BY` | row-level grouping field |
| calculated measure in values | `SELECT` | aggregate metric expression |
| calculated column filter | `WHERE` | row-level field filter |
| calculated measure filter | `HAVING` | aggregate metric filter |
| selected calculated column sort | `ORDER BY [Gross Margin Bucket]` | selected dimension can sort by alias |
| metric sort | `ORDER BY [Gross Margin %]` | metric sorts by alias |

## Final SQL Shape

```sql
SELECT
  (CASE WHEN (f.[SalesAmount] - f.[TotalProductCost]) > 0 THEN 'Profit' ELSE 'Loss' END) AS [Gross Margin Bucket],
  ROUND(
    ((SUM(f.[SalesAmount]) - SUM(f.[TotalProductCost])) / NULLIF(SUM(f.[SalesAmount]), 0)),
    4
  ) AS [Gross Margin %]
FROM [dbo].[FactSales] f
WHERE
  (CASE WHEN (f.[SalesAmount] - f.[TotalProductCost]) > 0 THEN 'Profit' ELSE 'Loss' END) = @p0
GROUP BY
  (CASE WHEN (f.[SalesAmount] - f.[TotalProductCost]) > 0 THEN 'Profit' ELSE 'Loss' END)
HAVING
  ROUND(((SUM(f.[SalesAmount]) - SUM(f.[TotalProductCost])) / NULLIF(SUM(f.[SalesAmount]), 0)), 4) >= @p1
ORDER BY
  [Gross Margin Bucket] ASC,
  [Gross Margin %] DESC
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY;
```

Parameters:

```json
{
  "p0": "Profit",
  "p1": 0.3
}
```

---

# 🧪 FULL EXAMPLE C: MEASURE ONLY

## Input

```json
{
  "rows": [],
  "values": ["metric.sum_factsales_salesamount"],
  "filters": [
    { "field": "metric.sum_factsales_salesamount", "operator": ">", "value": 100000 }
  ],
  "sort": [],
  "limit": 100,
  "offset": 0
}
```

## GROUP BY Rule

Measures exist, but no dimensions exist, so no `GROUP BY` is generated.

## SQL Shape

```sql
SELECT TOP (100)
  SUM(f.[SalesAmount]) AS [Total Sales Amount]
FROM [dbo].[FactSales] f
HAVING
  SUM(f.[SalesAmount]) > @p0;
```

Because no explicit order and offset is zero, compiler uses `TOP (100)`.

---

# 📋 FILE MAPPING TABLE

| Phase | File | Class / Function | Input | Processing | Output |
|---|---|---|---|---|---|
| Frontend request | `build-visual-query-request.ts` | `buildVisualQueryRequest()` | selected fields, filters, sorts, metadata | Splits rows/values, normalizes filters/sort/limit | `VisualQueryRequest` |
| Runtime metric ID | `build-visual-query-request.ts` | `buildRuntimeMetricId()` | field + aggregation | Builds `metric.{agg}_{table}_{column}` | metric ID string |
| API entry | `QueryController.cs` | `Execute()` | request body | Delegates to query service | response |
| Pipeline | `ReportQueryService.cs` | `ExecuteAsync()` | request + model | binder → context → measures → joins → plan → SQL | comprehensive result |
| Semantic binding | `SemanticModelBinder.cs` | `Bind()` | request + semantic model | Resolve fields, metrics, filters, sort | `BoundSemanticQuery` |
| Runtime metric backend | `RuntimeAggregateMetricFactory.cs` | `TryCreate()` | metric ID + model | Creates temporary metric from field aggregation | `SemanticMetric` |
| WHERE/HAVING split | `EvaluationContextBuilder.cs` | `Build()` | bound filters | TargetType dimension → where, metric → having | `EvaluationContext` |
| Measure SQL | `MeasureExpansionEngine.cs` | `Expand()` | selected metrics | Compile formulas to aggregate SQL | `ExpandedMeasure[]` |
| Relationship joins | `RelationshipTraversalEngine.cs` | `Build()` | tables required by query | Finds relationship paths | `JoinPlan` |
| Logical plan | `LogicalPlanBuilder.cs` | `Build()` | eval + measures + joins | SELECT, JOIN, WHERE, GROUP BY, HAVING, ORDER BY | `LogicalQueryPlan` |
| Final SQL | `SqlCompiler.cs` | `Compile()` | logical plan | Render SQL clauses and parameters | `SqlCompilationResult` |
| SQL execution | `SqlServerQueryExecutor.cs` | `ExecuteAsync()` | connection ID + SQL + params | Dapper query execution | `QueryResult` |

---

# 🎯 KEY DECISION POINTS

| Decision | Where | Rule | Result |
|---|---|---|---|
| Field vs metric filter | `SemanticModelBinder.ResolveFilters()` | field id found → dimension; metric id found → metric | dimension = `WHERE`, metric = `HAVING` |
| Metric filter must be selected | `LogicalPlanBuilder.BuildMetricFilter()` | metric filter ID must exist in selected measure map | otherwise validation exception |
| Calculated column expression | `LogicalPlanBuilder` / `SemanticExpressionCompiler` | `field.IsDerived && field.Expression != null` | expression used in `SELECT`, `WHERE`, `GROUP BY`, sort if unselected |
| Calculated measure expression | `MeasureExpansionEngine` | metric formula compiled | aggregate SQL in `SELECT` and `HAVING` |
| GROUP BY generation | `LogicalPlanBuilder` | measures exist and dimensions exist | group by every dimension expression |
| SELECT alias | `SqlCompiler` | every `SelectItem` renders `Expression AS [Alias]` | user-friendly output column names |
| Sort by selected dimension | `LogicalPlanBuilder.BuildOrderItem()` | sort field is selected row | `ORDER BY [Alias]` |
| Sort by metric | `LogicalPlanBuilder.BuildOrderItem()` | target type metric | `ORDER BY [Metric Alias]` |
| Sort by unselected field | `LogicalPlanBuilder.BuildOrderItem()` | dimension sort not in rows | `ORDER BY physical expression` |
| TOP vs OFFSET | `SqlCompiler` | limit > 0, offset 0, no order | `SELECT TOP (limit)` |
| Pagination | `SqlCompiler` | limit > 0 and order or offset | `OFFSET ... FETCH NEXT ...` |
| Offset without sort | `SqlCompiler` | offset > 0 and no order | `ORDER BY (SELECT NULL)` |
| Filter parameters | `SqlCompiler.CompileFilter()` | every value becomes `@pN` | parameterized SQL |

---

# ⚠️ IMPORTANT NOTES / CURRENT LIMITATIONS

1. **Metric filters become HAVING only if the metric is selected in `values[]`**. Filtering on an unselected metric throws a validation exception.
2. **Dimension filters always become WHERE**, including calculated-column filters.
3. **Calculated column expressions are repeated** in `SELECT`, `WHERE`, and `GROUP BY`; SQL aliases are not reused in `WHERE`/`GROUP BY`.
4. **Metric expressions are repeated** in `SELECT` and `HAVING`; SQL aliases are used only for `ORDER BY` when sorting selected metrics.
5. **GROUP BY is generated only when there is at least one measure and at least one row field**.
6. **Dimension-only queries have no GROUP BY** and return raw selected row expressions with limit/paging.
7. **Measure-only queries have no GROUP BY** and return one aggregate row, subject to HAVING if metric filters exist.
8. **All WHERE/HAVING filter values are parameterized** as `@p0`, `@p1`, etc.
9. **`CONTAINS` is string-only** and compiles to `LIKE '%' + @pN + '%'`.
10. **`IN` creates one SQL parameter per list item**.
11. **Runtime metrics are not saved in metadata**; backend creates them on demand from the runtime metric ID.
12. **Alias collisions are handled at table alias level**, but select aliases come from display names and should be kept unique by semantic design.
