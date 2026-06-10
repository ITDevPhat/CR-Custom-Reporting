# 📍 COMPLETE FLOW TRACE: Connected Data → Semantic Model Build Engine

## 🎬 END-TO-END JOURNEY: Connect Source → Semantic Metadata Ready for Report Builder

```text
Frontend (Next.js)
    ↓
User opens Connect Source modal
    ↓
POST /api/connections/test
    ↓
ConnectionsController.Test()
    ↓
SqlServerConnectionFactory.ToDefinition()
    ↓
SqlServerSchemaDiscoveryService.TestConnectionAsync()
    ↓
Connection saved in IConnectionRegistry
    ↓
Frontend clicks Next
    ↓
POST /api/connections/discover
    ↓
SqlServerSchemaDiscoveryService.DiscoverAsync()
    ├─ Read SQL Server tables from sys.tables/sys.schemas
    ├─ Read columns/types/nullability/order from sys.columns/sys.types
    ├─ Read primary keys from sys.key_constraints
    ├─ Read unique columns from sys.indexes/sys.index_columns
    ├─ Read foreign keys from sys.foreign_keys/sys.foreign_key_columns
    └─ Return DiscoverSchemaResponse { database, tables, relationships }
    ↓
Frontend navigator displays tables, columns, relationships, previews rows
    ↓
User selects tables and clicks Load
    ↓
POST /api/datasets/register-from-tables
    ↓
DatasetsController.RegisterFromTables()
    ├─ Save connection again and get connectionId
    ├─ Discover schema again from SQL Server
    ├─ SemanticMetadataGenerator.Generate()
    │   ├─ Filter to selected tables
    │   ├─ Build SemanticTable objects
    │   ├─ Classify every selected column into SemanticField
    │   ├─ Build relationships from database FK or inference
    │   └─ Auto-generate SemanticMetric objects from fact measure candidates
    ├─ MetadataConsistencyValidator.Validate()
    ├─ IDatasetRegistry.Save()
    ├─ DatasetMetadataService.GetMetadataAsync()
    └─ Return RegisterDatasetResponse { datasetId, connectionId, metadata, warnings, debugFields }
    ↓
Frontend stores dataset/connection/metadata state
    ↓
Schema Panel renders semantic tables, fields, metrics, relationships
    ↓
Report Builder can build VisualQueryRequest using semantic IDs
```

---

# 🔍 PHASE-BY-PHASE BREAKDOWN

## 🎯 PHASE 1: FRONTEND - USER ENTERS SQL SERVER CONNECTION

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

The connect modal starts with a SQL Server connection object:

```ts
const initialConnection: SqlServerConnectionRequest = {
  provider: 'sqlserver',
  server: 'localhost',
  database: 'AdventureWorksDW2025',
  authenticationType: 'sql',
  username: 'itdevphat',
  password: '',
  trustServerCertificate: true,
  encrypt: false,
  commandTimeoutSeconds: 30,
}
```

**Input**:

```ts
SqlServerConnectionRequest {
  provider: 'sqlserver'
  server: string
  database: string
  authenticationType: 'sql' | 'windows'
  username: string
  password: string
  trustServerCertificate: boolean
  encrypt: boolean
  commandTimeoutSeconds: number
}
```

**Output**:

- Connection form state in React.
- No semantic model exists yet.

---

## ✅ PHASE 2: FRONTEND TEST CONNECTION

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

When user clicks **Test Connection**, frontend runs:

```ts
const result = await testConnection(connection)
setTestState({ loading: false, ok: result.success, message: result.message })
```

**File**: `data-report-builder/lib/connections-api.ts`

```ts
export function testConnection(connection: SqlServerConnectionRequest) {
  return postJson<ConnectionTestResponse>('/api/connections/test', connection)
}
```

**HTTP Request**:

```http
POST /api/connections/test
Content-Type: application/json

{
  "provider": "sqlserver",
  "server": "localhost",
  "database": "AdventureWorksDW2025",
  "authenticationType": "sql",
  "username": "itdevphat",
  "password": "***",
  "trustServerCertificate": true,
  "encrypt": false,
  "commandTimeoutSeconds": 30
}
```

**Output**:

```json
{
  "success": true,
  "message": "Connection test succeeded.",
  "connection": {
    "connectionId": "conn_001",
    "provider": "sqlserver",
    "server": "localhost",
    "database": "AdventureWorksDW2025",
    "authenticationType": "sql",
    "trustServerCertificate": true,
    "encrypt": false,
    "commandTimeoutSeconds": 30
  }
}
```

---

# 🏗️ PHASE 3: BACKEND TEST CONNECTION

## 3A: Controller Entry

**File**: `ReportPlatform/Report.Api/Controllers/ConnectionsController.cs`

```csharp
[HttpPost("test")]
public async Task<IActionResult> Test(
    [FromBody] TestConnectionRequest request,
    CancellationToken ct)
{
    var definition = SqlServerConnectionFactory.ToDefinition(request);
    await _discoveryService.TestConnectionAsync(definition, ct);
    var saved = _connectionRegistry.Save(definition);

    return Ok(new ConnectionTestResponse
    {
        Success = true,
        Message = "Connection test succeeded.",
        Connection = ToDto(saved)
    });
}
```

**Input**:

- `TestConnectionRequest` from frontend.

**Processing**:

1. Convert request to backend `ConnectionDefinition`.
2. Open SQL Server connection.
3. Save connection in memory registry if successful.
4. Return public connection DTO without password.

**Output**:

- `ConnectionTestResponse`.
- New in-memory connection ID such as `conn_001`.

## 3B: SQL Server Open Check

**File**: `ReportPlatform/Report.Infrastructure/Connections/SqlServerSchemaDiscoveryService.cs`

```csharp
public async Task TestConnectionAsync(ConnectionDefinition definition, CancellationToken ct)
{
    await using var connection = new SqlConnection(
        SqlServerConnectionFactory.BuildConnectionString(definition));
    await connection.OpenAsync(ct);
}
```

**Output**:

- If connection opens: success.
- If connection fails: exception is caught in controller and returned as `success: false`.

## 3C: Connection Registry

**File**: `ReportPlatform/Report.Metadata/Connections/InMemoryConnectionRegistry.cs`

```csharp
var connectionId = string.IsNullOrWhiteSpace(definition.ConnectionId)
    ? $"conn_{Interlocked.Increment(ref _nextId):000}"
    : definition.ConnectionId;

_connections[connectionId] = saved;
```

**Important**:

This registry is in-memory. Restarting the backend loses these saved connections unless another persistence implementation is added.

---

# 🌐 PHASE 4: FRONTEND DISCOVERS DATABASE SCHEMA

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

When user clicks **Next**, frontend runs:

```ts
const response = await discoverSchema(connection)
setDiscovery(response)
setSelectedTables(new Set())
setActiveTable(response.tables[0] ?? null)
setStep('navigator')
```

**File**: `data-report-builder/lib/connections-api.ts`

```ts
export function discoverSchema(connection: SqlServerConnectionRequest) {
  return postJson<DiscoverSchemaResponse>('/api/connections/discover', connection)
}
```

**HTTP Request**:

```http
POST /api/connections/discover
Content-Type: application/json

{ ...SqlServerConnectionRequest }
```

**Frontend Output Type**:

```ts
DiscoverSchemaResponse {
  database: string
  tables: TableDto[]
  relationships: RelationshipDiscovery[]
}
```

Each table contains:

```ts
TableDto {
  schema: string
  table: string
  tableType: string
  columns: ColumnDto[]
}
```

Each column contains metadata such as:

```ts
ColumnDto {
  schema: string
  table: string
  column: string
  dataType: string
  sqlDataType: string
  ordinalPosition: number
  isNullable: boolean
  isPrimaryKey: boolean
  isForeignKey: boolean
  participatesInRelationship: boolean
  isUnique: boolean
  referencedSchema: string
  referencedTable: string
  referencedColumn: string
  foreignKeyName: string
}
```

---

# 🔎 PHASE 5: BACKEND DISCOVERS SQL SERVER SCHEMA

## 5A: Controller Entry

**File**: `ReportPlatform/Report.Api/Controllers/ConnectionsController.cs`

```csharp
[HttpPost("discover")]
public async Task<IActionResult> Discover(
    [FromBody] CreateConnectionRequest request,
    CancellationToken ct)
{
    var definition = SqlServerConnectionFactory.ToDefinition(request);
    var response = await _discoveryService.DiscoverAsync(definition, ct);
    return Ok(response);
}
```

**Input**:

- SQL Server connection request.

**Output**:

- `DiscoverSchemaResponse`.

## 5B: Discover Tables

**File**: `ReportPlatform/Report.Infrastructure/Connections/SqlServerSchemaDiscoveryService.cs`

```sql
SELECT s.name AS [Schema], t.name AS [Table], 'BASE TABLE' AS TableType
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
ORDER BY s.name, t.name;
```

**Output**:

```text
TableRow { Schema, Table, TableType }
```

## 5C: Discover Columns

```sql
SELECT
  s.name AS [Schema],
  t.name AS [Table],
  c.name AS [Column],
  ty.name AS DataType,
  ty.name AS SqlDataType,
  c.column_id AS OrdinalPosition,
  c.is_nullable AS IsNullable,
  c.max_length AS CharacterMaximumLength,
  c.precision AS NumericPrecision,
  c.scale AS NumericScale,
  ... AS DatetimePrecision
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
ORDER BY s.name, t.name, c.column_id;
```

**Output**:

```text
ColumnRow {
  Schema,
  Table,
  Column,
  DataType,
  SqlDataType,
  OrdinalPosition,
  IsNullable,
  CharacterMaximumLength,
  NumericPrecision,
  NumericScale,
  DatetimePrecision
}
```

## 5D: Discover Primary Keys

The service queries `sys.key_constraints`, `sys.index_columns`, and `sys.columns` where key constraint type is `PK`.

**Output**:

```text
HashSet<KeyRow> primaryKeys
```

Each `KeyRow` is compared case-insensitively by schema/table/column.

## 5E: Discover Unique Columns

The service queries `sys.indexes` where `is_unique = 1`.

**Output**:

```text
HashSet<KeyRow> uniqueColumns
```

## 5F: Discover Foreign Key Relationships

The service queries `sys.foreign_keys` and `sys.foreign_key_columns` and returns:

```text
RelationshipDiscoveryDto {
  ForeignKeyName,
  FromSchema,
  FromTable,
  FromColumn,
  ToSchema,
  ToTable,
  ToColumn
}
```

This gives real database FK edges, for example:

```text
dbo.FactSales.CustomerKey → dbo.DimCustomer.CustomerKey
```

## 5G: Enrich Column DTOs

For every discovered column, the service sets:

```csharp
IsPrimaryKey = primaryKeys.Contains(...)
IsForeignKey = foreignKeys.Contains(...)
ParticipatesInRelationship = relationshipColumns.Contains(...)
IsUnique = uniqueColumns.Contains(...)
ReferencedSchema = relationship?.ToSchema ?? ""
ReferencedTable = relationship?.ToTable ?? ""
ReferencedColumn = relationship?.ToColumn ?? ""
ForeignKeyName = relationship?.ForeignKeyName ?? ""
```

**Final Discovery Output**:

```json
{
  "database": "AdventureWorksDW2025",
  "tables": [
    {
      "schema": "dbo",
      "table": "FactSales",
      "tableType": "BASE TABLE",
      "columns": [
        {
          "schema": "dbo",
          "table": "FactSales",
          "column": "SalesAmount",
          "dataType": "money",
          "sqlDataType": "money",
          "ordinalPosition": 12,
          "isNullable": false,
          "isPrimaryKey": false,
          "isForeignKey": false,
          "participatesInRelationship": false,
          "isUnique": false
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

# 👁️ PHASE 6: FRONTEND PREVIEWS TABLE DATA

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

When user selects a table in the navigator, frontend calls:

```ts
const response = await previewTable(connection, table.schema, table.table, 20)
setPreview(response)
```

**File**: `data-report-builder/lib/connections-api.ts`

```ts
export function previewTable(connection, schema, table, limit = 20) {
  return postJson<TablePreviewResponse>('/api/connections/preview-table', {
    connection,
    schema,
    table,
    limit,
  })
}
```

**Backend**:

`SqlServerSchemaDiscoveryService.PreviewTableAsync()` clamps limit to `1..100` and runs:

```sql
SELECT TOP ({safeLimit}) * FROM [schema].[table];
```

**Output**:

```ts
TablePreviewResponse {
  schema: string
  table: string
  columns: ColumnDto[]
  rows: Record<string, unknown>[]
}
```

This preview is only for the UI; it does not build the semantic model.

---

# 📥 PHASE 7: FRONTEND LOADS SELECTED TABLES

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

When user clicks **Load**, frontend gathers selected table keys:

```ts
const selected = discovery.tables
  .filter((table) => selectedTables.has(tableKey(table)))
  .map((table) => ({ schema: table.schema, table: table.table }))
```

Then calls:

```ts
const response = await registerDatasetFromTables(
  connection.database,
  connection,
  selected
)
```

**File**: `data-report-builder/lib/connections-api.ts`

```ts
export function registerDatasetFromTables(
  datasetName: string,
  connection: SqlServerConnectionRequest,
  selectedTables: { schema: string; table: string }[]
) {
  return postJson<RegisterDatasetResponse>('/api/datasets/register-from-tables', {
    datasetName,
    connection,
    selectedTables,
  })
}
```

**HTTP Request**:

```http
POST /api/datasets/register-from-tables
Content-Type: application/json

{
  "datasetName": "AdventureWorksDW2025",
  "connection": { ...SqlServerConnectionRequest },
  "selectedTables": [
    { "schema": "dbo", "table": "FactSales" },
    { "schema": "dbo", "table": "DimCustomer" },
    { "schema": "dbo", "table": "DimDate" }
  ]
}
```

---

# 🏗️ PHASE 8: BACKEND DATASET REGISTRATION ENTRY POINT

**File**: `ReportPlatform/Report.Api/Controllers/DatasetsController.cs`

```csharp
[HttpPost("register-from-tables")]
public async Task<IActionResult> RegisterFromTables(
    [FromBody] RegisterDatasetRequest request,
    CancellationToken ct)
{
    if (request.SelectedTables.Count == 0)
    {
        return BadRequest("At least one table must be selected.");
    }

    var connection = _connectionRegistry.Save(
        SqlServerConnectionFactory.ToDefinition(request.Connection));

    var discovered = await _discoveryService.DiscoverAsync(connection, ct);
    var model = _metadataGenerator.Generate(
        request.DatasetName,
        discovered,
        request.SelectedTables);

    var consistency = _metadataConsistencyValidator.Validate(
        discovered.Tables,
        request.SelectedTables,
        model);

    var dataset = _datasetRegistry.Save(
        request.DatasetName,
        connection,
        model);

    var metadata = await _metadataService.GetMetadataAsync(dataset.DatasetId, ct);

    return Ok(new RegisterDatasetResponse { ... });
}
```

**Input**:

- Dataset name.
- SQL Server connection request.
- Selected table list.

**Processing**:

1. Reject empty table selection.
2. Save connection into registry.
3. Re-discover schema from SQL Server.
4. Generate semantic model from selected tables.
5. Validate metadata consistency.
6. Save dataset/model into registry.
7. Reload metadata DTO for frontend.
8. Return dataset ID, connection ID, semantic metadata, warnings, debug fields.

**Output**:

```json
{
  "datasetId": "dataset_adventureworksdw2025_001",
  "connectionId": "conn_002",
  "metadata": { ...DatasetMetadataResponse },
  "warnings": [],
  "consistency": [ ... ],
  "debugFields": [ ... ]
}
```

---

# 🧠 PHASE 9: SEMANTIC MODEL GENERATOR - CORE ENGINE

**File**: `ReportPlatform/Report.Api/Services/SemanticMetadataGenerator.cs`

Main function:

```csharp
public SemanticModel Generate(
    string datasetName,
    DiscoverSchemaResponse discovered,
    IReadOnlyCollection<SelectedTableDto> selectedTables)
```

This function transforms raw SQL Server metadata into semantic reporting metadata.

---

## 9A: Build Selected Table ID Set

```csharp
var selected = selectedTables
    .Select(t => BuildTableId(t.Schema, t.Table))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
```

`BuildTableId()` rule:

```csharp
return schema.Equals("dbo", StringComparison.OrdinalIgnoreCase)
    ? table
    : $"{schema}.{table}";
```

Examples:

| SQL Table | Semantic TableId |
|---|---|
| `dbo.FactSales` | `FactSales` |
| `sales.Invoice` | `sales.Invoice` |

**Output**:

```text
selected = { "FactSales", "DimCustomer", "DimDate" }
```

---

## 9B: Filter Discovered Tables to User Selection

```csharp
var tables = discovered.Tables
    .Where(t => selected.Contains(BuildTableId(t.Schema, t.Table)))
    .ToList();
```

Only selected tables are included in the semantic model.

---

## 9C: Build SemanticTable Objects

```csharp
var semanticTables = tables.Select(table =>
{
    var tableType = InferTableType(table.Table);
    return new SemanticTable
    {
        TableId = BuildTableId(table.Schema, table.Table),
        DisplayName = SplitName(RemoveKnownPrefix(table.Table)),
        TableType = tableType,
        Grain = InferGrain(table.Table),
        PhysicalSchema = table.Schema,
        PhysicalTable = table.Table
    };
}).ToList();
```

### Table Type Inference

```csharp
if (table.StartsWith("Fact")) return "fact";
if (table.StartsWith("Dim")) return "dimension";
return "unknown";
```

### Display Name Inference

```text
FactSales → Sales
DimCustomer → Customer
DimProductCategory → Product Category
```

### Grain Inference

```text
FactSales → sales
DimCustomer → customer
```

### Output Example

```csharp
SemanticTable
{
  TableId = "FactSales",
  DisplayName = "Sales",
  TableType = "fact",
  Grain = "sales",
  PhysicalSchema = "dbo",
  PhysicalTable = "FactSales"
}
```

---

## 9D: Build SemanticField Objects for Every Selected Column

```csharp
var fields = tables
    .SelectMany(table =>
    {
        var tableType = InferTableType(table.Table);
        return table.Columns
            .OrderBy(column => column.OrdinalPosition)
            .Select(column => MapField(table, column, tableType, tables));
    })
    .ToList();
```

### MapField Output

```csharp
return new SemanticField
{
    FieldId = $"{NormalizeId(table.Table)}.{NormalizeId(column.Column)}",
    TableId = BuildTableId(table.Schema, table.Table),
    PhysicalSchema = table.Schema,
    PhysicalTable = BuildTableId(table.Schema, table.Table),
    PhysicalColumn = column.Column,
    OrdinalPosition = column.OrdinalPosition,
    IsNullable = column.IsNullable,
    IsPrimaryKey = column.IsPrimaryKey,
    IsForeignKey = column.IsForeignKey,
    ParticipatesInRelationship = column.ParticipatesInRelationship,
    IsUnique = column.IsUnique,
    ReferencedSchema = column.ReferencedSchema,
    ReferencedTable = column.ReferencedTable,
    ReferencedColumn = column.ReferencedColumn,
    ForeignKeyName = column.ForeignKeyName,
    DisplayName = SplitName(column.Column),
    DataType = column.DataType,
    SqlDataType = column.SqlDataType,
    Role = classification.Role,
    Grain = InferGrain(table.Table),
    SemanticType = classification.SemanticType,
    DefaultAggregation = classification.DefaultAggregation,
    Format = classification.Format,
    IsHidden = classification.IsHidden,
    IsDraggable = classification.IsDraggable,
    ClassificationReason = classification.Reason
};
```

### Field ID Rule

```text
NormalizeId(table.Table) + "." + NormalizeId(column.Column)
```

Examples:

| Physical Column | Semantic FieldId |
|---|---|
| `FactSales.SalesAmount` | `factsales.salesamount` |
| `DimCustomer.CustomerName` | `dimcustomer.customername` |
| `DimDate.Calendar Year` | `dimdate.calendar_year` |

---

# 🧭 PHASE 10: FIELD CLASSIFICATION RULES

**File**: `ReportPlatform/Report.Api/Services/SemanticMetadataGenerator.cs`

The generator calls:

```csharp
var classification = ClassifyField(table, column, tableType, selectedTables);
```

The classification decides:

```text
Role
SemanticType
DefaultAggregation
Format
IsHidden
IsDraggable
ClassificationReason
```

---

## 10A: Primary Key

```csharp
if (column.IsPrimaryKey)
{
    return new FieldClassification(
        Role: "key",
        SemanticType: "identifier",
        DefaultAggregation: "none",
        Format: "general",
        IsHidden: false,
        IsDraggable: true,
        Reason: "SQL Server primary key");
}
```

Example:

```text
DimCustomer.CustomerKey → role = key
```

---

## 10B: Foreign Key

```csharp
if (column.IsForeignKey)
{
    return new FieldClassification(
        Role: "key",
        SemanticType: "identifier",
        DefaultAggregation: "none",
        Format: "general",
        Reason: "SQL Server foreign key");
}
```

Example:

```text
FactSales.CustomerKey → role = key
```

---

## 10C: Relationship Participant

```csharp
if (column.ParticipatesInRelationship)
{
    return new FieldClassification(
        Role: "key",
        SemanticType: "identifier",
        DefaultAggregation: "none",
        Format: "general",
        Reason: "Participates in discovered relationship");
}
```

This catches columns that are involved in discovered FK relationships even if they are not directly marked as a foreign key in the current column row.

---

## 10D: Inferred Fact Foreign Key Candidate

```csharp
if (IsInferredFactForeignKeyCandidate(table, column, tableType, selectedTables))
{
    return new FieldClassification(
        Role: "key",
        SemanticType: "identifier",
        Reason: "Inferred fact-to-dimension key candidate");
}
```

Rule:

- Table must be fact table.
- Column name must end with `Key`.
- A selected dimension table must have a primary key with matching or suffix-matching key name.

Example:

```text
FactSales.ProductKey + DimProduct.ProductKey → ProductKey treated as key
```

---

## 10E: Date Dimension / Date Part

```csharp
if (IsDateDimension(table.Table, tableType, column.Column))
{
    return new FieldClassification(
        Role: "dimension",
        SemanticType: InferDateDimensionSemanticType(...),
        DefaultAggregation: "none",
        Format: InferFormat(...),
        Reason: "Date dimension/date-part field");
}
```

Examples:

```text
DimDate.CalendarYear → dimension / number
DimDate.FullDateAlternateKey → dimension / date
```

---

## 10F: Business Identifier

```csharp
if (IsBusinessIdentifier(column.Column))
{
    return new FieldClassification(
        Role: "dimension",
        SemanticType: "identifier",
        DefaultAggregation: "none",
        Format: "general",
        Reason: "Business identifier: ID/Key/Code/Number pattern but not PK/FK");
}
```

Business identifier patterns:

```text
EndsWith ID
EndsWith Key
EndsWith Code
Contains Code
Contains Number
```

Examples:

```text
OrderNumber → dimension / identifier
ProductCode → dimension / identifier
```

---

## 10G: Dimension Table Non-Key Field

```csharp
if (tableType.Equals("dimension"))
{
    return new FieldClassification(
        Role: "dimension",
        SemanticType: InferSemanticType(...),
        DefaultAggregation: "none",
        Format: InferFormat(...),
        Reason: "Dimension table non-key field");
}
```

Examples:

```text
DimCustomer.CustomerName → dimension / category
DimProduct.Color → dimension / category
```

---

## 10H: Fact Numeric Non-Key Measure Candidate

```csharp
if (tableType.Equals("fact") &&
    IsNumeric(column.DataType) &&
    !IsDatePartOrIdentifier(column.Column))
{
    return new FieldClassification(
        Role: "measure_candidate",
        SemanticType: InferSemanticType(...),
        DefaultAggregation: InferDefaultAggregation(column.Column),
        Format: InferFormat(...),
        Reason: "Fact numeric non-key measure candidate");
}
```

Numeric SQL data types:

```text
tinyint, smallint, int, bigint, decimal, numeric, float, real, money, smallmoney
```

Default aggregation rule:

```csharp
Discount / Rate / Percent / Percentage → AVG
Otherwise → SUM
```

Examples:

| Column | Role | DefaultAggregation | Format |
|---|---|---|---|
| `SalesAmount` | `measure_candidate` | `SUM` | `currency` |
| `TotalProductCost` | `measure_candidate` | `SUM` | `decimal` or `currency` depending name |
| `DiscountRate` | `measure_candidate` | `AVG` | `percentage` |

---

## 10I: Fallback Dimension

If no other rule matches:

```csharp
return new FieldClassification(
    Role: "dimension",
    SemanticType: InferSemanticType(...),
    DefaultAggregation: "none",
    Format: InferFormat(...),
    Reason: "Fallback dimension field");
```

---

# 🔗 PHASE 11: RELATIONSHIP GENERATION

**File**: `ReportPlatform/Report.Api/Services/SemanticMetadataGenerator.cs`

Relationships are generated in two passes:

```csharp
var relationships = discovered.Relationships
    .Where(r => selected.Contains(BuildTableId(r.FromSchema, r.FromTable)) &&
        selected.Contains(BuildTableId(r.ToSchema, r.ToTable)))
    .Select(r => new SemanticRelationship { ... Source = "database_fk", Confidence = 1.0m })
    .Concat(InferRelationships(tables, discovered.Relationships))
    .GroupBy(r => $"{r.FromTableId}|{r.FromColumn}|{r.ToTableId}|{r.ToColumn}")
    .Select(group => group.First())
    .ToList();
```

## 11A: Database FK Relationships

If SQL Server has FK metadata and both sides are selected:

```csharp
SemanticRelationship
{
    RelationshipId = "rel_{guid}",
    FromTableId = BuildTableId(r.FromSchema, r.FromTable),
    FromColumn = r.FromColumn,
    ToTableId = BuildTableId(r.ToSchema, r.ToTable),
    ToColumn = r.ToColumn,
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

## 11B: Inferred Relationships

If discovered FK list is empty, the generator tries convention-based inference:

```csharp
var factTables = tables.Where(t => t.Table.StartsWith("Fact"));
var dimTables = tables.Where(t => t.Table.StartsWith("Dim"));
```

For every fact/dimension pair:

1. Find a dimension key ending with `Key`.
2. Find a fact key with same name or suffix.
3. Create relationship with `Source = "inferred"`, `Confidence = 0.85`, and warning.

Example:

```text
FactSales.CustomerKey → DimCustomer.CustomerKey
```

Output:

```csharp
SemanticRelationship
{
  FromTableId = "FactSales",
  FromColumn = "CustomerKey",
  ToTableId = "DimCustomer",
  ToColumn = "CustomerKey",
  Source = "inferred",
  Confidence = 0.85m,
  Warning = "Inferred relationship. Please verify before production use."
}
```

---

# 📊 PHASE 12: AUTO-GENERATE SEMANTIC METRICS

**File**: `ReportPlatform/Report.Api/Services/SemanticMetadataGenerator.cs`

After fields are classified, the generator creates metrics from fact table measure candidates:

```csharp
var factTableIds = semanticTables
    .Where(t => t.TableType == "fact")
    .Select(t => t.TableId)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var metrics = fields
    .Where(field => factTableIds.Contains(field.TableId) && field.Role == "measure_candidate")
    .SelectMany(field => SupportedAggregations()
        .Where(aggregation => AggregationAllowedForDataType(aggregation, field.DataType))
        .Select(aggregation => new SemanticMetric { ... }))
    .ToList();
```

Supported aggregations:

```text
SUM, AVG, MIN, MAX, COUNT, COUNT_DISTINCT
```

Aggregation allowed rules:

| Aggregation | Allowed When |
|---|---|
| `COUNT` | Any data type |
| `COUNT_DISTINCT` | Any data type |
| `SUM` | Numeric only |
| `AVG` | Numeric only |
| `MIN` | Any data type |
| `MAX` | Any data type |

Metric ID rule:

```csharp
MetricId = $"metric.{aggregation.ToLowerInvariant()}_{NormalizeId(field.TableId)}_{NormalizeId(field.PhysicalColumn)}"
```

Formula rule:

```csharp
Formula = $"{aggregation}([{field.FieldId}])"
```

Aggregation behavior:

```text
AVG and COUNT_DISTINCT → non_additive
Everything else → additive
```

Data type:

```text
COUNT / COUNT_DISTINCT → integer
Otherwise → source field data type
```

Format:

```text
COUNT / COUNT_DISTINCT → integer
Otherwise → InferFormat(field.PhysicalColumn, field.DataType)
```

### Example Input Field

```csharp
SemanticField
{
  FieldId = "factsales.salesamount",
  TableId = "FactSales",
  PhysicalColumn = "SalesAmount",
  Role = "measure_candidate",
  DataType = "money",
  Format = "currency"
}
```

### Example Generated Metrics

```csharp
SemanticMetric
{
  MetricId = "metric.sum_factsales_salesamount",
  DisplayName = "Total Sales Amount",
  Formula = "SUM([factsales.salesamount])",
  BaseTableId = "FactSales",
  AggregationBehavior = "additive",
  DataType = "money",
  Format = "currency",
  IsHidden = false,
  IsDraggable = true
}
```

```csharp
SemanticMetric
{
  MetricId = "metric.avg_factsales_salesamount",
  DisplayName = "Average Sales Amount",
  Formula = "AVG([factsales.salesamount])",
  BaseTableId = "FactSales",
  AggregationBehavior = "non_additive",
  DataType = "money",
  Format = "currency"
}
```

---

# 🧾 PHASE 13: SEMANTIC MODEL OUTPUT SHAPE

The generator returns:

```csharp
return new SemanticModel
{
    DisplayName = string.IsNullOrWhiteSpace(datasetName)
        ? discovered.Database
        : datasetName,
    Tables = semanticTables,
    Fields = fields,
    Relationships = relationships,
    Metrics = metrics
};
```

At this point the model has no `DatasetId` or `ConnectionId` yet. Those are added by the dataset registry when the model is saved.

### Complete SemanticModel Example

```csharp
SemanticModel
{
  DatasetId = "dataset_adventureworksdw2025_001",
  DisplayName = "AdventureWorksDW2025",
  ConnectionId = "conn_002",
  Tables = [
    SemanticTable { TableId = "FactSales", TableType = "fact", Grain = "sales" },
    SemanticTable { TableId = "DimCustomer", TableType = "dimension", Grain = "customer" }
  ],
  Fields = [
    SemanticField { FieldId = "factsales.salesamount", Role = "measure_candidate" },
    SemanticField { FieldId = "dimcustomer.customername", Role = "dimension" }
  ],
  Metrics = [
    SemanticMetric { MetricId = "metric.sum_factsales_salesamount", Formula = "SUM([factsales.salesamount])" }
  ],
  Relationships = [
    SemanticRelationship { FromTableId = "FactSales", ToTableId = "DimCustomer" }
  ]
}
```

---

# 🧪 PHASE 14: METADATA CONSISTENCY VALIDATION

**File**: `ReportPlatform/Report.Api/Services/MetadataConsistencyValidator.cs`

The controller calls:

```csharp
var consistency = _metadataConsistencyValidator.Validate(
    discovered.Tables,
    request.SelectedTables,
    model);
```

The validator checks whether each selected physical table has all discovered columns represented in the generated semantic fields.

**Input**:

- All discovered physical tables.
- User selected tables.
- Generated semantic model.

**Processing**:

- For each selected table, count discovered physical columns.
- Count generated fields for the semantic table ID.
- Find missing columns.
- Emit warnings when fields are missing.

**Output**:

```json
{
  "consistency": [
    {
      "tableId": "FactSales",
      "physicalColumnCount": 12,
      "registeredFieldCount": 12,
      "missingColumns": []
    }
  ],
  "warnings": []
}
```

---

# 💾 PHASE 15: DATASET REGISTRY SAVE

**File**: `ReportPlatform/Report.Metadata/Stores/InMemoryDatasetRegistry.cs`

The controller saves the model:

```csharp
var dataset = _datasetRegistry.Save(request.DatasetName, connection, model);
```

The registry creates a dataset ID:

```csharp
var normalizedName = NormalizeDatasetName(datasetName);
var datasetId = $"dataset_{normalizedName}_{Interlocked.Increment(ref _nextId):000}";
```

Example:

```text
AdventureWorksDW2025 → dataset_adventureworksdw2025_001
```

Then it copies the model and injects:

```csharp
DatasetId = datasetId
DisplayName = datasetName
ConnectionId = connection.ConnectionId
```

It also sets each field's `DatasetId`, each metric's `DatasetId`, and each relationship's `DatasetId`.

**Important**:

This registry is also in-memory. Restarting backend loses registered datasets unless another persistence layer is implemented.

---

# 📤 PHASE 16: DATASET METADATA RESPONSE FOR FRONTEND

**File**: `ReportPlatform/Report.Api/Services/DatasetMetadataService.cs`

After saving, the controller calls:

```csharp
var metadata = await _metadataService.GetMetadataAsync(dataset.DatasetId, ct);
```

The service maps internal semantic model to API DTO:

```csharp
DatasetMetadataResponse
{
  DatasetId = model.DatasetId,
  DisplayName = model.DisplayName,
  ConnectionId = model.ConnectionId,
  Tables = BuildTables(model, visibleFields),
  Metrics = model.Metrics.Select(MapMetric).ToList(),
  Relationships = model.Relationships.Select(MapRelationship).ToList()
}
```

Hidden fields are excluded:

```csharp
var visibleFields = model.Fields
    .Where(field => !IsHidden(field))
    .ToList();
```

### Metadata Table DTO

```json
{
  "tableId": "FactSales",
  "displayName": "Sales",
  "tableType": "fact",
  "grain": "sales",
  "fields": [ ... ]
}
```

### Metadata Field DTO

```json
{
  "fieldId": "factsales.salesamount",
  "displayName": "Sales Amount",
  "tableId": "FactSales",
  "physicalSchema": "dbo",
  "physicalTable": "FactSales",
  "physicalColumn": "SalesAmount",
  "dataType": "money",
  "sqlDataType": "money",
  "role": "measure_candidate",
  "semanticType": "currency",
  "defaultAggregation": "SUM",
  "format": "currency",
  "isHidden": false,
  "isDraggable": true,
  "classificationReason": "Fact numeric non-key measure candidate"
}
```

### Metadata Metric DTO

```json
{
  "metricId": "metric.sum_factsales_salesamount",
  "displayName": "Total Sales Amount",
  "baseTableId": "FactSales",
  "formula": "SUM([factsales.salesamount])",
  "aggregationBehavior": "additive",
  "dataType": "money",
  "format": "currency",
  "isHidden": false,
  "isDraggable": true
}
```

---

# 🧑‍💻 PHASE 17: FRONTEND RECEIVES LOADED DATASET

**File**: `data-report-builder/components/report-builder/connect-source-modals.tsx`

After registration succeeds:

```ts
onDatasetLoaded({
  datasetId: response.datasetId,
  connectionId: response.connectionId,
  displayName: response.metadata.displayName,
  connection,
  selectedTables: selected,
  metadata: response.metadata,
})
```

**File**: `data-report-builder/app/page.tsx`

The main page handles it:

```ts
setDatasetId(dataset.datasetId)
setConnectionId(dataset.connectionId)
setSourceConnection(dataset.connection)
setMetadata(dataset.metadata)
setConnectedSource(dataset.displayName)
setSelectedSourceTables(dataset.selectedTables)
setPersistedConnection(sanitizeConnection(dataset.connection))
writePersistedConnectedSource({ ... })
setSelectedFields([])
```

**Frontend state now has**:

```text
datasetId
connectionId
metadata.tables[]
metadata.tables[].fields[]
metadata.metrics[]
metadata.relationships[]
selectedSourceTables[]
```

This is what powers the schema panel and report builder.

---

# 🧭 PHASE 18: HOW SEMANTIC METADATA IS USED LATER BY QUERY ENGINE

Once metadata is loaded, the frontend uses semantic IDs instead of physical SQL:

```text
FieldId: factsales.salesamount
MetricId: metric.sum_factsales_salesamount
TableId: FactSales
Relationship: FactSales.CustomerKey → DimCustomer.CustomerKey
```

When the user runs a report:

```json
{
  "connectionId": "conn_002",
  "datasetId": "dataset_adventureworksdw2025_001",
  "rows": ["dimcustomer.customername"],
  "values": ["metric.sum_factsales_salesamount"],
  "filters": [],
  "sort": [],
  "limit": 100
}
```

Backend query engine then uses the saved semantic model to:

1. Resolve `dimcustomer.customername` to physical table/column.
2. Resolve `metric.sum_factsales_salesamount` to formula `SUM([factsales.salesamount])`.
3. Use relationships to join `FactSales` to `DimCustomer`.
4. Compile safe SQL with aliases, `GROUP BY`, `ORDER BY`, filters, and parameters.

---

# 🔄 COMPLETE FLOW SUMMARY

```text
┌───────────────────────────────────────────────┐
│ 1. User enters SQL Server connection          │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 2. Test connection                            │
│    Open SqlConnection                         │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 3. Save connection in registry                │
│    conn_001 / conn_002                        │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 4. Discover schema                            │
│    tables, columns, PK, unique, FK            │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 5. Frontend table navigator                   │
│    search/filter/select/preview               │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 6. User selects tables and clicks Load        │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 7. Register dataset endpoint                  │
│    Rediscover schema                          │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 8. SemanticMetadataGenerator.Generate()       │
│    Build tables, fields, relationships, metrics│
└───────────────────────────────────────────────┘
                    ↓
        ┌───────────┼───────────┬────────────┐
        ↓           ↓           ↓            ↓
   SemanticTable SemanticField Relationship SemanticMetric
        ↓           ↓           ↓            ↓
        └───────────┴───────────┴────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 9. Consistency validation                     │
│    Missing fields/warnings/debug fields       │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 10. Save dataset/model in registry            │
│     dataset_adventureworksdw2025_001          │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 11. Map to DatasetMetadataResponse            │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 12. Frontend stores metadata                  │
│     Schema panel becomes usable               │
└───────────────────────────────────────────────┘
                    ↓
┌───────────────────────────────────────────────┐
│ 13. Report query uses semantic IDs            │
│     Engine compiles physical SQL              │
└───────────────────────────────────────────────┘
```

---

# 📋 FILE MAPPING TABLE

| Phase | File | Class / Function | Input | Processing | Output |
|---|---|---|---|---|---|
| 1 | `connect-source-modals.tsx` | `ConnectSourceFlow` | User connection form | Stores connection state | `SqlServerConnectionRequest` |
| 2 | `connect-source-modals.tsx` | `handleTestConnection()` | Connection state | Calls `testConnection()` | Test state |
| 3 | `connections-api.ts` | `testConnection()` | Connection request | POST `/api/connections/test` | `ConnectionTestResponse` |
| 4 | `ConnectionsController.cs` | `Test()` | Test connection request | Converts, opens SQL connection, saves registry | Public connection DTO |
| 5 | `SqlServerSchemaDiscoveryService.cs` | `TestConnectionAsync()` | `ConnectionDefinition` | Opens `SqlConnection` | success/error |
| 6 | `InMemoryConnectionRegistry.cs` | `Save()` | `ConnectionDefinition` | Assigns `conn_###` | Saved connection |
| 7 | `connect-source-modals.tsx` | `handleNext()` | Connection state | Calls `discoverSchema()` | Navigator state |
| 8 | `connections-api.ts` | `discoverSchema()` | Connection request | POST `/api/connections/discover` | `DiscoverSchemaResponse` |
| 9 | `ConnectionsController.cs` | `Discover()` | Connection request | Calls discovery service | Schema response |
| 10 | `SqlServerSchemaDiscoveryService.cs` | `DiscoverAsync()` | Connection definition | Queries SQL Server catalog views | Tables, columns, relationships |
| 11 | `connect-source-modals.tsx` | `loadPreview()` | Active table | Calls `previewTable()` | Preview rows |
| 12 | `SqlServerSchemaDiscoveryService.cs` | `PreviewTableAsync()` | schema/table/limit | Runs `SELECT TOP` | `TablePreviewResponse` |
| 13 | `connect-source-modals.tsx` | `handleLoad()` | Selected tables | Calls register API | Loaded dataset callback |
| 14 | `connections-api.ts` | `registerDatasetFromTables()` | Dataset name, connection, tables | POST `/api/datasets/register-from-tables` | `RegisterDatasetResponse` |
| 15 | `DatasetsController.cs` | `RegisterFromTables()` | Register request | Save connection, discover, generate, validate, save | Dataset + metadata |
| 16 | `SemanticMetadataGenerator.cs` | `Generate()` | Discovered schema + selected tables | Build semantic model | `SemanticModel` |
| 17 | `SemanticMetadataGenerator.cs` | `MapField()` | Table/column/classification | Build semantic field | `SemanticField` |
| 18 | `SemanticMetadataGenerator.cs` | `ClassifyField()` | Column + table context | Assign semantic role/type/format | `FieldClassification` |
| 19 | `SemanticMetadataGenerator.cs` | `InferRelationships()` | Tables + discovered FKs | Convention-based relationship inference | `SemanticRelationship` |
| 20 | `MetadataConsistencyValidator.cs` | `Validate()` | Discovered + selected + model | Check missing registered columns | Consistency/warnings |
| 21 | `InMemoryDatasetRegistry.cs` | `Save()` | Dataset name, connection, model | Assign dataset ID and inject IDs | `RegisteredDataset` |
| 22 | `DatasetMetadataService.cs` | `GetMetadataAsync()` | Dataset ID | Map internal model to API DTO | `DatasetMetadataResponse` |
| 23 | `app/page.tsx` | `handleDatasetLoaded()` | Loaded dataset | Store state/localStorage | Active semantic metadata |

---

# 🎯 KEY DECISION POINTS

| Decision | Where | Rule | Result |
|---|---|---|---|
| Table ID | `BuildTableId()` | `dbo` schema drops prefix; other schemas keep prefix | `dbo.FactSales → FactSales`, `sales.Invoice → sales.Invoice` |
| Table type | `InferTableType()` | Name starts `Fact`/`Dim` | `fact`, `dimension`, or `unknown` |
| Table display | `RemoveKnownPrefix()` + `SplitName()` | Remove `Fact`/`Dim`, split PascalCase | `FactSales → Sales` |
| Primary key field | `ClassifyField()` | `IsPrimaryKey` | role `key` |
| Foreign key field | `ClassifyField()` | `IsForeignKey` | role `key` |
| Fact numeric measure | `ClassifyField()` | Fact + numeric + not date/id | role `measure_candidate` |
| Dimension field | `ClassifyField()` | Dimension table non-key | role `dimension` |
| Default aggregation | `InferDefaultAggregation()` | discount/rate/percent → AVG, else SUM | `AVG` or `SUM` |
| Semantic type | `InferSemanticType()` | name/type heuristics | currency, percentage, date, identifier, number, category |
| Format | `InferFormat()` | name/type heuristics | currency, percentage, decimal, general |
| Relationship source | generator relationship block | Real FK exists | `Source = database_fk`, `Confidence = 1.0` |
| Inferred relationship | `InferRelationships()` | No discovered relationships and Fact/Dim keys match | `Source = inferred`, `Confidence = 0.85` |
| Metric generation | metrics block | Fact `measure_candidate` fields | Auto `SemanticMetric` objects |
| Dataset persistence | `InMemoryDatasetRegistry.Save()` | New registration | `dataset_{name}_{###}` |

---

# 🧪 INPUT / PROCESS / OUTPUT EXAMPLE

## Example: AdventureWorks FactSales + DimCustomer + DimDate

### User Input

```json
{
  "datasetName": "AdventureWorksDW2025",
  "selectedTables": [
    { "schema": "dbo", "table": "FactSales" },
    { "schema": "dbo", "table": "DimCustomer" },
    { "schema": "dbo", "table": "DimDate" }
  ]
}
```

### Discovery Finds

```text
Tables:
  dbo.FactSales
  dbo.DimCustomer
  dbo.DimDate

Columns:
  FactSales.SalesKey PK
  FactSales.CustomerKey FK → DimCustomer.CustomerKey
  FactSales.OrderDateKey FK → DimDate.DateKey
  FactSales.SalesAmount money
  DimCustomer.CustomerName nvarchar
  DimDate.CalendarYear smallint

Relationships:
  FactSales.CustomerKey → DimCustomer.CustomerKey
  FactSales.OrderDateKey → DimDate.DateKey
```

### Semantic Tables

```json
[
  {
    "tableId": "FactSales",
    "displayName": "Sales",
    "tableType": "fact",
    "grain": "sales"
  },
  {
    "tableId": "DimCustomer",
    "displayName": "Customer",
    "tableType": "dimension",
    "grain": "customer"
  },
  {
    "tableId": "DimDate",
    "displayName": "Date",
    "tableType": "dimension",
    "grain": "date"
  }
]
```

### Semantic Fields

```json
[
  {
    "fieldId": "factsales.saleskey",
    "tableId": "FactSales",
    "physicalColumn": "SalesKey",
    "role": "key",
    "semanticType": "identifier",
    "defaultAggregation": "none",
    "classificationReason": "SQL Server primary key"
  },
  {
    "fieldId": "factsales.customerkey",
    "tableId": "FactSales",
    "physicalColumn": "CustomerKey",
    "role": "key",
    "semanticType": "identifier",
    "classificationReason": "SQL Server foreign key"
  },
  {
    "fieldId": "factsales.salesamount",
    "tableId": "FactSales",
    "physicalColumn": "SalesAmount",
    "role": "measure_candidate",
    "semanticType": "currency",
    "defaultAggregation": "SUM",
    "format": "currency",
    "classificationReason": "Fact numeric non-key measure candidate"
  },
  {
    "fieldId": "dimcustomer.customername",
    "tableId": "DimCustomer",
    "physicalColumn": "CustomerName",
    "role": "dimension",
    "semanticType": "category",
    "classificationReason": "Dimension table non-key field"
  }
]
```

### Semantic Relationships

```json
[
  {
    "fromTableId": "FactSales",
    "fromColumn": "CustomerKey",
    "toTableId": "DimCustomer",
    "toColumn": "CustomerKey",
    "joinType": "INNER",
    "cardinality": "N:1",
    "crossFilterDirection": "single",
    "isActive": true,
    "source": "database_fk",
    "confidence": 1.0
  }
]
```

### Auto Metrics

```json
[
  {
    "metricId": "metric.sum_factsales_salesamount",
    "displayName": "Total Sales Amount",
    "formula": "SUM([factsales.salesamount])",
    "baseTableId": "FactSales",
    "aggregationBehavior": "additive",
    "dataType": "money",
    "format": "currency"
  },
  {
    "metricId": "metric.avg_factsales_salesamount",
    "displayName": "Average Sales Amount",
    "formula": "AVG([factsales.salesamount])",
    "baseTableId": "FactSales",
    "aggregationBehavior": "non_additive",
    "dataType": "money",
    "format": "currency"
  }
]
```

### Final Frontend Metadata Shape

```json
{
  "datasetId": "dataset_adventureworksdw2025_001",
  "displayName": "AdventureWorksDW2025",
  "connectionId": "conn_002",
  "tables": [ ...semantic tables with fields... ],
  "metrics": [ ...auto generated metrics... ],
  "relationships": [ ...semantic relationships... ]
}
```

---

# ⚠️ IMPORTANT NOTES / CURRENT LIMITATIONS

1. **The backend rediscovers schema during registration**. The frontend discovery response is used for UI navigation, but registration calls SQL Server discovery again before semantic model generation.
2. **Connections and datasets are in-memory** in the current implementation. Backend restart loses saved connections/datasets.
3. **Table type inference is naming-based**. `Fact*` becomes `fact`; `Dim*` becomes `dimension`; anything else is `unknown`.
4. **Metric auto-generation only uses fact-table `measure_candidate` fields**.
5. **Keys are draggable** in the current classification output (`IsDraggable = true`), even though role is `key`.
6. **Database FK relationships are preferred**. Convention-based relationship inference only runs when no discovered relationships exist.
7. **Fact numeric IDs/date parts are not measure candidates** because date/id columns are excluded from fact measure inference.
8. **The returned debug fields are useful for troubleshooting** because each field includes role, semantic type, key flags, draggability, and classification reason.
9. **`dbo` is special**. It is removed from semantic `TableId`, while non-`dbo` schemas remain part of `TableId`.
10. **Semantic IDs are the contract for query execution**. The frontend sends `fieldId`/`metricId`; backend resolves them to physical SQL later.
