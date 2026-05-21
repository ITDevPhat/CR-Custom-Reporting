'use client'

import { useState, useMemo, useCallback, useEffect } from 'react'
import {
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  PointerSensor,
  useSensor,
  useSensors,
  useDroppable,
  closestCenter,
} from '@dnd-kit/core'
import {
  SortableContext,
  useSortable,
  horizontalListSortingStrategy,
  arrayMove,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useDraggable } from '@dnd-kit/core'
import {
  FunctionSquare,
  Search,
  Hash,
  Trash2,
  X,
  Copy,
  GripVertical,
  ChevronDown,
  ChevronRight,
  Table2,
  Parentheses,
} from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { cn } from '@/lib/utils'
import { toast } from 'sonner'
import {
  type CalculatedField,
} from '@/lib/schema-data'
import { type DatasetMetadataResponse } from '@/lib/report-metadata-api'
import { getFieldsForDerivedExpression } from '@/lib/metadata-selectors'
import { validateExpression, createMetric, createDerivedField } from '@/lib/semantic-management-api'

// Token types for the expression builder
export type ExpressionTokenType = 'field' | 'metric' | 'operator' | 'number' | 'string' | 'paren' | 'function' | 'column' | 'measure' | 'derived' | 'constant'
type ArithmeticOperator = '+' | '-' | '*' | '/' | '>' | '<' | '>=' | '<=' | '=' | 'AND' | 'OR'
type ExpressionToken = {
  id: string
  kind?: 'field' | 'metric' | 'operator' | 'number' | 'string' | 'paren' | 'function'
  fieldId?: string
  metricId?: string
  displayName?: string
  tableId?: string
  dataType?: string
  semanticType?: string
  role?: string
  formula?: string
  baseTableId?: string
  aggregationBehavior?: string
  operator?: ArithmeticOperator
  value?: string | number
  name?: string
  type?: ExpressionTokenType
  displayLabel?: string
  tableName?: string
  columnName?: string
}

const AGGREGATE_PATTERN = /\b(SUM|AVG|COUNT|COUNT_DISTINCT|MIN|MAX)\s*\(/i
const METRIC_TOKEN_PATTERN = /\[\s*metric\./i

const AGGREGATE_FUNCTIONS = new Set(['SUM', 'AVG', 'COUNT', 'COUNT_DISTINCT', 'MIN', 'MAX'])
const createId = () => `token-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`
function toSemanticToken(item: { metricId?: string; fieldId?: string; kind?: string; type?: string }) {
  if (item.metricId) return `[${item.metricId}]`
  if (item.fieldId) return `[${item.fieldId}]`
  return ''
}
const serializeExpressionToken = (t: ExpressionToken) => {
  switch (t.kind) {
    case 'field': return `[${t.fieldId}]`
    case 'metric': return `[${t.metricId}]`
    case 'operator': return t.operator ?? String(t.value ?? '')
    case 'number': return `${t.value}`
    case 'string': return `'${String(t.value ?? '').replaceAll("'", "''")}'`
    case 'paren': return String(t.value ?? '')
    case 'function': return t.name
  }
}

// Operators available for dragging
const operators = [
  { symbol: '+', label: 'Add' },
  { symbol: '-', label: 'Subtract' },
  { symbol: '*', label: 'Multiply' },
  { symbol: '/', label: 'Divide' },
  { symbol: '(', label: 'Open Paren' },
  { symbol: ')', label: 'Close Paren' },
]

const comparisonOperators = [
  { symbol: '>', label: 'Greater Than' },
  { symbol: '<', label: 'Less Than' },
  { symbol: '>=', label: 'Greater Equal' },
  { symbol: '<=', label: 'Less Equal' },
  { symbol: '=', label: 'Equal' },
]

const logicalOperators = [
  { symbol: 'AND', label: 'And' },
  { symbol: 'OR', label: 'Or' },
]


interface DerivedFieldExpressionBuilderProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (field: CalculatedField) => void
  existingMeasures: CalculatedField[]
  existingDerivedFields: CalculatedField[]
  metadata: DatasetMetadataResponse | null
}

// Draggable palette item component
function DraggablePaletteItem({
  id,
  type,
  label,
  sublabel,
  badge,
  badgeColor,
  tokenValue,
  token,
  onSelect,
}: {
  id: string
  type: ExpressionTokenType
  label: string
  sublabel?: string
  badge?: string
  badgeColor?: string
  tokenValue?: string
  token?: Partial<ExpressionToken>
  onSelect?: (token: ExpressionToken) => void
}) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `palette-${id}`,
    data: {
      type: 'palette-item',
      kind: type === 'column' ? 'field' : type === 'measure' ? 'metric' : type === 'operator' ? 'operator' : undefined,
      fieldId: type === 'column' ? label : undefined,
      metricId: type === 'measure' ? id : undefined,
      displayName: label,
      tableId: sublabel,
      value: tokenValue ?? label,
    },
  })
  const handleInsert = () => {
    if (type === 'operator') {
      const value = (tokenValue ?? label) as ArithmeticOperator | '(' | ')'
      onSelect?.(value === '(' || value === ')' ? { id: createId(), kind: 'paren', value } : { id: createId(), kind: 'operator', operator: value as ArithmeticOperator })
      return
    }
    if (type === 'column') {
      onSelect?.({ id: createId(), kind: 'field', fieldId: id, displayName: label, tableId: sublabel, ...token })
      return
    }
    if (type === 'measure') {
      onSelect?.({ id: createId(), kind: 'metric', metricId: id, displayName: label, ...token })
    }
  }

    return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      onClick={handleInsert}
      className={cn(
        'flex items-center gap-2 px-2 py-1.5 rounded-md cursor-grab active:cursor-grabbing transition-colors',
        'hover:bg-accent border border-transparent hover:border-border',
        isDragging && 'opacity-50'
      )}
    >
      <GripVertical className="h-3 w-3 text-muted-foreground" />
      <div className="flex-1 min-w-0">
        <span className="text-sm truncate block">{label}</span>
        {sublabel && (
          <span className="text-[10px] text-muted-foreground truncate block">{sublabel}</span>
        )}
      </div>
      {badge && (
        <Badge
          variant="secondary"
          className={cn('text-[10px] px-1.5 py-0 font-normal', badgeColor)}
        >
          {badge}
        </Badge>
      )}
    </div>
  )
}

// Draggable operator button
function DraggableOperator({ symbol, label, onSelect }: { symbol: string; label: string; onSelect?: (token: ExpressionToken) => void }) {
  const isParen = symbol === '(' || symbol === ')'
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `operator-${symbol}`,
    data: {
      type: 'palette-item',
      kind: isParen ? 'paren' : 'operator',
      operator: isParen ? undefined : symbol,
      value: isParen ? symbol : undefined,
      displayName: symbol,
    },
  })

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <button onClick={() => onSelect?.(isParen ? { id: createId(), kind: 'paren', value: symbol } : { id: createId(), kind: 'operator', operator: symbol as ArithmeticOperator })}
            ref={setNodeRef}
            {...listeners}
            {...attributes}
            className={cn(
              'w-10 h-10 flex items-center justify-center rounded-md border bg-muted/50 font-mono text-sm font-medium',
              'cursor-grab active:cursor-grabbing hover:bg-accent hover:border-primary/50 transition-colors',
              isDragging && 'opacity-50'
            )}
          >
            {symbol}
          </button>
        </TooltipTrigger>
        <TooltipContent side="bottom">
          <p className="text-xs">{label}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}

// Sortable token in the expression canvas
function SortableExpressionToken({
  token,
  onRemove,
  onDuplicate,
  onValueChange,
}: {
  token: ExpressionToken
  onRemove: () => void
  onDuplicate: () => void
  onValueChange?: (value: string | number) => void
}) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: token.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  }

  const getTokenStyle = () => {
    switch (token.kind) {
      case 'field':
        return 'bg-slate-100 border-slate-300 dark:bg-slate-800 dark:border-slate-600'
      case 'metric':
        return 'bg-cyan-100 border-cyan-300 dark:bg-cyan-900 dark:border-cyan-600'
      case 'operator':
      case 'paren':
        return 'bg-gray-100 border-gray-400 dark:bg-gray-800 dark:border-gray-500 font-mono'
      case 'number':
      case 'string':
        return 'bg-emerald-100 border-emerald-300 dark:bg-emerald-900 dark:border-emerald-600'
      case 'function':
        return 'bg-orange-100 border-orange-300 dark:bg-orange-900 dark:border-orange-600'
    }
  }

  const getBadgeLabel = () => {
    switch (token.kind) {
      case 'field': return 'Field'
      case 'metric': return 'Metric'
      case 'operator': return 'Op'
      case 'paren': return 'Op'
      case 'number': return 'Num'
      case 'string': return 'Str'
      case 'function': return 'Fn'
    }
  }

    return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'inline-flex items-center gap-1 px-2 py-1 rounded-md border text-sm group',
        getTokenStyle(),
        isDragging && 'opacity-50 shadow-lg'
      )}
    >
      <button
        {...attributes}
        {...listeners}
        className="cursor-grab active:cursor-grabbing text-muted-foreground hover:text-foreground"
      >
        <GripVertical className="h-3 w-3" />
      </button>

      {token.kind === 'number' ? (
        <input
          type="number"
          value={token.value === '' ? '' : Number(token.value ?? 0)}
          onChange={(e) => onValueChange?.(e.target.value === '' ? '' : Number(e.target.value))}
          onBlur={(e) => { if (e.target.value === '') onValueChange?.(0) }}
          onPointerDown={(e) => e.stopPropagation()}
          onKeyDown={(e) => e.stopPropagation()}
          className="w-16 bg-transparent border-none text-sm font-mono focus:outline-none focus:ring-1 focus:ring-primary rounded px-1"
        />
      ) : (
        <span className={cn('font-medium', (token.kind === 'operator' || token.kind === 'paren') && 'px-1')}>
          {token.kind === 'field' || token.kind === 'metric' ? token.displayName : token.kind === 'operator' ? token.operator : token.kind === 'paren' ? token.value : token.kind === 'string' ? token.value : token.kind === 'function' ? token.name : token.value}
        </span>
      )}

      <Badge variant="outline" className="text-[9px] px-1 py-0 font-normal ml-1">
        {getBadgeLabel()}
      </Badge>

      <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity ml-1">
        <button
          onClick={onDuplicate}
          className="p-0.5 hover:bg-background/50 rounded"
          title="Duplicate"
        >
          <Copy className="h-3 w-3 text-muted-foreground" />
        </button>
        <button
          onClick={onRemove}
          className="p-0.5 hover:bg-background/50 rounded"
          title="Remove"
        >
          <X className="h-3 w-3 text-destructive" />
        </button>
      </div>
    </div>
  )
}

// Expression canvas drop zone
function ExpressionCanvas({
  tokens,
  onRemoveToken,
  onDuplicateToken,
  onUpdateTokenValue,
}: {
  tokens: ExpressionToken[]
  onRemoveToken: (id: string) => void
  onDuplicateToken: (id: string) => void
  onUpdateTokenValue: (id: string, value: string | number) => void
}) {
  const { setNodeRef, isOver } = useDroppable({
    id: 'expression-canvas',
  })

    return (
    <div
      ref={setNodeRef}
      className={cn(
        'min-h-[120px] rounded-lg border-2 border-dashed p-3 transition-colors',
        isOver ? 'border-primary bg-primary/5' : 'border-muted-foreground/25',
        tokens.length === 0 && 'flex items-center justify-center'
      )}
    >
      {tokens.length === 0 ? (
        <p className="text-muted-foreground text-sm text-center">
          Drag fields, measures, or operators here
        </p>
      ) : (
        <SortableContext
          items={tokens.map(t => t.id)}
          strategy={horizontalListSortingStrategy}
        >
          <div className="flex flex-wrap gap-2">
            {tokens.map((token) => (
              <SortableExpressionToken
                key={token.id}
                token={token}
                onRemove={() => onRemoveToken(token.id)}
                onDuplicate={() => onDuplicateToken(token.id)}
                onValueChange={(value) => onUpdateTokenValue(token.id, value)}
              />
            ))}
          </div>
        </SortableContext>
      )}
    </div>
  )
}

// Left palette component
function ExpressionPalette({
  searchQuery,
  setSearchQuery,
  existingMeasures,
  existingDerivedFields,
  metadata,
  onInsertToken,
}: {
  searchQuery: string
  setSearchQuery: (q: string) => void
  existingMeasures: CalculatedField[]
  existingDerivedFields: CalculatedField[]
  metadata: DatasetMetadataResponse | null
  onInsertToken: (token: Partial<ExpressionToken>) => void
}) {
  const [expandedTables, setExpandedTables] = useState<Set<string>>(new Set())

  const toggleTable = (name: string) => {
    const newExpanded = new Set(expandedTables)
    if (newExpanded.has(name)) {
      newExpanded.delete(name)
    } else {
      newExpanded.add(name)
    }
    setExpandedTables(newExpanded)
  }

  const availableFields = useMemo(() => getFieldsForDerivedExpression(metadata), [metadata])
  const fieldsByTable = useMemo(() => {
    const grouped = new Map<string, typeof availableFields>()
    availableFields.forEach(field => {
      const current = grouped.get(field.tableId) ?? []
      current.push(field)
      grouped.set(field.tableId, current)
    })
    return Array.from(grouped.entries()).map(([tableId, fields]) => ({ tableId, fields }))
  }, [availableFields])

  const filteredTables = useMemo(() => {
    if (!searchQuery) return fieldsByTable
    const term = searchQuery.toLowerCase()
    return fieldsByTable.filter(table =>
      table.tableId.toLowerCase().includes(term) ||
      table.fields.some(col => col.displayName.toLowerCase().includes(term)))
  }, [searchQuery, fieldsByTable])

    return (
    <div className="h-full flex flex-col border-r">
      {/* Search */}
      <div className="p-2 border-b flex-shrink-0">
        <div className="relative">
          <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
          <Input
            placeholder="Search..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-7 h-8 text-sm"
          />
        </div>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="fields" className="flex-1 flex flex-col min-h-0">
        <TabsList className="w-full justify-start rounded-none border-b bg-transparent h-auto p-0 flex-shrink-0">
          <TabsTrigger
            value="fields"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent text-xs px-3 py-2"
          >
            Fields
          </TabsTrigger>
          <TabsTrigger
            value="measures"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent text-xs px-3 py-2"
          >
            Measures
          </TabsTrigger>
          <TabsTrigger
            value="operators"
            className="rounded-none border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent text-xs px-3 py-2"
          >
            Operators
          </TabsTrigger>
        </TabsList>

        <ScrollArea className="flex-1 min-h-0">
          <TabsContent value="fields" className="m-0 p-2">
            {filteredTables.map((table) => (
              <div key={table.tableId} className="mb-1">
                <button
                  onClick={() => toggleTable(table.tableId)}
                  className="w-full flex items-center gap-1.5 px-1.5 py-1 text-sm hover:bg-accent rounded-md"
                >
                  {expandedTables.has(table.tableId) ? (
                    <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                  ) : (
                    <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                  )}
                  <Table2 className="h-3.5 w-3.5 text-muted-foreground" />
                  <span className="truncate">{table.tableId}</span>
                </button>
                {expandedTables.has(table.tableId) && (
                  <div className="ml-4 space-y-0.5">
                    {table.fields.map((col) => (
                      <DraggablePaletteItem
                        key={col.fieldId}
                        id={col.fieldId}
                        type="column"
                        label={col.fieldId}
                        tokenValue={`[${table.tableId.toLowerCase()}.${col.fieldId.toLowerCase()}]`}
                        sublabel={table.tableId}
                        badge={col.sqlDataType || col.dataType}
                        badgeColor="bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300"
                        onSelect={onInsertToken}
                      />
                    ))}
                  </div>
                )}
              </div>
            ))}
          </TabsContent>

          <TabsContent value="measures" className="m-0 p-2 space-y-0.5">
            <p className="text-xs text-muted-foreground mb-2 px-1">
              Existing measures
            </p>
            {[...((metadata?.metrics ?? []).filter((metric) => !metric.isHidden).map((metric) => ({
              id: metric.metricId,
              name: metric.displayName,
              token: toSemanticToken({ kind: 'metric', metricId: metric.metricId }),
              detail: `${metric.formula} • Base: ${metric.baseTableId} • Agg: ${metric.aggregationBehavior} • Type: ${metric.dataType}/${metric.format}`,
              metricId: metric.metricId,
              tokenShape: {
                kind: 'metric' as const,
                metricId: metric.metricId,
                displayName: metric.displayName,
                formula: metric.formula,
                baseTableId: metric.baseTableId,
              },
            }))), ...existingMeasures.map((measure) => ({ id: measure.id, name: measure.name, token: toSemanticToken({ type: 'measure', metricId: measure.id }), detail: `${measure.aggregationFunction}([${measure.sourceTable?.toLowerCase()}.${measure.sourceColumn?.toLowerCase()}])`, metricId: measure.id, tokenShape: { kind: 'metric' as const, metricId: measure.id, displayName: measure.name } }))].map((measure) => (
              <DraggablePaletteItem
                key={measure.id}
                id={measure.id}
                type="measure"
                label={measure.name}
                token={measure.tokenShape}
                tokenValue={measure.token}
                sublabel={`${measure.metricId ? `${measure.metricId} • ` : ''}${measure.detail}`}
                badge="Measure"
                badgeColor="bg-orange-100 text-orange-700 dark:bg-orange-900 dark:text-orange-300"
                onSelect={onInsertToken}
              />
            ))}

            <div className="border-t mt-3 pt-3">
              <p className="text-xs text-muted-foreground mb-2 px-1">
                Existing derived fields
              </p>
              {existingDerivedFields.length === 0 ? (
                <p className="text-xs text-muted-foreground px-2 py-4 text-center">
                  No derived fields created yet
                </p>
              ) : (
                existingDerivedFields.map((derived) => (
                  <DraggablePaletteItem
                    key={derived.id}
                    id={derived.id}
                    type="derived"
                    label={`[${derived.name}]`}
                    sublabel={derived.expression?.substring(0, 30)}
                    badge="Derived"
                    badgeColor="bg-pink-100 text-pink-700 dark:bg-pink-900 dark:text-pink-300"
                    tokenValue={`[${derived.name}]`}
                    onSelect={onInsertToken}
                  />
                ))
              )}
            </div>
          </TabsContent>

          <TabsContent value="operators" className="m-0 p-2">
            <p className="text-xs text-muted-foreground mb-2 px-1">
              Arithmetic
            </p>
            <div className="flex flex-wrap gap-2 mb-4">
              {operators.map((op) => (
                <DraggableOperator key={op.symbol} symbol={op.symbol} label={op.label} onSelect={onInsertToken} />
              ))}
            </div>

            <p className="text-xs text-muted-foreground mb-2 px-1">
              Comparison
            </p>
            <div className="flex flex-wrap gap-2 mb-4">
              {comparisonOperators.map((op) => (
                <DraggableOperator key={op.symbol} symbol={op.symbol} label={op.label} onSelect={onInsertToken} />
              ))}
            </div>

            <p className="text-xs text-muted-foreground mb-2 px-1">
              Logical
            </p>
            <div className="flex flex-wrap gap-2">
              {logicalOperators.map((op) => (
                <DraggableOperator key={op.symbol} symbol={op.symbol} label={op.label} onSelect={onInsertToken} />
              ))}
            </div>
          </TabsContent>
        </ScrollArea>
      </Tabs>
    </div>
  )
}

export function DerivedFieldExpressionBuilder({
  open,
  onOpenChange,
  onSave,
  existingMeasures,
  existingDerivedFields,
  metadata,
}: DerivedFieldExpressionBuilderProps) {
  const [name, setName] = useState('')
  const [tokens, setTokens] = useState<ExpressionToken[]>([])
  const [searchQuery, setSearchQuery] = useState('')
  const [activeId, setActiveId] = useState<string | null>(null)
  const [mode, setMode] = useState<'visual' | 'formula'>('visual')
  const [formulaText, setFormulaText] = useState('')
  const [validationMessage, setValidationMessage] = useState<string>('')

  useEffect(() => {
    if (open) {
      console.log('Derived builder fields', getFieldsForDerivedExpression(metadata).map(f => f.fieldId))
    }
  }, [open, metadata])


  const appendToken = useCallback((token: Partial<ExpressionToken>) => {
    const next: ExpressionToken = {
      id: createId(),
      kind: token.kind,
      fieldId: token.fieldId,
      metricId: token.metricId,
      displayName: token.displayName,
      tableId: token.tableId,
      dataType: token.dataType,
      semanticType: token.semanticType,
      role: token.role,
      formula: token.formula,
      baseTableId: token.baseTableId,
      aggregationBehavior: token.aggregationBehavior,
      operator: token.operator,
      value: token.value,
      name: token.name,
    }
    setTokens(prev => [...prev, next])
  }, [])

  useEffect(() => {
    setTokens([])
    setSearchQuery('')
  }, [metadata?.datasetId])
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    })
  )

  // Generate expression string from tokens
  const expressionString = useMemo(() => {
    return mode === 'formula' ? formulaText : tokens.map(serializeExpressionToken).filter(Boolean).join(' ')
  }, [tokens, mode, formulaText])

  const hasAggregateExpression = useMemo(() => AGGREGATE_PATTERN.test(expressionString) || METRIC_TOKEN_PATTERN.test(expressionString), [expressionString])
  const detectedKind = useMemo<'calculated_measure' | 'calculated_column'>(() => (
    hasAggregateExpression ? 'calculated_measure' : 'calculated_column'
  ), [hasAggregateExpression])

  const handleDragStart = useCallback((event: DragStartEvent) => {
    setActiveId(event.active.id as string)
  }, [])

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event
    setActiveId(null)

    if (!over) return

    // Handle dropping from palette to canvas
    if (active.data.current?.type === 'palette-item') {
      if (over.id === 'expression-canvas' || tokens.some(t => t.id === over.id)) {
        const payload = active.data.current as Partial<ExpressionToken> & { tokenType?: ExpressionTokenType; value?: string | number }
        const legacyOperatorValue = typeof payload.value === 'string' ? payload.value : undefined
        const mappedLegacyKind = !payload.kind && payload.tokenType === 'operator'
          ? (legacyOperatorValue === '(' || legacyOperatorValue === ')' ? 'paren' : 'operator')
          : payload.kind
        const newToken: ExpressionToken = {
          id: createId(),
          kind: mappedLegacyKind,
          fieldId: payload.fieldId,
          metricId: payload.metricId,
          displayName: payload.displayName,
          tableId: payload.tableId,
          dataType: payload.dataType,
          semanticType: payload.semanticType,
          role: payload.role,
          formula: payload.formula,
          baseTableId: payload.baseTableId,
          aggregationBehavior: payload.aggregationBehavior,
          operator: mappedLegacyKind === 'operator' ? (payload.operator ?? (legacyOperatorValue as ArithmeticOperator | undefined)) : payload.operator,
          value: mappedLegacyKind === 'paren' ? (payload.value ?? legacyOperatorValue) : payload.value,
          name: payload.name,
        }

        // Find insertion index if dropped over another token
        if (over.id !== 'expression-canvas') {
          const overIndex = tokens.findIndex(t => t.id === over.id)
          if (overIndex !== -1) {
            setTokens(prev => [
              ...prev.slice(0, overIndex + 1),
              newToken,
              ...prev.slice(overIndex + 1),
            ])
            return
          }
        }

        setTokens(prev => [...prev, newToken])
      }
      return
    }

    // Handle reordering within canvas
    if (active.id !== over.id && tokens.some(t => t.id === active.id)) {
      setTokens((items) => {
        const oldIndex = items.findIndex(item => item.id === active.id)
        const newIndex = items.findIndex(item => item.id === over.id)

        if (oldIndex !== -1 && newIndex !== -1) {
          return arrayMove(items, oldIndex, newIndex)
        }
        return items
      })
    }
  }, [tokens])

  const removeToken = useCallback((id: string) => {
    setTokens(prev => prev.filter(t => t.id !== id))
  }, [])

  const duplicateToken = useCallback((id: string) => {
    const token = tokens.find(t => t.id === id)
    if (token) {
      const newToken: ExpressionToken = {
        ...token,
        id: `token-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      }
      const index = tokens.findIndex(t => t.id === id)
      setTokens(prev => [
        ...prev.slice(0, index + 1),
        newToken,
        ...prev.slice(index + 1),
      ])
    }
  }, [tokens])

  const updateTokenValue = useCallback((id: string, value: string | number) => {
    setTokens(prev =>
      prev.map(t =>
        t.id === id ? { ...t, value, displayName: String(value) } : t
      )
    )
  }, [])

  const clearExpression = useCallback(() => {
    setTokens([])
  }, [])

  const addParentheses = useCallback(() => {
    const openParen: ExpressionToken = {
      id: createId(),
      kind: 'paren',
      value: '(',
    }
    const closeParen: ExpressionToken = {
      id: createId(),
      kind: 'paren',
      value: ')',
    }
    setTokens(prev => [...prev, openParen, closeParen])
  }, [])

  const addNumber = useCallback(() => {
    const newToken: ExpressionToken = {
      id: createId(),
      kind: 'number',
      value: 0,
      displayName: '0',
    }
    setTokens(prev => [...prev, newToken])
  }, [])

  const removeLastToken = useCallback(() => {
    setTokens(prev => prev.slice(0, -1))
  }, [])

  const copyExpression = useCallback(() => {
    navigator.clipboard.writeText(expressionString)
    toast.success('Expression copied to clipboard')
  }, [expressionString])

  const handleSave = useCallback(async () => {
    if (!metadata?.datasetId) {
      toast.error('No dataset selected')
      return
    }
    if (!name.trim()) {
      toast.error('Please enter a derived field name')
      return
    }
    const serializedExpression = mode === 'formula' ? formulaText.trim() : tokens.map(serializeExpressionToken).filter(Boolean).join(' ')
    if (mode === 'visual') {
      const invalidToken = tokens.find((token) => {
        if (!token.kind) return true
        const serialized = serializeExpressionToken(token)
        return !serialized?.toString().trim()
      })
      if (invalidToken) {
        console.error('Invalid expression token', invalidToken)
        toast.error('Expression contains invalid token.')
        return
      }
    }
    if (!serializedExpression) {
      if (tokens.length > 0) {
        console.error('Expression serialization failed for tokens', tokens)
      }
      toast.error('Expression is empty. Please verify expression tokens.')
      return
    }
    if (tokens.some((token) => token.kind === 'number' && token.value !== '' && Number.isNaN(Number(token.value)))) {
      toast.error('Expression contains invalid number tokens')
      return
    }
    try {
      const validation = await validateExpression(metadata.datasetId, { expression: serializedExpression, targetKind: 'auto' })
      if (!validation.valid) {
        toast.error(validation.errors[0] ?? 'Expression validation failed')
        return
      }
      if (validation.detectedKind === 'calculated_measure') {
        await createMetric(metadata.datasetId, { displayName: name.trim(), formula: serializedExpression, baseTableId: metadata.tables[0]?.tableId ?? '', aggregationBehavior: 'calculated', dataType: 'decimal', format: 'general', isHidden: false, isDraggable: true })
        onSave({ id: `metric-${Date.now()}`, name: name.trim(), type: 'measure', expression: serializedExpression })
        toast.success(`Calculated measure "${name.trim()}" created`)
      } else {
        await createDerivedField(metadata.datasetId, { displayName: name.trim(), baseTableId: metadata.tables[0]?.tableId ?? '', expression: serializedExpression, dataType: 'nvarchar', semanticType: 'dimension', format: 'general', isHidden: false, isDraggable: true })
        onSave({ id: `derived-${Date.now()}`, name: name.trim(), type: 'derived', expression: serializedExpression })
        toast.success(`Calculated column "${name.trim()}" created`)
      }
      setName('')
      setTokens([])
      setSearchQuery('')
      onOpenChange(false)
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Save failed')
    }
  }, [metadata, name, mode, formulaText, tokens, onSave, onOpenChange])

  const handleValidate = useCallback(async () => {
    try {
      if (!metadata?.datasetId) throw new Error('No dataset selected.')
      const result = await validateExpression(metadata.datasetId, { expression: expressionString, targetKind: 'auto' })
      setValidationMessage(result.valid ? `Valid (${result.detectedKind}) • SQL: ${result.compiledSqlPreview}` : `Invalid: ${result.errors.join(', ')}`)
      if (result.valid) toast.success('Expression is valid')
      else toast.error(result.errors[0] ?? 'Expression invalid')
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Validation failed'
      setValidationMessage(message)
      toast.error(message)
    }
  }, [metadata?.datasetId, expressionString])

  const handleClose = useCallback(() => {
    setName('')
    setTokens([])
    setSearchQuery('')
    onOpenChange(false)
  }, [onOpenChange])

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      {/* <DialogContent className="max-w-[900px] max-h-[90vh] overflow-hidden flex flex-col p-0"> */}
      <DialogContent className="!w-[96vw] !max-w-none h-[92vh] overflow-hidden flex flex-col p-0">
        <DialogHeader className="px-6 pt-6 pb-4 border-b flex-shrink-0">
          <DialogTitle className="flex items-center gap-2">
            <FunctionSquare className="h-5 w-5 text-pink-600" />
            Create Calculated Field
          </DialogTitle>
          <p className="text-sm text-muted-foreground">
            Drag fields, measures, and operators to build an expression.
          </p>
          {hasAggregateExpression && (
            <p className="text-sm text-amber-600">
              This expression uses measures/aggregates. Save it as a Measure, not a Derived Field.
            </p>
          )}
        </DialogHeader>

        {/* Name Input */}
        <div className="px-6 py-3 border-b flex-shrink-0">
          <div className="mb-2 flex items-center gap-2">
            <Button size="sm" variant={mode === 'visual' ? 'default' : 'outline'} onClick={() => setMode('visual')}>Visual Builder</Button>
            <Button size="sm" variant={mode === 'formula' ? 'default' : 'outline'} onClick={() => setMode('formula')}>Formula Editor</Button>
            <Button size="sm" variant="outline" onClick={handleValidate}>Validate Expression</Button>
          </div>
          <Label htmlFor="derivedName" className="text-sm font-medium">
            Calculated Field Name
          </Label>
          <Input
            id="derivedName"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g., Profit Margin"
            className="mt-1.5"
          />
        </div>

        {/* Main Content - Two Column Layout */}
        {mode === 'visual' ? <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
        >
          <div className="flex-1 flex min-h-0 overflow-hidden">
            {/* Left Palette - 35% */}
            {/* <div className="w-[35%] min-h-0 overflow-hidden"> */}
            <div className="w-[40%] min-h-0 overflow-hidden">
        <ExpressionPalette
          searchQuery={searchQuery}
          setSearchQuery={setSearchQuery}
          existingMeasures={existingMeasures}
          existingDerivedFields={existingDerivedFields}
          metadata={metadata}
          onInsertToken={appendToken}
        />
            </div>

            {/* Right Expression Builder - 65% */}
            {/* <div className="flex-1 flex flex-col min-h-0 overflow-hidden"> */}
            <div className="flex-1 min-w-0 flex flex-col min-h-0 overflow-hidden">
              <ScrollArea className="flex-1 min-h-0">
                <div className="p-4 space-y-4">
                  {/* Quick Actions */}
                  <div className="flex items-center gap-2 flex-wrap">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={clearExpression}
                      disabled={tokens.length === 0}
                    >
                      <Trash2 className="h-3.5 w-3.5 mr-1" />
                      Clear
                    </Button>
                    <Button variant="outline" size="sm" onClick={addParentheses}>
                      <Parentheses className="h-3.5 w-3.5 mr-1" />
                      Add ( )
                    </Button>
                    <Button variant="outline" size="sm" onClick={addNumber}>
                      <Hash className="h-3.5 w-3.5 mr-1" />
                      Add Number
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={removeLastToken}
                      disabled={tokens.length === 0}
                    >
                      <X className="h-3.5 w-3.5 mr-1" />
                      Remove Last
                    </Button>
                  </div>

                  {/* Expression Canvas */}
                  <div>
                    <Label className="text-sm font-medium mb-2 block">
                      Expression Canvas
                    </Label>
                    <ExpressionCanvas
                      tokens={tokens}
                      onRemoveToken={removeToken}
                      onDuplicateToken={duplicateToken}
                      onUpdateTokenValue={updateTokenValue}
                    />
                  </div>

                  {/* Expression Preview */}
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <Label className="text-sm font-medium">Expression Preview</Label>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={copyExpression}
                        disabled={tokens.length === 0}
                        className="h-7 text-xs"
                      >
                        <Copy className="h-3 w-3 mr-1" />
                        Copy
                      </Button>
                    </div>
                    <div className="p-3 bg-muted/50 rounded-lg border min-h-[60px]">
                      {tokens.length === 0 ? (
                        <span className="text-muted-foreground text-sm">
                          Expression will appear here...
                        </span>
                      ) : (
                        <code className="font-mono text-sm break-all">{expressionString}</code>
                      )}
                    </div>
                  </div>
                </div>
              </ScrollArea>
            </div>
          </div>

          {/* Drag Overlay */}
          <DragOverlay>
            {activeId ? (
              <div className="flex items-center gap-2 bg-card border border-primary rounded-lg px-3 py-2 shadow-xl text-sm">
                <GripVertical className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium">Dragging...</span>
              </div>
            ) : null}
          </DragOverlay>
        </DndContext> : (
          <div className="p-4">
            <Label className="text-sm font-medium mb-2 block">Formula Editor</Label>
            <textarea value={formulaText} onChange={(e) => setFormulaText(e.target.value)} className="w-full min-h-[320px] rounded-md border p-3 font-mono text-sm" placeholder="Type formula, e.g. ROUND(([metric.sum_factsales_salesamount]-[metric.sum_factsales_totalproductcost])/[metric.sum_factsales_salesamount],4)" />
            <div className="mt-2 text-xs text-muted-foreground">
              Functions: IF, ROUND, COALESCE, NULLIF, CONCAT, YEAR, MONTH, DAY, SUM, AVG, MIN, MAX, COUNT, COUNT_DISTINCT
            </div>
            {validationMessage ? <div className="mt-2 text-xs">{validationMessage}</div> : null}
          </div>
        )}

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-6 py-4 border-t flex-shrink-0">
          <Button variant="outline" onClick={handleClose}>
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={!name.trim() || (mode === 'visual' ? tokens.length === 0 : !formulaText.trim())}
          >
            {detectedKind === 'calculated_measure' ? 'Save Calculated Measure' : 'Save Calculated Column'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}
