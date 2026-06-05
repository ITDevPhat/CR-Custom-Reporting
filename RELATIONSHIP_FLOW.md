# 📍 COMPLETE FLOW TRACE: Relationship Autodetect + Query Planning Workflow

## 🎬 END-TO-END JOURNEY: Connected Tables → Auto Relationships → Joins in SQL

```text
Frontend Connect Source
    ↓
POST /api/connections/discover
    ↓
SqlServerSchemaDiscoveryService.DiscoverAsync()
    ├─ Query SQL Server FK catalog
    ├─ Mark PK/FK/relationship columns on each ColumnDto
    └─ Return DiscoverSchemaResponse.relationships
    ↓
User selects tables and clicks Load
    ↓
POST /api/datasets/register-from-tables
    ↓
DatasetsController.RegisterFromTables()
    ↓
SemanticMetadataGenerator.Generate()
    ├─ Convert database FK rows into SemanticRelationship { Source = database_fk, Confidence = 1.0 }
    ├─ If no FK rows exist, infer Fact→Dim relationships by key-name convention
    ├─ De-duplicate same endpoints
    └─ Save relationships inside SemanticModel
    ↓
DatasetMetadataService.GetMetadataAsync()
    ↓
Frontend metadata.relationships[] renders in schema/report builder
    ↓
User opens Manage Relationships
    ↓
GET /api/datasets/{datasetId}/relationships
    ↓
DatasetRelationshipService.ListAsync()
    ├─ Group table-pair conflicts
    ├─ Count active relationships in each group
    └─ Add warnings for role-playing / multiple relationships
    ↓
User may click Autodetect
    ↓
POST /api/datasets/{datasetId}/relationships/autodetect
    ↓
DatasetRelationshipService.AutodetectAsync()
    ├─ InferByName(model)
    ├─ Skip existing endpoints unless includeExisting=true
    ├─ Add new inferred relationships
    ├─ Normalize one active relationship per table pair
    └─ Return summary
    ↓
User runs report
    ↓
RelationshipTraversalEngine.Build()
    ├─ Keep active N:1 / 1:1 relationships only
    ├─ Add reverse edges for 1:1 or cross-filter both
    ├─ Choose base table
    ├─ Find best relationship path with BFS
    ├─ Rank paths by primary, database_fk, confidence, shortest path
    └─ Return JoinPlan
    ↓
LogicalPlanBuilder + SqlCompiler
    ↓
Final SQL JOIN clauses
```

---

# 🔍 PHASE-BY-PHASE BREAKDOWN

## 🎯 PHASE 1: SQL SERVER RELATIONSHIP DISCOVERY DURING CONNECT SOURCE

**File**: `ReportPlatform/Report.Infrastructure/Connections/SqlServerSchemaDiscoveryService.cs`

When frontend calls `/api/connections/discover`, backend scans SQL Server metadata.

### 1A: Discover Foreign Keys

The service queries SQL Server catalog views:

```sql
SELECT
  fk.name AS ForeignKeyName,
  parent_schema.name AS FromSchema,
  parent_table.name AS FromTable,
  parent_column.name AS FromColumn,
  referenced_schema.name AS ToSchema,
  referenced_table.name AS ToTable,
  referenced_column.name AS ToColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables parent_table ON fkc.parent_object_id = parent_table.object_id
INNER JOIN sys.schemas parent_schema ON parent_table.schema_id = parent_schema.schema_id
INNER JOIN sys.columns parent_column
  ON fkc.parent_object_id = parent_column.object_id
 AND fkc.parent_column_id = parent_column.column_id
INNER JOIN sys.tables referenced_table ON fkc.referenced_object_id = referenced_table.object_id
INNER JOIN sys.schemas referenced_schema ON referenced_table.schema_id = referenced_schema.schema_id
INNER JOIN sys.columns referenced_column
  ON fkc.referenced_object_id = referenced_column.object_id
 AND fkc.referenced_column_id = referenced_column.column_id
ORDER BY FromSchema, FromTable, FromColumn;
```

**Input**:

- SQL Server connection.
- Database catalog metadata.

**Output**:

```csharp
RelationshipDiscoveryDto
{
    ForeignKeyName = "FK_FactSales_DimCustomer",
    FromSchema = "dbo",
    FromTable = "FactSales",
    FromColumn = "CustomerKey",
    ToSchema = "dbo",
    ToTable = "DimCustomer",
    ToColumn = "CustomerKey"
}
```

### 1B: Mark Relationship Columns

The discovery service builds sets:

```text
foreignKeys
relationshipColumns
primaryKeys
uniqueColumns
```

Then every `ColumnDto` receives flags:

```csharp
IsPrimaryKey = primaryKeys.Contains(column)
IsForeignKey = foreignKeys.Contains(column)
ParticipatesInRelationship = relationshipColumns.Contains(column)
IsUnique = uniqueColumns.Contains(column)
ReferencedSchema = fk?.ToSchema ?? ""
ReferencedTable = fk?.ToTable ?? ""
ReferencedColumn = fk?.ToColumn ?? ""
ForeignKeyName = fk?.ForeignKeyName ?? ""
```

**Output to frontend**:

```json
{
  "tables": [
    {
      "schema": "dbo",
      "table": "FactSales",
      "columns": [
        {
          "column": "CustomerKey",
          "isForeignKey": true,
          "participatesInRelationship": true,
          "referencedSchema": "dbo",
          "referencedTable": "DimCustomer",
          "referencedColumn": "CustomerKey",
          "foreignKeyName": "FK_FactSales_DimCustomer"
        }
      ]
    }
  ],
  "relationships": [
    {
      "foreignKeyName": "FK_FactSales_DimCustomer",
      "fromSchema": "dbo",
      "fromTable": "FactSales",
      "fromColumn": "CustomerKey",
      "toSchema": "dbo",
      "toTable": "DimCustomer",
      "toColumn": "CustomerKey"
    }
  ]
}
```

---

## 🧭 PHASE 2: FRONTEND SELECT RELATED TABLES BEFORE LOAD

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

In the table navigator, frontend can select related tables based on discovered FK edges:

```ts
const selectRelatedTables = () => {
  discovery.relationships.forEach((relationship) => {
    const fromKey = `${relationship.fromSchema}.${relationship.fromTable}`
    const toKey = `${relationship.toSchema}.${relationship.toTable}`
    if (fromKey === tableKey(activeTable)) related.add(toKey)
    if (toKey === tableKey(activeTable)) related.add(fromKey)
  })
}
```

**Input**:

- Active table selected in navigator.
- `DiscoverSchemaResponse.relationships`.

**Processing**:

- If active table is relationship source, select target table.
- If active table is relationship target, select source table.

**Output**:

- More tables selected before `register-from-tables`.

**Important**:

This frontend step does not create backend relationships. It only helps user select related tables.

---

# 🏗️ PHASE 3: RELATIONSHIPS GENERATED DURING DATASET REGISTRATION

**File**: `ReportPlatform/Report.Api/Controllers/DatasetsController.cs`

When user clicks **Load**, backend runs:

```csharp
var discovered = await _discoveryService.DiscoverAsync(connection, ct);
var model = _metadataGenerator.Generate(request.DatasetName, discovered, request.SelectedTables);
var dataset = _datasetRegistry.Save(request.DatasetName, connection, model);
```

The important function is:

```csharp
SemanticMetadataGenerator.Generate()
```

---

## 3A: Filter FK Rows to Selected Tables

**File**: `ReportPlatform/Report.Api/Services/SemanticMetadataGenerator.cs`

```csharp
var relationships = discovered.Relationships
    .Where(r => selected.Contains(BuildTableId(r.FromSchema, r.FromTable)) &&
        selected.Contains(BuildTableId(r.ToSchema, r.ToTable)))
```

**Rule**:

A database FK relationship is kept only when both tables were selected.

Example:

| FK | Selected FactSales? | Selected DimCustomer? | Included? |
|---|---:|---:|---:|
| FactSales.CustomerKey → DimCustomer.CustomerKey | yes | yes | yes |
| FactSales.ProductKey → DimProduct.ProductKey | yes | no | no |

---

## 3B: Convert Database FK to SemanticRelationship

```csharp
.Select(r => new SemanticRelationship
{
    RelationshipId = $"rel_{Guid.NewGuid():N}",
    FromTableId = BuildTableId(r.FromSchema, r.FromTable),
    FromColumn = r.FromColumn,
    ToTableId = BuildTableId(r.ToSchema, r.ToTable),
    ToColumn = r.ToColumn,
    JoinType = "INNER",
    Cardinality = "N:1",
    IsPrimary = true,
    CrossFilterDirection = "single",
    IsActive = true,
    Source = "database_fk",
    Confidence = 1.0m,
    Status = "active"
})
```

**Input**:

```text
dbo.FactSales.CustomerKey → dbo.DimCustomer.CustomerKey
```

**Output**:

```csharp
SemanticRelationship
{
    RelationshipId = "rel_a1b2...",
    FromTableId = "FactSales",
    FromColumn = "CustomerKey",
    ToTableId = "DimCustomer",
    ToColumn = "CustomerKey",
    JoinType = "INNER",
    Cardinality = "N:1",
    CrossFilterDirection = "single",
    IsActive = true,
    IsPrimary = true,
    Source = "database_fk",
    Confidence = 1.0m,
    Status = "active"
}
```

### Why Cardinality is `N:1`

The FK source table is treated as the many side, and referenced table is treated as the one side:

```text
FactSales many rows → one DimCustomer row
```

---

## 3C: Convention-Based Inference During Registration

Still in `SemanticMetadataGenerator.Generate()`:

```csharp
.Concat(InferRelationships(tables, discovered.Relationships))
```

### Important Condition

```csharp
if (discoveredRelationships.Count > 0)
{
    yield break;
}
```

**Meaning**:

During initial semantic model generation, convention-based relationship inference only runs when there are **no discovered FK relationships at all**.

If SQL Server has at least one FK, registration relies on database FK relationships and does not run this generator-level inference.

---

## 3D: Generator-Level Inference Algorithm

```csharp
var factTables = tables.Where(t => t.Table.StartsWith("Fact"));
var dimTables = tables.Where(t => t.Table.StartsWith("Dim"));

foreach (var fact in factTables)
{
    foreach (var dim in dimTables)
    {
        var dimKey = dim.Columns.FirstOrDefault(c => c.Column.EndsWith("Key"));
        var factKey = fact.Columns.FirstOrDefault(c =>
            c.Column.Equals(dimKey.Column) ||
            c.Column.EndsWith(dimKey.Column));

        if (factKey is null) continue;

        yield return new SemanticRelationship { ... Source = "inferred" ... };
    }
}
```

**Input Example**:

```text
FactSales.CustomerKey
DimCustomer.CustomerKey
```

**Inference Match**:

```text
Fact key CustomerKey == Dimension key CustomerKey
```

**Output**:

```csharp
SemanticRelationship
{
    FromTableId = "FactSales",
    FromColumn = "CustomerKey",
    ToTableId = "DimCustomer",
    ToColumn = "CustomerKey",
    JoinType = "INNER",
    Cardinality = "N:1",
    CrossFilterDirection = "single",
    IsActive = true,
    IsPrimary = true,
    Source = "inferred",
    Confidence = 0.85m,
    Status = "active",
    Warning = "Inferred relationship. Please verify before production use."
}
```

---

## 3E: De-Duplicate Same Relationship Endpoint

After database FK relationships and inferred relationships are combined:

```csharp
.GroupBy(r => $"{r.FromTableId}|{r.FromColumn}|{r.ToTableId}|{r.ToColumn}", StringComparer.OrdinalIgnoreCase)
.Select(group => group.First())
.ToList();
```

**Rule**:

Only one relationship is kept for the exact same endpoint:

```text
FromTable.FromColumn → ToTable.ToColumn
```

---

# 📤 PHASE 4: RELATIONSHIPS RETURNED AS METADATA

**File**: `ReportPlatform/Report.Api/Services/DatasetMetadataService.cs`

After dataset is saved, relationships are mapped to DTOs:

```csharp
Relationships = model.Relationships
    .OrderBy(relationship => relationship.FromTableId)
    .ThenBy(relationship => relationship.ToTableId)
    .Select(MapRelationship)
    .ToList()
```

Output DTO fields include:

```json
{
  "relationshipId": "rel_...",
  "datasetId": "dataset_...",
  "fromTableId": "FactSales",
  "fromColumn": "CustomerKey",
  "toTableId": "DimCustomer",
  "toColumn": "CustomerKey",
  "joinType": "INNER",
  "cardinality": "N:1",
  "crossFilterDirection": "single",
  "isActive": true,
  "isPrimary": true,
  "source": "database_fk",
  "confidence": 1.0,
  "status": "active",
  "warning": null
}
```

Frontend stores this under:

```ts
metadata.relationships
```

---

# 🧑‍💻 PHASE 5: RELATIONSHIP MANAGEMENT FRONTEND

## 5A: Open Manage Relationships

**File**: `data-report-builder/app/page.tsx`

The main page opens:

```tsx
<ManageRelationshipsModal
  open={relationshipModalOpen}
  datasetId={datasetId}
  metadata={metadata}
  onRelationshipsChanged={refreshMetadata}
/>
```

## 5B: Load Relationships

**File**: `data-report-builder/components/report-builder/relationship-modals.tsx`

```ts
const loadRelationships = async () => {
  setRelationships(await getRelationships(datasetId))
}
```

**File**: `data-report-builder/lib/relationships-api.ts`

```ts
export function getRelationships(datasetId: string) {
  return request<RelationshipDto[]>(`/api/datasets/${datasetId}/relationships`)
}
```

**HTTP Request**:

```http
GET /api/datasets/{datasetId}/relationships
```

---

# 🏗️ PHASE 6: BACKEND LIST RELATIONSHIPS + GROUP WARNINGS

**File**: `ReportPlatform/Report.Api/Controllers/RelationshipsController.cs`

```csharp
[HttpGet]
public async Task<IActionResult> List(string datasetId, CancellationToken ct) =>
    Ok(await _service.ListAsync(datasetId, ct));
```

**File**: `ReportPlatform/Report.Api/Services/DatasetRelationshipService.cs`

```csharp
public async Task<List<RelationshipDto>> ListAsync(string datasetId, CancellationToken ct)
{
    var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
    return BuildDtos(model.Relationships);
}
```

## 6A: Build DTOs With Group Metadata

```csharp
private static List<RelationshipDto> BuildDtos(IEnumerable<SemanticRelationship> relationships)
{
    var list = relationships.ToList();
    var groups = list.GroupBy(r => BuildGroupKey(r.FromTableId, r.ToTableId))
        .ToDictionary(g => g.Key, g => g.ToList());

    return list.Select(r =>
    {
        var key = BuildGroupKey(r.FromTableId, r.ToTableId);
        var group = groups[key];
        var active = group.Count(g => g.IsActive);
        return ToDto(r, group.Count, active, BuildGroupWarning(group, r));
    }).ToList();
}
```

Group key:

```csharp
private static string BuildGroupKey(string fromTableId, string toTableId) =>
    $"{fromTableId}->{toTableId}";
```

**Meaning**:

Relationships are grouped by table pair, not by column pair.

Example group:

```text
FactSales -> DimDate
  FactSales.OrderDateKey -> DimDate.DateKey
  FactSales.ShipDateKey  -> DimDate.DateKey
  FactSales.DueDateKey   -> DimDate.DateKey
```

This is a role-playing dimension scenario.

## 6B: Group Warnings

```csharp
if (group.Count == 1) return current.Warning;
if (active.Count == 0) return "No active relationship is selected for this table pair.";
if (active.Count > 1) return "Multiple active relationships detected for this table pair. Make exactly one active.";
return active[0].RelationshipId == current.RelationshipId
    ? "Multiple relationships exist for this table pair. Only this one is active."
    : "Multiple relationships exist for this table pair. This relationship is inactive.";
```

**Output DTO extras**:

```json
{
  "relationshipGroupKey": "FactSales->DimDate",
  "groupConflictCount": 3,
  "groupActiveCount": 1,
  "warning": "Multiple relationships exist for this table pair. This relationship is inactive."
}
```

---

# ⚡ PHASE 7: MANUAL AUTODETECT RELATIONSHIPS

## 7A: Frontend Autodetect Button

**File**: `data-report-builder/components/report-builder/relationship-modals.tsx`

```ts
const handleAutodetect = async () => {
  const response = await autodetectRelationships(datasetId)
  toast.success(
    `Detected ${response.summary.detected} relationships: ` +
    `${response.summary.databaseForeignKeys} FK, ${response.summary.inferredByName} inferred`
  )
  await refreshAll()
}
```

**File**: `data-report-builder/lib/relationships-api.ts`

```ts
export function autodetectRelationships(datasetId: string) {
  return request<AutodetectRelationshipsResponse>(
    `/api/datasets/${datasetId}/relationships/autodetect`,
    {
      method: 'POST',
      body: JSON.stringify({ datasetId, mode: 'safe', includeExisting: false }),
    }
  )
}
```

**HTTP Request**:

```http
POST /api/datasets/{datasetId}/relationships/autodetect
Content-Type: application/json

{
  "datasetId": "dataset_sales_001",
  "mode": "safe",
  "includeExisting": false
}
```

---

## 7B: Backend Autodetect Entry

**File**: `ReportPlatform/Report.Api/Controllers/RelationshipsController.cs`

```csharp
[HttpPost("autodetect")]
public async Task<IActionResult> Autodetect(
    string datasetId,
    [FromBody] AutodetectRelationshipsRequest request,
    CancellationToken ct) =>
    Ok(await _service.AutodetectAsync(datasetId, request, ct));
```

## 7C: Autodetect Service Main Flow

**File**: `ReportPlatform/Report.Api/Services/DatasetRelationshipService.cs`

```csharp
public async Task<AutodetectRelationshipsResponse> AutodetectAsync(
    string datasetId,
    AutodetectRelationshipsRequest request,
    CancellationToken ct)
{
    var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
    var skipped = 0;
    var detected = new List<SemanticRelationship>();

    foreach (var relationship in InferByName(model))
    {
        var exists = model.Relationships.Any(r => SameEndpoint(r, relationship));
        if (exists && !request.IncludeExisting)
        {
            skipped++;
            continue;
        }

        detected.Add(relationship);
        if (!exists)
        {
            model.Relationships.Add(relationship);
        }
    }

    NormalizeGroupActives(model.Relationships);

    return new AutodetectRelationshipsResponse { ... };
}
```

**Important**:

Manual autodetect does **name-based inference only** from the already saved semantic model. It does not re-query SQL Server catalog FKs. `DatabaseForeignKeys` in summary can be zero because inferred relationships have `Source = inferred`.

---

# 🧠 PHASE 8: MANUAL AUTODETECT NAME-MATCH ALGORITHM

**File**: `ReportPlatform/Report.Api/Services/DatasetRelationshipService.cs`

```csharp
private static IEnumerable<SemanticRelationship> InferByName(SemanticModel model)
{
    var facts = model.Tables.Where(t =>
        t.TableType == "fact" || t.TableId.StartsWith("Fact"));

    var dims = model.Tables.Where(t =>
        t.TableType == "dimension" || t.TableId.StartsWith("Dim"));

    foreach (var fact in facts)
    {
        var factKeys = model.Fields.Where(f =>
            f.TableId == fact.TableId &&
            f.PhysicalColumn.EndsWith("Key"));

        foreach (var factKey in factKeys)
        {
            foreach (var dim in dims)
            {
                var dimKey = model.Fields.FirstOrDefault(f =>
                    f.TableId == dim.TableId &&
                    (
                        f.PhysicalColumn == factKey.PhysicalColumn ||
                        (
                            dim.TableId.Contains("Date") &&
                            f.PhysicalColumn == "DateKey" &&
                            factKey.PhysicalColumn.EndsWith("DateKey")
                        )
                    ));

                if (dimKey is null || !Compatible(factKey.DataType, dimKey.DataType)) continue;

                var confidence = factKey.PhysicalColumn == dimKey.PhysicalColumn ? 1.0m : 0.85m;
                if (confidence < 0.75m) continue;

                yield return new SemanticRelationship { ... };
            }
        }
    }
}
```

## 8A: Fact Table Detection

A table is treated as fact if:

```text
SemanticTable.TableType == "fact"
OR TableId starts with "Fact"
```

## 8B: Dimension Table Detection

A table is treated as dimension if:

```text
SemanticTable.TableType == "dimension"
OR TableId starts with "Dim"
```

## 8C: Fact Key Candidate

A field is a fact key candidate if:

```text
field.TableId == fact.TableId
AND field.PhysicalColumn ends with "Key"
```

Examples:

```text
CustomerKey
ProductKey
OrderDateKey
ShipDateKey
DueDateKey
```

## 8D: Dimension Key Match

A dimension field matches if either:

### Exact Key Name Match

```text
factKey.PhysicalColumn == dimKey.PhysicalColumn
```

Example:

```text
FactSales.CustomerKey == DimCustomer.CustomerKey
```

### Date Role-Playing Match

```text
dim.TableId contains "Date"
AND dim field is "DateKey"
AND fact key ends with "DateKey"
```

Examples:

```text
FactSales.OrderDateKey → DimDate.DateKey
FactSales.ShipDateKey  → DimDate.DateKey
FactSales.DueDateKey   → DimDate.DateKey
```

## 8E: Data Type Compatibility

The service checks:

```csharp
if (dimKey is null || !Compatible(factKey.DataType, dimKey.DataType)) continue;
```

`Compatible()` allows matching data types. This prevents connecting, for example, an `int` key to an `nvarchar` key.

## 8F: Confidence

```csharp
var confidence = factKey.PhysicalColumn == dimKey.PhysicalColumn
    ? 1.0m
    : 0.85m;
```

| Match Type | Confidence |
|---|---:|
| Exact same key name | `1.0` |
| Date role-playing suffix match | `0.85` |

Relationships below `0.75` are ignored, but the current algorithm only emits `1.0` or `0.85`.

## 8G: Inferred Relationship Output

```csharp
SemanticRelationship
{
    RelationshipId = $"rel_{Guid.NewGuid():N}",
    DatasetId = model.DatasetId,
    FromTableId = fact.TableId,
    FromColumn = factKey.PhysicalColumn,
    ToTableId = dim.TableId,
    ToColumn = dimKey.PhysicalColumn,
    Cardinality = "N:1",
    JoinType = "INNER",
    CrossFilterDirection = "single",
    IsActive = true,
    IsPrimary = true,
    Source = "inferred",
    Confidence = confidence,
    Status = "warning",
    Warning = "Inferred relationship. Please verify before production use."
}
```

---

# 🧩 PHASE 9: EXISTING RELATIONSHIP SKIP / INCLUDE LOGIC

In `AutodetectAsync()`:

```csharp
var exists = model.Relationships.Any(r => SameEndpoint(r, relationship));
if (exists && !request.IncludeExisting)
{
    skipped++;
    continue;
}

detected.Add(relationship);
if (!exists)
{
    model.Relationships.Add(relationship);
}
```

Endpoint equality means:

```csharp
left.FromTableId == right.FromTableId &&
left.FromColumn == right.FromColumn &&
left.ToTableId == right.ToTableId &&
left.ToColumn == right.ToColumn
```

| includeExisting | Existing endpoint? | Added to model? | Included in response relationships? | skipped count |
|---:|---:|---:|---:|---:|
| false | yes | no | no | +1 |
| false | no | yes | yes | no |
| true | yes | no | yes | no |
| true | no | yes | yes | no |

Frontend currently sends:

```json
{ "includeExisting": false }
```

---

# ⚖️ PHASE 10: NORMALIZE ACTIVE RELATIONSHIP PER TABLE PAIR

**File**: `ReportPlatform/Report.Api/Services/DatasetRelationshipService.cs`

After autodetect:

```csharp
NormalizeGroupActives(model.Relationships);
```

## 10A: Group By Table Pair

```csharp
foreach (var group in relationships.GroupBy(r => BuildGroupKey(r.FromTableId, r.ToTableId)))
```

Group key:

```text
FromTableId -> ToTableId
```

Example:

```text
FactSales -> DimDate
```

## 10B: Chosen Active Relationship

```csharp
SemanticRelationship? chosen = null;
if (group.Count() == 1) chosen = group.First();
else
{
    chosen = group.FirstOrDefault(r => r.FromColumn.Equals("OrderDateKey"))
        ?? group.FirstOrDefault(r =>
            r.FromColumn.Contains("Order") &&
            r.FromColumn.Contains("Date"));
}

if (chosen is null) continue;
ApplyActivationRule(relationships, chosen.RelationshipId);
```

**Rule**:

- If only one relationship exists between a table pair, it becomes active.
- If multiple relationships exist, prefer `OrderDateKey`.
- Otherwise prefer a column containing both `Order` and `Date`.
- If no chosen relationship, leave group as-is.

## 10C: Apply Activation Rule

```csharp
private static void ApplyActivationRule(List<SemanticRelationship> relationships, string relationshipId)
{
    var target = relationships.First(r => r.RelationshipId == relationshipId);
    var key = BuildGroupKey(target.FromTableId, target.ToTableId);

    for each relationship in same table-pair group:
        IsActive = isTarget
        IsPrimary = isTarget
        Status = isTarget ? "active" : "inactive"
}
```

**Input group**:

```text
FactSales.OrderDateKey -> DimDate.DateKey active=true
FactSales.ShipDateKey  -> DimDate.DateKey active=true
FactSales.DueDateKey   -> DimDate.DateKey active=true
```

**Output group after normalize**:

```text
FactSales.OrderDateKey -> DimDate.DateKey active=true,  status=active
FactSales.ShipDateKey  -> DimDate.DateKey active=false, status=inactive
FactSales.DueDateKey   -> DimDate.DateKey active=false, status=inactive
```

---

# ✍️ PHASE 11: MANUAL CREATE / UPDATE / DELETE / ACTIVATE

## 11A: Manual Editor Frontend

**File**: `data-report-builder/components/report-builder/relationship-modals.tsx`

The editor lets user pick:

```ts
RelationshipRequest {
  fromTableId
  fromColumn
  toTableId
  toColumn
  cardinality: '1:1' | '1:N' | 'N:1' | 'N:N'
  joinType: 'INNER' | 'LEFT'
  crossFilterDirection: 'single' | 'both'
  isActive
  isPrimary
}
```

The editor uses metadata tables/fields and optional table previews to help choose columns.

## 11B: Create Endpoint

```http
POST /api/datasets/{datasetId}/relationships
```

Backend:

```csharp
public async Task<RelationshipDto> CreateAsync(...)
{
    var model = await LoadAndEnsureRegisteredAsync(datasetId, ct);
    Validate(model, request);
    var relationship = BuildRelationship(datasetId, request, $"rel_{Guid.NewGuid():N}", "manual", 1.0m);
    model.Relationships.Add(relationship);
    if (relationship.IsActive) ApplyActivationRule(model.Relationships, relationship.RelationshipId);
    return BuildDtos(model.Relationships).First(...);
}
```

## 11C: Update Endpoint

```http
PUT /api/datasets/{datasetId}/relationships/{relationshipId}
```

Backend keeps original `Source` and `Confidence`:

```csharp
var updated = BuildRelationship(
    datasetId,
    request,
    relationshipId,
    model.Relationships[index].Source,
    model.Relationships[index].Confidence);
```

## 11D: Delete Endpoint

```http
DELETE /api/datasets/{datasetId}/relationships/{relationshipId}
```

Backend removes the relationship by ID.

## 11E: Activate Endpoint

```http
POST /api/datasets/{datasetId}/relationships/{relationshipId}/activate
```

Frontend confirms if another relationship in the same group is active:

```ts
const currentActive = relationships.find(
  r => r.relationshipGroupKey === rel.relationshipGroupKey && r.isActive
)
```

Backend runs `ApplyActivationRule()` so exactly one relationship in that table-pair group becomes active.

---

# 🚦 PHASE 12: RELATIONSHIP VALIDATION RULES

**File**: `ReportPlatform/Report.Api/Services/DatasetRelationshipService.cs`

Before create/update, backend validates:

```csharp
if (!Cardinalities.Contains(request.Cardinality))
    throw "Unsupported cardinality."

if (request.FromTableId == request.ToTableId)
    throw "Self relationships are not supported for this MVP."

var from = model.Fields.FirstOrDefault(f =>
    f.TableId == request.FromTableId && f.PhysicalColumn == request.FromColumn);

var to = model.Fields.FirstOrDefault(f =>
    f.TableId == request.ToTableId && f.PhysicalColumn == request.ToColumn);

if (from is null) throw "From column was not found."
if (to is null) throw "To column was not found."
if (!Compatible(from.DataType, to.DataType))
    throw "Relationship column data types are not compatible."
```

Supported cardinalities:

```text
1:1, 1:N, N:1, N:N
```

Build warning rules:

```csharp
if (Cardinality == "N:N")
  "Many-to-many relationship is not supported by automatic query planning."

if (CrossFilterDirection == "both")
  "Bidirectional filter propagation is stored but not fully supported by query planner yet."

if (Source == "inferred")
  "Inferred relationship. Please verify before production use."
```

Important build behavior:

```csharp
IsActive = request.IsActive && request.Cardinality != "N:N"
```

So `N:N` relationships can be stored but are forced inactive for query planning.

---

# 🚀 PHASE 13: HOW RELATIONSHIPS DRIVE QUERY SQL

Relationships do not directly render in the frontend report. They matter when backend compiles a query.

**File**: `ReportPlatform/Report.QueryEngine/Relationships/RelationshipTraversalEngine.cs`

Main function:

```csharp
public JoinPlan Build(EvaluationContext context, List<ExpandedMeasure> measures, SemanticModel model)
```

## 13A: Gather Requested Tables

```csharp
var requestedTables = context.GroupFields
    .Select(f => f.TableId)
    .Concat(measures.Select(m => m.BaseTableId))
    .Concat(context.Filters.WhereFilters.Select(f => f.PhysicalTable))
    .Distinct()
    .ToList();
```

Requested tables come from:

| Query part | Table source |
|---|---|
| rows/dimensions | selected fields' `TableId` |
| values/measures | metric `BaseTableId` |
| dimension filters | filter field physical table |

Example:

```json
{
  "rows": ["dimcustomer.customername"],
  "values": ["metric.sum_factsales_salesamount"]
}
```

Requested tables:

```text
DimCustomer
FactSales
```

## 13B: Keep Only Traversable Relationships

```csharp
foreach (var relationship in model.Relationships
    .Where(r => r.IsActive && r.Cardinality is "N:1" or "1:1"))
```

Only these can drive automatic query joins:

| Relationship | Traversable? |
|---|---:|
| active `N:1` | yes |
| active `1:1` | yes |
| inactive | no |
| `1:N` | no in current planner |
| `N:N` | no |

## 13C: Reverse Edge Rules

```csharp
edges.Add(relationship);
if (relationship.CrossFilterDirection == "both" || relationship.Cardinality == "1:1")
{
    edges.Add(reverse relationship);
}
```

Default `N:1` single-direction relationship is traversed only from many side to one side:

```text
FactSales → DimCustomer
```

If cross-filter is `both` or cardinality is `1:1`, reverse edge is also available:

```text
DimCustomer → FactSales
```

## 13D: Choose Base Table

```csharp
var measureBaseTable = measures.FirstOrDefault()?.BaseTableId;
if (!string.IsNullOrWhiteSpace(measureBaseTable)) return measureBaseTable;
```

Rule priority:

1. If query has a measure, use first measure's base table.
2. If one requested table, use that table.
3. Otherwise find a bridge table that can reach all requested tables.
4. Prefer tables whose ID starts with `Fact`.
5. Prefer requested tables over non-requested bridge tables.
6. Fallback to first group field table.

Example:

```text
Rows: DimCustomer.CustomerName
Values: metric.sum_factsales_salesamount
Base table: FactSales
```

## 13E: Find Paths With BFS

For every required table different from base table:

```csharp
var path = FindPath(baseTable, table, activeRelationships);
```

`FindPath()` performs breadth-first search over active relationship edges.

If no path exists:

```text
NO_ACTIVE_RELATIONSHIP_PATH
No active relationship path from FactSales to DimCustomer.
```

## 13F: Rank Multiple Paths

If multiple paths exist, rank by:

```csharp
.OrderByDescending(PathPrimaryScore)
.ThenByDescending(PathDatabaseFkScore)
.ThenByDescending(PathConfidenceScore)
.ThenBy(p => p.Count)
```

Priority:

1. More primary relationships.
2. More `database_fk` relationships.
3. Higher total confidence.
4. Shorter path.

If top two paths tie on all ranking dimensions:

```text
AMBIGUOUS_RELATIONSHIP_PATH
Multiple equally ranked relationship paths exist between {baseTable} and {targetTable}.
```

## 13G: Build JoinPlan

For each relationship in the chosen path:

```csharp
joins.Add(new JoinDef
{
    RelationshipId = rel.RelationshipId,
    FromTableId = rel.FromTableId,
    ToTableId = rel.ToTableId,
    JoinType = rel.JoinType,
    FromColumn = rel.FromColumn,
    ToColumn = rel.ToColumn
});
```

Duplicate join keys are skipped:

```csharp
var key = $"{rel.FromTableId}.{rel.FromColumn}->{rel.ToTableId}.{rel.ToColumn}";
if (!seenJoinKeys.Add(key)) continue;
```

**Output**:

```csharp
JoinPlan
{
    BaseTableId = "FactSales",
    Joins = [
        JoinDef
        {
            FromTableId = "FactSales",
            FromColumn = "CustomerKey",
            ToTableId = "DimCustomer",
            ToColumn = "CustomerKey",
            JoinType = "INNER"
        }
    ]
}
```

---

# 🧱 PHASE 14: JOINPLAN → FINAL SQL

**File**: `ReportPlatform/Report.QueryEngine/Planning/LogicalPlanBuilder.cs`

The logical plan converts joins into join items:

```csharp
var joins = joinPlan.Joins.Select(j =>
    new JoinItem
    {
        JoinType = j.JoinType,
        TableId = j.ToTableId,
        Alias = aliases[j.ToTableId],
        Condition = $"{aliases[j.FromTableId]}.{QuoteColumn(j.FromColumn)} = {aliases[j.ToTableId]}.{QuoteColumn(j.ToColumn)}"
    }).ToList();
```

**File**: `ReportPlatform/Report.QueryEngine/Compilation/SqlCompiler.cs`

The SQL compiler renders:

```csharp
var joins = string.Join("\n",
    plan.Joins.Select(j =>
        $"{j.JoinType} JOIN {plan.TableExpressions[j.TableId]} {j.Alias} ON {j.Condition}"));
```

Example final SQL shape:

```sql
SELECT
  d.[CustomerName] AS [Customer Name],
  SUM(f.[SalesAmount]) AS [Total Sales Amount]
FROM [dbo].[FactSales] f
INNER JOIN [dbo].[DimCustomer] d
  ON f.[CustomerKey] = d.[CustomerKey]
GROUP BY
  d.[CustomerName]
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY;
```

---

# 🔄 COMPLETE FLOW SUMMARY

```text
┌────────────────────────────────────────────┐
│ 1. SQL Server discovery                    │
│    Read FK metadata from system catalogs   │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 2. Discovery response                      │
│    relationship rows + column FK flags     │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 3. Dataset registration                    │
│    Selected tables only                    │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 4. SemanticMetadataGenerator               │
│    DB FKs → SemanticRelationship           │
│    If no FKs → infer Fact/Dim key matches  │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 5. Relationship metadata saved in model    │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 6. Relationship manager                    │
│    List/create/update/delete/autodetect    │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 7. Autodetect                              │
│    InferByName + skip existing + normalize │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 8. Query run                               │
│    Active N:1 / 1:1 relationships only     │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 9. Traversal engine                        │
│    Choose base, BFS paths, rank best path  │
└────────────────────────────────────────────┘
                    ↓
┌────────────────────────────────────────────┐
│ 10. Logical plan + SQL compiler            │
│     JOIN clauses in final SQL              │
└────────────────────────────────────────────┘
```

---

# 📋 FILE MAPPING TABLE

| Phase | File | Class / Function | Input | Processing | Output |
|---|---|---|---|---|---|
| SQL FK scan | `SqlServerSchemaDiscoveryService.cs` | `DiscoverAsync()` | SQL Server connection | Queries `sys.foreign_keys`, `sys.foreign_key_columns` | `RelationshipDiscoveryDto[]` |
| Column FK flags | `SqlServerSchemaDiscoveryService.cs` | `DiscoverAsync()` | columns + FK rows | Marks `IsForeignKey`, `ParticipatesInRelationship`, referenced columns | enriched `ColumnDto[]` |
| Frontend related select | `connect-source-modals.tsx` | `selectRelatedTables()` | active table + discovered relationships | Adds connected tables to selection | updated selected table set |
| Dataset registration | `DatasetsController.cs` | `RegisterFromTables()` | selected tables | Saves connection, discovers schema, generates model | `RegisterDatasetResponse` |
| Initial DB FK mapping | `SemanticMetadataGenerator.cs` | `Generate()` relationship block | discovered FKs + selected tables | Filters both sides selected, maps FK to semantic relationship | `Source=database_fk` relationships |
| Initial inference | `SemanticMetadataGenerator.cs` | `InferRelationships()` | selected tables, no discovered FKs | Fact/Dim key-name matching | `Source=inferred` relationships |
| Metadata DTO | `DatasetMetadataService.cs` | `MapRelationship()` | `SemanticRelationship` | Maps model to API DTO | `MetadataRelationshipDto` |
| Frontend API | `relationships-api.ts` | `getRelationships()` | dataset ID | GET relationships endpoint | relationship array |
| Relationship list | `RelationshipsController.cs` | `List()` | dataset ID | Delegates service | relationship DTOs |
| Group warnings | `DatasetRelationshipService.cs` | `BuildDtos()` / `BuildGroupWarning()` | model relationships | Groups by `FromTable->ToTable` | group counts/warnings |
| Manual autodetect | `relationships-api.ts` | `autodetectRelationships()` | dataset ID | POST autodetect safe request | autodetect summary |
| Autodetect backend | `DatasetRelationshipService.cs` | `AutodetectAsync()` | semantic model | InferByName, skip existing, add new, normalize actives | response + saved model updates |
| Name inference | `DatasetRelationshipService.cs` | `InferByName()` | semantic tables/fields | Fact/Dim key matching | inferred relationships |
| Create/update validation | `DatasetRelationshipService.cs` | `Validate()` | relationship request | Cardinality/table/column/type checks | pass/error |
| Activate active path | `DatasetRelationshipService.cs` | `ApplyActivationRule()` | relationship ID | One active per table-pair group | updated active statuses |
| Query traversal | `RelationshipTraversalEngine.cs` | `Build()` | eval context + measures + model | Active relationship graph path finding | `JoinPlan` |
| Path selection | `RelationshipTraversalEngine.cs` | `FindPath()` | base table + target table | BFS + ranking | best relationship path |
| SQL join build | `LogicalPlanBuilder.cs` | `Build()` | `JoinPlan` | Builds join condition with aliases | logical join items |
| SQL render | `SqlCompiler.cs` | `Compile()` | logical plan joins | Renders `INNER/LEFT JOIN` clauses | final SQL |

---

# 🎯 KEY DECISION POINTS

| Decision | Where | Rule | Result |
|---|---|---|---|
| Include database FK in semantic model | `SemanticMetadataGenerator.Generate()` | Both FK tables are selected | Add `Source=database_fk`, `Confidence=1.0` |
| Run initial convention inference | `SemanticMetadataGenerator.InferRelationships()` | Only if discovered FK count is `0` | Add `Source=inferred`, `Confidence=0.85` |
| Manual autodetect candidates | `DatasetRelationshipService.InferByName()` | Fact tables + Dim tables + `*Key` fields | Candidate inferred relationships |
| Exact key confidence | `InferByName()` | fact key name equals dim key name | `Confidence=1.0` |
| Date role-playing confidence | `InferByName()` | fact key ends DateKey, dim key DateKey | `Confidence=0.85` |
| Skip existing autodetect | `AutodetectAsync()` | same endpoint exists and `includeExisting=false` | increment skipped, don't add |
| Active group normalization | `NormalizeGroupActives()` | one table pair group | Prefer single relationship, or `OrderDateKey` |
| Manual N:N active | `BuildRelationship()` | cardinality `N:N` | forced inactive |
| Query traversable relationships | `RelationshipTraversalEngine.GetTraversableRelationships()` | active and `N:1` or `1:1` | included in graph |
| Reverse traversal | `GetTraversableRelationships()` | cross-filter both or `1:1` | add reverse edge |
| Base table choice | `ChooseBaseTable()` | first metric base table wins | fact table often base |
| Path ranking | `FindPath()` | primary > database_fk > confidence > short path | deterministic best path |
| Ambiguous path error | `FindPath()` | top two paths tie | throw `AMBIGUOUS_RELATIONSHIP_PATH` |

---

# 🧪 INPUT / PROCESS / OUTPUT EXAMPLES

## Example A: Database FK Auto-Caught During Registration

### Input SQL Server FK

```text
FK_FactSales_DimCustomer:
  dbo.FactSales.CustomerKey → dbo.DimCustomer.CustomerKey
```

### Selected Tables

```json
[
  { "schema": "dbo", "table": "FactSales" },
  { "schema": "dbo", "table": "DimCustomer" }
]
```

### Processing

```text
DiscoverAsync:
  reads sys.foreign_keys row
  marks FactSales.CustomerKey as FK
  returns RelationshipDiscoveryDto

SemanticMetadataGenerator.Generate:
  both From/To tables selected
  maps FK to SemanticRelationship
  Source = database_fk
  Confidence = 1.0
  Status = active
```

### Output SemanticRelationship

```json
{
  "relationshipId": "rel_abc",
  "fromTableId": "FactSales",
  "fromColumn": "CustomerKey",
  "toTableId": "DimCustomer",
  "toColumn": "CustomerKey",
  "joinType": "INNER",
  "cardinality": "N:1",
  "crossFilterDirection": "single",
  "isActive": true,
  "isPrimary": true,
  "source": "database_fk",
  "confidence": 1.0,
  "status": "active"
}
```

---

## Example B: Initial Inference When Database Has No FKs

### Input Tables

```text
FactSales columns:
  SalesKey
  CustomerKey
  SalesAmount

DimCustomer columns:
  CustomerKey
  CustomerName
```

### Processing

```text
DiscoverAsync:
  discoveredRelationships.Count = 0

SemanticMetadataGenerator.InferRelationships:
  factTables = [FactSales]
  dimTables = [DimCustomer]
  dimKey = CustomerKey
  factKey = CustomerKey
  match found
```

### Output

```json
{
  "fromTableId": "FactSales",
  "fromColumn": "CustomerKey",
  "toTableId": "DimCustomer",
  "toColumn": "CustomerKey",
  "source": "inferred",
  "confidence": 0.85,
  "status": "active",
  "warning": "Inferred relationship. Please verify before production use."
}
```

---

## Example C: Manual Autodetect Finds Role-Playing Date Relationships

### Input Tables

```text
FactSales:
  OrderDateKey
  ShipDateKey
  DueDateKey

DimDate:
  DateKey
```

### Processing

```text
InferByName:
  fact table = FactSales
  dim table = DimDate
  fact keys end with DateKey
  dim table contains Date
  dim key is DateKey

Detected:
  OrderDateKey → DateKey confidence 0.85
  ShipDateKey → DateKey confidence 0.85
  DueDateKey → DateKey confidence 0.85

NormalizeGroupActives:
  group = FactSales->DimDate
  chosen = OrderDateKey
```

### Output Group

```json
[
  {
    "fromColumn": "OrderDateKey",
    "toColumn": "DateKey",
    "isActive": true,
    "status": "active",
    "groupConflictCount": 3,
    "groupActiveCount": 1,
    "warning": "Multiple relationships exist for this table pair. Only this one is active."
  },
  {
    "fromColumn": "ShipDateKey",
    "toColumn": "DateKey",
    "isActive": false,
    "status": "inactive",
    "groupConflictCount": 3,
    "groupActiveCount": 1,
    "warning": "Multiple relationships exist for this table pair. This relationship is inactive."
  }
]
```

---

## Example D: Query Uses Relationship to Generate JOIN

### Visual Query Request

```json
{
  "rows": ["dimcustomer.customername"],
  "values": ["metric.sum_factsales_salesamount"]
}
```

### Relationship Model

```json
{
  "fromTableId": "FactSales",
  "fromColumn": "CustomerKey",
  "toTableId": "DimCustomer",
  "toColumn": "CustomerKey",
  "isActive": true,
  "cardinality": "N:1",
  "joinType": "INNER"
}
```

### Traversal Processing

```text
requestedTables = [DimCustomer, FactSales]
baseTable = FactSales because selected metric base table is FactSales
requiredTables = [DimCustomer]
FindPath(FactSales, DimCustomer) finds FK edge
JoinPlan contains FactSales.CustomerKey -> DimCustomer.CustomerKey
```

### Output SQL Shape

```sql
SELECT
  d.[CustomerName] AS [Customer Name],
  SUM(f.[SalesAmount]) AS [Total Sales Amount]
FROM [dbo].[FactSales] f
INNER JOIN [dbo].[DimCustomer] d
  ON f.[CustomerKey] = d.[CustomerKey]
GROUP BY
  d.[CustomerName]
OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY;
```

---

# ⚠️ IMPORTANT NOTES / CURRENT LIMITATIONS

1. **There are two auto-relationship flows**: initial registration from SQL Server FK metadata, and manual Manage Relationships → Autodetect by name.
2. **Initial convention inference only runs when the database discovery returns zero FK relationships**.
3. **Manual autodetect does not re-query SQL Server FKs**; it infers from the saved semantic model's table/field names.
4. **Role-playing relationships are grouped by table pair**, e.g. `FactSales->DimDate`; only one active default relationship is allowed per pair.
5. **OrderDateKey wins by default** when multiple date relationships exist in the same table-pair group.
6. **Query planning only uses active `N:1` and `1:1` relationships**.
7. **`N:N` relationships can be stored but are forced inactive and are not used by automatic query planning**.
8. **`1:N` relationships are accepted by management validation but are not traversed by the current query planner**.
9. **Cross-filter `both` stores a warning and adds reverse traversal edges only when query planner builds graph**.
10. **Database FK paths are preferred over inferred paths during path ranking**.
11. **If two relationship paths tie after ranking, the query engine throws `AMBIGUOUS_RELATIONSHIP_PATH` instead of guessing**.
12. **If no active path exists, the query engine throws `NO_ACTIVE_RELATIONSHIP_PATH`**.
