'use client'

import { useEffect, useState, useMemo } from 'react'
import { 
  Search, 
  Table2, 
  Hash, 
  Type, 
  Calendar, 
  ChevronRight, 
  ChevronDown,
  ChevronsUpDown,
  Check,
  GripVertical,
  Calculator,
  Sigma,
  FunctionSquare,
  Plus,
  Trash2,
} from 'lucide-react'
import { useDraggable } from '@dnd-kit/core'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { 
  type TableSchema, 
  type ColumnSchema, 
  type SelectedField, 
  type DataType,
  type CalculatedField,
} from '@/lib/schema-data'
import {
  type MetadataField,
  type MetadataMetric,
  type MetadataTable,
} from '@/lib/report-metadata-api'
import {
  CreateMetricModal,
  CreateMeasureModal,
} from './calculated-fields'
import { DerivedFieldExpressionBuilder } from './derived-field-builder'

interface SchemaPanelProps {
  selectedFields: SelectedField[]
  onAddField: (field: MetadataField) => void
  onAddMetric: (metric: MetadataMetric) => void
  calculatedFields: CalculatedField[]
  onAddCalculatedField: (field: CalculatedField) => void
  onDeleteCalculatedField: (id: string) => void
  onAddCalculatedFieldToReport: (field: CalculatedField) => void
  loadedTables: TableSchema[] | null
  metadataTables: MetadataTable[]
  metadataMetrics: MetadataMetric[]
  metadataLoading: boolean
  metadataError: string | null
}

function getDataTypeIcon(dataType: DataType) {
  switch (dataType) {
    case 'int':
    case 'tinyint':
    case 'smallint':
      return <Hash className="h-3 w-3" />
    case 'decimal':
      return <Hash className="h-3 w-3" />
    case 'date':
      return <Calendar className="h-3 w-3" />
    case 'nvarchar':
      return <Type className="h-3 w-3" />
    default:
      return <Type className="h-3 w-3" />
  }
}

function getDataTypeBadgeColor(dataType: DataType) {
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

function getFieldState(field: MetadataField) {
  const isHidden = field.isHidden === true
  const isKey = field.role === 'key'
  const isDimension = field.role === 'dimension'
  const isMeasureCandidate = field.role === 'measure_candidate'
  const isDerived = field.role === 'derived_field' || field.isDerived

  const canDragAsRow =
    !isHidden && (isDimension || isDerived || isMeasureCandidate || isKey)

  const canUseAsMetricCandidate =
    !isHidden && isMeasureCandidate

  const shouldRender = !isHidden
  const isDisabled = isHidden

  return {
    isHidden,
    isKey,
    isDimension,
    isMeasureCandidate,
    isDerived,
    canDragAsRow,
    canUseAsMetricCandidate,
    shouldRender,
    isDisabled,
  }
}

interface DraggableFieldProps {
  field: MetadataField
  isSelected: boolean
  onAddField: (field: MetadataField) => void
}

function DraggableField({ field, isSelected, onAddField }: DraggableFieldProps) {
  const state = getFieldState(field)
  const displayDataType = field.sqlDataType || field.dataType
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: field.fieldId,
    data: {
      type: 'field',
      field,
    },
    disabled: state.isDisabled,
  })

  if (!state.shouldRender) return null

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      onClick={() => {
        if (!state.isDisabled) {
          onAddField(field)
        }
      }}
      className={cn(
        'flex items-center gap-2 px-2 py-1.5 rounded-md cursor-pointer transition-colors group',
        'hover:bg-accent',
        state.isDisabled && 'cursor-not-allowed opacity-60',
        state.isMeasureCandidate && !state.isDisabled && 'text-foreground',
        isDragging && 'opacity-50',
        isSelected && 'bg-primary/10'
      )}
    >
      <GripVertical className="h-3 w-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
      {state.isMeasureCandidate ? <Sigma className="h-3 w-3 text-orange-600" /> : getDataTypeIcon(displayDataType as DataType)}
      <span className="flex-1 text-sm truncate">{field.displayName}</span>
      {state.isMeasureCandidate && (
        <Badge variant="outline" className="text-[10px] px-1.5 py-0 font-normal">
          {field.defaultAggregation && field.defaultAggregation !== 'none' ? field.defaultAggregation : 'measure'}
        </Badge>
      )}
      {state.isDerived && (
        <Badge variant="outline" className="text-[10px] px-1.5 py-0 font-normal">
          derived
        </Badge>
      )}
      <Badge 
        variant="secondary" 
        className={cn('text-[10px] px-1.5 py-0 font-normal', getDataTypeBadgeColor(displayDataType as DataType))}
      >
        {displayDataType}
      </Badge>
      {isSelected && (
        <Check className="h-3 w-3 text-primary" />
      )}
    </div>
  )
}

interface DraggableMetricProps {
  metric: MetadataMetric
  isSelected: boolean
  onAddMetric: (metric: MetadataMetric) => void
}

function DraggableMetric({ metric, isSelected, onAddMetric }: DraggableMetricProps) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: metric.metricId,
    data: {
      type: 'metric',
      metric,
    },
    disabled: !metric.isDraggable,
  })

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      onClick={() => metric.isDraggable && onAddMetric(metric)}
      className={cn(
        'flex items-center gap-2 px-2 py-1.5 rounded-md cursor-pointer transition-colors group',
        'hover:bg-accent',
        !metric.isDraggable && 'cursor-not-allowed opacity-60',
        isDragging && 'opacity-50',
        isSelected && 'bg-primary/10'
      )}
    >
      <GripVertical className="h-3 w-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
      <Sigma className="h-3 w-3" />
      <div className="flex-1 min-w-0">
        <span className="text-sm truncate block">{metric.displayName}</span>
        <span className="text-[10px] text-muted-foreground truncate block">{metric.formula}</span>
      </div>
      <Badge variant="secondary" className="text-[10px] px-1.5 py-0 font-normal">
        metric
      </Badge>
      {isSelected && (
        <Check className="h-3 w-3 text-primary" />
      )}
    </div>
  )
}

interface DraggableCalculatedFieldProps {
  field: CalculatedField
  isSelected: boolean
  onAdd: () => void
  onDelete: () => void
}

function DraggableCalculatedField({ field, isSelected, onAdd, onDelete }: DraggableCalculatedFieldProps) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: field.id,
    data: {
      type: 'calculated',
      calculatedField: field,
    },
  })

  const getIcon = () => {
    switch (field.type) {
      case 'metric':
        return <Calculator className="h-3 w-3" />
      case 'measure':
        return <Sigma className="h-3 w-3" />
      case 'derived':
        return <FunctionSquare className="h-3 w-3" />
    }
  }

  const getSubtext = () => {
    switch (field.type) {
      case 'metric':
        return field.aggregationFunction
      case 'measure':
        return `${field.aggregationFunction}(${field.sourceTable}.${field.sourceColumn})`
      case 'derived':
        return field.expression?.substring(0, 25) + (field.expression && field.expression.length > 25 ? '...' : '')
    }
  }

  const getBadgeColor = () => {
    switch (field.type) {
      case 'metric':
        return 'bg-cyan-100 text-cyan-700 dark:bg-cyan-900 dark:text-cyan-300'
      case 'measure':
        return 'bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300'
      case 'derived':
        return 'bg-pink-100 text-pink-700 dark:bg-pink-900 dark:text-pink-300'
    }
  }

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      onClick={onAdd}
      className={cn(
        'flex items-center gap-2 px-2 py-1.5 rounded-md cursor-pointer transition-colors group',
        'hover:bg-accent',
        isDragging && 'opacity-50',
        isSelected && 'bg-primary/10'
      )}
    >
      <GripVertical className="h-3 w-3 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
      {getIcon()}
      <div className="flex-1 min-w-0">
        <span className="text-sm truncate block">{field.name}</span>
        <span className="text-[10px] text-muted-foreground truncate block">{getSubtext()}</span>
      </div>
      <Badge 
        variant="secondary" 
        className={cn('text-[10px] px-1.5 py-0 font-normal capitalize', getBadgeColor())}
      >
        {field.type}
      </Badge>
      {isSelected && (
        <Check className="h-3 w-3 text-primary" />
      )}
      <Button
        variant="ghost"
        size="icon"
        className="h-5 w-5 opacity-0 group-hover:opacity-100 transition-opacity"
        onClick={(e) => {
          e.stopPropagation()
          onDelete()
        }}
      >
        <Trash2 className="h-3 w-3 text-destructive" />
      </Button>
    </div>
  )
}

interface TableTreeProps {
  table: MetadataTable
  expanded: boolean
  onToggle: () => void
  selectedFields: SelectedField[]
  onAddField: (field: MetadataField) => void
  searchQuery: string
}

function TableTree({ table, expanded, onToggle, selectedFields, onAddField, searchQuery }: TableTreeProps) {
  const filteredFields = useMemo(() => {
    const visibleFields = table.fields.filter(field => {
      const state = getFieldState(field)
      return state.shouldRender && !state.isDerived
    })

    if (!searchQuery) return visibleFields
    return visibleFields.filter(field => 
      field.displayName.toLowerCase().includes(searchQuery.toLowerCase())
    )
  }, [table.fields, searchQuery])

  if (searchQuery && filteredFields.length === 0) return null
  const isFact = table.tableType === 'fact'

  return (
    <div className="mb-1">
      <button
        onClick={onToggle}
        className={cn(
          'w-full flex items-center gap-2 px-2 py-2 rounded-md hover:bg-accent transition-colors',
          isFact && 'bg-primary/5 border border-primary/20'
        )}
      >
        {expanded ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
        )}
        <Table2 className={cn('h-4 w-4', isFact ? 'text-primary' : 'text-muted-foreground')} />
        <span className={cn('font-medium text-sm', isFact && 'text-primary')}>
          {table.displayName}
        </span>
        {isFact && (
          <Badge variant="default" className="text-[10px] px-1.5 py-0 ml-auto">
            FACT
          </Badge>
        )}
      </button>
      {expanded && (
        <div className="ml-4 mt-1 space-y-0.5">
          {filteredFields.map((field) => {
            const isSelected = selectedFields.some(
              selected => selected.kind === 'field' && selected.id === field.fieldId
            )
            return (
              <DraggableField
                key={field.fieldId}
                field={field}
                isSelected={isSelected}
                onAddField={onAddField}
              />
            )
          })}
        </div>
      )}
    </div>
  )
}

export function SchemaPanel({ 
  selectedFields, 
  onAddField, 
  onAddMetric,
  calculatedFields,
  onAddCalculatedField,
  onDeleteCalculatedField,
  onAddCalculatedFieldToReport,
  metadataTables,
  metadataMetrics,
  metadataLoading,
  metadataError,
}: SchemaPanelProps) {
  const [searchQuery, setSearchQuery] = useState('')
  const [expandedTables, setExpandedTables] = useState<Set<string>>(
    new Set()
  )
  const [expandedCalcSections, setExpandedCalcSections] = useState<Set<string>>(
    new Set(['metadataMetrics', 'metadataDerived', 'metrics', 'measures', 'derived'])
  )
  const [metricModalOpen, setMetricModalOpen] = useState(false)
  const [measureModalOpen, setMeasureModalOpen] = useState(false)
  const [derivedModalOpen, setDerivedModalOpen] = useState(false)

  useEffect(() => {
    setExpandedTables(new Set(metadataTables.map(table => table.tableId)))
  }, [metadataTables])

  useEffect(() => {
    if (process.env.NODE_ENV !== 'development') return

    metadataTables.forEach(table => {
      const renderedCount = table.fields.filter(field => !field.isHidden).length
      if (table.fields.length > 0 && renderedCount === 0) {
        console.warn(`[metadata] ${table.tableId} has fields, but all are hidden in the Data Fields panel`)
      }
      if (table.fields.length !== renderedCount) {
        console.warn(
          `[metadata] ${table.tableId}: ${table.fields.length} metadata fields, ${renderedCount} rendered after hidden filtering`
        )
      }
    })
  }, [metadataTables])

  const activeTables = metadataTables

  const toggleTable = (tableId: string) => {
    const newExpanded = new Set(expandedTables)
    if (newExpanded.has(tableId)) {
      newExpanded.delete(tableId)
    } else {
      newExpanded.add(tableId)
    }
    setExpandedTables(newExpanded)
  }

  const toggleAllTables = () => {
    if (expandedTables.size === activeTables.length) {
      setExpandedTables(new Set())
    } else {
      setExpandedTables(new Set(activeTables.map(t => t.tableId)))
    }
  }

  const toggleCalcSection = (section: string) => {
    const newExpanded = new Set(expandedCalcSections)
    if (newExpanded.has(section)) {
      newExpanded.delete(section)
    } else {
      newExpanded.add(section)
    }
    setExpandedCalcSections(newExpanded)
  }

  const filteredTables = useMemo(() => {
    if (!searchQuery) return activeTables
    return activeTables.filter(table => 
      table.displayName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      table.fields.some(field => field.displayName.toLowerCase().includes(searchQuery.toLowerCase()))
    )
  }, [searchQuery, activeTables])

  const filteredMetrics = useMemo(() => {
    const visibleMetrics = metadataMetrics.filter(metric => !metric.isHidden)
    if (!searchQuery) return visibleMetrics
    return visibleMetrics.filter(metric =>
      metric.displayName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      metric.formula.toLowerCase().includes(searchQuery.toLowerCase())
    )
  }, [metadataMetrics, searchQuery])

  const metadataDerivedFields = useMemo(() => {
    const fields = metadataTables.flatMap(table => table.fields)
      .filter(field => {
        const state = getFieldState(field)
        return state.shouldRender && state.isDerived
      })

    if (!searchQuery) return fields
    return fields.filter(field => field.displayName.toLowerCase().includes(searchQuery.toLowerCase()))
  }, [metadataTables, searchQuery])

  const metrics = calculatedFields.filter(f => f.type === 'metric')
  const measures = calculatedFields.filter(f => f.type === 'measure')
  const derived = calculatedFields.filter(f => f.type === 'derived')

  return (
    <div className="h-full flex flex-col bg-card border-r border-border overflow-hidden">
      {/* Sticky Header */}
      <div className="flex-shrink-0 p-3 border-b border-border space-y-2 bg-card z-10">
        <div className="flex items-center justify-between">
          <h2 className="font-semibold text-sm">Data Fields</h2>
          <Button 
            variant="ghost" 
            size="sm" 
            onClick={toggleAllTables}
            className="h-7 px-2 text-xs"
          >
            <ChevronsUpDown className="h-3 w-3 mr-1" />
            {expandedTables.size === activeTables.length ? 'Collapse' : 'Expand'} All
          </Button>
        </div>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search fields..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-8 h-8 text-sm"
          />
        </div>
      </div>
      
      {/* Scrollable Content */}
      <ScrollArea className="flex-1 min-h-0">
        <div className="p-2">
          {metadataLoading && (
            <div className="space-y-2 p-2">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-5/6" />
              <Skeleton className="h-8 w-4/6" />
            </div>
          )}

          {metadataError && (
            <div className="m-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-xs text-destructive">
              {metadataError}
            </div>
          )}

          {!metadataLoading && !metadataError && metadataTables.length === 0 && metadataMetrics.length === 0 && (
            <div className="m-2 rounded-md border border-dashed p-4 text-center">
              <p className="text-sm font-medium">No source connected</p>
              <p className="text-xs text-muted-foreground mt-1">Connect a source to load fields</p>
            </div>
          )}

          {/* Physical Schema Tree */}
          {!metadataLoading && !metadataError && filteredTables.map((table) => (
            <TableTree
              key={table.tableId}
              table={table}
              expanded={expandedTables.has(table.tableId)}
              onToggle={() => toggleTable(table.tableId)}
              selectedFields={selectedFields}
              onAddField={onAddField}
              searchQuery={searchQuery}
            />
          ))}

          {!metadataLoading && !metadataError && (
            <div className="border-t border-border pt-3 mt-3">
              <button
                onClick={() => toggleCalcSection('metadataMetrics')}
                className="w-full flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-accent rounded-md transition-colors"
              >
                {expandedCalcSections.has('metadataMetrics') ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <Sigma className="h-4 w-4 text-orange-600" />
                <span>Measures</span>
                <span className="text-xs">({filteredMetrics.length})</span>
              </button>
              {expandedCalcSections.has('metadataMetrics') && (
                <div className="ml-4 space-y-0.5">
                  {filteredMetrics.map((metric) => (
                    <DraggableMetric
                      key={metric.metricId}
                      metric={metric}
                      isSelected={selectedFields.some(field => field.kind === 'metric' && field.id === metric.metricId)}
                      onAddMetric={onAddMetric}
                    />
                  ))}
                </div>
              )}
            </div>
          )}

          {!metadataLoading && !metadataError && metadataDerivedFields.length > 0 && (
            <div className="border-t border-border pt-3 mt-3">
              <button
                onClick={() => toggleCalcSection('metadataDerived')}
                className="w-full flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-accent rounded-md transition-colors"
              >
                {expandedCalcSections.has('metadataDerived') ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <FunctionSquare className="h-4 w-4 text-pink-600" />
                <span>Derived Fields</span>
                <span className="text-xs">({metadataDerivedFields.length})</span>
              </button>
              {expandedCalcSections.has('metadataDerived') && (
                <div className="ml-4 space-y-0.5">
                  {metadataDerivedFields.map((field) => (
                    <DraggableField
                      key={field.fieldId}
                      field={field}
                      isSelected={selectedFields.some(selected => selected.id === field.fieldId)}
                      onAddField={onAddField}
                    />
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Calculated Fields Section */}
          <div className="border-t border-border pt-3 mt-3">
            <h3 className="font-semibold text-sm px-2 mb-2">Calculated Fields</h3>

            {/* Metrics Section */}
            <div className="mb-2">
              <button
                onClick={() => toggleCalcSection('metrics')}
                className="w-full flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-accent rounded-md transition-colors"
              >
                {expandedCalcSections.has('metrics') ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <Calculator className="h-4 w-4 text-cyan-600" />
                <span>Metrics</span>
                <span className="text-xs">({metrics.length})</span>
              </button>
              {expandedCalcSections.has('metrics') && (
                <div className="ml-4 space-y-0.5">
                  {metrics.map((field) => (
                    <DraggableCalculatedField
                      key={field.id}
                      field={field}
                      isSelected={selectedFields.some(f => f.id === field.id)}
                      onAdd={() => onAddCalculatedFieldToReport(field)}
                      onDelete={() => onDeleteCalculatedField(field.id)}
                    />
                  ))}
                  <Button
                    variant="ghost"
                    size="sm"
                    className="w-full justify-start text-xs h-7 text-muted-foreground hover:text-foreground"
                    onClick={() => setMetricModalOpen(true)}
                  >
                    <Plus className="h-3 w-3 mr-1" />
                    New Metric
                  </Button>
                </div>
              )}
            </div>

            {/* Measures Section */}
            <div className="mb-2">
              <button
                onClick={() => toggleCalcSection('measures')}
                className="w-full flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-accent rounded-md transition-colors"
              >
                {expandedCalcSections.has('measures') ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <Sigma className="h-4 w-4 text-orange-600" />
                <span>Measures</span>
                <span className="text-xs">({measures.length})</span>
              </button>
              {expandedCalcSections.has('measures') && (
                <div className="ml-4 space-y-0.5">
                  {measures.map((field) => (
                    <DraggableCalculatedField
                      key={field.id}
                      field={field}
                      isSelected={selectedFields.some(f => f.id === field.id)}
                      onAdd={() => onAddCalculatedFieldToReport(field)}
                      onDelete={() => onDeleteCalculatedField(field.id)}
                    />
                  ))}
                  <Button
                    variant="ghost"
                    size="sm"
                    className="w-full justify-start text-xs h-7 text-muted-foreground hover:text-foreground"
                    onClick={() => setMeasureModalOpen(true)}
                  >
                    <Plus className="h-3 w-3 mr-1" />
                    New Measure
                  </Button>
                </div>
              )}
            </div>

            {/* Derived Fields Section */}
            <div className="mb-2">
              <button
                onClick={() => toggleCalcSection('derived')}
                className="w-full flex items-center gap-2 px-2 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-accent rounded-md transition-colors"
              >
                {expandedCalcSections.has('derived') ? (
                  <ChevronDown className="h-4 w-4" />
                ) : (
                  <ChevronRight className="h-4 w-4" />
                )}
                <FunctionSquare className="h-4 w-4 text-pink-600" />
                <span>Derived Fields</span>
                <span className="text-xs">({derived.length})</span>
              </button>
              {expandedCalcSections.has('derived') && (
                <div className="ml-4 space-y-0.5">
                  {derived.map((field) => (
                    <DraggableCalculatedField
                      key={field.id}
                      field={field}
                      isSelected={selectedFields.some(f => f.id === field.id)}
                      onAdd={() => onAddCalculatedFieldToReport(field)}
                      onDelete={() => onDeleteCalculatedField(field.id)}
                    />
                  ))}
                  <Button
                    variant="ghost"
                    size="sm"
                    className="w-full justify-start text-xs h-7 text-muted-foreground hover:text-foreground"
                    onClick={() => setDerivedModalOpen(true)}
                  >
                    <Plus className="h-3 w-3 mr-1" />
                    New Derived Field
                  </Button>
                </div>
              )}
            </div>
          </div>
        </div>
      </ScrollArea>

      {/* Modals */}
      <CreateMetricModal
        open={metricModalOpen}
        onOpenChange={setMetricModalOpen}
        onSave={onAddCalculatedField}
      />
      <CreateMeasureModal
        open={measureModalOpen}
        onOpenChange={setMeasureModalOpen}
        onSave={onAddCalculatedField}
      />
      <DerivedFieldExpressionBuilder
        open={derivedModalOpen}
        onOpenChange={setDerivedModalOpen}
        onSave={onAddCalculatedField}
        existingMeasures={measures}
        existingDerivedFields={derived}
      />
    </div>
  )
}
