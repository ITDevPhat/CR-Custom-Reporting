'use client'

import { useEffect, useMemo, useState } from 'react'
import { AlertTriangle, Edit, Plus, RefreshCw, Search, Trash2, Zap } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ScrollArea, ScrollBar } from '@/components/ui/scroll-area'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { previewTable, type SqlServerConnectionRequest, type TablePreviewResponse } from '@/lib/connections-api'
import { type DatasetMetadataResponse, type MetadataRelationship, type MetadataTable } from '@/lib/report-metadata-api'
import {
  autodetectRelationships,
  activateRelationship,
  createRelationship,
  deleteRelationship,
  getRelationships,
  updateRelationship,
  type RelationshipRequest,
} from '@/lib/relationships-api'

interface ManageRelationshipsModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  datasetId: string
  metadata: DatasetMetadataResponse | null
  connection: SqlServerConnectionRequest | null
  onRelationshipsChanged: () => void
}

type Cardinality = RelationshipRequest['cardinality']
type CrossFilterDirection = RelationshipRequest['crossFilterDirection']

function toPhysicalName(tableId: string) {
  const parts = tableId.split('.')
  return {
    schema: parts.length > 1 ? parts[0] : 'dbo',
    table: parts.length > 1 ? parts.slice(1).join('.') : tableId,
  }
}

function sourceLabel(source: string) {
  if (source === 'database_fk') return 'FK'
  if (source === 'inferred') return 'Inferred'
  return 'Manual'
}

function defaultRelationship(tables: MetadataTable[]): RelationshipRequest {
  const fromTable = tables.find((table) => table.tableType === 'fact') ?? tables[0]
  const toTable = tables.find((table) => table.tableType === 'dimension' && table.tableId !== fromTable?.tableId) ?? tables[1] ?? fromTable
  return {
    fromTableId: fromTable?.tableId ?? '',
    fromColumn: fromTable?.fields[0]?.displayName ?? '',
    toTableId: toTable?.tableId ?? '',
    toColumn: toTable?.fields[0]?.displayName ?? '',
    cardinality: 'N:1',
    joinType: 'INNER',
    crossFilterDirection: 'single',
    isActive: true,
    isPrimary: true,
  }
}

function fromRelationship(rel: MetadataRelationship): RelationshipRequest {
  return {
    fromTableId: rel.fromTableId,
    fromColumn: rel.fromColumn,
    toTableId: rel.toTableId,
    toColumn: rel.toColumn,
    cardinality: rel.cardinality as Cardinality,
    joinType: rel.joinType as 'INNER' | 'LEFT',
    crossFilterDirection: rel.crossFilterDirection as CrossFilterDirection,
    isActive: rel.isActive,
    isPrimary: rel.isPrimary,
  }
}

function RelationshipEditor({
  open,
  onOpenChange,
  metadata,
  connection,
  editing,
  onSave,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  metadata: DatasetMetadataResponse
  connection: SqlServerConnectionRequest | null
  editing: MetadataRelationship | null
  onSave: (request: RelationshipRequest) => Promise<void>
}) {
  const [draft, setDraft] = useState<RelationshipRequest>(() => editing ? fromRelationship(editing) : defaultRelationship(metadata.tables))
  const [saving, setSaving] = useState(false)
  const [fromPreview, setFromPreview] = useState<TablePreviewResponse | null>(null)
  const [toPreview, setToPreview] = useState<TablePreviewResponse | null>(null)

  useEffect(() => {
    setDraft(editing ? fromRelationship(editing) : defaultRelationship(metadata.tables))
  }, [editing, metadata])

  const fromTable = metadata.tables.find((table) => table.tableId === draft.fromTableId)
  const toTable = metadata.tables.find((table) => table.tableId === draft.toTableId)

  useEffect(() => {
    async function load() {
      if (!connection || !open) return
      try {
        const from = toPhysicalName(draft.fromTableId)
        const to = toPhysicalName(draft.toTableId)
        const [fromRows, toRows] = await Promise.all([
          previewTable(connection, from.schema, from.table, 5),
          previewTable(connection, to.schema, to.table, 5),
        ])
        setFromPreview(fromRows)
        setToPreview(toRows)
      } catch {
        setFromPreview(null)
        setToPreview(null)
      }
    }
    void load()
  }, [connection, draft.fromTableId, draft.toTableId, open])

  const updateTable = (side: 'from' | 'to', tableId: string) => {
    const table = metadata.tables.find((item) => item.tableId === tableId)
    const firstColumn = table?.fields[0]?.displayName ?? ''
    setDraft((current) => side === 'from'
      ? { ...current, fromTableId: tableId, fromColumn: firstColumn }
      : { ...current, toTableId: tableId, toColumn: firstColumn })
  }

  const previewGrid = (table: MetadataTable | undefined, preview: TablePreviewResponse | null, selectedColumn: string, onColumn: (column: string) => void) => (
    <div className="border rounded-lg overflow-hidden">
      <ScrollArea className="max-h-[170px]">
        <div className="min-w-max">
          <Table>
            <TableHeader className="sticky top-0 bg-muted z-10">
              <TableRow>
                {(preview?.columns.map((c) => c.column) ?? table?.fields.map((f) => f.displayName) ?? []).map((column) => (
                  <TableHead
                    key={column}
                    onClick={() => onColumn(column)}
                    className={cn('cursor-pointer text-xs whitespace-nowrap bg-muted', selectedColumn === column && 'bg-primary/15 border-primary border')}
                  >
                    {column}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {(preview?.rows ?? []).map((row, index) => (
                <TableRow key={index}>
                  {preview?.columns.map((column) => (
                    <TableCell
                      key={column.column}
                      onClick={() => onColumn(column.column)}
                      className={cn('text-xs whitespace-nowrap cursor-pointer', selectedColumn === column.column && 'bg-primary/10')}
                    >
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
  )

  const handleSave = async () => {
    if (!draft.fromTableId || !draft.fromColumn || !draft.toTableId || !draft.toColumn) {
      toast.error('Choose both tables and columns')
      return
    }
    try {
      setSaving(true)
      await onSave(draft)
      onOpenChange(false)
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="!w-[60vw] !max-w-none h-[92vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className="px-6 py-4 border-b">
          <DialogTitle>{editing ? 'Edit relationship' : 'New relationship'}</DialogTitle>
        </DialogHeader>
        <ScrollArea className="flex-1 min-h-0">
          <div className="p-6 space-y-6">
            <div className="space-y-3">
              <Label>From table</Label>
              <Select value={draft.fromTableId} onValueChange={(value) => updateTable('from', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{metadata.tables.map((table) => <SelectItem key={table.tableId} value={table.tableId}>{table.displayName}</SelectItem>)}</SelectContent>
              </Select>
              {previewGrid(fromTable, fromPreview, draft.fromColumn, (column) => setDraft((current) => ({ ...current, fromColumn: column })))}
            </div>
            <div className="space-y-3">
              <Label>To table</Label>
              <Select value={draft.toTableId} onValueChange={(value) => updateTable('to', value)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{metadata.tables.map((table) => <SelectItem key={table.tableId} value={table.tableId}>{table.displayName}</SelectItem>)}</SelectContent>
              </Select>
              {previewGrid(toTable, toPreview, draft.toColumn, (column) => setDraft((current) => ({ ...current, toColumn: column })))}
            </div>
            <div className="grid grid-cols-3 gap-4">
              <div className="space-y-2">
                <Label>Cardinality</Label>
                <Select value={draft.cardinality} onValueChange={(value: Cardinality) => setDraft((current) => ({ ...current, cardinality: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1:1">One to one (1:1)</SelectItem>
                    <SelectItem value="1:N">One to many (1:N)</SelectItem>
                    <SelectItem value="N:1">Many to one (N:1)</SelectItem>
                    <SelectItem value="N:N">Many to many (N:N)</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>Cross-filter</Label>
                <Select value={draft.crossFilterDirection} onValueChange={(value: CrossFilterDirection) => setDraft((current) => ({ ...current, crossFilterDirection: value }))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="single">Single</SelectItem>
                    <SelectItem value="both">Both</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-3 pt-7">
                <label className="flex items-center gap-2 text-sm"><Checkbox checked={draft.isActive} onCheckedChange={(checked) => setDraft((current) => ({ ...current, isActive: checked === true, isPrimary: checked === true }))} /> Make this relationship active</label>
              </div>
            </div>
          </div>
        </ScrollArea>
        <div className="px-6 py-4 border-t flex justify-end gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={handleSave} disabled={saving}>{saving && <RefreshCw className="h-4 w-4 mr-2 animate-spin" />}Save</Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}

export function ManageRelationshipsModal({
  open,
  onOpenChange,
  datasetId,
  metadata,
  connection,
  onRelationshipsChanged,
}: ManageRelationshipsModalProps) {
  const [relationships, setRelationships] = useState<MetadataRelationship[]>([])
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [editing, setEditing] = useState<MetadataRelationship | null>(null)
  const [editorOpen, setEditorOpen] = useState(false)

  const loadRelationships = async () => {
    if (!open) return
    try {
      setLoading(true)
      setRelationships(await getRelationships(datasetId))
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to load relationships')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadRelationships()
  }, [open, datasetId])

  const filtered = useMemo(() => {
    if (!search) return relationships
    const term = search.toLowerCase()
    return relationships.filter((relationship) =>
      Object.values(relationship).some((value) => String(value ?? '').toLowerCase().includes(term)))
  }, [relationships, search])
  const groupedConflicts = useMemo(() => relationships.filter(r => r.groupConflictCount > 1), [relationships])

  const refreshAll = async () => {
    await loadRelationships()
    onRelationshipsChanged()
  }

  const handleAutodetect = async () => {
    try {
      setLoading(true)
      const response = await autodetectRelationships(datasetId)
      toast.success(`Detected ${response.summary.detected} relationships: ${response.summary.databaseForeignKeys} FK, ${response.summary.inferredByName} inferred`)
      response.summary.warnings.forEach((warning) => toast.warning(warning))
      await refreshAll()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Autodetect failed')
    } finally {
      setLoading(false)
    }
  }

  const handleDelete = async () => {
    try {
      await Promise.all(Array.from(selectedIds).map((id) => deleteRelationship(datasetId, id)))
      toast.success(`Deleted ${selectedIds.size} relationship(s)`)
      setSelectedIds(new Set())
      await refreshAll()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Delete failed')
    }
  }

  const handleSave = async (request: RelationshipRequest) => {
    try {
      if (editing) await updateRelationship(datasetId, editing.relationshipId, request)
      else await createRelationship(datasetId, request)
      toast.success(editing ? 'Relationship updated' : 'Relationship created')
      setEditing(null)
      await refreshAll()
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Save failed')
      throw err
    }
  }
  const handleMakeActive = async (rel: MetadataRelationship) => {
    const currentActive = relationships.find(r => r.relationshipGroupKey === rel.relationshipGroupKey && r.isActive)
    if (currentActive && currentActive.relationshipId !== rel.relationshipId) {
      const ok = window.confirm(`Only one active relationship is allowed between ${rel.fromTableId} and ${rel.toTableId}. Activating this relationship will deactivate the current active relationship: ${currentActive.fromColumn} -> ${currentActive.toColumn}. Continue?`)
      if (!ok) return
    }
    await activateRelationship(datasetId, rel.relationshipId)
    toast.success('Relationship activated')
    await refreshAll()
  }

  const selectedOne = relationships.find((rel) => selectedIds.has(rel.relationshipId)) ?? null

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="!w-[60vw] !max-w-none h-[92vh] overflow-hidden flex flex-col p-0">
          <DialogHeader className="px-6 py-4">
            <DialogTitle>Manage relationships</DialogTitle>
          </DialogHeader>
          <div className="flex items-center gap-2 px-6 py-2 border-y">
            <Button size="sm" disabled={!metadata} onClick={() => { setEditing(null); setEditorOpen(true) }}><Plus className="h-4 w-4 mr-1" />New relationship</Button>
            <Button variant="outline" size="sm" onClick={handleAutodetect} disabled={loading}><Zap className="h-4 w-4 mr-1" />Autodetect</Button>
            <Button variant="outline" size="sm" disabled={selectedIds.size !== 1} onClick={() => { setEditing(selectedOne); setEditorOpen(true) }}><Edit className="h-4 w-4 mr-1" />Edit</Button>
            <Button variant="outline" size="sm" disabled={selectedIds.size === 0} onClick={handleDelete}><Trash2 className="h-4 w-4 mr-1" />Delete</Button>
            <div className="relative ml-auto w-64">
              <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input className="pl-8 h-8" placeholder="Filter relationships..." value={search} onChange={(e) => setSearch(e.target.value)} />
            </div>
          </div>
          {groupedConflicts.length > 0 && (
            <div className="mx-6 my-3 p-3 border rounded-md bg-amber-50 text-amber-900 text-sm">
              <div className="font-medium">Role-playing relationships detected. Only one active relationship can be used as the default path for each table pair.</div>
            </div>
          )}
          <ScrollArea className="flex-1 min-h-0 px-6">
            <Table>
              <TableHeader className="sticky top-0 bg-background z-10">
                <TableRow>
                  <TableHead className="w-10 bg-background"><Checkbox checked={selectedIds.size === relationships.length && relationships.length > 0} onCheckedChange={() => setSelectedIds(selectedIds.size === relationships.length ? new Set() : new Set(relationships.map((r) => r.relationshipId)))} /></TableHead>
                  <TableHead className="bg-background">From</TableHead>
                  <TableHead className="bg-background">Relationship</TableHead>
                  <TableHead className="bg-background">To</TableHead>
                  <TableHead className="bg-background">Status</TableHead>
                  <TableHead className="bg-background">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((rel) => (
                  <TableRow key={rel.relationshipId} className={cn(selectedIds.has(rel.relationshipId) && 'bg-accent/50')}>
                    <TableCell><Checkbox checked={selectedIds.has(rel.relationshipId)} onCheckedChange={() => setSelectedIds((current) => { const next = new Set(current); if (next.has(rel.relationshipId)) next.delete(rel.relationshipId); else next.add(rel.relationshipId); return next })} /></TableCell>
                    <TableCell><div className="font-medium">{rel.fromTableId}</div><div className="text-xs text-muted-foreground">({rel.fromColumn})</div></TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Badge variant="outline">{rel.cardinality}</Badge>
                        <Badge variant="secondary">{sourceLabel(rel.source)}</Badge>
                        {rel.source === 'inferred' && <span className="text-xs text-muted-foreground">{Math.round(rel.confidence * 100)}%</span>}
                        {rel.warning && <AlertTriangle className="h-4 w-4 text-amber-600" />}
                      </div>
                      {rel.warning && <div className="text-[10px] text-amber-700 mt-1">{rel.warning}</div>}
                    </TableCell>
                    <TableCell><div className="font-medium">{rel.toTableId}</div><div className="text-xs text-muted-foreground">({rel.toColumn})</div></TableCell>
                    <TableCell>
                      <Badge className={cn('text-[10px]', rel.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-700')}>
                        {rel.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      <Button size="sm" variant="outline" disabled={rel.isActive} onClick={() => void handleMakeActive(rel)}>
                        {rel.isActive ? 'Already active' : 'Make active'}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </ScrollArea>
          <div className="px-6 py-3 border-t flex justify-between bg-muted/30">
            <span className="text-sm text-muted-foreground">{relationships.length} relationship(s) • {selectedIds.size} selected</span>
            <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
          </div>
        </DialogContent>
      </Dialog>
      {metadata && (
        <RelationshipEditor
          open={editorOpen}
          onOpenChange={(next) => { setEditorOpen(next); if (!next) setEditing(null) }}
          metadata={metadata}
          connection={connection}
          editing={editing}
          onSave={handleSave}
        />
      )}
    </>
  )
}
