'use client'

import { useMemo, useState } from 'react'
import { Check, Database, RefreshCw, Search, Server, Table2, X } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'
import {
  discoverSchema,
  previewTable,
  registerDatasetFromTables,
  testConnection,
  type DiscoverSchemaResponse,
  type SqlServerConnectionRequest,
  type TableDto,
  type TablePreviewResponse,
} from '@/lib/connections-api'
import { type DatasetMetadataResponse } from '@/lib/report-metadata-api'

export type LoadedDataset = {
  datasetId: string
  connectionId: string
  displayName: string
  connection: SqlServerConnectionRequest
  selectedTables: { schema: string; table: string }[]
  metadata: DatasetMetadataResponse
}

interface ConnectSourceFlowProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onDatasetLoaded: (dataset: LoadedDataset) => void
}

const initialConnection: SqlServerConnectionRequest = {
  provider: 'sqlserver',
  server: 'localhost',
  database: 'AdventureWorksDW2025',
  authenticationType: 'sql',
  username: 'itdevphat',
  password: '',
  trustServerCertificate: true,
  encrypt: false,
  commandTimeoutSeconds: 30,
}

function tableKey(table: Pick<TableDto, 'schema' | 'table'>) {
  return `${table.schema}.${table.table}`
}

function inferTableKind(table: TableDto) {
  if (table.table.toLowerCase().startsWith('fact')) return 'fact'
  if (table.table.toLowerCase().startsWith('dim')) return 'dimension'
  return 'table'
}

export function ConnectSourceFlow({ open, onOpenChange, onDatasetLoaded }: ConnectSourceFlowProps) {
  const [step, setStep] = useState<'connect' | 'navigator'>('connect')
  const [connection, setConnection] = useState<SqlServerConnectionRequest>(initialConnection)
  const [testState, setTestState] = useState<{ loading: boolean; message: string; ok: boolean | null }>({
    loading: false,
    message: '',
    ok: null,
  })
  const [discoverState, setDiscoverState] = useState<{ loading: boolean; error: string | null }>({
    loading: false,
    error: null,
  })
  const [discovery, setDiscovery] = useState<DiscoverSchemaResponse | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [filter, setFilter] = useState<'all' | 'fact' | 'dimension' | 'selected'>('all')
  const [selectedTables, setSelectedTables] = useState<Set<string>>(new Set())
  const [activeTable, setActiveTable] = useState<TableDto | null>(null)
  const [preview, setPreview] = useState<TablePreviewResponse | null>(null)
  const [previewState, setPreviewState] = useState<{ loading: boolean; error: string | null }>({
    loading: false,
    error: null,
  })
  const [loadState, setLoadState] = useState<{ loading: boolean; error: string | null }>({
    loading: false,
    error: null,
  })

  const reset = () => {
    setStep('connect')
    setConnection(initialConnection)
    setTestState({ loading: false, message: '', ok: null })
    setDiscovery(null)
    setSelectedTables(new Set())
    setActiveTable(null)
    setPreview(null)
    setDiscoverState({ loading: false, error: null })
    setPreviewState({ loading: false, error: null })
    setLoadState({ loading: false, error: null })
  }

  const updateConnection = (patch: Partial<SqlServerConnectionRequest>) => {
    setConnection((current) => ({ ...current, ...patch }))
    setTestState({ loading: false, message: '', ok: null })
  }

  const handleClose = (nextOpen: boolean) => {
    onOpenChange(nextOpen)
    if (!nextOpen) reset()
  }

  const handleTestConnection = async () => {
    if (!connection.server.trim() || !connection.database.trim()) {
      setTestState({ loading: false, ok: false, message: 'Server and database are required.' })
      return
    }

    try {
      setTestState({ loading: true, ok: null, message: 'Testing connection...' })
      const result = await testConnection(connection)
      setTestState({ loading: false, ok: result.success, message: result.message })
      if (result.success) toast.success('Connection test succeeded')
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Connection test failed'
      setTestState({ loading: false, ok: false, message })
    }
  }

  const handleNext = async () => {
    if (!connection.server.trim() || !connection.database.trim()) {
      setTestState({ loading: false, ok: false, message: 'Server and database are required.' })
      return
    }

    try {
      setDiscoverState({ loading: true, error: null })
      const response = await discoverSchema(connection)
      setDiscovery(response)
      setSelectedTables(new Set())
      setActiveTable(response.tables[0] ?? null)
      setStep('navigator')
      if (response.tables[0]) {
        void loadPreview(response.tables[0])
      }
    } catch (err) {
      setDiscoverState({
        loading: false,
        error: err instanceof Error ? err.message : 'Schema discovery failed',
      })
      return
    } finally {
      setDiscoverState((current) => ({ ...current, loading: false }))
    }
  }

  const loadPreview = async (table: TableDto) => {
    setActiveTable(table)
    setPreview(null)
    try {
      setPreviewState({ loading: true, error: null })
      const response = await previewTable(connection, table.schema, table.table, 20)
      setPreview(response)
    } catch (err) {
      setPreviewState({
        loading: false,
        error: err instanceof Error ? err.message : 'Preview failed',
      })
      return
    } finally {
      setPreviewState((current) => ({ ...current, loading: false }))
    }
  }

  const filteredTables = useMemo(() => {
    const tables = discovery?.tables ?? []
    return tables.filter((table) => {
      const kind = inferTableKind(table)
      const key = tableKey(table)
      const matchesSearch = !searchQuery ||
        key.toLowerCase().includes(searchQuery.toLowerCase())
      const matchesFilter =
        filter === 'all' ||
        (filter === 'selected' && selectedTables.has(key)) ||
        filter === kind

      return matchesSearch && matchesFilter
    })
  }, [discovery, searchQuery, filter, selectedTables])

  const toggleTable = (table: TableDto) => {
    const key = tableKey(table)
    setSelectedTables((current) => {
      const next = new Set(current)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  const selectRelatedTables = () => {
    if (!discovery || !activeTable) return
    const related = new Set(selectedTables)
    related.add(tableKey(activeTable))

    discovery.relationships.forEach((relationship) => {
      const fromKey = `${relationship.fromSchema}.${relationship.fromTable}`
      const toKey = `${relationship.toSchema}.${relationship.toTable}`
      if (fromKey === tableKey(activeTable)) related.add(toKey)
      if (toKey === tableKey(activeTable)) related.add(fromKey)
    })

    setSelectedTables(related)
    toast.success('Related tables selected')
  }

  const handleLoad = async () => {
    if (!discovery || selectedTables.size === 0) {
      setLoadState({ loading: false, error: 'Select at least one table.' })
      return
    }

    const selected = discovery.tables
      .filter((table) => selectedTables.has(tableKey(table)))
      .map((table) => ({ schema: table.schema, table: table.table }))

    try {
      setLoadState({ loading: true, error: null })
      const response = await registerDatasetFromTables(connection.database, connection, selected)
      onDatasetLoaded({
        datasetId: response.datasetId,
        connectionId: response.connectionId,
        displayName: response.metadata.displayName,
        connection,
        selectedTables: selected,
        metadata: response.metadata,
      })
      if (response.consistency?.length) {
        console.table(response.consistency)
      }
      if (response.warnings?.length) {
        console.warn('[metadata registration warnings]', response.warnings)
      }
      if (response.debugFields?.length) {
        console.table(response.debugFields)
      }
      toast.success('Tables loaded')
      handleClose(false)
    } catch (err) {
      setLoadState({
        loading: false,
        error: err instanceof Error ? err.message : 'Metadata registration failed',
      })
      return
    } finally {
      setLoadState((current) => ({ ...current, loading: false }))
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className={cn('p-0 gap-0', step === 'navigator' ? 'w-[96vw] !max-w-[1600px] h-[90vh]' : 'w-[720px] max-w-[92vw]')}>
        {step === 'connect' ? (
          <>
            <DialogHeader className="px-6 py-4 border-b">
              <DialogTitle className="flex items-center gap-2">
                <Database className="h-5 w-5" />
                SQL Server
              </DialogTitle>
            </DialogHeader>
            <div className="p-6 space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label>Server</Label>
                  <Input value={connection.server} onChange={(e) => updateConnection({ server: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label>Database</Label>
                  <Input value={connection.database} onChange={(e) => updateConnection({ database: e.target.value })} />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label>Authentication</Label>
                  <Select
                    value={connection.authenticationType}
                    onValueChange={(value: 'sql' | 'windows') => updateConnection({ authenticationType: value })}
                  >
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="sql">SQL Server</SelectItem>
                      <SelectItem value="windows">Windows</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Username</Label>
                  <Input
                    value={connection.username}
                    disabled={connection.authenticationType === 'windows'}
                    onChange={(e) => updateConnection({ username: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Password</Label>
                  <Input
                    type="password"
                    value={connection.password}
                    disabled={connection.authenticationType === 'windows'}
                    onChange={(e) => updateConnection({ password: e.target.value })}
                  />
                </div>
              </div>
              <div className="flex items-center gap-6">
                <label className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={connection.encrypt}
                    onCheckedChange={(checked) => updateConnection({ encrypt: checked === true })}
                  />
                  Encrypt connection
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={connection.trustServerCertificate}
                    onCheckedChange={(checked) => updateConnection({ trustServerCertificate: checked === true })}
                  />
                  Trust server certificate
                </label>
                <div className="flex items-center gap-2">
                  <Label className="text-sm">Timeout</Label>
                  <Input
                    type="number"
                    min={1}
                    className="w-20 h-8"
                    value={connection.commandTimeoutSeconds}
                    onChange={(e) => updateConnection({ commandTimeoutSeconds: Number(e.target.value) || 30 })}
                  />
                </div>
              </div>
              {testState.message && (
                <div className={cn(
                  'rounded-md border p-3 text-sm',
                  testState.ok ? 'border-green-500/40 bg-green-500/10 text-green-700' : 'border-destructive/40 bg-destructive/10 text-destructive'
                )}>
                  {testState.message}
                </div>
              )}
              {discoverState.error && (
                <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
                  {discoverState.error}
                </div>
              )}
            </div>
            <DialogFooter className="px-6 py-4 border-t">
              <Button variant="outline" onClick={() => handleClose(false)}>Cancel</Button>
              <Button variant="outline" onClick={handleTestConnection} disabled={testState.loading}>
                {testState.loading && <RefreshCw className="h-4 w-4 mr-2 animate-spin" />}
                Test Connection
              </Button>
              <Button onClick={handleNext} disabled={discoverState.loading}>
                {discoverState.loading && <RefreshCw className="h-4 w-4 mr-2 animate-spin" />}
                Next
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader className="px-6 py-4 border-b">
              <div className="flex items-center justify-between">
                <DialogTitle>Navigator</DialogTitle>
                <Button variant="ghost" size="icon" onClick={() => handleClose(false)}>
                  <X className="h-4 w-4" />
                </Button>
              </div>
            </DialogHeader>
            <div className="flex flex-1 min-h-0 overflow-hidden">
              <div className="w-[34%] border-r flex flex-col min-h-0">
                <div className="p-3 border-b space-y-2">
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input className="pl-8 h-8" placeholder="Search tables..." value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} />
                  </div>
                  <Select value={filter} onValueChange={(value: 'all' | 'fact' | 'dimension' | 'selected') => setFilter(value)}>
                    <SelectTrigger className="h-8"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All tables</SelectItem>
                      <SelectItem value="fact">Fact</SelectItem>
                      <SelectItem value="dimension">Dimension</SelectItem>
                      <SelectItem value="selected">Selected</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <ScrollArea className="flex-1 min-h-0">
                  <div className="p-2">
                    <div className="flex items-center gap-2 px-2 py-1.5 text-sm font-medium">
                      <Server className="h-4 w-4 text-muted-foreground" />
                      <span>{connection.server}</span>
                    </div>
                    <div className="flex items-center gap-2 px-6 py-1.5 text-sm font-medium">
                      <Database className="h-4 w-4 text-muted-foreground" />
                      <span>{discovery?.database}</span>
                    </div>
                    <div className="pl-6">
                      {filteredTables.map((table) => {
                        const key = tableKey(table)
                        return (
                          <div
                            key={key}
                            className={cn(
                              'flex items-center gap-2 px-2 py-1.5 rounded-md cursor-pointer text-sm hover:bg-accent',
                              activeTable && tableKey(activeTable) === key && 'bg-primary/10',
                              selectedTables.has(key) && 'bg-accent'
                            )}
                            onClick={() => loadPreview(table)}
                          >
                            <Checkbox checked={selectedTables.has(key)} onCheckedChange={() => toggleTable(table)} onClick={(e) => e.stopPropagation()} />
                            <Table2 className="h-4 w-4 text-muted-foreground" />
                            <span className="flex-1 truncate">{key}</span>
                            <span className="text-[10px] text-muted-foreground">{inferTableKind(table)}</span>
                            {selectedTables.has(key) && <Check className="h-3 w-3 text-primary" />}
                          </div>
                        )
                      })}
                    </div>
                  </div>
                </ScrollArea>
              </div>
              <div className="flex-1 flex flex-col min-h-0">
                <div className="p-3 border-b">
                  <h3 className="font-medium text-sm">{activeTable ? tableKey(activeTable) : 'Select a table'}</h3>
                  {activeTable && <p className="text-xs text-muted-foreground">Previewing 20 rows</p>}
                </div>
                <div className="flex-1 min-h-0 p-3">
                  {previewState.loading && <div className="text-sm text-muted-foreground">Loading preview...</div>}
                  {previewState.error && <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{previewState.error}</div>}
                  {preview && (
                    <div className="h-full border rounded-lg overflow-hidden">
                      <ScrollArea className="h-full w-full">
                        <div className="min-w-max">
                          <Table>
                            <TableHeader className="sticky top-0 bg-muted z-10">
                              <TableRow>
                                {preview.columns.map((column) => (
                                  <TableHead key={column.column} className="whitespace-nowrap text-xs bg-muted">
                                    {column.column}
                                  </TableHead>
                                ))}
                              </TableRow>
                            </TableHeader>
                            <TableBody>
                              {preview.rows.map((row, rowIndex) => (
                                <TableRow key={rowIndex}>
                                  {preview.columns.map((column) => (
                                    <TableCell key={column.column} className="whitespace-nowrap text-xs">
                                      {String(row[column.column] ?? '')}
                                    </TableCell>
                                  ))}
                                </TableRow>
                              ))}
                            </TableBody>
                          </Table>
                        </div>
                        <ScrollBar orientation="horizontal" />
                      </ScrollArea>
                    </div>
                  )}
                </div>
              </div>
            </div>
            <div className="px-6 py-4 border-t flex items-center justify-between">
              <Button variant="outline" onClick={selectRelatedTables}>Select Related Tables</Button>
              <div className="flex gap-2">
                <Button onClick={handleLoad} disabled={loadState.loading}>
                  {loadState.loading && <RefreshCw className="h-4 w-4 mr-2 animate-spin" />}
                  Load
                </Button>
                <Button variant="outline" disabled>Transform Data</Button>
                <Button variant="outline" onClick={() => handleClose(false)}>Cancel</Button>
              </div>
            </div>
            {loadState.error && (
              <div className="px-6 pb-4 text-sm text-destructive">{loadState.error}</div>
            )}
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
