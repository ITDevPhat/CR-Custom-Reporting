import { type MetadataField, type MetadataMetric } from './report-metadata-api'
import { type VisualQueryRequest } from './report-api'

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5000'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!res.ok) throw new Error(await res.text())
  if (res.status === 204) return undefined as T
  return res.json()
}

export type MetricRequest = {
  displayName: string
  formula: string
  baseTableId: string
  aggregationBehavior: 'additive' | 'semi_additive' | 'non_additive' | 'ratio' | 'calculated'
  dataType: 'decimal' | 'integer' | 'percentage' | 'currency'
  format: 'general' | 'currency' | 'percentage' | 'integer' | 'decimal'
  isHidden: boolean
  isDraggable: boolean
}

export type DerivedFieldRequest = {
  displayName: string
  baseTableId: string
  expression: string
  dataType: string
  semanticType: string
  format: string
  isHidden: boolean
  isDraggable: boolean
}

export type ValidationResponse = {
  valid: boolean
  errors: string[]
  warnings: string[]
}

export type ExpressionValidationRequest = {
  expression: string
  targetKind: 'auto' | 'calculated_column' | 'calculated_measure'
}

export type ExpressionValidationResult = {
  valid: boolean
  detectedKind: 'calculated_column' | 'calculated_measure'
  returnType: string
  dependencies: string[]
  compiledSqlPreview: string
  errors: string[]
}

export function updateField(datasetId: string, fieldId: string, body: Partial<MetadataField>) {
  return request<MetadataField>(`/api/datasets/${encodeURIComponent(datasetId)}/fields/${encodeURIComponent(fieldId)}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  })
}

export function validateMetric(datasetId: string, body: MetricRequest) {
  return request<ValidationResponse>(`/api/datasets/${encodeURIComponent(datasetId)}/metrics/validate`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function createMetric(datasetId: string, body: MetricRequest) {
  return request<MetadataMetric>(`/api/datasets/${encodeURIComponent(datasetId)}/metrics`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function validateDerivedField(datasetId: string, body: DerivedFieldRequest) {
  return request<ValidationResponse>(`/api/datasets/${encodeURIComponent(datasetId)}/derived-fields/validate`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function createDerivedField(datasetId: string, body: DerivedFieldRequest) {
  return request<MetadataField>(`/api/datasets/${encodeURIComponent(datasetId)}/derived-fields`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function validateExpression(datasetId: string, body: ExpressionValidationRequest) {
  return request<ExpressionValidationResult>(`/api/datasets/${encodeURIComponent(datasetId)}/expressions/validate`, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export type ReportDefinition = VisualQueryRequest & {
  title: string
  description: string
  layout: Record<string, unknown>
  semanticModelVersion: string
  createdAt?: string
  updatedAt?: string
}

export function saveReportDefinition(reportId: string | null, body: ReportDefinition) {
  return request<ReportDefinition & { reportId: string }>(reportId ? `/api/reports/${encodeURIComponent(reportId)}` : '/api/reports', {
    method: reportId ? 'PUT' : 'POST',
    body: JSON.stringify(body),
  })
}
