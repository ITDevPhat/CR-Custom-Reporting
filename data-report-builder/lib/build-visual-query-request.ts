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
  placement?: 'rows' | 'values'
  aggregation?: null | 'SUM' | 'AVG' | 'COUNT' | 'COUNT_DISTINCT' | 'MIN' | 'MAX'
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
  displayName: string
  tableId: string
  physicalColumn: string
  dataType: string
  kind: 'field' | 'metric' | 'derived'
  role: string
  isHidden: boolean
  isDraggable: boolean
}

export function buildVisualQueryRequest(state: BuildVisualQueryRequestState): VisualQueryRequest {
  const lookup = buildLookup(state.metadata)
  addRuntimeMetricLookups(lookup, state.selectedFields)
  const selected = state.selectedFields
    .map((field) => toSelectedReportItem(field, lookup))
    .filter((field): field is SelectedReportItem => Boolean(field))
    .filter((field) => !field.isHidden)

  const selectedIds = new Set(selected.map((field) => field.id))

  const rows = selected
    .filter((field) =>
      (!field.aggregation || field.placement !== 'values') &&
      (field.kind === 'field' || field.kind === 'derived') &&
      field.role !== 'metric')
    .map((field) => field.id)

  const values = selected
    .flatMap((field) => {
      if (field.kind === 'metric' || field.role === 'metric') return [field.id]
      if (field.placement === 'values' && field.aggregation) {
        return [buildRuntimeMetricId(field, lookup)]
      }

      return []
    })

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

function addRuntimeMetricLookups(lookup: Map<string, FieldLookup>, selectedFields: SelectedField[]) {
  selectedFields.forEach((selectedField) => {
    if (!selectedField.aggregation) return

    const source = lookup.get(selectedField.id)
    if (!source) return

    const reportItem: SelectedReportItem = {
      id: selectedField.id,
      kind: source.kind,
      role: source.role as SelectedReportItem['role'],
      displayName: selectedField.displayName ?? selectedField.columnName,
      tableId: source.tableId,
      dataType: source.dataType,
      isHidden: source.isHidden,
      isDraggable: source.isDraggable,
      placement: selectedField.placement,
      aggregation: selectedField.aggregation,
    }
    const metricId = buildRuntimeMetricId(reportItem, lookup)

    lookup.set(metricId, {
      displayName: buildRuntimeMetricDisplayName(reportItem.displayName, selectedField.aggregation),
      tableId: source.tableId,
      physicalColumn: source.physicalColumn,
      dataType: selectedField.aggregation.startsWith('COUNT') ? 'integer' : source.dataType,
      kind: 'metric',
      role: 'metric',
      isHidden: false,
      isDraggable: true,
    })
  })
}

function buildLookup(metadata: DatasetMetadataResponse | null) {
  const lookup = new Map<string, FieldLookup>()

  metadata?.tables.forEach((table) => {
    table.fields.forEach((field) => {
      lookup.set(field.fieldId, {
        displayName: field.displayName,
        tableId: field.tableId,
        physicalColumn: field.physicalColumn,
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
      displayName: metric.displayName,
      tableId: metric.baseTableId,
      physicalColumn: metric.metricId,
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
    placement: field.placement,
    aggregation: field.aggregation,
  }
}

export function buildRuntimeMetricId(
  field: SelectedReportItem,
  lookup: Map<string, FieldLookup>
) {
  const metadata = lookup.get(field.id)
  const tableId = metadata?.tableId ?? field.tableId ?? field.id.split('.')[0] ?? ''
  const physicalColumn = metadata?.physicalColumn ?? field.id.split('.').at(-1) ?? field.displayName

  return `metric.${field.aggregation!.toLowerCase()}_${normalizeMetricIdPart(tableId)}_${normalizeMetricIdPart(physicalColumn)}`
}

export function buildRuntimeMetricIdFromMetadata(
  fieldId: string,
  aggregation: NonNullable<SelectedReportItem['aggregation']>,
  metadata: DatasetMetadataResponse | null
) {
  const field = metadata?.tables.flatMap((table) => table.fields).find((item) => item.fieldId === fieldId)
  if (!field) return null

  return `metric.${aggregation.toLowerCase()}_${normalizeMetricIdPart(field.tableId)}_${normalizeMetricIdPart(field.physicalColumn)}`
}

export function buildRuntimeMetricDisplayName(
  fieldName: string,
  aggregation: NonNullable<SelectedReportItem['aggregation']>
) {
  switch (aggregation) {
    case 'SUM':
      return `Sum ${fieldName}`
    case 'AVG':
      return `Average ${fieldName}`
    case 'MIN':
      return `Min ${fieldName}`
    case 'MAX':
      return `Max ${fieldName}`
    case 'COUNT':
      return `Count ${fieldName}`
    case 'COUNT_DISTINCT':
      return `Distinct ${fieldName} Count`
  }
}

function normalizeMetricIdPart(value: string) {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '')
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
