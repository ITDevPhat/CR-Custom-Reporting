'use client'

import { useState, useMemo, useCallback, useEffect, useRef } from 'react'
import {
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  PointerSensor,
  useSensor,
  useSensors,
  useDroppable,
  useDraggable,
} from '@dnd-kit/core'
import {
  Search,
  GripVertical,
  X,
  Trash2,
  Eye,
  Lock,
  ChevronDown,
  ChevronUp,
  Calendar,
  Plus,
  Calculator,
  Sigma,
  FunctionSquare,
  Hash,
  Type,
  CalendarDays,
} from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'
import { cn } from '@/lib/utils'
import { type SelectedField, type CalculatedField, type TableSchema, schemaData } from '@/lib/schema-data'
import {
  type AppliedFilter,
  type FilterType,
  type FilterFieldKind,
  type FilterCondition,
  type TopNConfig,
  type NumericFilterOperator,
  type TextFilterOperator,
  type DateFilterOperator,
  getFieldKind,
  operatorRequiresValue,
  generateFilterSummary,
  numericOperators,
  textOperators,
  dateOperators,
  getMockDistinctValues,
} from '@/lib/filter-types'

interface FilterableField {
  id: string
  fieldId: string
  fieldName: string
  qualifiedName: string
  tableName?: string
  dataType: string
  sourceType: 'field' | 'column' | 'metric' | 'measure' | 'derived'
  sourceInfo: string
  fieldKind: FilterFieldKind
}

interface FilterBuilderDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  selectedFields: SelectedField[]
  calculatedFields: CalculatedField[]
  loadedTables: TableSchema[] | null
  appliedFilters: AppliedFilter[]
  onApplyFilters: (filters: AppliedFilter[]) => void
}

// Draggable field item - separating drag handle from click area
function DraggableFieldItem({ field, onClick }: { field: FilterableField; onClick: () => void }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `filter-field-${field.id}`,
    data: { type: 'filter-field', field },
  })

  const style = transform ? {
    transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`,
  } : undefined

  const getTypeIcon = () => {
    switch (field.sourceType) {
      case 'metric':
        return <Calculator className="h-3.5 w-3.5 text-cyan-600" />
      case 'measure':
        return <Sigma className="h-3.5 w-3.5 text-orange-600" />
      case 'derived':
        return <FunctionSquare className="h-3.5 w-3.5 text-pink-600" />
      default:
        switch (field.fieldKind) {
          case 'number':
            return <Hash className="h-3.5 w-3.5 text-blue-600" />
          case 'date':
            return <CalendarDays className="h-3.5 w-3.5 text-amber-600" />
          default:
            return <Type className="h-3.5 w-3.5 text-purple-600" />
        }
    }
  }

  const getTypeBadge = () => {
    if (field.sourceType !== 'column' && field.sourceType !== 'field') {
      return (
        <Badge variant="secondary" className="text-[10px] px-1.5 py-0 capitalize">
          {field.sourceType}
        </Badge>
      )
    }
    return (
      <Badge variant="secondary" className="text-[10px] px-1.5 py-0 capitalize">
        {field.fieldKind}
      </Badge>
    )
  }

  const handleClick = () => {
    if (!isDragging) {
      onClick()
    }
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      onClick={handleClick}
      className={cn(
        'flex items-center gap-2 p-2 rounded-md border bg-card hover:bg-accent/50 transition-colors cursor-pointer',
        isDragging && 'opacity-50'
      )}
    >
      {/* Drag handle - only this element initiates drag */}
      <div 
        {...attributes} 
        {...listeners} 
        className="cursor-grab active:cursor-grabbing flex-shrink-0"
        onClick={(event) => event.stopPropagation()}
      >
        <GripVertical className="h-4 w-4 text-muted-foreground" />
      </div>
      {/* Clickable content area */}
      <div 
        className="flex-1 flex items-center gap-2 min-w-0"
      >
        {getTypeIcon()}
        <div className="flex-1 min-w-0">
          <p className="text-sm font-medium truncate">{field.fieldName}</p>
          <p className="text-[10px] text-muted-foreground truncate">{field.sourceInfo}</p>
        </div>
        {getTypeBadge()}
      </div>
    </div>
  )
}

// Filter drop zone
function FilterDropZone({ children, isEmpty }: { children: React.ReactNode; isEmpty: boolean }) {
  const { setNodeRef, isOver } = useDroppable({
    id: 'filter-canvas-dropzone',
  })

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'flex-1 min-h-[200px] rounded-lg border-2 border-dashed transition-colors',
        isOver ? 'border-primary bg-primary/5' : 'border-muted-foreground/25',
        isEmpty && 'flex items-center justify-center'
      )}
    >
      {isEmpty ? (
        <p className="text-muted-foreground text-sm text-center p-4">
          Drag or click fields to create filters
        </p>
      ) : (
        children
      )}
    </div>
  )
}

// Numeric Filter UI
function NumericFilterCard({
  filter,
  onUpdate,
  onApply,
  onClear,
  onRemove,
}: {
  filter: AppliedFilter
  onUpdate: (updates: Partial<AppliedFilter>) => void
  onApply: () => void
  onClear: () => void
  onRemove: () => void
}) {
  const [isExpanded, setIsExpanded] = useState(true)
  const condition1 = filter.conditions[0] || { operator: '' as NumericFilterOperator, value: '' }
  const condition2 = filter.conditions[1] || { operator: '' as NumericFilterOperator, value: '' }

  const updateCondition = (index: number, updates: Partial<FilterCondition>) => {
    const newConditions = [...filter.conditions]
    if (!newConditions[index]) {
      newConditions[index] = { operator: '' as NumericFilterOperator, value: '' }
    }
    newConditions[index] = { ...newConditions[index], ...updates }
    onUpdate({ conditions: newConditions })
  }

  return (
    <div className="border rounded-lg bg-card overflow-hidden">
      <Collapsible open={isExpanded} onOpenChange={setIsExpanded}>
        <CollapsibleTrigger asChild>
          <div className="flex items-center justify-between p-3 hover:bg-accent/50 cursor-pointer">
            <div className="flex items-center gap-2">
              <Hash className="h-4 w-4 text-blue-600" />
              <span className="font-medium text-sm">{filter.fieldName}</span>
              {filter.isApplied && (
                <Badge variant="default" className="text-[10px] px-1.5 py-0">
                  Applied
                </Badge>
              )}
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="icon" className="h-7 w-7" disabled>
                <Lock className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" disabled>
                <Eye className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); onClear(); }}>
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); onRemove(); }}>
                <X className="h-3.5 w-3.5" />
              </Button>
              {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </div>
          </div>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <div className="p-3 pt-0 space-y-3">
            <p className="text-xs text-muted-foreground">{filter.sourceInfo}</p>
            <Separator />
            
            <div className="text-xs font-medium text-muted-foreground">Advanced filtering</div>
            <p className="text-xs text-muted-foreground">Show items when the value</p>

            {/* Condition 1 */}
            <div className="space-y-2">
              <Select
                value={condition1.operator}
                onValueChange={(v) => updateCondition(0, { operator: v as NumericFilterOperator })}
              >
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue placeholder="Select operator" />
                </SelectTrigger>
                <SelectContent>
                  {numericOperators.map((op) => (
                    <SelectItem key={op} value={op} className="text-xs">
                      {op}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {operatorRequiresValue(condition1.operator) && (
                <Input
                  type="number"
                  placeholder="Enter value"
                  value={condition1.value}
                  onChange={(e) => updateCondition(0, { value: e.target.value })}
                  className="h-8 text-xs"
                />
              )}
            </div>

            {/* AND/OR */}
            <RadioGroup
              value={filter.logic}
              onValueChange={(v) => onUpdate({ logic: v as 'AND' | 'OR' })}
              className="flex items-center gap-4"
            >
              <div className="flex items-center space-x-2">
                <RadioGroupItem value="AND" id={`${filter.id}-and`} />
                <Label htmlFor={`${filter.id}-and`} className="text-xs">AND</Label>
              </div>
              <div className="flex items-center space-x-2">
                <RadioGroupItem value="OR" id={`${filter.id}-or`} />
                <Label htmlFor={`${filter.id}-or`} className="text-xs">OR</Label>
              </div>
            </RadioGroup>

            {/* Condition 2 */}
            <div className="space-y-2">
              <Select
                value={condition2.operator || '__none__'}
                onValueChange={(v) => updateCondition(1, { operator: v === '__none__' ? '' as NumericFilterOperator : v as NumericFilterOperator })}
              >
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue placeholder="Select operator (optional)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__" className="text-xs">None</SelectItem>
                  {numericOperators.map((op) => (
                    <SelectItem key={op} value={op} className="text-xs">
                      {op}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {condition2.operator && operatorRequiresValue(condition2.operator) && (
                <Input
                  type="number"
                  placeholder="Enter value"
                  value={condition2.value}
                  onChange={(e) => updateCondition(1, { value: e.target.value })}
                  className="h-8 text-xs"
                />
              )}
            </div>

            <div className="flex gap-2 pt-2">
              <Button variant="outline" size="sm" className="text-xs" onClick={onClear}>
                Clear filter
              </Button>
              <Button size="sm" className="text-xs" onClick={onApply}>
                Apply filter
              </Button>
            </div>
          </div>
        </CollapsibleContent>
      </Collapsible>
    </div>
  )
}

// Text Filter UI
function TextFilterCard({
  filter,
  onUpdate,
  onApply,
  onClear,
  onRemove,
  numericFields,
}: {
  filter: AppliedFilter
  onUpdate: (updates: Partial<AppliedFilter>) => void
  onApply: () => void
  onClear: () => void
  onRemove: () => void
  numericFields: FilterableField[]
}) {
  const [isExpanded, setIsExpanded] = useState(true)
  const [searchValue, setSearchValue] = useState('')
  const [selectAll, setSelectAll] = useState(false)
  const mockValues = useMemo(() => getMockDistinctValues(filter.fieldName), [filter.fieldName])
  
  const filteredValues = useMemo(() => {
    if (!searchValue) return mockValues
    return mockValues.filter(v => v.value.toLowerCase().includes(searchValue.toLowerCase()))
  }, [mockValues, searchValue])

  const condition1 = filter.conditions[0] || { operator: '' as TextFilterOperator, value: '' }
  const condition2 = filter.conditions[1] || { operator: '' as TextFilterOperator, value: '' }

  const updateCondition = (index: number, updates: Partial<FilterCondition>) => {
    const newConditions = [...filter.conditions]
    if (!newConditions[index]) {
      newConditions[index] = { operator: '' as TextFilterOperator, value: '' }
    }
    newConditions[index] = { ...newConditions[index], ...updates }
    onUpdate({ conditions: newConditions })
  }

  const handleValueToggle = (value: string) => {
    const newValues = filter.selectedValues.includes(value)
      ? filter.selectedValues.filter(v => v !== value)
      : [...filter.selectedValues, value]
    onUpdate({ selectedValues: newValues })
  }

  const handleSelectAll = (checked: boolean) => {
    setSelectAll(checked)
    if (checked) {
      onUpdate({ selectedValues: mockValues.map(v => v.value) })
    } else {
      onUpdate({ selectedValues: [] })
    }
  }

  const updateTopNConfig = (updates: Partial<TopNConfig>) => {
    const current = filter.topNConfig || { direction: 'top', count: 10, byFieldId: null, byFieldName: null }
    onUpdate({ topNConfig: { ...current, ...updates } })
  }

  return (
    <div className="border rounded-lg bg-card overflow-hidden">
      <Collapsible open={isExpanded} onOpenChange={setIsExpanded}>
        <CollapsibleTrigger asChild>
          <div className="flex items-center justify-between p-3 hover:bg-accent/50 cursor-pointer">
            <div className="flex items-center gap-2">
              <Type className="h-4 w-4 text-purple-600" />
              <span className="font-medium text-sm">{filter.fieldName}</span>
              {filter.isApplied && (
                <Badge variant="default" className="text-[10px] px-1.5 py-0">
                  Applied
                </Badge>
              )}
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="icon" className="h-7 w-7" disabled>
                <Lock className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" disabled>
                <Eye className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); onClear(); }}>
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); onRemove(); }}>
                <X className="h-3.5 w-3.5" />
              </Button>
              {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </div>
          </div>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <div className="p-3 pt-0 space-y-3">
            <p className="text-xs text-muted-foreground">{filter.sourceInfo}</p>
            <Separator />

            {/* Filter Type Selector */}
            <Select
              value={filter.filterType}
              onValueChange={(v) => onUpdate({ filterType: v as FilterType })}
            >
              <SelectTrigger className="h-8 text-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="basic" className="text-xs">Basic filtering</SelectItem>
                <SelectItem value="advanced" className="text-xs">Advanced filtering</SelectItem>
                <SelectItem value="topN" className="text-xs">Top N</SelectItem>
              </SelectContent>
            </Select>

            {/* Basic Filtering */}
            {filter.filterType === 'basic' && (
              <div className="space-y-2">
                <div className="relative">
                  <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
                  <Input
                    placeholder="Search values..."
                    value={searchValue}
                    onChange={(e) => setSearchValue(e.target.value)}
                    className="h-8 pl-8 text-xs"
                  />
                </div>
                <div className="flex items-center space-x-2">
                  <Checkbox
                    id={`${filter.id}-selectall`}
                    checked={selectAll}
                    onCheckedChange={(checked) => handleSelectAll(checked as boolean)}
                  />
                  <Label htmlFor={`${filter.id}-selectall`} className="text-xs">Select all</Label>
                </div>
                <ScrollArea className="h-[150px] border rounded-md">
                  <div className="p-2 space-y-1">
                    {filteredValues.map((item) => (
                      <div key={item.value} className="flex items-center justify-between py-1 px-2 hover:bg-accent/50 rounded">
                        <div className="flex items-center space-x-2">
                          <Checkbox
                            id={`${filter.id}-${item.value}`}
                            checked={filter.selectedValues.includes(item.value)}
                            onCheckedChange={() => handleValueToggle(item.value)}
                          />
                          <Label htmlFor={`${filter.id}-${item.value}`} className="text-xs cursor-pointer">
                            {item.value}
                          </Label>
                        </div>
                        <span className="text-[10px] text-muted-foreground">{item.count}</span>
                      </div>
                    ))}
                  </div>
                </ScrollArea>
              </div>
            )}

            {/* Advanced Filtering */}
            {filter.filterType === 'advanced' && (
              <div className="space-y-2">
                <p className="text-xs text-muted-foreground">Show items when the value</p>
                
                <Select
                  value={condition1.operator}
                  onValueChange={(v) => updateCondition(0, { operator: v as TextFilterOperator })}
                >
                  <SelectTrigger className="h-8 text-xs">
                    <SelectValue placeholder="Select operator" />
                  </SelectTrigger>
                  <SelectContent>
                    {textOperators.map((op) => (
                      <SelectItem key={op} value={op} className="text-xs">
                        {op}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {operatorRequiresValue(condition1.operator) && (
                  <Input
                    placeholder="Enter value"
                    value={condition1.value}
                    onChange={(e) => updateCondition(0, { value: e.target.value })}
                    className="h-8 text-xs"
                  />
                )}

                <RadioGroup
                  value={filter.logic}
                  onValueChange={(v) => onUpdate({ logic: v as 'AND' | 'OR' })}
                  className="flex items-center gap-4"
                >
                  <div className="flex items-center space-x-2">
                    <RadioGroupItem value="AND" id={`${filter.id}-adv-and`} />
                    <Label htmlFor={`${filter.id}-adv-and`} className="text-xs">AND</Label>
                  </div>
                  <div className="flex items-center space-x-2">
                    <RadioGroupItem value="OR" id={`${filter.id}-adv-or`} />
                    <Label htmlFor={`${filter.id}-adv-or`} className="text-xs">OR</Label>
                  </div>
                </RadioGroup>

                <Select
                  value={condition2.operator || '__none__'}
                  onValueChange={(v) => updateCondition(1, { operator: v === '__none__' ? '' as TextFilterOperator : v as TextFilterOperator })}
                >
                  <SelectTrigger className="h-8 text-xs">
                    <SelectValue placeholder="Select operator (optional)" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__" className="text-xs">None</SelectItem>
                    {textOperators.map((op) => (
                      <SelectItem key={op} value={op} className="text-xs">
                        {op}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {condition2.operator && operatorRequiresValue(condition2.operator) && (
                  <Input
                    placeholder="Enter value"
                    value={condition2.value}
                    onChange={(e) => updateCondition(1, { value: e.target.value })}
                    className="h-8 text-xs"
                  />
                )}
              </div>
            )}

            {/* Top N Filtering */}
            {filter.filterType === 'topN' && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <span className="text-xs">Show items:</span>
                  <Select
                    value={filter.topNConfig?.direction || 'top'}
                    onValueChange={(v) => updateTopNConfig({ direction: v as 'top' | 'bottom' })}
                  >
                    <SelectTrigger className="w-24 h-8 text-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="top" className="text-xs">Top</SelectItem>
                      <SelectItem value="bottom" className="text-xs">Bottom</SelectItem>
                    </SelectContent>
                  </Select>
                  <Input
                    type="number"
                    min={1}
                    value={filter.topNConfig?.count || 10}
                    onChange={(e) => updateTopNConfig({ count: parseInt(e.target.value) || 10 })}
                    className="w-20 h-8 text-xs"
                  />
                </div>
                <div className="space-y-2">
                  <span className="text-xs">By value:</span>
                  <Select
                    value={filter.topNConfig?.byFieldId || ''}
                    onValueChange={(v) => {
                      const field = numericFields.find(f => f.fieldId === v)
                      updateTopNConfig({ byFieldId: v, byFieldName: field?.fieldName || null })
                    }}
                  >
                    <SelectTrigger className="h-8 text-xs">
                      <SelectValue placeholder="Select a numeric field" />
                    </SelectTrigger>
                    <SelectContent>
                      {numericFields.map((f) => (
                        <SelectItem key={f.fieldId} value={f.fieldId} className="text-xs">
                          {f.fieldName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
            )}

            <div className="flex gap-2 pt-2">
              <Button variant="outline" size="sm" className="text-xs" onClick={onClear}>
                Clear filter
              </Button>
              <Button size="sm" className="text-xs" onClick={onApply}>
                Apply filter
              </Button>
            </div>
          </div>
        </CollapsibleContent>
      </Collapsible>
    </div>
  )
}

// Date Filter UI
function DateFilterCard({
  filter,
  onUpdate,
  onApply,
  onClear,
  onRemove,
}: {
  filter: AppliedFilter
  onUpdate: (updates: Partial<AppliedFilter>) => void
  onApply: () => void
  onClear: () => void
  onRemove: () => void
}) {
  const [isExpanded, setIsExpanded] = useState(true)
  const condition1 = filter.conditions[0] || { operator: '' as DateFilterOperator, value: '' }
  const condition2 = filter.conditions[1] || { operator: '' as DateFilterOperator, value: '' }

  const updateCondition = (index: number, updates: Partial<FilterCondition>) => {
    const newConditions = [...filter.conditions]
    if (!newConditions[index]) {
      newConditions[index] = { operator: '' as DateFilterOperator, value: '' }
    }
    newConditions[index] = { ...newConditions[index], ...updates }
    onUpdate({ conditions: newConditions })
  }

  return (
    <div className="border rounded-lg bg-card overflow-hidden">
      <Collapsible open={isExpanded} onOpenChange={setIsExpanded}>
        <CollapsibleTrigger asChild>
          <div className="flex items-center justify-between p-3 hover:bg-accent/50 cursor-pointer">
            <div className="flex items-center gap-2">
              <CalendarDays className="h-4 w-4 text-amber-600" />
              <span className="font-medium text-sm">{filter.fieldName}</span>
              {filter.isApplied && (
                <Badge variant="default" className="text-[10px] px-1.5 py-0">
                  Applied
                </Badge>
              )}
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="icon" className="h-7 w-7" disabled>
                <Lock className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" disabled>
                <Eye className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); onClear(); }}>
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
              <Button variant="ghost" size="icon" className="h-7 w-7" onClick={(e) => { e.stopPropagation(); onRemove(); }}>
                <X className="h-3.5 w-3.5" />
              </Button>
              {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </div>
          </div>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <div className="p-3 pt-0 space-y-3">
            <p className="text-xs text-muted-foreground">{filter.sourceInfo}</p>
            <Separator />

            <div className="text-xs font-medium text-muted-foreground">Advanced filtering</div>
            <p className="text-xs text-muted-foreground">Show items when the value</p>

            {/* Condition 1 */}
            <div className="space-y-2">
              <Select
                value={condition1.operator}
                onValueChange={(v) => updateCondition(0, { operator: v as DateFilterOperator })}
              >
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue placeholder="Select operator" />
                </SelectTrigger>
                <SelectContent>
                  {dateOperators.map((op) => (
                    <SelectItem key={op} value={op} className="text-xs">
                      {op}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {operatorRequiresValue(condition1.operator) && (
                <div className="flex gap-2">
                  <Input
                    type="date"
                    value={condition1.value}
                    onChange={(e) => updateCondition(0, { value: e.target.value })}
                    className="h-8 text-xs flex-1"
                  />
                </div>
              )}
            </div>

            {/* AND/OR */}
            <RadioGroup
              value={filter.logic}
              onValueChange={(v) => onUpdate({ logic: v as 'AND' | 'OR' })}
              className="flex items-center gap-4"
            >
              <div className="flex items-center space-x-2">
                <RadioGroupItem value="AND" id={`${filter.id}-date-and`} />
                <Label htmlFor={`${filter.id}-date-and`} className="text-xs">AND</Label>
              </div>
              <div className="flex items-center space-x-2">
                <RadioGroupItem value="OR" id={`${filter.id}-date-or`} />
                <Label htmlFor={`${filter.id}-date-or`} className="text-xs">OR</Label>
              </div>
            </RadioGroup>

            {/* Condition 2 */}
            <div className="space-y-2">
              <Select
                value={condition2.operator || '__none__'}
                onValueChange={(v) => updateCondition(1, { operator: v === '__none__' ? '' as DateFilterOperator : v as DateFilterOperator })}
              >
                <SelectTrigger className="h-8 text-xs">
                  <SelectValue placeholder="Select operator (optional)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__" className="text-xs">None</SelectItem>
                  {dateOperators.map((op) => (
                    <SelectItem key={op} value={op} className="text-xs">
                      {op}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {condition2.operator && operatorRequiresValue(condition2.operator) && (
                <div className="flex gap-2">
                  <Input
                    type="date"
                    value={condition2.value}
                    onChange={(e) => updateCondition(1, { value: e.target.value })}
                    className="h-8 text-xs flex-1"
                  />
                </div>
              )}
            </div>

            <div className="flex gap-2 pt-2">
              <Button variant="outline" size="sm" className="text-xs" onClick={onClear}>
                Clear filter
              </Button>
              <Button size="sm" className="text-xs" onClick={onApply}>
                Apply filter
              </Button>
            </div>
          </div>
        </CollapsibleContent>
      </Collapsible>
    </div>
  )
}

export function FilterBuilderDialog({
  open,
  onOpenChange,
  selectedFields,
  calculatedFields,
  loadedTables,
  appliedFilters,
  onApplyFilters,
}: FilterBuilderDialogProps) {
  const [searchQuery, setSearchQuery] = useState('')
  const [localFilters, setLocalFilters] = useState<AppliedFilter[]>(appliedFilters)
  const [activeId, setActiveId] = useState<string | null>(null)
  const [activeDragField, setActiveDragField] = useState<FilterableField | null>(null)
  const localFiltersRef = useRef<AppliedFilter[]>(appliedFilters)
  const filterCardRefs = useRef<Record<string, HTMLDivElement | null>>({})

  useEffect(() => {
    localFiltersRef.current = localFilters
  }, [localFilters])

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 5 },
    })
  )

  // Build available fields list
  const availableFields = useMemo<FilterableField[]>(() => {
    const fields: FilterableField[] = []

    // Add selected report columns
    selectedFields.forEach((sf) => {
      const fieldId = sf.id || `${sf.tableName}.${sf.columnName}`
      const fieldKind = getFieldKind(sf.dataType, sf.columnName)
      fields.push({
        id: fieldId,
        fieldId,
        fieldName: sf.columnName,
        qualifiedName: fieldId,
        tableName: sf.tableName,
        dataType: sf.dataType,
        sourceType: sf.kind,
        sourceInfo: sf.kind === 'column' || sf.kind === 'field' ? fieldId : sf.kind,
        fieldKind,
      })
    })

    // Add physical data fields from loaded tables, falling back to the default schema
    const physicalTables = loadedTables ?? schemaData
    physicalTables.forEach((table) => {
      table.columns.forEach((col) => {
        const id = `${table.name}.${col.name}`
        if (!fields.some(f => f.fieldId === id)) {
          fields.push({
            id,
            fieldId: id,
            fieldName: col.name,
            qualifiedName: id,
            tableName: table.name,
            dataType: col.dataType,
            sourceType: 'column',
            sourceInfo: `${table.name}.${col.name}`,
            fieldKind: getFieldKind(col.dataType, col.name),
          })
        }
      })
    })

    // Add calculated fields
    calculatedFields.forEach((cf) => {
      const fieldId = cf.id || `${cf.sourceTable || 'Calculated'}.${cf.name}`
      fields.push({
        id: fieldId,
        fieldId,
        fieldName: cf.name,
        qualifiedName: fieldId,
        tableName: cf.sourceTable,
        dataType: 'decimal',
        sourceType: cf.type,
        sourceInfo: cf.type === 'measure' 
          ? `${cf.aggregationFunction}(${cf.sourceTable}.${cf.sourceColumn})`
          : cf.expression || cf.aggregationFunction || '',
        fieldKind: 'number',
      })
    })

    return fields
  }, [selectedFields, loadedTables, calculatedFields])

  // Filter available fields by search
  const filteredFields = useMemo(() => {
    if (!searchQuery) return availableFields
    return availableFields.filter(f => 
      f.fieldName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      f.sourceInfo.toLowerCase().includes(searchQuery.toLowerCase())
    )
  }, [availableFields, searchQuery])

  // Numeric fields for Top N
  const numericFields = useMemo(() => {
    return availableFields.filter(f => f.fieldKind === 'number')
  }, [availableFields])

  const focusFilterCard = useCallback((fieldId: string) => {
    requestAnimationFrame(() => {
      const card = filterCardRefs.current[fieldId]
      if (!card) return

      card.scrollIntoView({ behavior: 'smooth', block: 'center' })
      card.focus({ preventScroll: true })
    })
  }, [])

  const normalizeFilterField = useCallback((field: Partial<FilterableField> | null | undefined): FilterableField | null => {
    if (!field) {
      return null
    }

    const fieldName = field.fieldName?.trim()
    const tableName = field.tableName?.trim()
    const qualifiedName = field.qualifiedName?.trim()
    const fieldId = field.fieldId?.trim()
      || qualifiedName
      || field.id?.trim()
      || (tableName && fieldName ? `${tableName}.${fieldName}` : '')
      || field.sourceInfo?.trim()

    if (!fieldName || !fieldId) {
      return null
    }

    const dataType = field.dataType || 'nvarchar'
    const sourceType = field.sourceType || 'column'
    const fieldKind = field.fieldKind || getFieldKind(dataType, fieldName)
    const resolvedQualifiedName = qualifiedName || fieldId

    return {
      id: field.id || fieldId,
      fieldId,
      fieldName,
      qualifiedName: resolvedQualifiedName,
      tableName,
      dataType,
      sourceType,
      sourceInfo: field.sourceInfo || resolvedQualifiedName,
      fieldKind,
    }
  }, [])

  // Add field to filters with defensive guards
  const addFieldToFilter = useCallback((field: FilterableField | null | undefined) => {
    const normalizedField = normalizeFilterField(field)
    if (!normalizedField) {
      toast.error('Invalid field')
      return
    }

    const existingFilter = localFiltersRef.current.find(f => f.fieldId === normalizedField.fieldId)
    if (existingFilter) {
      toast.info(`${normalizedField.fieldName} filter already exists`)
      focusFilterCard(existingFilter.fieldId)
      return
    }

    const isTextField = normalizedField.fieldKind === 'text'
    const newFilter: AppliedFilter = {
      id: `filter-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      fieldId: normalizedField.fieldId,
      fieldName: normalizedField.fieldName,
      fieldKind: normalizedField.fieldKind,
      sourceType: normalizedField.sourceType,
      sourceInfo: normalizedField.sourceInfo,
      dataType: normalizedField.dataType,
      filterType: isTextField ? 'basic' : 'advanced',
      logic: 'AND',
      conditions: isTextField ? [] : [{ operator: 'is', value: '' }],
      selectedValues: [],
      topNConfig: null,
      summary: '',
      isApplied: false,
    }

    setLocalFilters(prev => {
      if (prev.some(f => f.fieldId === normalizedField.fieldId)) {
        return prev
      }

      const nextFilters = [...prev, newFilter]
      localFiltersRef.current = nextFilters
      return nextFilters
    })
    toast.success('Filter added')
    focusFilterCard(newFilter.fieldId)
  }, [focusFilterCard, normalizeFilterField])

  // Update filter
  const updateFilter = useCallback((filterId: string, updates: Partial<AppliedFilter>) => {
    setLocalFilters(prev => {
      const nextFilters = prev.map(f => 
        f.id === filterId ? { ...f, ...updates } : f
      )
      localFiltersRef.current = nextFilters
      return nextFilters
    })
  }, [])

  // Apply single filter
  const applySingleFilter = useCallback((filterId: string) => {
    setLocalFilters(prev => {
      const nextFilters = prev.map(f => {
        if (f.id === filterId) {
          const summary = generateFilterSummary(f)
          return { ...f, summary, isApplied: true }
        }
        return f
      })
      localFiltersRef.current = nextFilters
      return nextFilters
    })
  }, [])

  // Clear single filter
  const clearSingleFilter = useCallback((filterId: string) => {
    setLocalFilters(prev => {
      const nextFilters = prev.map(f => {
        if (f.id === filterId) {
          return {
            ...f,
            conditions: f.fieldKind === 'text' ? [] : [{ operator: 'is' as const, value: '' }],
            selectedValues: [],
            topNConfig: null,
            summary: '',
            isApplied: false,
          }
        }
        return f
      })
      localFiltersRef.current = nextFilters
      return nextFilters
    })
  }, [])

  // Remove filter
  const removeFilter = useCallback((filterId: string) => {
    setLocalFilters(prev => {
      const nextFilters = prev.filter(f => f.id !== filterId)
      localFiltersRef.current = nextFilters
      return nextFilters
    })
  }, [])

  // Clear all filters
  const clearAllFilters = useCallback(() => {
    localFiltersRef.current = []
    setLocalFilters([])
  }, [])

  // Apply all and close
  const applyAllAndClose = useCallback(() => {
    // Update summaries for all filters
    const updatedFilters = localFilters.map(f => ({
      ...f,
      summary: generateFilterSummary(f),
    }))
    onApplyFilters(updatedFilters)
    onOpenChange(false)
  }, [localFilters, onApplyFilters, onOpenChange])

  // Drag handlers
  const handleDragStart = useCallback((event: DragStartEvent) => {
    const { active } = event
    setActiveId(active.id as string)
    if (active.data.current?.type === 'filter-field') {
      setActiveDragField(active.data.current.field)
    }
  }, [])

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event
    setActiveId(null)
    setActiveDragField(null)

    if (!over) return

    if (over.id === 'filter-canvas-dropzone' && active.data.current?.type === 'filter-field') {
      addFieldToFilter(active.data.current.field)
    }
  }, [addFieldToFilter])

  // Reset local state when dialog opens
  const handleOpenChange = useCallback((newOpen: boolean) => {
    if (newOpen) {
      localFiltersRef.current = appliedFilters
      setLocalFilters(appliedFilters)
    }
    onOpenChange(newOpen)
  }, [appliedFilters, onOpenChange])

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="!w-[80vw] !max-w-[1600px] h-[92vh] flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 py-4 border-b flex-shrink-0">
          <DialogTitle>Filter Builder</DialogTitle>
          <DialogDescription>
            Drag fields into the filter area and define filter conditions.
          </DialogDescription>
        </DialogHeader>

        <DndContext
          sensors={sensors}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
        >
          <div className="flex-1 flex min-h-0 overflow-hidden">
            {/* Left Panel - Available Fields */}
            <div className="w-[30%] border-r flex flex-col min-h-0">
              <div className="p-4 border-b flex-shrink-0">
                <div className="relative">
                  <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    placeholder="Search fields..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="pl-8"
                  />
                </div>
              </div>
              <ScrollArea className="flex-1 min-h-0">
                <div className="p-4 space-y-2">
                  {filteredFields.map((field) => (
                    <DraggableFieldItem
                      key={field.fieldId}
                      field={field}
                      onClick={() => addFieldToFilter(field)}
                    />
                  ))}
                  {filteredFields.length === 0 && (
                    <p className="text-sm text-muted-foreground text-center py-4">
                      No fields found
                    </p>
                  )}
                </div>
              </ScrollArea>
            </div>

            {/* Right Panel - Filter Canvas */}
            <div className="flex-1 flex flex-col min-h-0 p-4">
              <FilterDropZone isEmpty={localFilters.length === 0}>
                <ScrollArea className="h-full">
                  <div className="p-4 space-y-3">
                    {localFilters.map((filter) => {
                      if (filter.fieldKind === 'number') {
                        return (
                          <div
                            key={filter.id}
                            ref={(node) => {
                              filterCardRefs.current[filter.fieldId] = node
                            }}
                            tabIndex={-1}
                            className="rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-primary"
                          >
                            <NumericFilterCard
                              filter={filter}
                              onUpdate={(updates) => updateFilter(filter.id, updates)}
                              onApply={() => applySingleFilter(filter.id)}
                              onClear={() => clearSingleFilter(filter.id)}
                              onRemove={() => removeFilter(filter.id)}
                            />
                          </div>
                        )
                      }
                      if (filter.fieldKind === 'date') {
                        return (
                          <div
                            key={filter.id}
                            ref={(node) => {
                              filterCardRefs.current[filter.fieldId] = node
                            }}
                            tabIndex={-1}
                            className="rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-primary"
                          >
                            <DateFilterCard
                              filter={filter}
                              onUpdate={(updates) => updateFilter(filter.id, updates)}
                              onApply={() => applySingleFilter(filter.id)}
                              onClear={() => clearSingleFilter(filter.id)}
                              onRemove={() => removeFilter(filter.id)}
                            />
                          </div>
                        )
                      }
                      return (
                        <div
                          key={filter.id}
                          ref={(node) => {
                            filterCardRefs.current[filter.fieldId] = node
                          }}
                          tabIndex={-1}
                          className="rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-primary"
                        >
                          <TextFilterCard
                            filter={filter}
                            onUpdate={(updates) => updateFilter(filter.id, updates)}
                            onApply={() => applySingleFilter(filter.id)}
                            onClear={() => clearSingleFilter(filter.id)}
                            onRemove={() => removeFilter(filter.id)}
                            numericFields={numericFields}
                          />
                        </div>
                      )
                    })}
                  </div>
                </ScrollArea>
              </FilterDropZone>
            </div>
          </div>

          <DragOverlay>
            {activeId && activeDragField ? (
              <div className="flex items-center gap-2 p-2 rounded-md border bg-card shadow-lg">
                <GripVertical className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium text-sm">{activeDragField.fieldName}</span>
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>

        <DialogFooter className="px-6 py-4 border-t flex-shrink-0">
          <div className="flex items-center justify-between w-full">
            <Button variant="outline" onClick={clearAllFilters}>
              Clear All
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button onClick={applyAllAndClose}>
                Apply All Filters
              </Button>
            </div>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
