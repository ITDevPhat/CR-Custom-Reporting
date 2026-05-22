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

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5000'

export type RenderReportRequest = {
  format: 'PDF' | 'XLSX'
  reportTitle?: string
  query: VisualQueryRequest
}

export async function renderReport(request: RenderReportRequest): Promise<Response> {
  const res = await fetch(`${API_BASE}/api/report-exports/render`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  if (!res.ok) {
    throw new Error(await res.text())
  }

  return res
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

export async function executeReportQuery(request: VisualQueryRequest): Promise<QueryResult> {
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
