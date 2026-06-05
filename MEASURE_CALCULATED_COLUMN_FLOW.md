# 📍 COMPLETE FLOW TRACE: Measure + Calculated Column Workflow

## 🎬 END-TO-END JOURNEY: Builder Dialog → Semantic Model → Query SQL → Export Artifact

```text
Frontend (Next.js)
    ↓
User opens Schema Panel → New Measure / New Calculated Column
    ↓
UI builds a semantic expression string
    ↓
POST /api/datasets/{datasetId}/expressions/validate
    ↓
ExpressionsController.Validate()
    ↓
ExpressionValidationService.Validate()
    ↓
SemanticExpressionValidationService.Validate()
    ├─ Tokenize expression
    ├─ Parse tokens into AST
    ├─ Bind [field] and [metric.*] references to semantic metadata
    ├─ Detect row scope vs aggregate scope
    ├─ Validate aggregate rules
    ├─ Detect circular dependencies
    ├─ Infer return data type
    └─ Compile SQL preview
    ↓
Frontend receives validation result
    ↓
POST /api/datasets/{datasetId}/calculated-objects
    ↓
CalculatedObjectsController.Create()
    ↓
SemanticModelMutationService.CreateCalculatedObjectAsync()
    ├─ calculated_column → append SemanticField with IsDerived = true
    └─ calculated_measure → append SemanticMetric with AggregationBehavior = calculated
    ↓
Semantic model is saved in dataset registry/store
    ↓
Frontend refreshes dataset metadata
    ↓
User drags calculated column / measure into report workspace
    ↓
buildVisualQueryRequest()
    ├─ calculated column id goes to rows[]
    └─ calculated measure id goes to values[]
    ↓
POST /api/query/execute
    ↓
ReportQueryService.ExecuteAsync()
    ├─ Bind request fields/metrics
    ├─ Compile calculated column expressions into SELECT/GROUP BY/WHERE SQL
    ├─ Compile calculated measure formulas into aggregate SELECT/HAVING SQL
    ├─ Build joins
    ├─ Compile final SQL
    ├─ Execute SQL Server query
    └─ Build execution artifact
    ↓
Query result + artifact key returned to frontend
    ↓
Optional export uses saved artifact for CSV/Telerik rendering
```

---

# 🔍 PHASE-BY-PHASE BREAKDOWN

## 🎯 PHASE 1: FRONTEND ENTRY - USER OPENS CALCULATED BUILDER

**File**: `data-report-builder/components/report-builder/schema-panel.tsx`

The schema panel owns the buttons/modals for calculated content:

```tsx
<Button onClick={() => setDerivedModalOpen(true)}>
  New Calculated Column
</Button>

<DerivedFieldExpressionBuilder
  open={derivedModalOpen}
  onOpenChange={setDerivedModalOpen}
  onSave={onAddCalculatedField}
  existingMeasures={measures}
  existingDerivedFields={existingDerivedForBuilder}
  metadata={semanticMetadata}
/>
```

**Input**:

- Current semantic `metadata`.
- Existing frontend `calculatedFields`.
- Callback `onAddCalculatedField` from `app/page.tsx`.

**Important note**:

The UI text says **New Calculated Column**, but the expression builder can also create a **Calculated Measure** when the expression contains aggregate functions or metric references.

**Output**:

- Opens `DerivedFieldExpressionBuilder`.
- User builds or types an expression.

---

## 🧩 PHASE 2: FRONTEND EXPRESSION BUILDER - TOKEN PALETTE AND INPUTS

**File**: `data-report-builder/components/report-builder/derived-field-builder.tsx`

### 2A: Token Types

```tsx
export type ExpressionTokenType =
  | 'field'
  | 'metric'
  | 'operator'
  | 'number'
  | 'string'
  | 'paren'
  | 'function'
  | 'column'
  | 'measure'
  | 'derived'
  | 'constant'
```

The builder can compose:

| Token | Example | Meaning |
|---|---|---|
| Field | `[dbo.sales.amount]` | Row-level column from semantic metadata |
| Metric | `[metric.gross_margin]` | Aggregate-level measure |
| Operator | `+`, `-`, `*`, `/`, `>`, `<`, `AND`, `OR` | Math/comparison/logical operation |
| Number | `100`, `0.15` | Numeric literal |
| String | `'Retail'` | String literal |
| Parentheses | `(`, `)` | Expression grouping |
| Function | `SUM(`, `ROUND(`, `IF(` | Scalar or aggregate function |

### 2B: Available Field List

The field palette uses:

```ts
const availableFields = useMemo(
  () => getFieldsForDerivedExpression(metadata),
  [metadata]
)
```

**File**: `data-report-builder/lib/metadata-selectors.ts`

```ts
export function getFieldsForDerivedExpression(metadata) {
  return getAvailableFields(metadata).filter(field => field.role !== 'metric')
}
```

**Input**:

- `DatasetMetadataResponse` from backend.

**Processing**:

- Flatten metadata tables into fields.
- Remove hidden fields.
- Remove fields whose role is `metric`.

**Output**:

- Palette-ready fields grouped by `tableId`.

### 2C: Existing Measures Palette

The measure tab includes backend metrics from `metadata.metrics`, plus existing local measure objects:

```tsx
(metadata?.metrics ?? [])
  .filter((metric) => !metric.isHidden)
  .map((metric) => ({
    id: metric.metricId,
    token: `[${metric.metricId}]`,
    detail: `${metric.formula} • Base: ${metric.baseTableId}`,
  }))
```

**Important expression convention**:

- Backend metric IDs are normally shaped like `metric.some_name`.
- The serialized expression token becomes `[metric.some_name]`.
- The backend tokenizer sees references beginning with `metric.` as `MetricReference` tokens.

---

## 🧵 PHASE 3: FRONTEND SERIALIZATION - TOKENS → EXPRESSION STRING

**File**: `data-report-builder/components/report-builder/derived-field-builder.tsx`

The builder converts visual tokens into an expression string:

```tsx
const serializeExpressionToken = (t: ExpressionToken) => {
  switch (t.kind) {
    case 'field': return `[${t.fieldId}]`
    case 'metric': return `[${t.metricId}]`
    case 'operator': return t.operator ?? String(t.value ?? '')
    case 'number': return `${t.value}`
    case 'string': return `'${String(t.value ?? '').replaceAll("'", "''")}'`
    case 'paren': return String(t.value ?? '')
    case 'function': return t.name
  }
}
```

Then:

```tsx
const expressionString =
  mode === 'formula'
    ? formulaText
    : tokens.map(serializeExpressionToken).filter(Boolean).join(' ')
```

### Example 1: Calculated Column

```text
[FactSales.SalesAmount] - [FactSales.TotalProductCost]
```

This is **row-level** because it has no aggregate function and no metric reference.

### Example 2: Calculated Measure

```text
ROUND(([metric.sales_amount] - [metric.total_cost]) / [metric.sales_amount], 4)
```

This is **aggregate-level** because it references metrics.

### Example 3: Calculated Measure with Aggregation

```text
SUM([FactSales.SalesAmount]) / NULLIF(COUNT([FactSales.SalesKey]), 0)
```

This is **aggregate-level** because it calls aggregate functions.

---

## 🧠 PHASE 4: FRONTEND DETECTION - COLUMN VS MEASURE HINT

**File**: `data-report-builder/components/report-builder/derived-field-builder.tsx`

The frontend does a fast heuristic detection:

```tsx
const hasAggregateExpression =
  AGGREGATE_PATTERN.test(expressionString) ||
  METRIC_TOKEN_PATTERN.test(expressionString)

const detectedKind = hasAggregateExpression
  ? 'calculated_measure'
  : 'calculated_column'
```

Where:

```tsx
const AGGREGATE_PATTERN = /\b(SUM|AVG|COUNT|COUNT_DISTINCT|MIN|MAX)\s*\(/i
const METRIC_TOKEN_PATTERN = /\[\s*metric\./i
```

**Input**:

- Serialized expression string.

**Processing**:

- If expression contains `SUM(`, `AVG(`, `COUNT(`, `COUNT_DISTINCT(`, `MIN(`, or `MAX(`, mark as calculated measure.
- If expression contains `[metric.` reference, mark as calculated measure.
- Otherwise mark as calculated column.

**Output**:

- Button label becomes either `Save Calculated Measure` or `Save Calculated Column`.

**Important note**:

This frontend detection is only a UI hint. The backend performs the authoritative detection and validation.

---

## ✅ PHASE 5: FRONTEND VALIDATION REQUEST

**File**: `data-report-builder/components/report-builder/derived-field-builder.tsx`

When saving, the builder validates the expression first:

```tsx
const validation = await validateExpression(metadata.datasetId, {
  expression: serializedExpression,
  targetKind: 'auto',
})

if (!validation.valid) {
  toast.error(formatValidationErrors(validation.errors))
  return
}
```

**File**: `data-report-builder/lib/semantic-management-api.ts`

```ts
export function validateExpression(datasetId, body) {
  return request(`/api/datasets/${datasetId}/expressions/validate`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}
```

**HTTP Request**:

```http
POST /api/datasets/{datasetId}/expressions/validate
Content-Type: application/json

{
  "expression": "SUM([FactSales.SalesAmount])",
  "targetKind": "auto"
}
```

**Input**:

- `datasetId`
- `expression`
- `targetKind`: `auto`, `calculated_column`, or `calculated_measure`

**Output**:

```ts
{
  valid: boolean,
  detectedKind: 'calculated_column' | 'calculated_measure',
  detectedScope: 'Row' | 'Aggregate',
  dataType: string,
  returnType: string,
  dependencies: string[],
  sqlPreview?: string,
  compiledSqlPreview: string,
  errors: { code: string; message: string }[],
  warnings?: { code: string; message: string }[]
}
```

---

# 🏗️ PHASE 6: BACKEND VALIDATION ENTRY POINT

## 6A: Controller

**File**: `ReportPlatform/Report.Api/Controllers/ExpressionsController.cs`

```csharp
[HttpPost("validate")]
public async Task<IActionResult> Validate(
    string datasetId,
    [FromBody] ExpressionValidationRequest request,
    CancellationToken ct)
{
    var model = await _mutationService.LoadAsync(datasetId, ct);
    return Ok(_validator.Validate(model, request));
}
```

**Input**:

- Route `datasetId`.
- Body `ExpressionValidationRequest`.

**Processing**:

1. Load semantic model for dataset.
2. Pass model + expression request to validation service.

**Output**:

- `ExpressionValidationResponse`.

## 6B: API Response Mapper

**File**: `ReportPlatform/Report.Api/Services/ExpressionValidationService.cs`

```csharp
var result = validator.Validate(model, request.Expression, request.TargetKind);
return new ExpressionValidationResponse
{
    Valid = result.Valid,
    DetectedKind = result.DetectedKind,
    DetectedScope = result.DetectedScope.ToString(),
    DataType = result.DataType,
    ReturnType = result.DataType,
    Dependencies = result.Dependencies,
    SqlPreview = result.SqlPreview,
    CompiledSqlPreview = result.SqlPreview ?? "",
    Errors = result.Errors.Select(...).ToList(),
    Warnings = result.Warnings.Select(...).ToList()
};
```

**Output shape**:

The backend returns both semantic information and SQL preview information so the frontend can display validation status.

---

# ⚙️ PHASE 7: BACKEND SEMANTIC EXPRESSION VALIDATION ENGINE

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Validation/SemanticExpressionValidationService.cs`

```csharp
public SemanticExpressionValidationResult Validate(
    SemanticModel model,
    string expression,
    string targetKind = "auto",
    string? candidateId = null)
{
    var ast = parser.Parse(tokenizer.Tokenize(expression));
    var bound = binder.Bind(ast, model);
    var scope = scopeResolver.Resolve(ast);
    var detectedKind = scope == ExpressionScope.Aggregate
        ? "calculated_measure"
        : "calculated_column";

    aggregationValidator.Validate(ast, scope, targetKind);
    dependencyGraph.ValidateNoCycles(model, candidateId, bound.Dependencies);
    var dataType = typeInference.Infer(ast, model);
    var sql = sqlCompiler.Compile(bound, model);

    return new SemanticExpressionValidationResult { ... };
}
```

This is the most important backend function for calculated columns/measures.

## 7A: Tokenization

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Tokenization/ExpressionToken.cs`

The tokenizer recognizes:

| Token Type | Example |
|---|---|
| `FieldReference` | `[FactSales.SalesAmount]` |
| `MetricReference` | `[metric.gross_margin]` |
| `FunctionName` | `SUM`, `ROUND`, `IF` |
| `NumberLiteral` | `100`, `4`, `0.25` |
| `StringLiteral` | `'Retail'` |
| `Operator` | `+`, `-`, `/`, `AND`, `OR`, `>=` |
| `OpenParen` / `CloseParen` | `(`, `)` |
| `Comma` | `,` |

Important behavior:

```csharp
reference.StartsWith("metric.")
    ? ExpressionTokenType.MetricReference
    : ExpressionTokenType.FieldReference
```

So:

```text
[metric.margin] → MetricReference
[FactSales.Amount] → FieldReference
```

## 7B: Parsing

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Parsing/ExpressionParser.cs`

The parser builds an AST with operator precedence:

```text
OR
  ↓
AND
  ↓
comparison (=, !=, <, >, <=, >=)
  ↓
additive (+, -)
  ↓
multiplicative (*, /)
  ↓
unary (-, NOT)
  ↓
primary: field, metric, number, string, boolean, null, function, parenthesized expression
```

Example:

```text
SUM([SalesAmount]) / NULLIF(COUNT([SalesKey]), 0)
```

becomes an AST roughly like:

```text
BinaryExpressionNode /
├─ FunctionCallNode SUM
│  └─ FieldReferenceNode SalesAmount
└─ FunctionCallNode NULLIF
   ├─ FunctionCallNode COUNT
   │  └─ FieldReferenceNode SalesKey
   └─ NumberLiteralNode 0
```

## 7C: Binding References

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Binding/ExpressionSemanticBinder.cs`

Binding validates that referenced fields/metrics exist and are visible:

```csharp
case FieldReferenceNode fieldRef:
    var field = model.Fields.FirstOrDefault(...);
    if (field is null) throw UNKNOWN_FIELD_REFERENCE;
    if (field.IsHidden) throw UNKNOWN_FIELD_REFERENCE;
    dependencies.Add(field.FieldId);
    fields.Add(field);
    baseTables.Add(field.TableId);
    break;

case MetricReferenceNode metricRef:
    var metric = model.Metrics.FirstOrDefault(...);
    if (metric is null) throw UNKNOWN_METRIC_REFERENCE;
    if (metric.IsHidden) throw UNKNOWN_METRIC_REFERENCE;
    dependencies.Add(metric.MetricId);
    metrics.Add(metric);
    baseTables.Add(metric.BaseTableId);
    break;
```

**Input**:

- AST.
- Semantic model fields and metrics.

**Processing**:

- Resolve field references to `SemanticField` objects.
- Resolve metric references to `SemanticMetric` objects.
- Collect dependencies.
- Collect referenced base tables.

**Output**:

```csharp
BoundExpression
{
    Ast,
    Dependencies,
    ReferencedFields,
    ReferencedMetrics,
    BaseTableIds
}
```

## 7D: Scope Detection

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Validation/ExpressionScopeResolver.cs`

```csharp
public ExpressionScope Resolve(ExpressionNode ast) =>
    ContainsAggregateShape(ast) ? ExpressionScope.Aggregate : ExpressionScope.Row;
```

An expression is **Aggregate** if it contains:

- A metric reference, e.g. `[metric.sales_amount]`.
- An aggregate function, e.g. `SUM`, `AVG`, `COUNT`, `COUNT_DISTINCT`, `MIN`, `MAX`.

Otherwise it is **Row**.

| Scope | Backend Detected Kind | Stored As |
|---|---|---|
| `Row` | `calculated_column` | `SemanticField` with `IsDerived = true` |
| `Aggregate` | `calculated_measure` | `SemanticMetric` with `AggregationBehavior = calculated` |

## 7E: Aggregate Rule Validation

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Validation/AggregationValidationService.cs`

Rules:

1. A forced `calculated_column` cannot contain aggregate scope.
2. In aggregate scope, raw row-level fields must be inside an aggregate function.
3. Nested aggregate functions are not allowed.
4. Aggregate functions cannot aggregate metric references.

Examples:

| Expression | Result | Why |
|---|---|---|
| `[SalesAmount] - [Cost]` | ✅ calculated column | Row-level only |
| `SUM([SalesAmount])` | ✅ calculated measure | Aggregate function |
| `[metric.sales] / [metric.cost]` | ✅ calculated measure | Metric refs are aggregate-level |
| `SUM([metric.sales])` | ❌ invalid | Cannot aggregate metric reference |
| `SUM(AVG([SalesAmount]))` | ❌ invalid | Nested aggregate |
| `SUM([SalesAmount]) + [Cost]` | ❌ invalid | Row field outside aggregate in aggregate expression |

## 7F: Circular Dependency Check

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Dependencies/ExpressionDependencyGraphService.cs`

The validation engine calls:

```csharp
dependencyGraph.ValidateNoCycles(model, candidateId, bound.Dependencies);
```

Purpose:

- Prevent calculated object A depending on calculated object B when B already depends on A.
- Prevent circular calculated measure/column dependencies.

Example invalid cycle:

```text
metric.a = [metric.b] + 1
metric.b = [metric.a] + 1
```

## 7G: Type Inference

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Validation/ExpressionTypeInferenceService.cs`

The type inference engine returns normalized types:

| Source Type | Normalized Type |
|---|---|
| `tinyint`, `smallint`, `int`, `bigint`, `integer` | `integer` |
| `decimal`, `numeric`, `float`, `real`, `money` | `decimal` |
| date/time types | `datetime` |
| `bit`, `bool`, `boolean` | `boolean` |
| other | `string` |

Important behavior:

- Division returns `decimal`.
- Numeric math requires numeric operands.
- String `+` requires both sides to be strings.
- Comparisons return `boolean`.
- `SUM` and `AVG` return `decimal`.
- `COUNT` and `COUNT_DISTINCT` return `integer`.
- `IF` returns common type of true/false branches.

## 7H: SQL Preview Compilation

**File**: `ReportPlatform/Report.QueryEngine/Expressions/Compilation/SemanticExpressionSqlCompiler.cs`

The SQL compiler converts AST into SQL fragments:

```csharp
FieldReferenceNode → tableAliasOrTableId.[PhysicalColumn]
MetricReferenceNode → recursively compile metric.Formula
StringLiteralNode → quoted SQL string
BooleanLiteralNode → 1 or 0
IF(...) → CASE WHEN ... THEN ... ELSE ... END
COUNT_DISTINCT(x) → COUNT(DISTINCT x)
/ operator → wraps denominator with NULLIF(..., 0)
```

Example:

```text
[FactSales.SalesAmount] - [FactSales.TotalProductCost]
```

SQL preview:

```sql
FactSales.[SalesAmount] - FactSales.[TotalProductCost]
```

Example:

```text
ROUND([metric.gross_margin] / [metric.sales_amount], 4)
```

If `metric.gross_margin` and `metric.sales_amount` are known metrics, the compiler recursively expands their formulas.

---

# 💾 PHASE 8: FRONTEND SAVE REQUEST

After validation succeeds, `DerivedFieldExpressionBuilder` calls `onSave()` with a local `CalculatedField` object:

```tsx
await onSave({
  id: `calculated-${Date.now()}`,
  name: name.trim(),
  type: 'derived',
  expression: serializedExpression,
})
```

**File**: `data-report-builder/app/page.tsx`

The main page receives this object in `addCalculatedField`.

For `field.type === 'derived'`, it validates again and then calls `createCalculatedObject`:

```tsx
const created = await createCalculatedObject(datasetId, {
  displayName: field.name,
  expression: formula,
  format: 'general',
  isHidden: false,
  isDraggable: true,
  targetKind: 'auto',
})

await refreshMetadata()
```

**HTTP Request**:

```http
POST /api/datasets/{datasetId}/calculated-objects
Content-Type: application/json

{
  "displayName": "Gross Margin",
  "expression": "[FactSales.SalesAmount] - [FactSales.TotalProductCost]",
  "targetKind": "auto",
  "format": "general",
  "isHidden": false,
  "isDraggable": true
}
```

**Output**:

```json
{
  "id": "factsales.gross_margin",
  "detectedKind": "calculated_column",
  "scope": "Row",
  "dataType": "decimal",
  "dependencies": [
    "FactSales.SalesAmount",
    "FactSales.TotalProductCost"
  ]
}
```

or, for a calculated measure:

```json
{
  "id": "metric.gross_margin_pct",
  "detectedKind": "calculated_measure",
  "scope": "Aggregate",
  "dataType": "decimal",
  "dependencies": [
    "metric.gross_margin",
    "metric.sales_amount"
  ]
}
```

---

# 🏗️ PHASE 9: BACKEND SAVE ENTRY POINT

## 9A: Controller

**File**: `ReportPlatform/Report.Api/Controllers/CalculatedObjectsController.cs`

```csharp
[HttpPost]
public async Task<IActionResult> Create(
    string datasetId,
    [FromBody] CreateCalculatedObjectRequest request,
    CancellationToken ct) =>
    Ok(await service.CreateCalculatedObjectAsync(datasetId, request, ct));
```

**Input**:

- Route `datasetId`.
- Body `CreateCalculatedObjectRequest`.

**Output**:

- `CreateCalculatedObjectResponse`.

## 9B: Request Contract

**File**: `ReportPlatform/Report.Contracts/Semantic/ExpressionValidationContracts.cs`

```csharp
public sealed class CreateCalculatedObjectRequest
{
    public string DisplayName { get; init; } = "";
    public string Expression { get; init; } = "";
    public string TargetKind { get; init; } = "auto";
    public string? Format { get; init; }
    public bool IsHidden { get; init; }
    public bool IsDraggable { get; init; } = true;
}
```

## 9C: Response Contract

```csharp
public sealed class CreateCalculatedObjectResponse
{
    public string Id { get; init; } = "";
    public string DetectedKind { get; init; } = "";
    public string Scope { get; init; } = "";
    public string DataType { get; init; } = "";
    public List<string> Dependencies { get; init; } = [];
}
```

---

# 🧬 PHASE 10: BACKEND MODEL MUTATION - SAVE COLUMN OR MEASURE

**File**: `ReportPlatform/Report.Api/Services/SemanticModelMutationService.cs`

The save function is:

```csharp
CreateCalculatedObjectAsync(datasetId, request, ct)
```

### 10A: Validate Name

```csharp
if (string.IsNullOrWhiteSpace(request.DisplayName))
{
    throw new SemanticQueryValidationException(...);
}
```

### 10B: Load Semantic Model

```csharp
var model = await LoadAsync(datasetId, ct);
```

`LoadAsync` loads the semantic model and makes sure the dataset registry knows about it.

### 10C: Backend Authoritative Validation

```csharp
var candidateId = BuildCalculatedObjectId(request.DisplayName, request.TargetKind);
var validation = _expressionValidator.Validate(
    model,
    request.Expression,
    request.TargetKind,
    candidateId);

if (!validation.Valid) throw SemanticQueryValidationException(...);
```

This means save repeats the validation path. Frontend validation is not trusted as final.

---

## 🔀 PHASE 10D: BRANCH 1 - CALCULATED MEASURE

If backend detects:

```text
validation.DetectedKind == "calculated_measure"
```

then it creates a `SemanticMetric`:

```csharp
model.Metrics.Add(new SemanticMetric
{
    MetricId = id,
    DatasetId = datasetId,
    DisplayName = request.DisplayName,
    Formula = request.Expression,
    BaseTableId = validation.BoundExpression?.BaseTableIds.FirstOrDefault() ?? "",
    AggregationBehavior = "calculated",
    DataType = validation.DataType,
    Format = request.Format ?? "general",
    IsHidden = request.IsHidden,
    IsDraggable = request.IsDraggable
});
```

It also adds a semantic object record:

```csharp
model.SemanticObjects.Add(CreateSemanticObject(
    id,
    datasetId,
    null,
    request,
    SemanticObjectType.CalculatedMeasure,
    ExpressionScope.Aggregate,
    validation.DataType,
    validation.Dependencies));
```

### Calculated Measure Storage Shape

```csharp
SemanticMetric
{
    MetricId = "metric.gross_margin_pct",
    DisplayName = "Gross Margin %",
    Formula = "ROUND([metric.gross_margin] / [metric.sales_amount], 4)",
    BaseTableId = "FactSales",
    AggregationBehavior = "calculated",
    DataType = "decimal",
    Format = "general",
    IsHidden = false,
    IsDraggable = true
}
```

### Calculated Measure Output to Metadata

Later, metadata service maps it into:

```ts
MetadataMetric {
  metricId,
  displayName,
  baseTableId,
  formula,
  aggregationBehavior,
  dataType,
  format,
  isHidden,
  isDraggable
}
```

---

## 🔀 PHASE 10E: BRANCH 2 - CALCULATED COLUMN

If backend detects:

```text
validation.DetectedKind == "calculated_column"
```

then it creates a `SemanticField` with `IsDerived = true`:

```csharp
model.Fields.Add(new SemanticField
{
    FieldId = id,
    DatasetId = datasetId,
    TableId = baseTableId,
    PhysicalTable = baseTableId,
    PhysicalColumn = Slug(request.DisplayName),
    DisplayName = request.DisplayName,
    DataType = validation.DataType,
    Role = "calculated_field",
    SemanticType = "calculated",
    Format = request.Format ?? "general",
    IsHidden = request.IsHidden,
    IsDraggable = request.IsDraggable,
    Expression = request.Expression,
    BaseTableId = baseTableId,
    IsDerived = true
});
```

It also adds a semantic object record:

```csharp
model.SemanticObjects.Add(CreateSemanticObject(
    id,
    datasetId,
    baseTableId,
    request,
    SemanticObjectType.CalculatedColumn,
    ExpressionScope.Row,
    validation.DataType,
    validation.Dependencies));
```

### Calculated Column Base Table Rule

For a calculated column, backend requires fields from exactly one table:

```csharp
var baseTableId = validation.BoundExpression?.ReferencedFields
    .Select(f => f.TableId)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .SingleOrDefault();

if (string.IsNullOrWhiteSpace(baseTableId))
{
    throw ... "Calculated column must reference fields from exactly one table."
}
```

### Calculated Column Storage Shape

```csharp
SemanticField
{
    FieldId = "factsales.gross_margin",
    TableId = "FactSales",
    DisplayName = "Gross Margin",
    DataType = "decimal",
    Role = "calculated_field",
    SemanticType = "calculated",
    Expression = "[FactSales.SalesAmount] - [FactSales.TotalProductCost]",
    BaseTableId = "FactSales",
    IsDerived = true,
    IsDraggable = true
}
```

### Calculated Column Output to Metadata

Later, metadata service maps it into:

```ts
MetadataField {
  fieldId,
  displayName,
  tableId,
  physicalColumn,
  dataType,
  role,
  semanticType,
  expression,
  baseTableId,
  isDerived,
  isHidden,
  isDraggable
}
```

---

# 🔄 PHASE 11: FRONTEND REFRESHES METADATA

**File**: `data-report-builder/app/page.tsx`

After saving, the frontend calls:

```tsx
await refreshMetadata()
```

`refreshMetadata()` calls:

```tsx
const response = await getDatasetMetadata(datasetId)
setMetadata(response)
setConnectionId(response.connectionId || connectionId)
```

**File**: `data-report-builder/lib/report-metadata-api.ts`

```ts
export async function getDatasetMetadata(datasetId: string) {
  const res = await fetch(`${API_BASE}/api/datasets/${datasetId}/metadata`)
  return res.json()
}
```

**Backend File**: `ReportPlatform/Report.Api/Services/DatasetMetadataService.cs`

- Calculated columns appear in `tables[].fields[]` because they are stored as `SemanticField`.
- Calculated measures appear in `metrics[]` because they are stored as `SemanticMetric`.

---

# 🧱 PHASE 12: USER DRAGS CALCULATED OBJECT INTO REPORT

**File**: `data-report-builder/lib/build-visual-query-request.ts`

When the user selects fields/metrics for a report, the frontend builds a `VisualQueryRequest`.

### 12A: Lookup Build

```ts
metadata?.tables.forEach((table) => {
  table.fields.forEach((field) => {
    lookup.set(field.fieldId, {
      kind: field.isDerived || field.role === 'derived_field' ? 'derived' : 'field',
      role: field.isDerived ? 'derived_field' : field.role,
      ...
    })
  })
})

metadata?.metrics.forEach((metric) => {
  lookup.set(metric.metricId, {
    kind: 'metric',
    role: 'metric',
    ...
  })
})
```

### 12B: Rows vs Values

```ts
const rows = selected
  .filter((field) =>
    (!field.aggregation || field.placement !== 'values') &&
    (field.kind === 'field' || field.kind === 'derived') &&
    field.role !== 'metric')
  .map((field) => field.id)

const values = selected.flatMap((field) => {
  if (field.kind === 'metric' || field.role === 'metric') return [field.id]
  if (field.placement === 'values' && field.aggregation) {
    return [buildRuntimeMetricId(field, lookup)]
  }
  return []
})
```

| Object | `VisualQueryRequest` Placement |
|---|---|
| Calculated column | `rows[]` |
| Calculated measure | `values[]` |
| Aggregated regular field | runtime metric ID in `values[]` |

### Example Request With Calculated Column

```json
{
  "connectionId": "conn-1",
  "datasetId": "sales-dataset",
  "reportId": "rpt_001",
  "visualType": "table",
  "rows": ["factsales.gross_margin"],
  "columns": [],
  "values": [],
  "filters": [],
  "sort": [],
  "limit": 100,
  "offset": 0
}
```

### Example Request With Calculated Measure

```json
{
  "connectionId": "conn-1",
  "datasetId": "sales-dataset",
  "reportId": "rpt_001",
  "visualType": "table",
  "rows": ["DimDate.CalendarYear"],
  "columns": [],
  "values": ["metric.gross_margin_pct"],
  "filters": [],
  "sort": [],
  "limit": 100,
  "offset": 0
}
```

---

# 🚀 PHASE 13: QUERY EXECUTION - WHERE CALCULATED OBJECTS BECOME SQL

**File**: `ReportPlatform/Report.QueryEngine/Services/ReportQueryService.cs`

`ExecuteAsync()` runs this pipeline:

```text
Load semantic model
Run validation stage 1 and 2
Bind VisualQueryRequest
Build EvaluationContext
Expand measures
Build relationship joins
Build logical plan
Compile SQL
Execute query
Build artifact
Return comprehensive response
```

## 13A: Semantic Binding

**File**: `ReportPlatform/Report.QueryEngine/Binding/SemanticModelBinder.cs`

The binder resolves:

- `rows[]` to semantic fields.
- `values[]` to semantic metrics.
- `filters[]` to either dimension filters or metric filters.
- `sort[]` to fields or metrics.

For calculated columns, the important behavior is:

```csharp
PhysicalExpression = field.IsDerived && field.Expression is not null
    ? SemanticExpressionCompiler.CompileDerivedExpression(field.Expression, model)
    : $"{field.TableId}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}"
```

For calculated measures, it resolves the metric and carries its formula into measure expansion.

## 13B: Measure Expansion

**File**: `ReportPlatform/Report.QueryEngine/Measures/MeasureExpansionEngine.cs`

```csharp
sqlExpression = SemanticExpressionCompiler.CompileMetricFormula(metric.Formula, model);
```

**Input**:

```text
metric.Formula = "ROUND([metric.gross_margin] / [metric.sales_amount], 4)"
```

**Processing**:

- Parse formula.
- Validate aggregate rules for calculated measure.
- Compile metric references recursively.

**Output**:

```sql
ROUND((SUM(f.[SalesAmount]) - SUM(f.[TotalProductCost])) / NULLIF(SUM(f.[SalesAmount]), 0), 4)
```

The exact aliases are applied later by the logical plan builder.

## 13C: Logical Plan Builder for Calculated Columns

**File**: `ReportPlatform/Report.QueryEngine/Planning/LogicalPlanBuilder.cs`

For group fields:

```csharp
Expression = field.IsDerived && field.Expression is not null
    ? ApplyAliases(SemanticExpressionCompiler.CompileDerivedExpression(field.Expression, model), aliases)
    : $"{aliases[field.TableId]}.{SqlIdentifier.QuoteColumn(field.PhysicalColumn)}"
```

For group-by:

```csharp
GroupBy = context.GroupFields
    .Select(f => f.IsDerived && f.Expression is not null
        ? ApplyAliases(SemanticExpressionCompiler.CompileDerivedExpression(f.Expression, model), aliases)
        : $"{aliases[f.TableId]}.{SqlIdentifier.QuoteColumn(f.PhysicalColumn)}")
```

**Result**:

A calculated column expression appears in both `SELECT` and `GROUP BY` when metrics are present.

## 13D: Logical Plan Builder for Calculated Measures

For expanded measures:

```csharp
var expr = ApplyAliases(measure.SqlExpression, aliases);
select.Add(new SelectItem
{
    Expression = expr,
    Alias = measure.Alias,
    Role = "metric"
});
```

**Result**:

A calculated measure expression appears as an aggregate expression in `SELECT`.

## 13E: SQL Compiler

**File**: `ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs`

The final compiler generates:

```sql
SELECT
  {select expressions}
FROM {base table} {base alias}
{joins}
WHERE
  {dimension filters}
GROUP BY
  {calculated column / dimension expressions}
HAVING
  {metric filters}
ORDER BY
  {sort expressions}
OFFSET ... FETCH NEXT ...
```

### Example SQL With Calculated Column + Measure

Input:

```json
{
  "rows": ["factsales.gross_margin_bucket"],
  "values": ["metric.gross_margin_pct"]
}
```

Possible SQL shape:

```sql
SELECT
  CASE WHEN f.[SalesAmount] - f.[TotalProductCost] > 0
       THEN 'Profit'
       ELSE 'Loss'
  END AS [Gross Margin Bucket],
  ROUND(
    (SUM(f.[SalesAmount]) - SUM(f.[TotalProductCost])) /
    NULLIF(SUM(f.[SalesAmount]), 0),
    4
  ) AS [Gross Margin %]
FROM [dbo].[FactSales] f
GROUP BY
  CASE WHEN f.[SalesAmount] - f.[TotalProductCost] > 0
       THEN 'Profit'
       ELSE 'Loss'
  END
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY;
```

---

# 📦 PHASE 14: OUTPUT - QUERY RESULT AND ARTIFACT

**File**: `ReportPlatform/Report.QueryEngine/Services/ReportQueryService.cs`

After SQL executes:

```csharp
var queryResult = await _queryExecutor.ExecuteAsync(
    request.ConnectionId,
    sql,
    expected,
    ct);
```

Then the service builds an artifact:

```csharp
var built = _artifactBuilder.Build(artifactKey, header, table);
await _artifactStore.SaveAsync(artifactKey, built.ArtifactStream, ct);
await _executionRepository.MarkCompletedAsync(...);
```

**Output to frontend**:

```json
{
  "success": true,
  "columns": [
    { "name": "Gross Margin Bucket", "type": "string" },
    { "name": "Gross Margin %", "type": "decimal" }
  ],
  "data": [
    { "Gross Margin Bucket": "Profit", "Gross Margin %": 0.42 }
  ],
  "compilation": {
    "success": true,
    "sql": "SELECT ...",
    "parameters": {}
  },
  "executionId": "exec-...",
  "artifactKey": "reports/rpt_001/.../artifact-v1.seaf"
}
```

---

# 📤 PHASE 15: HOW CALCULATED OBJECTS AFFECT EXPORT

Calculated columns/measures are not re-created during export.

Export uses the already executed report artifact:

```text
Run Report
    ↓
SQL with calculated columns/measures executes
    ↓
Rows are saved into artifact
    ↓
Export loads artifact
    ↓
CSV/Telerik output contains the calculated result columns
```

So if the report was executed with:

```text
rows[] = [calculated column]
values[] = [calculated measure]
```

then export output includes the resulting calculated columns/measures because they are already materialized in the saved artifact rows.

### CSV Export Shape

```csv
Gross Margin Bucket,Gross Margin %
Profit,0.42
Loss,-0.13
```

### PDF/XLSX/DOCX Export Shape

Telerik receives a `DataTable` loaded from the artifact. The report table contains columns matching the query result column names.

---

# 🔄 COMPLETE FLOW SUMMARY

```text
┌────────────────────────────────────────────┐
│ 1. User opens calculated builder           │
│    Schema Panel → New Calculated Column    │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 2. User builds expression                  │
│    Tokens or formula textarea              │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 3. Frontend serializes expression          │
│    [field] + operators/functions           │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 4. Frontend validates expression           │
│    POST /expressions/validate              │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 5. Backend validates expression            │
│    tokenize → parse → bind → scope → type  │
└────────────────────────────────────────────┘
                    ↓
          ┌─────────┴─────────┐
          ↓                   ↓
┌──────────────────┐ ┌───────────────────────┐
│ Row scope         │ │ Aggregate scope        │
│ calculated_column │ │ calculated_measure     │
└──────────────────┘ └───────────────────────┘
          ↓                   ↓
┌──────────────────┐ ┌───────────────────────┐
│ Save SemanticField│ │ Save SemanticMetric    │
│ IsDerived = true  │ │ Formula = expression   │
└──────────────────┘ └───────────────────────┘
          ↓                   ↓
          └─────────┬─────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 6. Frontend refreshes metadata             │
│    Calculated column in tables[].fields[]  │
│    Calculated measure in metrics[]         │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 7. User selects calculated object          │
│    column → rows[] / measure → values[]    │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 8. Backend executes query                  │
│    derived expression → SQL fragment       │
│    metric formula → aggregate SQL          │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 9. Query result + artifact produced        │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 10. Export reads artifact                  │
│     CSV/Telerik output includes results    │
└────────────────────────────────────────────┘
```

---

# 📋 FILE MAPPING TABLE

| Phase | File | Function/Class | Input | Processing | Output |
|---|---|---|---|---|---|
| 1 | `schema-panel.tsx` | `DerivedFieldExpressionBuilder` modal mount | User clicks New Calculated Column | Opens builder | Builder visible |
| 2 | `derived-field-builder.tsx` | `ExpressionPalette` | Metadata fields/metrics | Builds field/measure/operator palette | Drag/select tokens |
| 3 | `derived-field-builder.tsx` | `serializeExpressionToken()` | UI tokens | Converts tokens to `[field]`, `[metric]`, operators, literals | Expression string |
| 4 | `derived-field-builder.tsx` | `handleSave()` | Name + expression | Calls validation API | Valid/invalid UI state |
| 5 | `semantic-management-api.ts` | `validateExpression()` | Dataset + expression | POST `/expressions/validate` | Validation result |
| 6 | `ExpressionsController.cs` | `Validate()` | Dataset + request | Loads model and delegates | `ExpressionValidationResponse` |
| 7 | `ExpressionValidationService.cs` | `Validate()` | Model + request | Maps internal validation result | API response DTO |
| 8 | `SemanticExpressionValidationService.cs` | `Validate()` | Semantic model + expression | Tokenize, parse, bind, scope, validate, type, SQL preview | Internal validation result |
| 9 | `ExpressionToken.cs` | `ExpressionTokenizer.Tokenize()` | Expression string | Lexical scanning | Token list |
| 10 | `ExpressionParser.cs` | `ExpressionParser.Parse()` | Token list | Builds AST | Expression AST |
| 11 | `ExpressionSemanticBinder.cs` | `Bind()` | AST + model | Resolves fields/metrics | Bound expression |
| 12 | `ExpressionScopeResolver.cs` | `Resolve()` | AST | Detects aggregate shape | Row/Aggregate scope |
| 13 | `AggregationValidationService.cs` | `Validate()` | AST + scope | Rejects invalid aggregate usage | Pass/error |
| 14 | `ExpressionTypeInferenceService.cs` | `Infer()` | AST + model | Calculates return type | Data type |
| 15 | `SemanticExpressionSqlCompiler.cs` | `Compile()` | Bound expression | Builds SQL fragment | SQL preview |
| 16 | `app/page.tsx` | `addCalculatedField()` | Builder save object | Calls `createCalculatedObject` and refreshes metadata | New metadata |
| 17 | `semantic-management-api.ts` | `createCalculatedObject()` | Request body | POST `/calculated-objects` | Created object response |
| 18 | `CalculatedObjectsController.cs` | `Create()` | Dataset + request | Delegates to mutation service | Created object response |
| 19 | `SemanticModelMutationService.cs` | `CreateCalculatedObjectAsync()` | Request + semantic model | Saves `SemanticField` or `SemanticMetric` | Mutated semantic model |
| 20 | `DatasetMetadataService.cs` | `GetMetadataAsync()` | Dataset ID | Maps semantic model to metadata DTO | Updated metadata |
| 21 | `build-visual-query-request.ts` | `buildVisualQueryRequest()` | Selected fields + metadata | Places columns in rows and measures in values | `VisualQueryRequest` |
| 22 | `ReportQueryService.cs` | `ExecuteAsync()` | `VisualQueryRequest` | Validates, compiles, executes, artifacts | Query response |
| 23 | `MeasureExpansionEngine.cs` | `Expand()` | Selected metrics | Compiles formulas | Expanded measure SQL |
| 24 | `LogicalPlanBuilder.cs` | `Build()` | Eval context + measures + joins | Adds calculated expressions to select/group by | Logical plan |
| 25 | `SqlCompiler.cs` | `Compile()` | Logical plan | Final SQL string + params | SQL Server command |

---

# 🎯 KEY DECISION POINTS

| Decision | Where | Rule | Result |
|---|---|---|---|
| UI detects measure vs column | `derived-field-builder.tsx` | Aggregate function or `[metric.` means measure | Button label changes |
| Backend detects measure vs column | `ExpressionScopeResolver` | Aggregate shape means `ExpressionScope.Aggregate` | Authoritative detected kind |
| Save as measure | `SemanticModelMutationService.CreateCalculatedObjectAsync()` | `DetectedKind == calculated_measure` | Add `SemanticMetric` |
| Save as column | `SemanticModelMutationService.CreateCalculatedObjectAsync()` | `DetectedKind == calculated_column` | Add derived `SemanticField` |
| Calculated column base table | `SemanticModelMutationService.CreateCalculatedObjectAsync()` | Referenced fields must resolve to exactly one table | Prevent ambiguous row expression |
| Row field in aggregate expression | `AggregationValidationService` | Raw field outside aggregate is invalid | Error |
| Nested aggregate | `AggregationValidationService` | Aggregate inside aggregate is invalid | Error |
| Metric reference in aggregate function | `AggregationValidationService` | `SUM([metric.x])` invalid | Error |
| Runtime placement | `buildVisualQueryRequest()` | derived fields → `rows[]`, metrics → `values[]` | Query request shape |
| SQL generation | `LogicalPlanBuilder` / `MeasureExpansionEngine` | fields compile as scalar SQL, metrics compile as aggregate SQL | Final SQL |

---

# 🧪 INPUT / PROCESS / OUTPUT EXAMPLES

## Example A: Calculated Column

### User Input

```text
Name: Gross Margin
Expression: [FactSales.SalesAmount] - [FactSales.TotalProductCost]
```

### Backend Processing

```text
Tokenize:
  FieldReference FactSales.SalesAmount
  Operator -
  FieldReference FactSales.TotalProductCost

Parse:
  BinaryExpressionNode(-)

Bind:
  dependencies = [FactSales.SalesAmount, FactSales.TotalProductCost]
  baseTables = [FactSales]

Scope:
  Row

Detected kind:
  calculated_column

Type:
  decimal

SQL preview:
  FactSales.[SalesAmount] - FactSales.[TotalProductCost]
```

### Save Output

```json
{
  "id": "factsales.gross_margin",
  "detectedKind": "calculated_column",
  "scope": "Row",
  "dataType": "decimal",
  "dependencies": [
    "FactSales.SalesAmount",
    "FactSales.TotalProductCost"
  ]
}
```

### Query Output Shape

```json
{
  "rows": ["factsales.gross_margin"],
  "values": []
}
```

---

## Example B: Calculated Measure

### User Input

```text
Name: Gross Margin %
Expression: ROUND(([metric.gross_margin] / [metric.sales_amount]), 4)
```

### Backend Processing

```text
Tokenize:
  Function ROUND
  MetricReference metric.gross_margin
  Operator /
  MetricReference metric.sales_amount
  NumberLiteral 4

Parse:
  FunctionCallNode ROUND

Bind:
  dependencies = [metric.gross_margin, metric.sales_amount]
  referencedMetrics = [gross_margin, sales_amount]

Scope:
  Aggregate

Detected kind:
  calculated_measure

Type:
  decimal

SQL preview:
  ROUND((expanded gross_margin SQL) / NULLIF((expanded sales_amount SQL), 0), 4)
```

### Save Output

```json
{
  "id": "metric.gross_margin_pct",
  "detectedKind": "calculated_measure",
  "scope": "Aggregate",
  "dataType": "decimal",
  "dependencies": [
    "metric.gross_margin",
    "metric.sales_amount"
  ]
}
```

### Query Output Shape

```json
{
  "rows": ["DimDate.CalendarYear"],
  "values": ["metric.gross_margin_pct"]
}
```

---

# 💡 IMPORTANT NOTES

1. **Calculated column = row-level expression**. It is stored as a `SemanticField` with `IsDerived = true`.
2. **Calculated measure = aggregate-level expression**. It is stored as a `SemanticMetric` with `AggregationBehavior = calculated`.
3. **Frontend detection is only a hint**. Backend `ExpressionScopeResolver` is authoritative.
4. **Calculated columns must reference one base table**. This avoids row-level ambiguity before joins.
5. **Calculated measures can reference metrics**. The SQL compiler recursively expands metric formulas.
6. **Raw fields in calculated measures must be aggregated**. Use `SUM([field])`, `AVG([field])`, etc.
7. **Nested aggregates are blocked**. `SUM(AVG([field]))` is invalid.
8. **Metric references cannot be aggregated again**. `SUM([metric.sales])` is invalid.
9. **Division is protected**. SQL compilation wraps denominators with `NULLIF(..., 0)` unless already protected.
10. **Exports use artifacts**. After report execution, calculated outputs are saved in the artifact; export reads the artifact instead of recalculating the expression.
