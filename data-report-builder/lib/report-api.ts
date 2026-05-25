export type VisualQueryRequest = {
  connectionId: string
  datasetId: string
  reportId: string
  visualType: 'table'
  rows: string[]
  columns: string[]
  values: string[]
  filters: {
    field: string
    operator: string
    value: unknown
    scope?: string
  }[]
  sort: {
    field: string
    direction: 'ASC' | 'DESC'
  }[]
  limit: number
  offset: number
}

export type ReportFilterOperator =
  | '='
  | '!='
  | '>'
  | '<'
  | '>='
  | '<='
  | 'IN'
  | 'BETWEEN'
  | 'CONTAINS'

export type ReportFilterDraft = {
  id: string
  field: string
  operator: ReportFilterOperator
  value: string
  valueTo?: string
}

export type ReportFilterFieldOption = {
  fieldId: string
  label: string
  type: 'dimension' | 'metric'
  dataType: string
}

export type ReportSortDraft = {
  id: string
  field: string
  direction: 'ASC' | 'DESC'
}

export type QueryColumn = {
  name: string
  type: string
}

export type QueryResult = {
  status?: 'success'
  executionId?: string
  artifactKey?: string
  queryFingerprint?: string
  semanticModelVersion?: string
  columns: QueryColumn[]
  rows: Record<string, unknown>[]
  metadata: {
    rowCount: number
    executionMs: number
    sql: string
    parameters: Record<string, unknown>
    warnings?: { code: string; message: string }[]
  }
}


export type ValidationSeverity = 'Info' | 'Warning' | 'Error'

export type ValidationIssue = {
  code: string
  message: string
  target: string
  severity: ValidationSeverity
  suggestedFix: string
  details: Record<string, unknown>
}

export type ValidationResult = {
  stage: string
  context: Record<string, unknown>
  validationDurationMs: number
  errors: ValidationIssue[]
  warnings: ValidationIssue[]
  isValid: boolean
}

export type ExecutionMetadata = {
  totalDurationMs: number
  errorCount: number
  warningCount: number
  executedStages: string[]
}

export type CompilationResult = {
  success: boolean
  sql: string
  parameters: Record<string, unknown>
}

export type ComprehensiveQueryResponse = {
  success: boolean
  columns: QueryColumn[]
  data: Record<string, unknown>[]
  compilation?: CompilationResult
  metadata: ExecutionMetadata
  validationResults: ValidationResult[]
  executionId?: string
  artifactKey?: string
  queryFingerprint?: string
  semanticModelVersion?: string
}

export type ValidationNotification = {
  id: string
  severity: 'error' | 'warning' | 'info'
  code: string
  message: string
  target?: string
  suggestedFix?: string
}

export function createNotificationsFromResponse(response: ComprehensiveQueryResponse): ValidationNotification[] {
  const notifications: ValidationNotification[] = []
  for (const validationResult of response.validationResults) {
    for (const error of validationResult.errors) {
      notifications.push({
        id: `${validationResult.stage}-${error.code}-${error.target ?? 'na'}`,
        severity: 'error',
        code: error.code,
        message: error.message,
        target: error.target,
        suggestedFix: error.suggestedFix,
      })
    }
    for (const warning of validationResult.warnings) {
      notifications.push({
        id: `${validationResult.stage}-${warning.code}-${warning.target ?? 'na'}`,
        severity: 'warning',
        code: warning.code,
        message: warning.message,
        target: warning.target,
        suggestedFix: warning.suggestedFix,
      })
    }
  }
  return notifications
}

export class ReportApiError extends Error {
  sql?: string
  errorCode?: string

  constructor(message: string, options?: { sql?: string; errorCode?: string }) {
    super(message)
    this.name = 'ReportApiError'
    this.sql = options?.sql
    this.errorCode = options?.errorCode
  }
}

type QueryExecutionError = {
  status: 'error'
  errorCode: string
  message: string
  sql?: string
  details?: unknown[]
}

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5224'

export type RenderReportRequest = {
  format: 'PDF' | 'XLSX' | 'CSV'
  reportTitle?: string
  query: VisualQueryRequest
  exportFullData?: boolean
}

export async function renderReport(request: RenderReportRequest): Promise<Response> {
  return fetch(`${API_BASE}/api/report-exports/render`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}

export async function renderReportExecution(executionId: string, format: string): Promise<Response> {
  return fetch(`${API_BASE}/api/report-executions/${encodeURIComponent(executionId)}/export/${format.toLowerCase()}`, {
    method: 'GET',
  })
}

export async function compileReportQuery(request: VisualQueryRequest) {
  const res = await fetch(`${API_BASE}/api/query/compile`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  if (!res.ok) {
    throw new Error(await res.text())
  }

  return res.json()
}

export async function executeReportQuery(request: VisualQueryRequest): Promise<ComprehensiveQueryResponse> {
  const res = await fetch(`${API_BASE}/api/query/execute`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  if (!res.ok) {
    const text = await res.text()
    let parsedError: QueryExecutionError | null = null

    try {
      parsedError = JSON.parse(text) as QueryExecutionError
    } catch {
      parsedError = null
    }

    throw new ReportApiError(parsedError?.message || text, {
      sql: parsedError?.sql,
      errorCode: parsedError?.errorCode,
    })
  }

  return res.json()
}
