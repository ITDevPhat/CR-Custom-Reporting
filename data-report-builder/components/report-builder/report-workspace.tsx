'use client'

import { useDroppable } from '@dnd-kit/core'
import {
  SortableContext,
  useSortable,
  rectSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { 
  GripVertical, 
  X, 
  Filter, 
  ArrowUpDown, 
  ChevronDown,
  Sigma,
  FunctionSquare,
  Rows3,
  Calculator,
  Edit2,
  Trash2,
  Plus,
  Download,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { type SelectedField } from '@/lib/schema-data'
import { type AppliedFilter, type AppliedSort } from '@/lib/filter-types'
import {
  type QueryResult,
  type ReportFilterDraft,
  type ReportFilterFieldOption,
  type ReportFilterOperator,
  type ReportSortDraft,
  type VisualQueryRequest,
} from '@/lib/report-api'

interface ReportWorkspaceProps {
  selectedFields: SelectedField[]
  onRemoveField: (id: string) => void
  onUpdateField: (id: string, patch: Partial<SelectedField>) => void
  previewLimit: number
  onPreviewLimitChange: (limit: number) => void
  appliedFilters: AppliedFilter[]
  appliedSorts: AppliedSort[]
  onOpenFilterBuilder: () => void
  onOpenSortBuilder: () => void
  onRemoveFilter: (filterId: string) => void
  onRemoveSort: (sortId: string) => void
  onEditFilter: () => void
  onEditSort: () => void
  result: QueryResult | null
  isRunning: boolean
  runError: string | null
  errorSql: string | null
  reportFilters: ReportFilterDraft[]
  filterFieldOptions: ReportFilterFieldOption[]
  onAddReportFilter: () => void
  onUpdateReportFilter: (id: string, patch: Partial<ReportFilterDraft>) => void
  onRemoveReportFilter: (id: string) => void
  reportSorts: ReportSortDraft[]
  sortFieldOptions: ReportFilterFieldOption[]
  runtimePayload: VisualQueryRequest | null
  onAddReportSort: () => void
  onUpdateReportSort: (id: string, patch: Partial<ReportSortDraft>) => void
  onRemoveReportSort: (id: string) => void
  isExporting: boolean
  onExport: (format: string, enabled: boolean) => void
}

const EXPORT_FORMATS = [
  { label: 'Acrobat (PDF) file', value: 'PDF', enabled: true },
  { label: 'CSV (comma delimited)', value: 'CSV', enabled: true },
  { label: 'Excel Worksheet', value: 'XLSX', enabled: true },
  { label: 'PowerPoint Presentation', value: 'PPTX', enabled: true },
  { label: 'Rich Text Format', value: 'RTF', enabled: true },
  { label: 'TIFF file', value: 'TIFF', enabled: true },
  { label: 'Word Document', value: 'DOCX', enabled: true },
] as const

function getDataTypeBadgeColor(dataType: string) {
  switch (dataType) {
    case 'int':
    case 'tinyint':
    case 'smallint':
      return 'bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300'
    case 'decimal':
      return 'bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300'
    case 'date':
      return 'bg-amber-100 text-amber-700 dark:bg-amber-900 dark:text-amber-300'
    case 'nvarchar':
      return 'bg-purple-100 text-purple-700 dark:bg-purple-900 dark:text-purple-300'
    default:
      return 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300'
  }
}

function getKindBadgeColor(kind: SelectedField['kind']) {
  switch (kind) {
    case 'field':
    case 'column':
      return 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300'
    case 'metric':
      return 'bg-cyan-100 text-cyan-700 dark:bg-cyan-900 dark:text-cyan-300'
    case 'measure':
      return 'bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300'
    case 'derived':
      return 'bg-pink-100 text-pink-700 dark:bg-pink-900 dark:text-pink-300'
  }
}

function getKindIcon(kind: SelectedField['kind']) {
  switch (kind) {
    case 'metric':
      return <Calculator className="h-3 w-3" />
    case 'measure':
      return <Sigma className="h-3 w-3" />
    case 'derived':
      return <FunctionSquare className="h-3 w-3" />
    default:
      return null
  }
}

function getKindLabel(kind: SelectedField['kind']) {
  return kind === 'derived' ? 'calculated column' : kind
}

interface SortableFieldChipProps {
  field: SelectedField
  onRemove: (id: string) => void
  onUpdate: (id: string, patch: Partial<SelectedField>) => void
}

function SortableFieldChip({ field, onRemove, onUpdate }: SortableFieldChipProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: field.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  }

  const isAggregatableField = field.kind === 'field' || field.kind === 'column' || field.kind === 'derived'

  const getSourceInfo = () => {
    switch (field.kind) {
      case 'field':
        return `${field.tableId ?? field.tableName}.${field.id}`
      case 'column':
        return `${field.tableName}.${field.columnName}`
      case 'metric':
        return field.calculatedField?.aggregationFunction || ''
      case 'measure':
        return `${field.calculatedField?.aggregationFunction}(${field.calculatedField?.sourceTable}.${field.calculatedField?.sourceColumn})`
      case 'derived':
        return field.calculatedField?.expression || ''
    }
  }

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            ref={setNodeRef}
            style={style}
            className={cn(
              'flex min-w-0 items-center gap-2 rounded-lg border border-border bg-card px-3 py-2 shadow-sm',
              'hover:border-primary/50 transition-colors',
              isDragging && 'opacity-50 shadow-lg border-primary'
            )}
          >
            <button
              {...attributes}
              {...listeners}
              className="shrink-0 cursor-grab text-muted-foreground hover:text-foreground active:cursor-grabbing"
            >
              <GripVertical className="h-4 w-4" />
            </button>
            {getKindIcon(field.kind)}
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="truncate text-sm font-medium">{field.columnName}</span>
              <span className="truncate text-[10px] text-muted-foreground">
                {getSourceInfo()}
              </span>
            </div>
            {field.kind === 'field' || field.kind === 'column' ? (
              <Badge 
                variant="secondary" 
                className={cn('ml-1 shrink-0 px-1.5 py-0 text-[10px] font-normal', getDataTypeBadgeColor(field.dataType))}
              >
                {field.dataType}
              </Badge>
            ) : (
              <Badge 
                variant="secondary" 
                className={cn('ml-1 shrink-0 px-1.5 py-0 text-[10px] font-normal capitalize', getKindBadgeColor(field.kind))}
              >
                {getKindLabel(field.kind)}
              </Badge>
            )}
            {isAggregatableField && (
              <select
                value={field.aggregation ?? ''}
                onPointerDown={(event) => event.stopPropagation()}
                onClick={(event) => event.stopPropagation()}
                onChange={(event) => {
                  const aggregation = event.target.value as SelectedField['aggregation'] | ''
                  onUpdate(field.id, aggregation
                    ? { aggregation, placement: 'values' }
                    : { aggregation: null, placement: 'rows' })
                }}
                className="h-6 max-w-[128px] shrink-0 rounded border border-border bg-background px-1 text-[10px] text-foreground"
                aria-label={`Aggregation for ${field.columnName}`}
              >
                <option value="">Don't summarize</option>
                <option value="SUM">Sum</option>
                <option value="AVG">Average</option>
                <option value="MIN">Min</option>
                <option value="MAX">Max</option>
                <option value="COUNT">Count</option>
                <option value="COUNT_DISTINCT">Count distinct</option>
              </select>
            )}
            <button
              onClick={() => onRemove(field.id)}
              className="ml-1 shrink-0 text-muted-foreground transition-colors hover:text-destructive"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </TooltipTrigger>
        <TooltipContent side="bottom" className="max-w-[300px]">
          <div className="space-y-1">
            <p className="font-medium">{field.columnName}</p>
            <p className="text-xs text-muted-foreground">
              Type: <span className="capitalize">{getKindLabel(field.kind)}</span>
            </p>
            <p className="text-xs text-muted-foreground break-all">
              Source: {getSourceInfo()}
            </p>
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}

function DropZone({
  selectedFields,
  onRemoveField,
  onUpdateField,
}: {
  selectedFields: SelectedField[]
  onRemoveField: (id: string) => void
  onUpdateField: (id: string, patch: Partial<SelectedField>) => void
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: 'report-dropzone',
  })

  return (
    <Card>
      <CardHeader className="py-3">
        <CardTitle className="text-sm font-medium">Selected Columns</CardTitle>
      </CardHeader>
      <CardContent>
          <div
            ref={setNodeRef}
            className={cn(
            'min-h-[100px] min-w-0 max-w-full overflow-hidden rounded-lg border-2 border-dashed p-4 transition-colors',
            isOver ? 'border-primary bg-primary/5' : 'border-muted-foreground/25',
            selectedFields.length === 0 && 'flex items-center justify-center'
          )}
        >
          {selectedFields.length === 0 ? (
            <p className="text-muted-foreground text-sm text-center">
              Drag fields here to build your report
            </p>
          ) : (
            <SortableContext
              items={selectedFields.map(f => f.id)}
              strategy={rectSortingStrategy}
            >
              <div className="grid max-h-40 min-w-0 max-w-full grid-cols-[repeat(auto-fit,minmax(280px,1fr))] gap-2 overflow-y-auto overflow-x-hidden pr-1">
                {selectedFields.map((field) => (
                  <SortableFieldChip
                    key={field.id}
                    field={field}
                    onRemove={onRemoveField}
                    onUpdate={onUpdateField}
                  />
                ))}
              </div>
            </SortableContext>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function ReportPreviewTable({
  selectedFields,
  previewLimit,
  result,
  isRunning,
  isExporting,
  onExport,
}: {
  selectedFields: SelectedField[]
  previewLimit: number
  result: QueryResult | null
  isRunning: boolean
  isExporting: boolean
  onExport: (format: string, enabled: boolean) => void
}) {
  if (isRunning) {
    return (
      <Card>
        <CardHeader className="py-3">
          <CardTitle className="text-sm font-medium">Report Preview</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="h-[200px] flex items-center justify-center text-muted-foreground text-sm border rounded-lg">
            Running query...
          </div>
        </CardContent>
      </Card>
    )
  }

  if (!result) {
    return (
      <Card>
        <CardHeader className="py-3">
          <CardTitle className="text-sm font-medium">Report Preview</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="h-[200px] flex items-center justify-center text-muted-foreground text-sm border rounded-lg">
            {selectedFields.length === 0
              ? 'Select fields to preview report data'
              : 'Run report to preview result rows'}
          </div>
        </CardContent>
      </Card>
    )
  }

  return (
    <TooltipProvider>
      <Card className="min-w-0 overflow-hidden">
        <CardHeader className="py-3 flex flex-row items-center justify-between min-w-0">
          <CardTitle className="text-sm font-medium">Report Preview</CardTitle>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" disabled={isExporting} className="h-8 gap-1 px-2">
                <Download className="h-3.5 w-3.5" />
                <ChevronDown className="h-3.5 w-3.5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {EXPORT_FORMATS.map((format) => (
                <DropdownMenuItem
                  key={format.value}
                  disabled={isExporting}
                  onClick={() => onExport(format.value, format.enabled)}
                  className={cn(!format.enabled && 'opacity-50')}
                >
                  {format.label}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </CardHeader>
        <CardContent className="min-w-0 overflow-hidden p-0">
          <div className="mx-4 mb-4 min-w-0 max-w-full overflow-hidden rounded-lg border">
            <div className="max-h-[300px] max-w-full overflow-auto">
              <div className="min-w-max">
                <Table className="w-max min-w-full">
                  <TableHeader className="sticky top-0 bg-muted z-10">
                    <TableRow>
                      {result.columns.map((column) => (
                        <Tooltip key={column.name}>
                          <TooltipTrigger asChild>
                            <TableHead className="font-semibold whitespace-nowrap cursor-help bg-muted">
                              <div className="flex flex-col gap-0.5">
                                <div className="flex items-center gap-1">
                                  <span>{column.name}</span>
                                </div>
                                <span className="text-[10px] font-normal text-muted-foreground">
                                  {column.type}
                                </span>
                              </div>
                            </TableHead>
                          </TooltipTrigger>
                          <TooltipContent side="bottom" className="max-w-[300px]">
                            <p className="text-xs break-all">{column.name} ({column.type})</p>
                          </TooltipContent>
                        </Tooltip>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {result.rows.map((row, rowIndex) => (
                      <TableRow key={rowIndex}>
                        {result.columns.map((column) => (
                          <TableCell key={column.name} className="whitespace-nowrap">
                            {String(row[column.name] ?? '')}
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                    {result.rows.length === 0 && (
                      <TableRow>
                        <TableCell colSpan={result.columns.length || 1} className="h-20 text-center text-muted-foreground">
                          No rows returned
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            </div>
          </div>
          <div className="flex min-w-0 items-center justify-between gap-3 border-t bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
            <span>Rows: {result.metadata.rowCount} of max {previewLimit}</span>
            <span>Execution: {result.metadata.executionMs} ms</span>
          </div>
        </CardContent>
      </Card>
    </TooltipProvider>
  )
}

interface ConfigurationSidebarProps {
  previewLimit: number
  onPreviewLimitChange: (limit: number) => void
  appliedFilters: AppliedFilter[]
  appliedSorts: AppliedSort[]
  onOpenFilterBuilder: () => void
  onOpenSortBuilder: () => void
  onRemoveFilter: (filterId: string) => void
  onRemoveSort: (sortId: string) => void
  onEditFilter: () => void
  onEditSort: () => void
  result: QueryResult | null
  runError: string | null
  errorSql: string | null
  reportFilters: ReportFilterDraft[]
  filterFieldOptions: ReportFilterFieldOption[]
  onAddReportFilter: () => void
  onUpdateReportFilter: (id: string, patch: Partial<ReportFilterDraft>) => void
  onRemoveReportFilter: (id: string) => void
  reportSorts: ReportSortDraft[]
  sortFieldOptions: ReportFilterFieldOption[]
  runtimePayload: VisualQueryRequest | null
  onAddReportSort: () => void
  onUpdateReportSort: (id: string, patch: Partial<ReportSortDraft>) => void
  onRemoveReportSort: (id: string) => void
}

const filterOperators: ReportFilterOperator[] = [
  '=',
  '!=',
  '>',
  '<',
  '>=',
  '<=',
  'IN',
  'BETWEEN',
  'CONTAINS',
]

function getCompiledSql(result: QueryResult | null, errorSql: string | null) {
  return result?.metadata?.sql || errorSql
}

function ConfigurationSidebar({ 
  previewLimit, 
  onPreviewLimitChange,
  appliedFilters,
  appliedSorts,
  onOpenFilterBuilder,
  onOpenSortBuilder,
  onRemoveFilter,
  onRemoveSort,
  onEditFilter,
  onEditSort,
  result,
  runError,
  errorSql,
  reportFilters,
  filterFieldOptions,
  onAddReportFilter,
  onUpdateReportFilter,
  onRemoveReportFilter,
  reportSorts,
  sortFieldOptions,
  runtimePayload,
  onAddReportSort,
  onUpdateReportSort,
  onRemoveReportSort,
}: ConfigurationSidebarProps) {
  const filterCount = reportFilters.filter(f => f.field && f.value).length
  const sortCount = reportSorts.filter(sort => sort.field).length
  const compiledSql = getCompiledSql(result, errorSql)

  return (
    <Card>
      <Collapsible defaultOpen={true}>
        <CollapsibleTrigger asChild>
          <CardHeader className="py-3 cursor-pointer hover:bg-accent/50 transition-colors">
            <div className="flex items-center justify-between">
              <CardTitle className="text-sm font-medium">Report Configuration</CardTitle>
              <ChevronDown className="h-4 w-4 text-muted-foreground" />
            </div>
          </CardHeader>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <CardContent className="space-y-4">
            {/* Filters */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
                  <Filter className="h-4 w-4" />
                  Filters
                  {filterCount > 0 && (
                    <Badge variant="secondary" className="text-[10px] px-1.5 py-0">
                      {filterCount}
                    </Badge>
                  )}
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 px-2 text-xs"
                  onClick={onAddReportFilter}
                >
                  <Plus className="h-3 w-3 mr-1" />
                  Add Filter
                </Button>
              </div>
              <div className="space-y-2 p-3 border rounded-md bg-muted/30">
                {reportFilters.length === 0 && (
                  <p className="text-xs text-muted-foreground text-center">No filters applied</p>
                )}

                {reportFilters.map((filter) => (
                  <div key={filter.id} className="grid grid-cols-[1fr_84px_1fr_auto] gap-2 items-center">
                    <select
                      value={filter.field}
                      onChange={(event) => onUpdateReportFilter(filter.id, { field: event.target.value })}
                      className="h-8 rounded-md border bg-background px-2 text-xs"
                    >
                      <option value="">Field</option>
                      {filterFieldOptions.map((option) => (
                        <option key={option.fieldId} value={option.fieldId}>
                          {option.label}
                        </option>
                      ))}
                    </select>

                    <select
                      value={filter.operator}
                      onChange={(event) => onUpdateReportFilter(filter.id, {
                        operator: event.target.value as ReportFilterOperator,
                      })}
                      className="h-8 rounded-md border bg-background px-2 text-xs"
                    >
                      {filterOperators.map((operator) => (
                        <option key={operator} value={operator}>
                          {operator}
                        </option>
                      ))}
                    </select>

                    {filter.operator === 'BETWEEN' ? (
                      <div className="grid grid-cols-2 gap-2">
                        <input
                          value={filter.value}
                          onChange={(event) => onUpdateReportFilter(filter.id, { value: event.target.value })}
                          placeholder="From"
                          className="h-8 rounded-md border bg-background px-2 text-xs"
                        />
                        <input
                          value={filter.valueTo ?? ''}
                          onChange={(event) => onUpdateReportFilter(filter.id, { valueTo: event.target.value })}
                          placeholder="To"
                          className="h-8 rounded-md border bg-background px-2 text-xs"
                        />
                      </div>
                    ) : (
                      <input
                        value={filter.value}
                        onChange={(event) => onUpdateReportFilter(filter.id, { value: event.target.value })}
                        placeholder={filter.operator === 'IN' ? 'A, B, C' : 'Value'}
                        className="h-8 rounded-md border bg-background px-2 text-xs"
                      />
                    )}

                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-8 w-8 p-0 text-destructive hover:text-destructive"
                      onClick={() => onRemoveReportFilter(filter.id)}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </div>
                ))}

                {reportFilters.length > 0 && (
                  <p className="text-[10px] text-muted-foreground">
                    Use comma-separated values for IN. BETWEEN uses From and To.
                  </p>
                )}
              </div>
            </div>

            {/* Sort */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
                  <ArrowUpDown className="h-4 w-4" />
                  Sort
                  {sortCount > 0 && (
                    <Badge variant="secondary" className="text-[10px] px-1.5 py-0">
                      {sortCount}
                    </Badge>
                  )}
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-7 px-2 text-xs"
                  onClick={onAddReportSort}
                >
                  <Plus className="h-3 w-3 mr-1" />
                  Add Sort
                </Button>
              </div>
              <div className="space-y-2 p-3 border rounded-md bg-muted/30">
                {reportSorts.length === 0 && (
                  <p className="text-xs text-muted-foreground text-center">No sorting applied</p>
                )}

                {reportSorts.map((sort) => (
                  <div key={sort.id} className="grid grid-cols-[1fr_90px_auto] gap-2 items-center">
                    <select
                      value={sort.field}
                      onChange={(event) => onUpdateReportSort(sort.id, { field: event.target.value })}
                      className="h-8 rounded-md border bg-background px-2 text-xs"
                    >
                      <option value="">Field</option>
                      {sortFieldOptions.map((option) => (
                        <option key={option.fieldId} value={option.fieldId}>
                          {option.label}
                        </option>
                      ))}
                    </select>

                    <select
                      value={sort.direction}
                      onChange={(event) => onUpdateReportSort(sort.id, {
                        direction: event.target.value as 'ASC' | 'DESC',
                      })}
                      className="h-8 rounded-md border bg-background px-2 text-xs"
                    >
                      <option value="ASC">ASC</option>
                      <option value="DESC">DESC</option>
                    </select>

                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-8 w-8 p-0 text-destructive hover:text-destructive"
                      onClick={() => onRemoveReportSort(sort.id)}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </div>
                ))}

                {reportSorts.length > 0 && (
                  <p className="text-[10px] text-muted-foreground">
                    Sort fields are sent as semantic IDs and resolved by the backend.
                  </p>
                )}
              </div>
            </div>

            {/* Preview Limit */}
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
                <Rows3 className="h-4 w-4" />
                Preview Limit
              </div>
              <div className="p-3 border rounded-md bg-muted/30">
                <div className="flex items-center gap-2">
                  <label htmlFor="previewLimit" className="text-xs text-muted-foreground">
                    Preview rows:
                  </label>
                  <input
                    id="previewLimit"
                    type="number"
                    min={1}
                    max={1000}
                    value={previewLimit}
                    onChange={(e) => onPreviewLimitChange(Math.max(1, parseInt(e.target.value) || 50))}
                    className="w-20 h-7 px-2 text-xs border rounded-md bg-background"
                  />
                </div>
                <p className="text-[10px] text-muted-foreground text-center mt-2">
                  Limit rows shown in preview.
                </p>
              </div>
            </div>

            {runError && (
              <div className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-xs text-destructive">
                {runError}
              </div>
            )}

            {result?.metadata?.warnings && result.metadata.warnings.length > 0 && (
              <div className="rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-xs text-amber-700 space-y-1">
                {result.metadata.warnings.map((warning) => (
                  <div key={`${warning.code}-${warning.message}`}>{warning.code}: {warning.message}</div>
                ))}
              </div>
            )}

            {compiledSql && (
              <pre className="max-h-80 overflow-auto rounded-md border bg-muted p-4 text-xs">
                {compiledSql}
              </pre>
            )}

            {runtimePayload && (
              <pre className="max-h-80 overflow-auto rounded-md border bg-muted p-4 text-xs">
                {JSON.stringify(runtimePayload, null, 2)}
              </pre>
            )}
          </CardContent>
        </CollapsibleContent>
      </Collapsible>
    </Card>
  )
}

export function ReportWorkspace({
  selectedFields, 
  onRemoveField, 
  onUpdateField,
  previewLimit, 
  onPreviewLimitChange,
  appliedFilters,
  appliedSorts,
  onOpenFilterBuilder,
  onOpenSortBuilder,
  onRemoveFilter,
  onRemoveSort,
  onEditFilter,
  onEditSort,
  result,
  isRunning,
  runError,
  errorSql,
  reportFilters,
  filterFieldOptions,
  onAddReportFilter,
  onUpdateReportFilter,
  onRemoveReportFilter,
  reportSorts,
  sortFieldOptions,
  runtimePayload,
  onAddReportSort,
  onUpdateReportSort,
  onRemoveReportSort,
  isExporting,
  onExport,
}: ReportWorkspaceProps) {
  return (
    <div className="h-full flex flex-col bg-muted/30 overflow-hidden">
      <ScrollArea className="flex-1 min-h-0 min-w-0">
        <div className="min-w-0 max-w-full space-y-4 overflow-hidden p-4">
          <DropZone selectedFields={selectedFields} onRemoveField={onRemoveField} onUpdateField={onUpdateField} />
        <ReportPreviewTable
          selectedFields={selectedFields}
          previewLimit={previewLimit}
          result={result}
          isRunning={isRunning}
          isExporting={isExporting}
          onExport={onExport}
        />
          <ConfigurationSidebar 
            previewLimit={previewLimit} 
            onPreviewLimitChange={onPreviewLimitChange}
            appliedFilters={appliedFilters}
            appliedSorts={appliedSorts}
            onOpenFilterBuilder={onOpenFilterBuilder}
            onOpenSortBuilder={onOpenSortBuilder}
            onRemoveFilter={onRemoveFilter}
            onRemoveSort={onRemoveSort}
            onEditFilter={onEditFilter}
            onEditSort={onEditSort}
            result={result}
            runError={runError}
            errorSql={errorSql}
            reportFilters={reportFilters}
            filterFieldOptions={filterFieldOptions}
            onAddReportFilter={onAddReportFilter}
            onUpdateReportFilter={onUpdateReportFilter}
            onRemoveReportFilter={onRemoveReportFilter}
            reportSorts={reportSorts}
            sortFieldOptions={sortFieldOptions}
            runtimePayload={runtimePayload}
            onAddReportSort={onAddReportSort}
            onUpdateReportSort={onUpdateReportSort}
            onRemoveReportSort={onRemoveReportSort}
          />
        </div>
      </ScrollArea>
    </div>
  )
}
