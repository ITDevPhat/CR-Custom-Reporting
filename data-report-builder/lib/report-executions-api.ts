/**
 * Report Executions API client
 *
 * Real source of truth: GET /api/report-executions
 * Mock data exists ONLY as isolated fallback for local UI preview when backend is unavailable.
 */

import { getMockReportExecutions, getMockReportExecution } from './mock-report-executions'

export type ReportExecutionStatus =
  | 'Requested'
  | 'Processing'
  | 'Completed'
  | 'Failed'
  | 'ArtifactMissing'
  | 'ArtifactCorrupted'
  | 'ArtifactVersionMismatch'
  | 'Expired'

export type StorageMode = 'Local' | 'S3' | 'InMemory'

export type ReportExecution = {
  executionId: string
  reportId?: string
  reportName?: string
  templateId?: string
  status: ReportExecutionStatus
  rowCount?: number
  artifactKey?: string
  artifactAvailable: boolean
  storageMode: StorageMode
  createdAtUtc: string
  completedAtUtc?: string
  durationMs?: number
  errorMessage?: string
  queryFingerprint?: string
  semanticModelVersion?: string
  compiledSql?: string
}

export type ReportExecutionDetail = ReportExecution & {
  compiledSql?: string
}

export type ReportExecutionsResponse = {
  executions: ReportExecution[]
  total: number
  isMockData?: boolean
}

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_BASE_URL
  ?? process.env.NEXT_PUBLIC_REPORT_API_URL
  ?? 'http://localhost:5224'

/**
 * Check if we're in development mode
 */
function isDevelopment(): boolean {
  return process.env.NODE_ENV === 'development'
}

/**
 * Fetches all report executions from the real API.
 * Falls back to mock data in development when the API is unavailable.
 * Returns { executions, isMockData } to indicate data source.
 */
export async function getReportExecutions(): Promise<ReportExecutionsResponse> {
  try {
    const res = await fetch(`${API_BASE}/api/report-executions`, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
      },
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(text || `Failed to fetch report executions: ${res.status}`)
    }

    const data = await res.json()

    // Handle both array response and wrapped response
    if (Array.isArray(data)) {
      return { executions: data, total: data.length, isMockData: false }
    }

    if (data.executions && Array.isArray(data.executions)) {
      return { executions: data.executions, total: data.total ?? data.executions.length, isMockData: false }
    }

    return { executions: [], total: 0, isMockData: false }
  } catch (error) {
    // In development, fall back to mock data
    if (isDevelopment()) {
      console.warn('[v0] Using mock report executions because backend API is unavailable.')
      const mockData = getMockReportExecutions()
      return { executions: mockData, total: mockData.length, isMockData: true }
    }

    // In production, re-throw the error
    throw error
  }
}

/**
 * Fetches a single report execution by ID.
 * Falls back to mock data in development when the API is unavailable.
 */
export async function getReportExecution(
  executionId: string,
  useMockFallback = false
): Promise<ReportExecutionDetail | null> {
  // If we know we're in mock mode, use mock data directly
  if (useMockFallback && isDevelopment()) {
    const mockExecution = getMockReportExecution(executionId)
    if (mockExecution) {
      return mockExecution as ReportExecutionDetail
    }
  }

  try {
    const res = await fetch(`${API_BASE}/api/report-executions/${executionId}`, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
      },
    })

    if (!res.ok) {
      const text = await res.text()
      throw new Error(text || `Failed to fetch report execution: ${res.status}`)
    }

    return res.json()
  } catch (error) {
    // In development with mock fallback, try mock data
    if (useMockFallback && isDevelopment()) {
      console.warn('[v0] Using mock report execution detail because backend API is unavailable.')
      const mockExecution = getMockReportExecution(executionId)
      if (mockExecution) {
        return mockExecution as ReportExecutionDetail
      }
    }

    throw error
  }
}

export type ExportFormat = 'PDF' | 'XLSX' | 'CSV' | 'DOCX'
export type ReportPreviewReference = {
  executionId: string
  reportSource: string
  viewerUrl: string
  status: string
  artifactAvailable: boolean
}

/**
 * Downloads a report execution in the specified format.
 * Returns the blob and suggested filename.
 * In mock mode, creates a dummy file for testing.
 */
export async function downloadReportExecution(
  executionId: string,
  format: ExportFormat,
  useMockFallback = false
): Promise<{ blob: Blob; filename: string }> {
  // If in mock mode, create a dummy file
  if (useMockFallback && isDevelopment()) {
    console.warn('[v0] Using mock download because backend API is unavailable.')
    const mockContent = `Mock ${format} export for execution ${executionId}\n\nThis is a development preview. Connect the backend API to download real reports.`
    const blob = new Blob([mockContent], { type: 'text/plain' })
    const filename = `mock-report-${executionId}.${format.toLowerCase()}`
    return { blob, filename }
  }

  const res = await fetch(
    `${API_BASE}/api/report-executions/${executionId}/export/${format.toLowerCase()}`,
    {
      method: 'GET',
    }
  )

  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `Download failed: ${res.status}`)
  }

  const blob = await res.blob()

  // Extract filename from Content-Disposition header if available
  const disposition = res.headers.get('content-disposition') ?? ''
  const filenameMatch = disposition.match(/filename\*?=(?:UTF-8''|")?([^\";]+)/i)
  const filename = filenameMatch?.[1]
    ? decodeURIComponent(filenameMatch[1].replace(/"/g, ''))
    : `report-${executionId}.${format.toLowerCase()}`

  return { blob, filename }
}

export async function getReportPreviewReference(executionId: string): Promise<ReportPreviewReference> {
  const res = await fetch(`${API_BASE}/api/report-executions/${executionId}/preview-reference`, {
    method: 'GET',
    headers: { 'Content-Type': 'application/json' },
  })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `Preview reference failed: ${res.status}`)
  }
  return res.json()
}

/**
 * Triggers browser download for a blob with the given filename.
 */
export function triggerBlobDownload(blob: Blob, filename: string): void {
  const url = window.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(url)
}
