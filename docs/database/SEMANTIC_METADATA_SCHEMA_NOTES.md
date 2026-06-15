# Planned Semantic Metadata Persistence Schema

This document records the SQL Server semantic metadata schema supplied for the next persistence phase. The current application still runs semantic metadata through in-memory stores/registries; do not treat this as implemented runtime behavior until repository/store implementations are added and wired through DI.

## Scope

Target database engine: SQL Server.

Persistence scope:

- Semantic source connections, without plain-text passwords.
- Registered datasets and semantic model versions.
- Selected physical tables from dataset registration.
- Semantic tables.
- Semantic fields, including derived/calculated columns.
- Semantic metrics/calculated measures.
- Semantic objects and dependencies.
- Semantic relationships and relationship conflict metadata.
- Saved semantic report definitions.
- Semantic change/audit log.
- Hydration views for API-style metadata responses.
- Timestamp update triggers.

Explicitly out of scope for this schema:

- Report execution runtime tables.
- Artifact storage tables.
- Power BI integration configuration.

## Table mapping to current code concepts

| SQL object | Current code concept | Notes |
| --- | --- | --- |
| `dbo.SemanticConnections` | `ConnectionDefinition`, `IConnectionRegistry` | Stores provider/server/database/auth metadata and `SecretReference` instead of a plain-text password. |
| `dbo.SemanticDatasets` | `RegisteredDataset`, `SemanticModel` dataset identity | Owns dataset display metadata, connection reference, active flag, and current semantic model version. |
| `dbo.SemanticModelVersions` | `SemanticModel.Version` / future version store | Supports draft/published/archived versions plus optional JSON snapshot/checksum. |
| `dbo.SemanticDatasetSelectedTables` | `RegisterDatasetRequest.SelectedTables` | Stores physical tables selected during `register-from-tables`. |
| `dbo.SemanticTables` | `SemanticTable` | Stores semantic table identity, type, grain, physical schema/table, active flag. |
| `dbo.SemanticFields` | `SemanticField` | Stores physical metadata, semantic role/type, aggregation/format, key/FK hints, visibility/draggability, derived expression, and base table. |
| `dbo.SemanticMetrics` | `SemanticMetric` | Stores metric formula, base table, aggregation behavior, type/format, visibility/draggability. |
| `dbo.SemanticObjects` | `SemanticObject` | Records expression-level metadata for physical columns, calculated columns, calculated measures, metrics, and expression fragments. |
| `dbo.SemanticObjectDependencies` | `ExpressionDependencyGraphService` output / future dependency persistence | Stores dependencies by id and kind (`field`, `metric`, `object`, `function`). |
| `dbo.SemanticRelationships` | `SemanticRelationship` | Stores endpoints, join type, cardinality, cross-filter direction, active/primary flags, source/confidence/status/warning, and computed relationship group key/hash. |
| `dbo.SemanticReportDefinitions` | `ReportDefinition`, `IReportRegistry` | Stores report definition JSON arrays for rows/columns/values/filters/sort and layout JSON. |
| `dbo.SemanticChangeLog` | future mutation/audit persistence | Supports field, metric, derived field, relationship, report definition, and model version mutation auditing. |

## Views and API hydration

The proposed schema includes views intended to hydrate current API response shapes:

- `dbo.vw_SemanticDatasetMetadata`: dataset summary with table/field/metric/relationship counts.
- `dbo.vw_SemanticFieldsForBuilder`: visible/draggable fields joined to table display names for builder UX.
- `dbo.vw_SemanticRelationshipsWithGroupStats`: relationships plus conflict/active counts per endpoint group.

## Index and constraint intent

- Primary keys are dataset-scoped for semantic tables, fields, metrics, objects, and relationships.
- Foreign keys preserve dataset ownership across semantic metadata objects.
- Check constraints encode currently expected enums such as table type, field role, aggregation behavior, relationship cardinality, cross-filter direction, source, and status.
- `UX_SemanticRelationships_OneActivePrimaryPerGroup` enforces only one active primary relationship per endpoint group.
- Indexes optimize dataset-based metadata hydration, field lookup by table/role/physical column, metric lookup by base table, relationship traversal from/to endpoints, report listing by dataset, and audit-log lookup by dataset/time.
- Timestamp triggers maintain `UpdatedAtUtc` on mutable metadata tables.

## Required implementation work before use

1. Add a SQL migration or schema bootstrap script for these objects.
2. Add durable implementations for `IConnectionRegistry`, `IDatasetRegistry`, `ISemanticModelStore`, and `IReportRegistry` backed by this schema.
3. Add a secret-provider abstraction so `SemanticConnections.SecretReference` can resolve credentials without storing plain-text passwords.
4. Map `SemanticModel` load/save to normalized tables and optionally `SemanticModelVersions.SnapshotJson` for version snapshots.
5. Update DI in `Report.Api/Program.cs` to select in-memory vs SQL-backed semantic metadata stores from configuration.
6. Add integration tests that round-trip a discovered dataset, semantic mutations, relationships, saved reports, and query execution against SQL-backed metadata.

## Current-state reminder

As of this note, the engine uses in-memory semantic metadata stores. The DDL is a planned persistence contract and should not be described to users as active behavior until the SQL-backed store implementations are committed.
