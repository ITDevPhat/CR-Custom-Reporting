import { type DatasetMetadataResponse } from './report-metadata-api'
import { type ReportFilterDraft, type ReportSortDraft, type VisualQueryRequest } from './report-api'
import { type SelectedField } from './schema-data'

export type SelectedReportItem = {
  id: string
  kind: 'field' | 'metric' | 'derived'
  role: 'dimension' | 'measure_candidate' | 'metric' | 'derived_field' | 'key'
  displayName: string
  tableId?: string
  dataType: string
  isHidden?: boolean
  isDraggable?: boolean
}

export type BuildVisualQueryRequestState = {
  connectionId: string
  datasetId: string
  reportId: string
  visualType?: 'table'
  selectedFields: SelectedField[]
  filters: ReportFilterDraft[]
  sortRules: ReportSortDraft[]
  limit?: number
  offset?: number
  metadata: DatasetMetadataResponse | null
}

type FieldLookup = {
  dataType: string
  kind: 'field' | 'metric' | 'derived'
  role: string
  isHidden: boolean
  isDraggable: boolean
}

export function buildVisualQueryRequest(state: BuildVisualQueryRequestState): VisualQueryRequest {
  const lookup = buildLookup(state.metadata)
  const selected = state.selectedFields
    .map((field) => toSelectedReportItem(field, lookup))
    .filter((field): field is SelectedReportItem => Boolean(field))
    .filter((field) => !field.isHidden && field.isDraggable !== false)

  const selectedIds = new Set(selected.map((field) => field.id))

  const rows = selected
    .filter((field) =>
      (field.kind === 'field' && field.role === 'dimension') ||
      field.kind === 'derived' ||
      field.role === 'derived_field')
    .map((field) => field.id)

  const values = selected
    .filter((field) => field.kind === 'metric' || field.role === 'metric')
    .map((field) => field.id)

  const filters = state.filters
    .map((filter) => normalizeFilter(filter, lookup))
    .filter((filter): filter is NonNullable<typeof filter> => Boolean(filter))

  const seenSorts = new Set<string>()
  const sort = state.sortRules
    .filter((rule) => rule.field && selectedIds.has(rule.field))
    .filter((rule) => {
      if (seenSorts.has(rule.field)) return false
      seenSorts.add(rule.field)
      return true
    })
    .map((rule) => ({
      field: rule.field,
      direction: rule.direction === 'DESC' ? 'DESC' as const : 'ASC' as const,
    }))

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
}

function buildLookup(metadata: DatasetMetadataResponse | null) {
  const lookup = new Map<string, FieldLookup>()

  metadata?.tables.forEach((table) => {
    table.fields.forEach((field) => {
      lookup.set(field.fieldId, {
        dataType: field.dataType,
        kind: field.isDerived || field.role === 'derived_field' ? 'derived' : 'field',
        role: field.isDerived ? 'derived_field' : field.role,
        isHidden: field.isHidden,
        isDraggable: field.isDraggable,
      })
    })
  })

  metadata?.metrics.forEach((metric) => {
    lookup.set(metric.metricId, {
      dataType: metric.dataType,
      kind: 'metric',
      role: 'metric',
      isHidden: metric.isHidden,
      isDraggable: metric.isDraggable,
    })
  })

  return lookup
}

function toSelectedReportItem(field: SelectedField, lookup: Map<string, FieldLookup>): SelectedReportItem | null {
  const metadata = lookup.get(field.id)
  if (!metadata) return null

  return {
    id: field.id,
    kind: metadata.kind,
    role: metadata.role as SelectedReportItem['role'],
    displayName: field.displayName ?? field.columnName,
    tableId: field.tableId,
    dataType: metadata.dataType,
    isHidden: metadata.isHidden,
    isDraggable: metadata.isDraggable,
  }
}

function normalizeFilter(filter: ReportFilterDraft, lookup: Map<string, FieldLookup>) {
  if (!filter.field || !filter.operator) return null
  const target = lookup.get(filter.field)
  if (!target) return null

  if (filter.operator === 'BETWEEN') {
    if (!filter.value || !filter.valueTo) return null
    return {
      field: filter.field,
      operator: filter.operator,
      value: [
        coerceValue(filter.value, target.dataType),
        coerceValue(filter.valueTo, target.dataType),
      ],
      scope: 'visual',
    }
  }

  if (filter.operator === 'IN') {
    const values = filter.value
      .split(',')
      .map((value) => value.trim())
      .filter(Boolean)
      .map((value) => coerceValue(value, target.dataType))
    if (values.length === 0) return null

    return {
      field: filter.field,
      operator: filter.operator,
      value: values,
      scope: 'visual',
    }
  }

  if (!filter.value) return null

  return {
    field: filter.field,
    operator: filter.operator,
    value: coerceValue(filter.value, target.dataType),
    scope: 'visual',
  }
}

function coerceValue(raw: string, dataType: string) {
  const value = raw.trim()
  const normalizedType = dataType.toLowerCase()

  if (['tinyint', 'smallint', 'int', 'bigint', 'decimal', 'numeric', 'float', 'real', 'money', 'integer', 'currency', 'percentage'].includes(normalizedType)) {
    const numeric = Number(value)
    return Number.isNaN(numeric) ? value : numeric
  }

  if (normalizedType === 'bit' || normalizedType === 'boolean') {
    return value.toLowerCase() === 'true' || value === '1'
  }

  if (normalizedType.includes('date') || normalizedType.includes('time')) {
    const date = new Date(value)
    return Number.isNaN(date.getTime()) ? value : date.toISOString().slice(0, 10)
  }

  return value
}
