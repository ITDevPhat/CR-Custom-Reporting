'use client'

import { useEffect, useState, useCallback, useMemo } from 'react'
import {
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  PointerSensor,
  useSensor,
  useSensors,
  closestCenter,
} from '@dnd-kit/core'
import { arrayMove } from '@dnd-kit/sortable'
import { toast } from 'sonner'

import { GlobalToolbar } from '@/components/report-builder/global-toolbar'
import { ReportHeader } from '@/components/report-builder/report-header'
import { SchemaPanel } from '@/components/report-builder/schema-panel'
import { ReportWorkspace } from '@/components/report-builder/report-workspace'
import { ActionBar } from '@/components/report-builder/action-bar'
import { ManageRelationshipsModal } from '@/components/report-builder/relationship-modals'
import { ConnectSourceFlow, type LoadedDataset } from '@/components/report-builder/connect-source-modals'
import { FilterBuilderDialog } from '@/components/report-builder/filter-builder-dialog'
import { SortBuilderDialog } from '@/components/report-builder/sort-builder-dialog'
import { 
  type SelectedField, 
  type ColumnSchema, 
  type CalculatedField,
  type TableSchema,
} from '@/lib/schema-data'
import { type AppliedFilter, type AppliedSort } from '@/lib/filter-types'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { GripVertical, Calculator, Sigma, FunctionSquare } from 'lucide-react'
import {
  ReportApiError,
  executeReportQuery,
  type QueryResult,
  type ReportFilterDraft,
  type ReportFilterFieldOption,
  type ReportSortDraft,
  type VisualQueryRequest,
} from '@/lib/report-api'
import {
  getDatasetMetadata,
  type DatasetMetadataResponse,
  type MetadataField,
  type MetadataMetric,
} from '@/lib/report-metadata-api'
import { type SqlServerConnectionRequest } from '@/lib/connections-api'
import { buildVisualQueryRequest as buildRuntimeVisualQueryRequest } from '@/lib/build-visual-query-request'
import {
  createDerivedField,
  createMetric,
  saveReportDefinition,
  validateDerivedField,
  validateMetric,
  type DerivedFieldRequest,
  type MetricRequest,
} from '@/lib/semantic-management-api'

const USE_DEMO_SCHEMA = process.env.NEXT_PUBLIC_USE_DEMO_SCHEMA === 'true'
const DEFAULT_DATASET_ID = USE_DEMO_SCHEMA ? 'sales' : null

function normalizeFilterValue(
  filter: ReportFilterDraft,
  fieldOptions: ReportFilterFieldOption[]
) {
  if (filter.operator === 'IN') {
    return filter.value
      .split(',')
      .map((value) => coerceFilterScalar(filter.field, value.trim(), fieldOptions))
      .filter((value) => value !== '')
  }

  if (filter.operator === 'BETWEEN') {
    return [
      coerceFilterScalar(filter.field, filter.value, fieldOptions),
      coerceFilterScalar(filter.field, filter.valueTo ?? '', fieldOptions),
    ]
  }

  return coerceFilterScalar(filter.field, filter.value, fieldOptions)
}

function coerceFilterScalar(
  fieldId: string,
  rawValue: string,
  fieldOptions: ReportFilterFieldOption[]
) {
  const option = fieldOptions.find((item) => item.fieldId === fieldId)
  const trimmed = rawValue.trim()

  if (option?.dataType !== 'nvarchar' && trimmed !== '') {
    const numericValue = Number(trimmed)
    return Number.isNaN(numericValue) ? trimmed : numericValue
  }

  return trimmed
}

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

export default function ReportBuilderPage() {
  const [selectedFields, setSelectedFields] = useState<SelectedField[]>([])
  const [calculatedFields, setCalculatedFields] = useState<CalculatedField[]>([])
  const [metadata, setMetadata] = useState<DatasetMetadataResponse | null>(null)
  const [datasetId, setDatasetId] = useState<string | null>(DEFAULT_DATASET_ID)
  const [connectionId, setConnectionId] = useState<string | null>(USE_DEMO_SCHEMA ? 'conn_001' : null)
  const [sourceConnection, setSourceConnection] = useState<SqlServerConnectionRequest | null>(null)
  const [reportId, setReportId] = useState<string | null>(null)
  const [metadataLoading, setMetadataLoading] = useState(Boolean(DEFAULT_DATASET_ID))
  const [metadataError, setMetadataError] = useState<string | null>(null)
  const [reportTitle, setReportTitle] = useState('Untitled Sales Report')
  const [reportDescription, setReportDescription] = useState('')
  const [lastRunTime, setLastRunTime] = useState<string | null>(null)
  const [result, setResult] = useState<QueryResult | null>(null)
  const [runtimePayload, setRuntimePayload] = useState<VisualQueryRequest | null>(null)
  const [isRunning, setIsRunning] = useState(false)
  const [runError, setRunError] = useState<string | null>(null)
  const [errorSql, setErrorSql] = useState<string | null>(null)
  const [reportFilters, setReportFilters] = useState<ReportFilterDraft[]>([])
  const [reportSorts, setReportSorts] = useState<ReportSortDraft[]>([])
  const [previewMode, setPreviewMode] = useState(false)
  const [previewLimit, setPreviewLimit] = useState(50)
  const [activeId, setActiveId] = useState<string | null>(null)
  const [activeDragData, setActiveDragData] = useState<{
    type: 'field' | 'metric' | 'selected' | 'calculated'
    tableName?: string
    column?: ColumnSchema
    metadataField?: MetadataField
    metric?: MetadataMetric
    field?: SelectedField
    calculatedField?: CalculatedField
  } | null>(null)
  const [relationshipModalOpen, setRelationshipModalOpen] = useState(false)
  const [connectSourceOpen, setConnectSourceOpen] = useState(false)
  const [connectedSource, setConnectedSource] = useState<string | null>(null)
  const [loadedTables, setLoadedTables] = useState<TableSchema[] | null>(null)
  
  // Filter and Sort state
  const [appliedFilters, setAppliedFilters] = useState<AppliedFilter[]>([])
  const [appliedSorts, setAppliedSorts] = useState<AppliedSort[]>([])
  const [filterBuilderOpen, setFilterBuilderOpen] = useState(false)
  const [sortBuilderOpen, setSortBuilderOpen] = useState(false)

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    })
  )

  useEffect(() => {
    let isMounted = true

    async function loadMetadata() {
      if (!datasetId) {
        setMetadata(null)
        setMetadataLoading(false)
        setMetadataError(null)
        return
      }

      try {
        setMetadataLoading(true)
        setMetadataError(null)
        const response = await getDatasetMetadata(datasetId)

        if (!isMounted) return
        setMetadata(response)
        setConnectionId(response.connectionId || null)

        if (process.env.NODE_ENV === 'development') {
          console.log('Active dataset', datasetId)
          console.table(response.tables.flatMap(table =>
            table.fields.map(field => ({
              fieldId: field.fieldId,
              displayName: field.displayName,
              tableId: field.tableId,
              role: field.role,
              sqlDataType: field.sqlDataType,
            }))
          ))
        }
      } catch (err) {
        if (!isMounted) return
        const message = err instanceof Error ? err.message : 'Failed to load dataset metadata'
        setMetadataError(message)
      } finally {
        if (isMounted) {
          setMetadataLoading(false)
        }
      }
    }

    loadMetadata()

    return () => {
      isMounted = false
    }
  }, [datasetId])

  const refreshMetadata = useCallback(async () => {
    if (!datasetId) {
      toast.info('No source connected')
      return
    }

    const response = await getDatasetMetadata(datasetId)
    setMetadata(response)
    setConnectionId(response.connectionId || connectionId)
  }, [datasetId, connectionId])

  const findMetadataField = useCallback((tableId?: string, columnName?: string) => {
    const normalize = (value: string) => value.toLowerCase().replace(/[^a-z0-9]/g, '')
    return metadata?.tables
      .find((table) => !tableId || table.tableId === tableId || normalize(table.tableId) === normalize(tableId))
      ?.fields.find((field) => !columnName || normalize(field.displayName) === normalize(columnName) || normalize(field.fieldId).endsWith(normalize(columnName)))
  }, [metadata])

  const semanticFilterOptions = useMemo<ReportFilterFieldOption[]>(() => {
    if (!metadata) return []

    const fieldOptions = metadata.tables.flatMap((table) =>
      table.fields.map((field) => ({
        fieldId: field.fieldId,
        label: `${table.displayName} / ${field.displayName}`,
        type: field.role === 'dimension' ? 'dimension' as const : 'dimension' as const,
        dataType: field.dataType,
      }))
    )

    const metricOptions = metadata.metrics.map((metric) => ({
      fieldId: metric.metricId,
      label: `Measures / ${metric.displayName}`,
      type: 'metric' as const,
      dataType: metric.dataType,
    }))

    return [...fieldOptions, ...metricOptions]
  }, [metadata])

  const sortFieldOptions = useMemo(() => {
    const selectedIds = selectedFields
      .filter((field) => field.kind === 'field' || (field.kind === 'metric' && !field.calculatedField))
      .map((field) => field.id)

    return semanticFilterOptions.filter((option) => selectedIds.includes(option.fieldId))
  }, [selectedFields, semanticFilterOptions])

  const addMetric = useCallback((metric: MetadataMetric) => {
    if (selectedFields.some(item => item.id === metric.metricId)) {
      toast.info('Metric already selected')
      return
    }

    const newField: SelectedField = {
      id: metric.metricId,
      displayName: metric.displayName,
      tableId: metric.baseTableId,
      tableName: metric.baseTableId,
      columnName: metric.displayName,
      dataType: metric.dataType,
      baseTableId: metric.baseTableId,
      aggregationBehavior: metric.aggregationBehavior,
      kind: 'metric',
      role: 'metric',
      placement: 'values',
    }

    setSelectedFields(prev => [...prev, newField])
    toast.success(`Added ${metric.displayName}`)
  }, [selectedFields])

  const addField = useCallback((field: MetadataField) => {
    const isHidden = field.isHidden === true
    const isDerived = field.role === 'derived_field' || field.isDerived

    if (isHidden) {
      toast.info(`${field.displayName} is hidden`)
      return
    }

    if (selectedFields.some(item => item.id === field.fieldId)) {
      toast.info('Field already selected')
      return
    }

    const newField: SelectedField = {
      id: field.fieldId,
      displayName: field.displayName,
      tableId: field.tableId,
      tableName: field.tableId,
      columnName: field.displayName,
      dataType: field.dataType,
      role: isDerived ? 'derived_field' : field.role,
      grain: field.grain,
      kind: isDerived ? 'derived' : 'field',
      placement: 'rows',
      aggregation: null,
    }

    setSelectedFields(prev => [...prev, newField])
    toast.success(`Added ${field.displayName}`)
  }, [selectedFields])

  const addCalculatedFieldToReport = useCallback((calcField: CalculatedField) => {
    if (selectedFields.some(f => f.id === calcField.id)) {
      toast.info('Field already selected')
      return
    }

    const newField: SelectedField = {
      id: calcField.id,
      tableName: calcField.type === 'measure' ? calcField.sourceTable || 'Calculated' : 'Calculated',
      columnName: calcField.name,
      dataType: 'decimal',
      kind: calcField.type,
      calculatedField: calcField,
    }

    setSelectedFields(prev => [...prev, newField])
    toast.success(`Added ${calcField.name}`)
  }, [selectedFields])

  const addCalculatedField = useCallback(async (field: CalculatedField) => {
    try {
      if (!datasetId) throw new Error('Connect a source before creating semantic fields.')

      if (field.type === 'measure') {
        const source = findMetadataField(field.sourceTable, field.sourceColumn)
        if (!source) throw new Error('Source field was not found in dataset metadata.')
        const aggregate = field.aggregationFunction === 'COUNT DISTINCT' ? 'COUNT_DISTINCT' : field.aggregationFunction
        const body: MetricRequest = {
          displayName: field.name,
          formula: `${aggregate}([${source.fieldId}])`,
          baseTableId: source.tableId,
          aggregationBehavior: 'additive',
          dataType: source.semanticType === 'currency' ? 'currency' : source.semanticType === 'percentage' ? 'percentage' : source.semanticType === 'number' ? 'decimal' : 'decimal',
          format: source.format === 'currency' || source.format === 'percentage' ? source.format : 'decimal',
          isHidden: false,
          isDraggable: true,
        }
        const validation = await validateMetric(datasetId, body)
        if (!validation.valid) throw new Error(validation.errors.join(' '))
        await createMetric(datasetId, body)
        await refreshMetadata()
        return
      }

      if (field.type === 'derived') {
        const baseTable = metadata?.tables[0]
        const firstField = baseTable?.fields[0]
        if (!baseTable || !firstField) throw new Error('No base field is available for the derived expression.')
        const body: DerivedFieldRequest = {
          displayName: field.name,
          baseTableId: baseTable.tableId,
          expression: field.expression?.includes('[') ? field.expression : `[${firstField.fieldId}]`,
          dataType: 'nvarchar',
          semanticType: 'category',
          format: 'general',
          isHidden: false,
          isDraggable: true,
        }
        const validation = await validateDerivedField(datasetId, body)
        if (!validation.valid) throw new Error(validation.errors.join(' '))
        await createDerivedField(datasetId, body)
        await refreshMetadata()
        return
      }

      setCalculatedFields(prev => [...prev, field])
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Unable to save calculated field')
    }
  }, [datasetId, findMetadataField, metadata, refreshMetadata])

  const deleteCalculatedField = useCallback((id: string) => {
    setCalculatedFields(prev => prev.filter(f => f.id !== id))
    // Also remove from selected fields if present
    setSelectedFields(prev => prev.filter(f => f.id !== id))
  }, [])

  const removeField = useCallback((id: string) => {
    setSelectedFields(prev => prev.filter(f => f.id !== id))
  }, [])

  const updateSelectedField = useCallback((id: string, patch: Partial<SelectedField>) => {
    setSelectedFields(prev => prev.map(field =>
      field.id === id ? { ...field, ...patch } : field
    ))
  }, [])

  const clearFields = useCallback(() => {
    setSelectedFields([])
    toast.success('All fields cleared')
  }, [])

  const resetReport = useCallback(() => {
    setSelectedFields([])
    setAppliedFilters([])
    setAppliedSorts([])
    setReportFilters([])
    setReportSorts([])
    setReportTitle('Untitled Sales Report')
    setReportDescription('')
    setLastRunTime(null)
    setResult(null)
    setRunError(null)
    setErrorSql(null)
    setRuntimePayload(null)
    toast.success('Report reset')
  }, [])

  const clearSource = useCallback(() => {
    setDatasetId(null)
    setConnectionId(null)
    setSourceConnection(null)
    setMetadata(null)
    setMetadataError(null)
    setMetadataLoading(false)
    setConnectedSource(null)
    setLoadedTables(null)
    setSelectedFields([])
    setAppliedFilters([])
    setAppliedSorts([])
    setReportFilters([])
    setReportSorts([])
    setResult(null)
    setRunError(null)
    setErrorSql(null)
    setRuntimePayload(null)
    toast.success('Source cleared')
  }, [])

  const buildVisualQueryRequest = useCallback((): VisualQueryRequest => buildRuntimeVisualQueryRequest({
    connectionId: connectionId ?? '',
    datasetId: metadata?.datasetId ?? datasetId ?? '',
    reportId: reportId ?? 'rpt_001',
    visualType: 'table',
    selectedFields,
    filters: reportFilters,
    sortRules: reportSorts,
    limit: previewLimit,
    offset: 0,
    metadata,
  }), [connectionId, datasetId, metadata, previewLimit, reportFilters, reportId, reportSorts, selectedFields])

  const addReportFilter = useCallback(() => {
    setReportFilters((current) => [
      ...current,
      {
        id: `filter-${Date.now()}`,
        field: semanticFilterOptions[0]?.fieldId ?? '',
        operator: '=',
        value: '',
      },
    ])
  }, [semanticFilterOptions])

  const updateReportFilter = useCallback((id: string, patch: Partial<ReportFilterDraft>) => {
    setReportFilters((current) => current.map((filter) =>
      filter.id === id ? { ...filter, ...patch } : filter
    ))
  }, [])

  const removeReportFilter = useCallback((id: string) => {
    setReportFilters((current) => current.filter((filter) => filter.id !== id))
  }, [])

  const addReportSort = useCallback(() => {
    setReportSorts((current) => [
      ...current,
      {
        id: `sort-${Date.now()}`,
        field: sortFieldOptions[0]?.fieldId ?? semanticFilterOptions[0]?.fieldId ?? '',
        direction: 'ASC',
      },
    ])
  }, [sortFieldOptions, semanticFilterOptions])

  const updateReportSort = useCallback((id: string, patch: Partial<ReportSortDraft>) => {
    setReportSorts((current) => current.map((sort) =>
      sort.id === id ? { ...sort, ...patch } : sort
    ))
  }, [])

  const removeReportSort = useCallback((id: string) => {
    setReportSorts((current) => current.filter((sort) => sort.id !== id))
  }, [])

  const runReport = useCallback(async () => {
    if (!datasetId || !connectionId || !metadata) {
      toast.warning('Connect a source before running report')
      return
    }

    if (selectedFields.length === 0) {
      toast.warning('Select at least one field before running report')
      return
    }

    try {
      setIsRunning(true)
      setResult(null)
      setRunError(null)
      setErrorSql(null)

      const request = buildVisualQueryRequest()
      setRuntimePayload(request)

      if (request.rows.length === 0 && request.values.length === 0) {
        throw new Error('Selected fields are not available in the current semantic model.')
      }

      const queryResult = await executeReportQuery(request)
      setResult(queryResult)

      const now = new Date()
      const timeString = now.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      })
      setLastRunTime(timeString)
      toast.success('Report executed successfully')
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Run report failed'
      const sql = err instanceof ReportApiError ? err.sql ?? null : null
      setRunError(message)
      setErrorSql(sql)
      toast.error('Run report failed')
    } finally {
      setIsRunning(false)
    }
  }, [selectedFields, buildVisualQueryRequest])

  const saveDraft = useCallback(async (duplicate: boolean) => {
    try {
      const request = buildVisualQueryRequest()
      const saved = await saveReportDefinition(duplicate ? null : reportId, {
        ...request,
        title: duplicate ? `${reportTitle} Copy` : reportTitle,
        description: reportDescription,
        layout: {},
        semanticModelVersion: 'v1',
      })
      setReportId(saved.reportId)
      if (duplicate) setReportTitle(saved.title)
      toast.success(duplicate ? 'Report duplicated' : 'Draft saved')
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Unable to save report')
    }
  }, [buildVisualQueryRequest, reportId, reportTitle, reportDescription])

  const handleDatasetLoaded = useCallback((dataset: LoadedDataset) => {
    if (datasetId && dataset.datasetId !== datasetId && selectedFields.length > 0) {
      const confirmed = window.confirm('Switching source will clear the current report configuration.')
      if (!confirmed) return
    }
    setDatasetId(dataset.datasetId)
    setConnectionId(dataset.connectionId)
    setSourceConnection(dataset.connection)
    setMetadata(dataset.metadata)
    setLoadedTables(null)
    setConnectedSource(dataset.displayName)
    setSelectedFields([])
    setAppliedFilters([])
    setAppliedSorts([])
    setReportFilters([])
    setReportSorts([])
    setResult(null)
    setRuntimePayload(null)
    setCalculatedFields([])
  }, [datasetId, selectedFields.length])

  const hasDataset = Boolean(datasetId && metadata)

  // Filter handlers
  const handleApplyFilters = useCallback((filters: AppliedFilter[]) => {
    setAppliedFilters(filters)
    const appliedCount = filters.filter(f => f.isApplied).length
    if (appliedCount > 0) {
      toast.success(`${appliedCount} filter${appliedCount !== 1 ? 's' : ''} applied`)
    }
  }, [])

  const handleRemoveFilter = useCallback((filterId: string) => {
    setAppliedFilters(prev => prev.filter(f => f.id !== filterId))
    toast.info('Filter removed')
  }, [])

  const handleEditFilter = useCallback(() => {
    console.log('[v0] Setting filterBuilderOpen to true')
    setFilterBuilderOpen(true)
  }, [])

  // Sort handlers
  const handleApplySorts = useCallback((sorts: AppliedSort[]) => {
    setAppliedSorts(sorts)
    if (sorts.length > 0) {
      toast.success(`${sorts.length} sort rule${sorts.length !== 1 ? 's' : ''} applied`)
    }
  }, [])

  const handleRemoveSort = useCallback((sortId: string) => {
    setAppliedSorts(prev => prev.filter(s => s.id !== sortId))
    toast.info('Sort removed')
  }, [])

  const handleEditSort = useCallback(() => {
    setSortBuilderOpen(true)
  }, [])

  const handleDragStart = useCallback((event: DragStartEvent) => {
    const { active } = event
    setActiveId(active.id as string)
    
    if (active.data.current?.type === 'field') {
      setActiveDragData({
        type: 'field',
        metadataField: active.data.current.field,
      })
    } else if (active.data.current?.type === 'metric') {
      setActiveDragData({
        type: 'metric',
        metric: active.data.current.metric,
      })
    } else if (active.data.current?.type === 'calculated') {
      setActiveDragData({
        type: 'calculated',
        calculatedField: active.data.current.calculatedField,
      })
    } else {
      const field = selectedFields.find(f => f.id === active.id)
      if (field) {
        setActiveDragData({
          type: 'selected',
          field,
        })
      }
    }
  }, [selectedFields])

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event

    setActiveId(null)
    setActiveDragData(null)

    if (!over) return

    // Handle dropping a new field from schema panel
    if (active.data.current?.type === 'field') {
      if (over.id === 'report-dropzone' || selectedFields.some(f => f.id === over.id)) {
        addField(active.data.current.field)
      }
      return
    }

    if (active.data.current?.type === 'metric') {
      if (over.id === 'report-dropzone' || selectedFields.some(f => f.id === over.id)) {
        addMetric(active.data.current.metric)
      }
      return
    }

    // Handle dropping a calculated field from schema panel
    if (active.data.current?.type === 'calculated') {
      if (over.id === 'report-dropzone' || selectedFields.some(f => f.id === over.id)) {
        const { calculatedField } = active.data.current
        addCalculatedFieldToReport(calculatedField)
      }
      return
    }

    // Handle reordering selected fields
    if (active.id !== over.id) {
      setSelectedFields((items) => {
        const oldIndex = items.findIndex(item => item.id === active.id)
        const newIndex = items.findIndex(item => item.id === over.id)
        
        if (oldIndex !== -1 && newIndex !== -1) {
          return arrayMove(items, oldIndex, newIndex)
        }
        return items
      })
    }
  }, [selectedFields, addField, addMetric, addCalculatedFieldToReport])

  const getKindIcon = (kind: SelectedField['kind']) => {
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

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
    >
      <div className="h-screen flex flex-col bg-background overflow-hidden">
        {/* Fixed Header Section */}
        <div className="flex-shrink-0">
          <GlobalToolbar 
            onOpenRelationshipManagement={() => {
              if (!hasDataset) {
                toast.info('No source connected')
                return
              }
              setRelationshipModalOpen(true)
            }}
            onConnectSource={() => setConnectSourceOpen(true)}
            onRefreshMetadata={refreshMetadata}
            onClearSource={clearSource}
            connectedSource={connectedSource}
            hasDataset={hasDataset}
          />
          <ReportHeader
            reportTitle={reportTitle}
            setReportTitle={setReportTitle}
            reportDescription={reportDescription}
            setReportDescription={setReportDescription}
            selectedFieldsCount={selectedFields.length}
            lastRunTime={lastRunTime}
            onClearFields={clearFields}
            previewMode={previewMode}
            setPreviewMode={setPreviewMode}
            onSaveDraft={() => saveDraft(false)}
            onDuplicate={() => saveDraft(true)}
          />
        </div>
        
        {/* Main Content Area - Takes remaining height */}
        <div className="flex-1 flex min-h-0 overflow-hidden">
          <div className="w-[30%] min-w-[280px] max-w-[400px] min-h-0">
            <SchemaPanel
              selectedFields={selectedFields}
              onAddField={addField}
              onAddMetric={addMetric}
              calculatedFields={calculatedFields}
              onAddCalculatedField={addCalculatedField}
              onDeleteCalculatedField={deleteCalculatedField}
              onAddCalculatedFieldToReport={addCalculatedFieldToReport}
              loadedTables={loadedTables}
              metadataTables={metadata?.tables ?? []}
              metadataMetrics={metadata?.metrics ?? []}
              metadataLoading={metadataLoading}
              metadataError={metadataError}
            />
          </div>
          <div className="flex-1 min-h-0">
            <ReportWorkspace
              selectedFields={selectedFields}
              onRemoveField={removeField}
              onUpdateField={updateSelectedField}
              previewLimit={previewLimit}
              onPreviewLimitChange={setPreviewLimit}
              appliedFilters={appliedFilters}
              appliedSorts={appliedSorts}
              onOpenFilterBuilder={() => setFilterBuilderOpen(true)}
              onOpenSortBuilder={() => setSortBuilderOpen(true)}
              onRemoveFilter={handleRemoveFilter}
              onRemoveSort={handleRemoveSort}
              onEditFilter={handleEditFilter}
              onEditSort={handleEditSort}
              result={result}
              isRunning={isRunning}
              runError={runError}
              errorSql={errorSql}
              reportFilters={reportFilters}
              filterFieldOptions={semanticFilterOptions}
              onAddReportFilter={addReportFilter}
              onUpdateReportFilter={updateReportFilter}
              onRemoveReportFilter={removeReportFilter}
              reportSorts={reportSorts}
              sortFieldOptions={sortFieldOptions}
              onAddReportSort={addReportSort}
              onUpdateReportSort={updateReportSort}
              onRemoveReportSort={removeReportSort}
              runtimePayload={runtimePayload}
            />
          </div>
        </div>
        
        {/* Fixed Footer */}
        <div className="flex-shrink-0">
          <ActionBar onRunReport={runReport} onReset={resetReport} isRunning={isRunning} canRun={hasDataset} />
        </div>
      </div>

      {/* Relationship Management Modal */}
      <ManageRelationshipsModal
        open={relationshipModalOpen}
        onOpenChange={setRelationshipModalOpen}
        datasetId={metadata?.datasetId ?? datasetId ?? ''}
        metadata={metadata}
        connection={sourceConnection}
        onRelationshipsChanged={refreshMetadata}
      />

      {/* Connect Source Flow */}
      <ConnectSourceFlow
        open={connectSourceOpen}
        onOpenChange={setConnectSourceOpen}
        onDatasetLoaded={handleDatasetLoaded}
      />

      {/* Filter Builder Dialog */}
      <FilterBuilderDialog
        open={filterBuilderOpen}
        onOpenChange={setFilterBuilderOpen}
        selectedFields={selectedFields}
        calculatedFields={calculatedFields}
        loadedTables={loadedTables}
        appliedFilters={appliedFilters}
        onApplyFilters={handleApplyFilters}
      />

      {/* Sort Builder Dialog */}
      <SortBuilderDialog
        open={sortBuilderOpen}
        onOpenChange={setSortBuilderOpen}
        selectedFields={selectedFields}
        appliedSorts={appliedSorts}
        onApplySorts={handleApplySorts}
      />

      {/* Drag Overlay */}
      <DragOverlay>
        {activeId && activeDragData ? (
          <div
            className={cn(
              'flex items-center gap-2 bg-card border border-primary rounded-lg px-3 py-2 shadow-xl',
            )}
          >
            <GripVertical className="h-4 w-4 text-muted-foreground" />
            {activeDragData.type === 'calculated' && activeDragData.calculatedField && (
              <>
                {getKindIcon(activeDragData.calculatedField.type)}
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium text-sm">
                    {activeDragData.calculatedField.name}
                  </span>
                  <span className="text-[10px] text-muted-foreground">
                    {activeDragData.calculatedField.type}
                  </span>
                </div>
                <Badge 
                  variant="secondary" 
                  className={cn(
                    'text-[10px] px-1.5 py-0 font-normal ml-1 capitalize', 
                    getKindBadgeColor(activeDragData.calculatedField.type)
                  )}
                >
                  {activeDragData.calculatedField.type}
                </Badge>
              </>
            )}
            {activeDragData.type === 'field' && (
              <>
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium text-sm">
                    {activeDragData.metadataField?.displayName}
                  </span>
                  <span className="text-[10px] text-muted-foreground">
                    {activeDragData.metadataField?.tableId}
                  </span>
                </div>
                <Badge 
                  variant="secondary" 
                  className={cn(
                    'text-[10px] px-1.5 py-0 font-normal ml-1', 
                    getDataTypeBadgeColor(activeDragData.metadataField?.dataType || '')
                  )}
                >
                  {activeDragData.metadataField?.dataType}
                </Badge>
              </>
            )}
            {activeDragData.type === 'metric' && (
              <>
                <Sigma className="h-3 w-3" />
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium text-sm">
                    {activeDragData.metric?.displayName}
                  </span>
                  <span className="text-[10px] text-muted-foreground">
                    {activeDragData.metric?.formula}
                  </span>
                </div>
                <Badge 
                  variant="secondary" 
                  className="text-[10px] px-1.5 py-0 font-normal ml-1 capitalize"
                >
                  metric
                </Badge>
              </>
            )}
            {activeDragData.type === 'selected' && activeDragData.field && (
              <>
                {getKindIcon(activeDragData.field.kind)}
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium text-sm">
                    {activeDragData.field.columnName}
                  </span>
                  <span className="text-[10px] text-muted-foreground">
                    {activeDragData.field.tableName}
                  </span>
                </div>
                <Badge 
                  variant="secondary" 
                  className={cn(
                    'text-[10px] px-1.5 py-0 font-normal ml-1', 
                    activeDragData.field.kind === 'field' || activeDragData.field.kind === 'column'
                      ? getDataTypeBadgeColor(activeDragData.field.dataType)
                      : getKindBadgeColor(activeDragData.field.kind)
                  )}
                >
                  {activeDragData.field.kind === 'field' || activeDragData.field.kind === 'column'
                    ? activeDragData.field.dataType 
                    : activeDragData.field.kind}
                </Badge>
              </>
            )}
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  )
}
