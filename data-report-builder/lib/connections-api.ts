import { type DatasetMetadataResponse } from './report-metadata-api'

export type SqlServerConnectionRequest = {
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

export type ConnectionDto = {
  connectionId: string
  provider: string
  server: string
  database: string
  authenticationType: string
  trustServerCertificate: boolean
  encrypt: boolean
  commandTimeoutSeconds: number
}

export type ConnectionTestResponse = {
  success: boolean
  message: string
  connection?: ConnectionDto
}

export type ColumnDto = {
  schema: string
  table: string
  column: string
  dataType: string
  sqlDataType: string
  characterMaximumLength?: number | null
  numericPrecision?: number | null
  numericScale?: number | null
  datetimePrecision?: number | null
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

export type TableDto = {
  schema: string
  table: string
  tableType: string
  columns: ColumnDto[]
}

export type RelationshipDiscovery = {
  fromSchema: string
  fromTable: string
  fromColumn: string
  toSchema: string
  toTable: string
  toColumn: string
}

export type DiscoverSchemaResponse = {
  database: string
  tables: TableDto[]
  relationships: RelationshipDiscovery[]
}

export type TablePreviewResponse = {
  schema: string
  table: string
  columns: ColumnDto[]
  rows: Record<string, unknown>[]
}

export type RegisterDatasetResponse = {
  datasetId: string
  connectionId: string
  metadata: DatasetMetadataResponse
  warnings: string[]
  consistency: {
    tableId: string
    physicalColumnCount: number
    registeredFieldCount: number
    missingColumns: string[]
  }[]
  debugFields: {
    fieldId: string
    physicalColumn: string
    sqlDataType: string
    role: string
    semanticType: string
    isPrimaryKey: boolean
    isForeignKey: boolean
    participatesInRelationship: boolean
    isDraggable: boolean
    classificationReason: string
  }[]
}

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5224'

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (!res.ok) {
    throw new Error(await res.text())
  }

  return res.json()
}

export function testConnection(connection: SqlServerConnectionRequest) {
  return postJson<ConnectionTestResponse>('/api/connections/test', connection)
}

export function discoverSchema(connection: SqlServerConnectionRequest) {
  return postJson<DiscoverSchemaResponse>('/api/connections/discover', connection)
}

export function previewTable(
  connection: SqlServerConnectionRequest,
  schema: string,
  table: string,
  limit = 20
) {
  return postJson<TablePreviewResponse>('/api/connections/preview-table', {
    connection,
    schema,
    table,
    limit,
  })
}

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
