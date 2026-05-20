'use client'

import { useState, useMemo, useCallback } from 'react'
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
  closestCenter,
} from '@dnd-kit/core'
import {
  SortableContext,
  useSortable,
  verticalListSortingStrategy,
  arrayMove,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import {
  Search,
  GripVertical,
  X,
  ArrowUpDown,
  SortAsc,
  SortDesc,
  Hash,
  Type,
  CalendarDays,
  Calculator,
  Sigma,
  FunctionSquare,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
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
import { cn } from '@/lib/utils'
import { type SelectedField } from '@/lib/schema-data'
import { type AppliedSort } from '@/lib/filter-types'
import { getFieldKind } from '@/lib/filter-types'

interface SortableField {
  id: string
  name: string
  dataType: string
  sourceType: 'field' | 'column' | 'metric' | 'measure' | 'derived'
  sourceInfo: string
}

interface SortBuilderDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  selectedFields: SelectedField[]
  appliedSorts: AppliedSort[]
  onApplySorts: (sorts: AppliedSort[]) => void
}

// Draggable field item in the left panel
function DraggableFieldItem({ field, onClick, isAdded }: { field: SortableField; onClick: () => void; isAdded: boolean }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `sort-field-${field.id}`,
    data: { type: 'sort-field', field },
    disabled: isAdded,
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
        const kind = getFieldKind(field.dataType, field.name)
        switch (kind) {
          case 'number':
            return <Hash className="h-3.5 w-3.5 text-blue-600" />
          case 'date':
            return <CalendarDays className="h-3.5 w-3.5 text-amber-600" />
          default:
            return <Type className="h-3.5 w-3.5 text-purple-600" />
        }
    }
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'flex items-center gap-2 p-2 rounded-md border bg-card transition-colors',
        isAdded 
          ? 'opacity-50 cursor-not-allowed' 
          : 'hover:bg-accent/50 cursor-grab',
        isDragging && 'opacity-50'
      )}
      onClick={() => !isAdded && onClick()}
    >
      {!isAdded && (
        <button {...attributes} {...listeners} className="cursor-grab active:cursor-grabbing">
          <GripVertical className="h-4 w-4 text-muted-foreground" />
        </button>
      )}
      {isAdded && <div className="w-4" />}
      {getTypeIcon()}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium truncate">{field.name}</p>
        <p className="text-[10px] text-muted-foreground truncate">{field.sourceInfo}</p>
      </div>
      {isAdded && (
        <Badge variant="secondary" className="text-[10px] px-1.5 py-0">
          Added
        </Badge>
      )}
    </div>
  )
}

// Sortable sort item in the right panel
function SortableSortItem({
  sort,
  onDirectionChange,
  onRemove,
}: {
  sort: AppliedSort
  onDirectionChange: (direction: 'ASC' | 'DESC') => void
  onRemove: () => void
}) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: sort.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={cn(
        'flex items-center gap-3 p-3 rounded-md border bg-card',
        isDragging && 'opacity-50 shadow-lg'
      )}
    >
      <button
        {...attributes}
        {...listeners}
        className="cursor-grab active:cursor-grabbing text-muted-foreground hover:text-foreground"
      >
        <GripVertical className="h-4 w-4" />
      </button>
      
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium">{sort.fieldName}</p>
      </div>

      <Select
        value={sort.direction}
        onValueChange={(v) => onDirectionChange(v as 'ASC' | 'DESC')}
      >
        <SelectTrigger className="w-[100px] h-8 text-xs">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="ASC" className="text-xs">
            <div className="flex items-center gap-1">
              <SortAsc className="h-3.5 w-3.5" />
              ASC
            </div>
          </SelectItem>
          <SelectItem value="DESC" className="text-xs">
            <div className="flex items-center gap-1">
              <SortDesc className="h-3.5 w-3.5" />
              DESC
            </div>
          </SelectItem>
        </SelectContent>
      </Select>

      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-8"
        onClick={onRemove}
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  )
}

// Sort drop zone
function SortDropZone({ children, isEmpty }: { children: React.ReactNode; isEmpty: boolean }) {
  const { setNodeRef, isOver } = useDroppable({
    id: 'sort-drop-zone',
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
          Drag selected report columns here to define sort order
        </p>
      ) : (
        children
      )}
    </div>
  )
}

export function SortBuilderDialog({
  open,
  onOpenChange,
  selectedFields,
  appliedSorts,
  onApplySorts,
}: SortBuilderDialogProps) {
  const [searchQuery, setSearchQuery] = useState('')
  const [localSorts, setLocalSorts] = useState<AppliedSort[]>(appliedSorts)
  const [activeId, setActiveId] = useState<string | null>(null)
  const [activeDragField, setActiveDragField] = useState<SortableField | null>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 5 },
    })
  )

  // Build available fields list (only selected report columns)
  const availableFields = useMemo<SortableField[]>(() => {
    return selectedFields.map((sf) => ({
      id: sf.id,
      name: sf.columnName,
      dataType: sf.dataType,
      sourceType: sf.kind,
      sourceInfo: sf.kind === 'column' || sf.kind === 'field' ? `${sf.tableName}.${sf.columnName}` : sf.kind,
    }))
  }, [selectedFields])

  // Filter available fields by search
  const filteredFields = useMemo(() => {
    if (!searchQuery) return availableFields
    return availableFields.filter(f => 
      f.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      f.sourceInfo.toLowerCase().includes(searchQuery.toLowerCase())
    )
  }, [availableFields, searchQuery])

  // Check if field is already in sorts
  const addedFieldIds = useMemo(() => {
    return new Set(localSorts.map(s => s.fieldId))
  }, [localSorts])

  // Add field to sorts
  const addFieldToSorts = useCallback((field: SortableField) => {
    if (localSorts.some(s => s.fieldId === field.id)) return

    const newSort: AppliedSort = {
      id: `sort-${Date.now()}`,
      fieldId: field.id,
      fieldName: field.name,
      direction: 'ASC',
    }

    setLocalSorts(prev => [...prev, newSort])
  }, [localSorts])

  // Update sort direction
  const updateSortDirection = useCallback((sortId: string, direction: 'ASC' | 'DESC') => {
    setLocalSorts(prev => prev.map(s => 
      s.id === sortId ? { ...s, direction } : s
    ))
  }, [])

  // Remove sort
  const removeSort = useCallback((sortId: string) => {
    setLocalSorts(prev => prev.filter(s => s.id !== sortId))
  }, [])

  // Clear all sorts
  const clearAllSorts = useCallback(() => {
    setLocalSorts([])
  }, [])

  // Apply and close
  const applyAndClose = useCallback(() => {
    onApplySorts(localSorts)
    onOpenChange(false)
  }, [localSorts, onApplySorts, onOpenChange])

  // Drag handlers
  const handleDragStart = useCallback((event: DragStartEvent) => {
    const { active } = event
    setActiveId(active.id as string)
    if (active.data.current?.type === 'sort-field') {
      setActiveDragField(active.data.current.field)
    }
  }, [])

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event
    setActiveId(null)
    setActiveDragField(null)

    if (!over) return

    // Handle dropping a new field from left panel
    if (active.data.current?.type === 'sort-field' && over.id === 'sort-drop-zone') {
      addFieldToSorts(active.data.current.field)
      return
    }

    // Handle reordering sort items
    if (active.id !== over.id && !active.data.current?.type) {
      setLocalSorts((items) => {
        const oldIndex = items.findIndex(item => item.id === active.id)
        const newIndex = items.findIndex(item => item.id === over.id)
        
        if (oldIndex !== -1 && newIndex !== -1) {
          return arrayMove(items, oldIndex, newIndex)
        }
        return items
      })
    }
  }, [addFieldToSorts])

  // Reset local state when dialog opens
  const handleOpenChange = useCallback((newOpen: boolean) => {
    if (newOpen) {
      setLocalSorts(appliedSorts)
    }
    onOpenChange(newOpen)
  }, [appliedSorts, onOpenChange])

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="w-[900px] max-w-[95vw] max-h-[80vh] flex flex-col p-0 gap-0">
        <DialogHeader className="px-6 py-4 border-b flex-shrink-0">
          <DialogTitle>Sort Builder</DialogTitle>
          <DialogDescription>
            Sort report results using selected report columns.
          </DialogDescription>
        </DialogHeader>

        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
        >
          <div className="flex-1 flex min-h-0 overflow-hidden">
            {/* Left Panel - Selected Report Columns */}
            <div className="w-[40%] border-r flex flex-col min-h-0">
              <div className="p-4 border-b flex-shrink-0">
                <p className="text-xs text-muted-foreground mb-2">
                  Only fields in Selected Columns can be used for sorting
                </p>
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
                  {filteredFields.length === 0 ? (
                    <p className="text-sm text-muted-foreground text-center py-4">
                      {selectedFields.length === 0 
                        ? 'Add fields to Selected Columns first'
                        : 'No fields found'
                      }
                    </p>
                  ) : (
                    filteredFields.map((field) => (
                      <DraggableFieldItem
                        key={field.id}
                        field={field}
                        onClick={() => addFieldToSorts(field)}
                        isAdded={addedFieldIds.has(field.id)}
                      />
                    ))
                  )}
                </div>
              </ScrollArea>
            </div>

            {/* Right Panel - Sort List */}
            <div className="flex-1 flex flex-col min-h-0 p-4">
              <SortDropZone isEmpty={localSorts.length === 0}>
                <ScrollArea className="h-full">
                  <SortableContext
                    items={localSorts.map(s => s.id)}
                    strategy={verticalListSortingStrategy}
                  >
                    <div className="p-4 space-y-2">
                      {localSorts.map((sort, index) => (
                        <div key={sort.id} className="flex items-center gap-2">
                          <span className="text-xs text-muted-foreground w-4">
                            {index + 1}.
                          </span>
                          <div className="flex-1">
                            <SortableSortItem
                              sort={sort}
                              onDirectionChange={(dir) => updateSortDirection(sort.id, dir)}
                              onRemove={() => removeSort(sort.id)}
                            />
                          </div>
                        </div>
                      ))}
                    </div>
                  </SortableContext>
                </ScrollArea>
              </SortDropZone>
            </div>
          </div>

          <DragOverlay>
            {activeId && activeDragField ? (
              <div className="flex items-center gap-2 p-2 rounded-md border bg-card shadow-lg">
                <GripVertical className="h-4 w-4 text-muted-foreground" />
                <span className="font-medium text-sm">{activeDragField.name}</span>
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>

        <DialogFooter className="px-6 py-4 border-t flex-shrink-0">
          <div className="flex items-center justify-between w-full">
            <Button variant="outline" onClick={clearAllSorts}>
              Clear Sort
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button onClick={applyAndClose}>
                Apply Sort
              </Button>
            </div>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
