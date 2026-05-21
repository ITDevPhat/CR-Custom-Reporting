'use client'

import { useMemo } from 'react'
import { type DatasetMetadataResponse } from '@/lib/report-metadata-api'
import { getAvailableFields, getFieldsForDerivedExpression, getFieldsForFilters, getFieldsForMetricExpression } from '@/lib/metadata-selectors'

export function useActiveSemanticModel(activeConnectionId: string | null, activeDatasetId: string | null, metadata: DatasetMetadataResponse | null, isLoading: boolean, error: string | null) {
  const tables = metadata?.tables ?? []
  const fields = getAvailableFields(metadata)
  const metrics = metadata?.metrics ?? []
  const relationships = metadata?.relationships ?? []
  const factTables = tables.filter(t => t.tableType === 'fact')
  const measureCandidates = getFieldsForMetricExpression(metadata)
  const derivedFields = getFieldsForDerivedExpression(metadata).filter(f => f.isDerived || f.role === 'derived_field')
  const filterOptions = getFieldsForFilters(metadata)

  return useMemo(() => ({
    activeConnectionId,
    activeDatasetId,
    metadata,
    tables,
    fields,
    metrics,
    derivedFields,
    relationships,
    factTables,
    measureCandidates,
    filterOptions,
    sortOptions: filterOptions,
    isConnected: Boolean(activeDatasetId && activeConnectionId && metadata),
    isLoading,
    error,
  }), [activeConnectionId, activeDatasetId, metadata, tables, fields, metrics, derivedFields, relationships, factTables, measureCandidates, filterOptions, isLoading, error])
}
