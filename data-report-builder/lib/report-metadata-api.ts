export type MetadataField = {
  fieldId: string
  displayName: string
  tableId: string
  physicalSchema: string
  physicalTable: string
  physicalColumn: string
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
  dataType: string
  sqlDataType: string
  characterMaximumLength?: number | null
  numericPrecision?: number | null
  numericScale?: number | null
  datetimePrecision?: number | null
  role: string
  grain: string
  semanticType: string
  defaultAggregation: string
  format: string
  expression?: string | null
  baseTableId?: string | null
  isDerived: boolean
  isHidden: boolean
  isDraggable: boolean
  classificationReason: string
}

export type MetadataTable = {
  tableId: string
  displayName: string
  tableType: string
  grain: string
  fields: MetadataField[]
}

export type MetadataMetric = {
  metricId: string
  displayName: string
  baseTableId: string
  formula: string
  aggregationBehavior: string
  dataType: string
  format: string
  isHidden: boolean
  isDraggable: boolean
}

export type MetadataRelationship = {
  relationshipId: string
  datasetId: string
  fromTableId: string
  fromColumn: string
  toTableId: string
  toColumn: string
  joinType: string
  cardinality: string
  crossFilterDirection: string
  isActive: boolean
  isPrimary: boolean
  source: string
  confidence: number
  status: string
  warning: string | null
}

export type DatasetMetadataResponse = {
  datasetId: string
  displayName: string
  connectionId: string
  tables: MetadataTable[]
  metrics: MetadataMetric[]
  relationships: MetadataRelationship[]
}

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5000'

export async function getDatasetMetadata(datasetId: string): Promise<DatasetMetadataResponse> {
  const res = await fetch(`${API_BASE}/api/datasets/${encodeURIComponent(datasetId)}/metadata`)

  if (!res.ok) {
    throw new Error(await res.text())
  }

  return res.json()
}
