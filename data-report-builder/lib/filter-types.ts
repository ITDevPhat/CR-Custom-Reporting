// Filter Types and State Management

export type FilterType = 'basic' | 'advanced' | 'topN'
export type FilterFieldKind = 'text' | 'number' | 'date'

// Numeric Filter Operators
export type NumericFilterOperator =
  | 'is'
  | 'is not'
  | 'is less than'
  | 'is less than or equal to'
  | 'is greater than'
  | 'is greater than or equal to'
  | 'is blank'
  | 'is not blank'

// Text Filter Operators
export type TextFilterOperator =
  | 'contains'
  | 'does not contain'
  | 'starts with'
  | 'does not start with'
  | 'is'
  | 'is not'
  | 'is blank'
  | 'is not blank'
  | 'is empty'
  | 'is not empty'

// Date Filter Operators
export type DateFilterOperator =
  | 'is'
  | 'is not'
  | 'is after'
  | 'is on or after'
  | 'is before'
  | 'is on or before'
  | 'is blank'
  | 'is not blank'

export type FilterOperator = NumericFilterOperator | TextFilterOperator | DateFilterOperator

export interface FilterCondition {
  operator: FilterOperator
  value: string
}

export interface TopNConfig {
  direction: 'top' | 'bottom'
  count: number
  byFieldId: string | null
  byFieldName: string | null
}

export interface AppliedFilter {
  id: string
  fieldId: string
  fieldName: string
  fieldKind: FilterFieldKind
  sourceType: 'field' | 'column' | 'metric' | 'measure' | 'derived'
  sourceInfo: string
  dataType: string
  filterType: FilterType
  logic: 'AND' | 'OR'
  conditions: FilterCondition[]
  selectedValues: string[] // For basic filtering
  topNConfig: TopNConfig | null
  summary: string
  isApplied: boolean
}

export interface AppliedSort {
  id: string
  fieldId: string
  fieldName: string
  direction: 'ASC' | 'DESC'
}

// Determine field kind based on data type
export function getFieldKind(dataType: string, fieldName: string): FilterFieldKind {
  const lowerType = dataType.toLowerCase()
  const lowerName = fieldName.toLowerCase()
  
  // Number-like fields
  if (['int', 'tinyint', 'smallint', 'decimal', 'numeric', 'float', 'real', 'bigint'].includes(lowerType)) {
    return 'number'
  }
  
  // Date-like fields
  if (lowerType === 'date' || lowerType === 'datetime' || lowerType === 'datetime2') {
    return 'date'
  }
  
  // Fields with "Date" in name that are int (like DateKey) can be treated as date
  if (lowerName.includes('date') && lowerType === 'int') {
    return 'date'
  }
  
  // Default to text
  return 'text'
}

// Check if operator requires a value input
export function operatorRequiresValue(operator: FilterOperator): boolean {
  return !['is blank', 'is not blank', 'is empty', 'is not empty'].includes(operator)
}

// Generate filter summary
export function generateFilterSummary(filter: AppliedFilter): string {
  const { fieldName, filterType, logic, conditions, selectedValues, topNConfig } = filter

  if (filterType === 'basic') {
    if (selectedValues.length === 0) return `${fieldName} (no values selected)`
    if (selectedValues.length === 1) return `${fieldName} is ${selectedValues[0]}`
    return `${fieldName} in ${selectedValues.length} values`
  }

  if (filterType === 'topN' && topNConfig) {
    const byField = topNConfig.byFieldName || 'value'
    return `${topNConfig.direction === 'top' ? 'Top' : 'Bottom'} ${topNConfig.count} ${fieldName} by ${byField}`
  }

  // Advanced filtering
  const validConditions = conditions.filter(c => c.operator)
  if (validConditions.length === 0) return `${fieldName} (no conditions)`
  
  if (validConditions.length === 1) {
    const c = validConditions[0]
    if (operatorRequiresValue(c.operator)) {
      return `${fieldName} ${c.operator} ${c.value || '(empty)'}`
    }
    return `${fieldName} ${c.operator}`
  }

  const summaries = validConditions.map(c => {
    if (operatorRequiresValue(c.operator)) {
      return `${c.operator} ${c.value || '(empty)'}`
    }
    return c.operator
  })

  return `${fieldName} ${summaries.join(` ${logic} `)}`
}

// Numeric operators list
export const numericOperators: NumericFilterOperator[] = [
  'is',
  'is not',
  'is less than',
  'is less than or equal to',
  'is greater than',
  'is greater than or equal to',
  'is blank',
  'is not blank',
]

// Text operators list
export const textOperators: TextFilterOperator[] = [
  'contains',
  'does not contain',
  'starts with',
  'does not start with',
  'is',
  'is not',
  'is blank',
  'is not blank',
  'is empty',
  'is not empty',
]

// Date operators list
export const dateOperators: DateFilterOperator[] = [
  'is',
  'is not',
  'is after',
  'is on or after',
  'is before',
  'is on or before',
  'is blank',
  'is not blank',
]

// Mock distinct values for basic filtering
export function getMockDistinctValues(fieldName: string): { value: string; count: number }[] {
  const lowerName = fieldName.toLowerCase()
  
  if (lowerName.includes('productname')) {
    return [
      { value: 'While you Were Out', count: 45 },
      { value: '#10-4 1/8 x 9 1/2', count: 32 },
      { value: '#10 Gummed Flap', count: 28 },
      { value: 'Bush Birmingham Bookcase', count: 67 },
      { value: 'Sauder Camden Bookcase', count: 54 },
      { value: 'Office Chair Pro', count: 89 },
      { value: 'Desk Lamp LED', count: 112 },
      { value: 'Monitor Stand Adjustable', count: 76 },
    ]
  }
  
  if (lowerName.includes('segment')) {
    return [
      { value: 'Consumer', count: 5234 },
      { value: 'Corporate', count: 3210 },
      { value: 'Home Office', count: 1892 },
      { value: 'Small Business', count: 945 },
    ]
  }
  
  if (lowerName.includes('category')) {
    return [
      { value: 'Furniture', count: 2145 },
      { value: 'Office Supplies', count: 4532 },
      { value: 'Technology', count: 1876 },
      { value: 'Storage', count: 989 },
    ]
  }
  
  if (lowerName.includes('region')) {
    return [
      { value: 'North', count: 1234 },
      { value: 'South', count: 2345 },
      { value: 'East', count: 1876 },
      { value: 'West', count: 2098 },
      { value: 'Central', count: 1567 },
    ]
  }
  
  if (lowerName.includes('country')) {
    return [
      { value: 'United States', count: 5432 },
      { value: 'Canada', count: 1234 },
      { value: 'United Kingdom', count: 876 },
      { value: 'Germany', count: 654 },
      { value: 'France', count: 543 },
    ]
  }
  
  if (lowerName.includes('shipmode')) {
    return [
      { value: 'Standard Class', count: 4567 },
      { value: 'Second Class', count: 2345 },
      { value: 'First Class', count: 1234 },
      { value: 'Same Day', count: 567 },
    ]
  }

  // Default values
  return [
    { value: 'Value A', count: 234 },
    { value: 'Value B', count: 189 },
    { value: 'Value C', count: 156 },
    { value: 'Value D', count: 123 },
    { value: 'Value E', count: 98 },
  ]
}
