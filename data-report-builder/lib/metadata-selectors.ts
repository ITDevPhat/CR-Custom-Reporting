import { type DatasetMetadataResponse, type MetadataField, type MetadataMetric } from './report-metadata-api'

const NUMERIC_TYPES = new Set(['tinyint', 'smallint', 'int', 'bigint', 'decimal', 'numeric', 'float', 'real', 'money'])

export function getAvailableFields(metadata: DatasetMetadataResponse | null): MetadataField[] {
  if (!metadata) return []
  return metadata.tables.flatMap(table => table.fields).filter(field => !field.isHidden)
}

export function getFieldsForDerivedExpression(metadata: DatasetMetadataResponse | null): MetadataField[] {
  return getAvailableFields(metadata).filter(field => field.role !== 'metric')
}

export function getFieldsForMetricExpression(metadata: DatasetMetadataResponse | null): MetadataField[] {
  return getAvailableFields(metadata).filter((field) =>
    field.role === 'measure_candidate' ||
    NUMERIC_TYPES.has((field.sqlDataType || field.dataType).toLowerCase()) ||
    (field.defaultAggregation && field.defaultAggregation.toLowerCase() !== 'none'))
}

export function getFieldsForFilters(metadata: DatasetMetadataResponse | null): Array<MetadataField | MetadataMetric> {
  if (!metadata) return []
  return [...getAvailableFields(metadata), ...metadata.metrics.filter(metric => !metric.isHidden)]
}
