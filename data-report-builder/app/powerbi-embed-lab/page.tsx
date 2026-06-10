'use client'

import { useCallback, useMemo, useState } from 'react'
import {
  CheckCircle2,
  Database,
  Eraser,
  FileSearch,
  KeyRound,
  Loader2,
  Play,
  RefreshCw,
  Save,
  Search,
  ServerCog,
  ShieldCheck,
} from 'lucide-react'
import { toast } from 'sonner'

import { PowerBIReportEmbed } from '@/components/powerbi/powerbi-report-embed'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Separator } from '@/components/ui/separator'
import {
  discoverPowerBIDatasets,
  discoverPowerBIReports,
  discoverPowerBIWorkspaces,
  generatePowerBIEmbedToken,
  loadPowerBIConfiguration,
  savePowerBIConfiguration,
  testPowerBIConnection,
  type PowerBIConfiguration,
  type PowerBIDataset,
  type PowerBIEmbedTokenResponse,
  type PowerBIReport,
  type PowerBIWorkspace,
} from '@/lib/powerbi-api'
import { cn } from '@/lib/utils'

type DebugEntry = {
  time: string
  level: 'info' | 'success' | 'error'
  message: string
  detail?: unknown
}

const DEFAULT_FORM: PowerBIConfiguration = {
  tenantId: '',
  clientId: '',
  clientSecret: '',
  workspaceId: '',
  reportId: '',
  datasetId: '',
  authorityUrl: 'https://login.microsoftonline.com',
  apiBaseUrl: 'https://api.powerbi.com/v1.0/myorg',
  hasClientSecret: false,
  sources: [],
}

const FIELD_LABELS: Record<keyof Pick<PowerBIConfiguration, 'tenantId' | 'clientId' | 'clientSecret' | 'workspaceId' | 'reportId' | 'datasetId' | 'authorityUrl' | 'apiBaseUrl'>, string> = {
  tenantId: 'Tenant ID',
  clientId: 'Client ID',
  clientSecret: 'Client Secret',
  workspaceId: 'Workspace ID',
  reportId: 'Report ID',
  datasetId: 'Dataset ID',
  authorityUrl: 'Authority URL',
  apiBaseUrl: 'API Base URL',
}

function nowLabel() {
  return new Date().toLocaleTimeString()
}

function maskSecret(value?: string | null) {
  if (!value) return ''
  if (value === '********') return value
  return value.length <= 8 ? '********' : `${value.slice(0, 3)}****${value.slice(-3)}`
}

function shortValue(value?: string | null) {
  if (!value) return '-'
  return value.length <= 18 ? value : `${value.slice(0, 8)}...${value.slice(-6)}`
}

export default function PowerBIEmbedLabPage() {
  const [form, setForm] = useState<PowerBIConfiguration>(DEFAULT_FORM)
  const [workspaces, setWorkspaces] = useState<PowerBIWorkspace[]>([])
  const [reports, setReports] = useState<PowerBIReport[]>([])
  const [datasets, setDatasets] = useState<PowerBIDataset[]>([])
  const [embedConfig, setEmbedConfig] = useState<PowerBIEmbedTokenResponse | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [busyAction, setBusyAction] = useState<string | null>(null)
  const [authStatus, setAuthStatus] = useState('Not tested')
  const [apiStatus, setApiStatus] = useState('Idle')
  const [lastError, setLastError] = useState<string | null>(null)
  const [events, setEvents] = useState<DebugEntry[]>([])

  const selectedWorkspace = useMemo(
    () => workspaces.find((item) => item.id === form.workspaceId) ?? null,
    [form.workspaceId, workspaces]
  )
  const selectedReport = useMemo(
    () => reports.find((item) => item.id === form.reportId) ?? null,
    [form.reportId, reports]
  )

  const pushEvent = useCallback((entry: Omit<DebugEntry, 'time'>) => {
    setEvents((current) => [
      { ...entry, time: nowLabel() },
      ...current,
    ].slice(0, 80))
  }, [])

  const runAction = useCallback(async <T,>(name: string, action: () => Promise<T>) => {
    setBusyAction(name)
    setApiStatus(`${name} pending`)
    setLastError(null)
    try {
      const result = await action()
      setApiStatus(`${name} succeeded`)
      pushEvent({ level: 'success', message: `${name} succeeded` })
      return result
    } catch (err) {
      const message = err instanceof Error ? err.message : `${name} failed`
      setApiStatus(`${name} failed`)
      setLastError(message)
      pushEvent({ level: 'error', message, detail: { action: name } })
      toast.error(message)
      return null
    } finally {
      setBusyAction(null)
    }
  }, [pushEvent])

  const updateField = (field: keyof PowerBIConfiguration, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
  }

  const loadConfiguration = async () => {
    const config = await runAction('Load Configuration', loadPowerBIConfiguration)
    if (!config) return
    setForm({
      ...DEFAULT_FORM,
      ...config,
      clientSecret: config.hasClientSecret ? '********' : '',
    })
    pushEvent({ level: 'info', message: 'Configuration loaded with secrets masked.' })
  }

  const saveConfiguration = async () => {
    const config = await runAction('Save Configuration', () => savePowerBIConfiguration(form))
    if (!config) return
    setForm({
      ...DEFAULT_FORM,
      ...config,
      clientSecret: config.hasClientSecret ? '********' : '',
    })
  }

  const testConnection = async () => {
    const result = await runAction('Test Connection', testPowerBIConnection)
    if (!result) return
    setAuthStatus(result.authenticationStatus)
    if (result.workspaceName && !selectedWorkspace) {
      pushEvent({ level: 'info', message: `Workspace resolved: ${result.workspaceName}` })
    }
    if (result.reportName && !selectedReport) {
      pushEvent({ level: 'info', message: `Report resolved: ${result.reportName}` })
    }
    if (!result.success && result.diagnostics.length) {
      setLastError(result.diagnostics.join(' '))
    }
  }

  const discoverWorkspaces = async () => {
    const result = await runAction('Discover Workspaces', discoverPowerBIWorkspaces)
    if (!result) return
    setWorkspaces(result)
    pushEvent({ level: 'info', message: `${result.length} workspace(s) discovered.` })
  }

  const discoverReports = async () => {
    if (!form.workspaceId) {
      toast.warning('Workspace ID is required.')
      return
    }

    const reportResult = await runAction('Discover Reports', () => discoverPowerBIReports(form.workspaceId!))
    if (reportResult) {
      setReports(reportResult)
      pushEvent({ level: 'info', message: `${reportResult.length} report(s) discovered.` })
    }

    const datasetResult = await runAction('Discover Datasets', () => discoverPowerBIDatasets(form.workspaceId!))
    if (datasetResult) {
      setDatasets(datasetResult)
    }
  }

  const generateEmbedToken = async () => {
    const result = await runAction('Generate Embed Token', () => generatePowerBIEmbedToken({
      workspaceId: form.workspaceId,
      reportId: form.reportId,
      datasetId: form.datasetId,
    }))
    if (!result) return
    setEmbedConfig(result)
    setForm((current) => ({
      ...current,
      reportId: result.reportId,
    }))
    pushEvent({
      level: 'success',
      message: 'Embed token generated. Token value is hidden.',
      detail: { expiration: result.expiration, reportName: result.reportName },
    })
  }

  const embedReport = () => {
    if (!embedConfig) {
      toast.warning('Generate an embed token before embedding.')
      return
    }
    setReloadKey((current) => current + 1)
    pushEvent({ level: 'info', message: 'Embed Report requested.' })
  }

  const clearSession = () => {
    setWorkspaces([])
    setReports([])
    setDatasets([])
    setEmbedConfig(null)
    setAuthStatus('Not tested')
    setApiStatus('Idle')
    setLastError(null)
    setEvents([])
    setReloadKey(0)
  }

  const onSdkEvent = useCallback((eventName: string, detail?: unknown) => {
    pushEvent({ level: eventName === 'error' ? 'error' : 'info', message: `SDK event: ${eventName}`, detail })
  }, [pushEvent])

  const onSdkError = useCallback((message: string, detail?: unknown) => {
    setLastError(message)
    pushEvent({ level: 'error', message, detail })
  }, [pushEvent])

  const renderActionButton = (
    actionName: string,
    label: string,
    icon: React.ReactNode,
    onClick: () => void | Promise<void>,
    variant: 'default' | 'outline' | 'secondary' = 'outline'
  ) => (
    <Button
      type="button"
      variant={variant}
      className="justify-start"
      onClick={onClick}
      disabled={busyAction !== null}
    >
      {busyAction === actionName ? <Loader2 className="animate-spin" /> : icon}
      {label}
    </Button>
  )

  return (
    <main className="min-h-screen bg-background text-foreground">
      <div className="flex min-h-screen flex-col">
        <header className="border-b border-border px-6 py-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h1 className="text-xl font-semibold">Power BI Embed Lab</h1>
              <p className="text-sm text-muted-foreground">App owns data validation workspace</p>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <span className={cn(
                'inline-flex items-center gap-2 rounded-md border px-3 py-1.5',
                authStatus === 'Authenticated' ? 'border-emerald-300 text-emerald-700' : 'border-border text-muted-foreground'
              )}>
                <ShieldCheck className="h-4 w-4" />
                {authStatus}
              </span>
              <span className="rounded-md border border-border px-3 py-1.5 text-muted-foreground">{apiStatus}</span>
            </div>
          </div>
        </header>

        <div className="grid flex-1 grid-cols-1 gap-0 lg:grid-cols-[420px_minmax(0,1fr)_360px]">
          <aside className="border-r border-border p-4">
            <section className="space-y-4">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-semibold uppercase tracking-normal text-muted-foreground">Configuration</h2>
                <span className="text-xs text-muted-foreground">{form.sources?.join(', ') || 'local form'}</span>
              </div>

              <div className="grid gap-3">
                {(Object.keys(FIELD_LABELS) as Array<keyof typeof FIELD_LABELS>).map((field) => (
                  <div key={field} className="grid gap-1.5">
                    <Label htmlFor={field}>{FIELD_LABELS[field]}</Label>
                    <Input
                      id={field}
                      type={field === 'clientSecret' ? 'password' : 'text'}
                      value={(form[field] as string | null | undefined) ?? ''}
                      placeholder={field === 'clientSecret' && form.hasClientSecret ? '********' : undefined}
                      onChange={(event) => updateField(field, event.target.value)}
                    />
                  </div>
                ))}
              </div>

              <Separator />

              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                {renderActionButton('Load Configuration', 'Load Configuration', <RefreshCw />, loadConfiguration)}
                {renderActionButton('Save Configuration', 'Save Configuration', <Save />, saveConfiguration, 'default')}
                {renderActionButton('Test Connection', 'Test Connection', <ShieldCheck />, testConnection)}
                {renderActionButton('Discover Workspaces', 'Discover Workspaces', <ServerCog />, discoverWorkspaces)}
                {renderActionButton('Discover Reports', 'Discover Reports', <FileSearch />, discoverReports)}
                {renderActionButton('Generate Embed Token', 'Generate Token', <KeyRound />, generateEmbedToken, 'secondary')}
                {renderActionButton('Embed Report', 'Embed Report', <Play />, embedReport)}
                {renderActionButton('Reload Report', 'Reload Report', <RefreshCw />, embedReport)}
                {renderActionButton('Clear Session', 'Clear Session', <Eraser />, clearSession)}
              </div>
            </section>
          </aside>

          <section className="flex min-h-[720px] flex-col p-4">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 className="text-sm font-semibold uppercase tracking-normal text-muted-foreground">Report Container</h2>
                <p className="text-sm text-muted-foreground">{embedConfig?.reportName ?? selectedReport?.name ?? 'No report embedded'}</p>
              </div>
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Database className="h-4 w-4" />
                <span>{shortValue(form.workspaceId)}</span>
                <span>/</span>
                <span>{shortValue(form.reportId)}</span>
              </div>
            </div>
            <div className="min-h-0 flex-1">
              <PowerBIReportEmbed
                embedConfig={embedConfig}
                reloadKey={reloadKey}
                onEvent={onSdkEvent}
                onError={onSdkError}
              />
            </div>
          </section>

          <aside className="border-l border-border p-4">
            <section className="space-y-4">
              <div>
                <h2 className="text-sm font-semibold uppercase tracking-normal text-muted-foreground">Debug</h2>
                <p className="text-sm text-muted-foreground">Sensitive values are masked.</p>
              </div>

              <div className="grid gap-2 text-sm">
                <DebugRow label="Workspace" value={selectedWorkspace?.name ?? shortValue(form.workspaceId)} />
                <DebugRow label="Report" value={embedConfig?.reportName ?? selectedReport?.name ?? shortValue(form.reportId)} />
                <DebugRow label="Report ID" value={shortValue(embedConfig?.reportId ?? form.reportId)} />
                <DebugRow label="Embed URL" value={embedConfig?.embedUrl ? shortValue(embedConfig.embedUrl) : '-'} />
                <DebugRow label="Token Expiration" value={embedConfig?.expiration ? new Date(embedConfig.expiration).toLocaleString() : '-'} />
                <DebugRow label="Client Secret" value={maskSecret(form.clientSecret)} />
                <DebugRow label="API Status" value={apiStatus} />
                <DebugRow label="Error" value={lastError ?? '-'} tone={lastError ? 'error' : 'default'} />
              </div>

              <Separator />

              <DiscoveryList
                title="Workspaces"
                items={workspaces.map((item) => ({ id: item.id, name: item.name }))}
                selectedId={form.workspaceId ?? ''}
                onSelect={(id) => updateField('workspaceId', id)}
              />
              <DiscoveryList
                title="Reports"
                items={reports.map((item) => ({ id: item.id, name: item.name }))}
                selectedId={form.reportId ?? ''}
                onSelect={(id) => updateField('reportId', id)}
              />
              <DiscoveryList
                title="Datasets"
                items={datasets.map((item) => ({ id: item.id, name: item.name }))}
                selectedId={form.datasetId ?? ''}
                onSelect={(id) => updateField('datasetId', id)}
              />

              <Separator />

              <div className="space-y-2">
                <div className="flex items-center gap-2 text-sm font-medium">
                  <Search className="h-4 w-4" />
                  Events
                </div>
                <div className="max-h-[300px] space-y-2 overflow-auto pr-1 custom-scrollbar-thin">
                  {events.length === 0 ? (
                    <p className="text-sm text-muted-foreground">No events yet.</p>
                  ) : events.map((event, index) => (
                    <div key={`${event.time}-${index}`} className="rounded-md border border-border p-2 text-xs">
                      <div className="flex items-center justify-between gap-2">
                        <span className={cn(
                          'font-medium',
                          event.level === 'success' && 'text-emerald-700',
                          event.level === 'error' && 'text-destructive'
                        )}>{event.message}</span>
                        <span className="text-muted-foreground">{event.time}</span>
                      </div>
                      {event.detail ? (
                        <pre className="mt-2 max-h-24 overflow-auto whitespace-pre-wrap text-muted-foreground">
                          {JSON.stringify(event.detail, null, 2)}
                        </pre>
                      ) : null}
                    </div>
                  ))}
                </div>
              </div>
            </section>
          </aside>
        </div>
      </div>
    </main>
  )
}

function DebugRow({ label, value, tone = 'default' }: { label: string; value: string; tone?: 'default' | 'error' }) {
  return (
    <div className="grid grid-cols-[130px_minmax(0,1fr)] gap-3 rounded-md border border-border px-3 py-2">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn('break-words font-medium', tone === 'error' && 'text-destructive')}>{value}</span>
    </div>
  )
}

function DiscoveryList({
  title,
  items,
  selectedId,
  onSelect,
}: {
  title: string
  items: { id: string; name: string }[]
  selectedId: string
  onSelect: (id: string) => void
}) {
  return (
    <div className="space-y-2">
      <div className="text-sm font-medium">{title}</div>
      <div className="max-h-32 space-y-1 overflow-auto pr-1 custom-scrollbar-thin">
        {items.length === 0 ? (
          <p className="text-sm text-muted-foreground">No {title.toLowerCase()} loaded.</p>
        ) : items.map((item) => (
          <button
            key={item.id}
            type="button"
            className={cn(
              'w-full rounded-md border px-2 py-1.5 text-left text-sm transition-colors',
              selectedId === item.id
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-border hover:bg-accent'
            )}
            onClick={() => onSelect(item.id)}
          >
            <span className="flex items-center gap-2">
              {selectedId === item.id ? <CheckCircle2 className="h-4 w-4" /> : null}
              <span className="min-w-0 truncate">{item.name}</span>
            </span>
            <span className="block truncate text-xs opacity-75">{item.id}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
