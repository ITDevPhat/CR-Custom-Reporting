export type PowerBIConfiguration = {
  tenantId?: string | null
  clientId?: string | null
  clientSecret?: string | null
  workspaceId?: string | null
  reportId?: string | null
  datasetId?: string | null
  authorityUrl?: string | null
  apiBaseUrl?: string | null
  hasClientSecret: boolean
  sources: string[]
}

export type SavePowerBIConfigurationRequest = {
  tenantId?: string | null
  clientId?: string | null
  clientSecret?: string | null
  workspaceId?: string | null
  reportId?: string | null
  datasetId?: string | null
  authorityUrl?: string | null
  apiBaseUrl?: string | null
}

export type PowerBIConnectionTestResponse = {
  success: boolean
  authenticationStatus: string
  workspaceAccessible: boolean
  reportAccessible: boolean
  workspaceName?: string | null
  reportName?: string | null
  message?: string | null
  diagnostics: string[]
}

export type PowerBIWorkspace = {
  id: string
  name: string
  isReadOnly?: boolean | null
  isOnDedicatedCapacity?: boolean | null
  capacityId?: string | null
}

export type PowerBIReport = {
  id: string
  name: string
  webUrl?: string | null
  embedUrl?: string | null
  datasetId?: string | null
}

export type PowerBIDataset = {
  id: string
  name: string
  configuredBy?: string | null
  isRefreshable?: boolean | null
  isEffectiveIdentityRequired?: boolean | null
  isEffectiveIdentityRolesRequired?: boolean | null
}

export type PowerBIEmbedTokenRequest = {
  workspaceId?: string | null
  reportId?: string | null
  datasetId?: string | null
}

export type PowerBIEmbedTokenResponse = {
  reportId: string
  reportName: string
  embedUrl: string
  embedToken: string
  tokenType: string
  expiration: string
}

export type PowerBIApiError = {
  code?: string
  message?: string
  detail?: string | null
}

const API_BASE = process.env.NEXT_PUBLIC_REPORT_API_URL ?? 'http://localhost:5224'

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
  })

  if (!res.ok) {
    const text = await res.text()
    let parsed: PowerBIApiError | null = null
    try {
      parsed = JSON.parse(text) as PowerBIApiError
    } catch {
      parsed = null
    }

    const message = parsed?.message || text || `Power BI API returned ${res.status}`
    const detail = parsed?.detail ? ` ${parsed.detail}` : ''
    throw new Error(parsed?.code ? `${parsed.code}: ${message}${detail}` : `${message}${detail}`)
  }

  return res.json()
}

export function loadPowerBIConfiguration() {
  return requestJson<PowerBIConfiguration>('/api/powerbi/config')
}

export function savePowerBIConfiguration(request: SavePowerBIConfigurationRequest) {
  return requestJson<PowerBIConfiguration>('/api/powerbi/config/save', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}

export function testPowerBIConnection() {
  return requestJson<PowerBIConnectionTestResponse>('/api/powerbi/test-connection', {
    method: 'POST',
    body: JSON.stringify({}),
  })
}

export function discoverPowerBIWorkspaces() {
  return requestJson<PowerBIWorkspace[]>('/api/powerbi/workspaces')
}

export function discoverPowerBIReports(workspaceId: string) {
  return requestJson<PowerBIReport[]>(`/api/powerbi/workspaces/${workspaceId}/reports`)
}

export function getPowerBIReport(workspaceId: string, reportId: string) {
  return requestJson<PowerBIReport>(`/api/powerbi/workspaces/${workspaceId}/reports/${reportId}`)
}

export function discoverPowerBIDatasets(workspaceId: string) {
  return requestJson<PowerBIDataset[]>(`/api/powerbi/workspaces/${workspaceId}/datasets`)
}

export function generatePowerBIEmbedToken(request: PowerBIEmbedTokenRequest) {
  return requestJson<PowerBIEmbedTokenResponse>('/api/powerbi/embed-token', {
    method: 'POST',
    body: JSON.stringify(request),
  })
}
