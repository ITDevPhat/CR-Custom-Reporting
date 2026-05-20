export type DataType = 'int' | 'nvarchar' | 'date' | 'tinyint' | 'smallint' | 'decimal'

export interface ColumnSchema {
  name: string
  dataType: DataType
}

export interface TableSchema {
  name: string
  columns: ColumnSchema[]
  isFact: boolean
}

// Calculated Fields Types
export type MetricFunction = 'SUM' | 'AVG' | 'MIN' | 'MAX' | 'COUNT' | 'COUNT DISTINCT'

export type CalculatedFieldType = 'metric' | 'measure' | 'derived'

export interface CalculatedField {
  id: string
  name: string
  type: CalculatedFieldType
  aggregationFunction?: MetricFunction
  sourceTable?: string
  sourceColumn?: string
  expression?: string
}

// Updated SelectedField to support calculated fields
export interface SelectedField {
  id: string
  displayName?: string
  tableId?: string
  tableName: string
  columnName: string
  dataType: DataType | string
  role?: string
  grain?: string
  baseTableId?: string
  aggregationBehavior?: string
  kind: 'field' | 'metric' | 'column' | 'measure' | 'derived'
  calculatedField?: CalculatedField
}

// Relationship Types
export type RelationshipCardinality = 'many-to-one' | 'one-to-many' | 'one-to-one' | 'many-to-many'
export type CrossFilterDirection = 'single' | 'both'

export interface Relationship {
  id: string
  fromTable: string
  fromColumn: string
  toTable: string
  toColumn: string
  cardinality: RelationshipCardinality
  crossFilterDirection: CrossFilterDirection
  isActive: boolean
  assumeReferentialIntegrity: boolean
  applySecurityFilterBothDirections: boolean
}

// Default relationships
export const defaultRelationships: Relationship[] = [
  {
    id: 'rel-1',
    fromTable: 'FactSales',
    fromColumn: 'CustomerKey',
    toTable: 'DimCustomer',
    toColumn: 'CustomerKey',
    cardinality: 'many-to-one',
    crossFilterDirection: 'single',
    isActive: true,
    assumeReferentialIntegrity: false,
    applySecurityFilterBothDirections: false,
  },
  {
    id: 'rel-2',
    fromTable: 'FactSales',
    fromColumn: 'LocationKey',
    toTable: 'DimLocation',
    toColumn: 'LocationKey',
    cardinality: 'many-to-one',
    crossFilterDirection: 'single',
    isActive: true,
    assumeReferentialIntegrity: false,
    applySecurityFilterBothDirections: false,
  },
  {
    id: 'rel-3',
    fromTable: 'FactSales',
    fromColumn: 'OrderDateKey',
    toTable: 'DimDate',
    toColumn: 'DateKey',
    cardinality: 'many-to-one',
    crossFilterDirection: 'single',
    isActive: true,
    assumeReferentialIntegrity: false,
    applySecurityFilterBothDirections: false,
  },
  {
    id: 'rel-4',
    fromTable: 'FactSales',
    fromColumn: 'ProductKey',
    toTable: 'DimProduct',
    toColumn: 'ProductKey',
    cardinality: 'many-to-one',
    crossFilterDirection: 'single',
    isActive: true,
    assumeReferentialIntegrity: false,
    applySecurityFilterBothDirections: false,
  },
  {
    id: 'rel-5',
    fromTable: 'FactSales',
    fromColumn: 'ShipModeKey',
    toTable: 'DimShipMode',
    toColumn: 'ShipModeKey',
    cardinality: 'many-to-one',
    crossFilterDirection: 'single',
    isActive: true,
    assumeReferentialIntegrity: false,
    applySecurityFilterBothDirections: false,
  },
]

export const schemaData: TableSchema[] = [
  {
    name: 'DimCustomer',
    isFact: false,
    columns: [
      { name: 'CustomerKey', dataType: 'int' },
      { name: 'CustomerID', dataType: 'nvarchar' },
      { name: 'CustomerName', dataType: 'nvarchar' },
      { name: 'Segment', dataType: 'nvarchar' },
      { name: 'Customer Value Tag', dataType: 'nvarchar' },
    ],
  },
  {
    name: 'DimDate',
    isFact: false,
    columns: [
      { name: 'DateKey', dataType: 'int' },
      { name: 'FullDate', dataType: 'date' },
      { name: 'DayNumber', dataType: 'tinyint' },
      { name: 'MonthNumber', dataType: 'tinyint' },
      { name: 'MonthName', dataType: 'nvarchar' },
      { name: 'QuarterNumber', dataType: 'tinyint' },
      { name: 'YearNumber', dataType: 'smallint' },
    ],
  },
  {
    name: 'DimLocation',
    isFact: false,
    columns: [
      { name: 'LocationKey', dataType: 'int' },
      { name: 'Country', dataType: 'nvarchar' },
      { name: 'Region', dataType: 'nvarchar' },
      { name: 'State', dataType: 'nvarchar' },
      { name: 'City', dataType: 'nvarchar' },
      { name: 'PostalCode', dataType: 'nvarchar' },
    ],
  },
  {
    name: 'DimProduct',
    isFact: false,
    columns: [
      { name: 'ProductKey', dataType: 'int' },
      { name: 'ProductID', dataType: 'nvarchar' },
      { name: 'ProductName', dataType: 'nvarchar' },
      { name: 'Category', dataType: 'nvarchar' },
      { name: 'SubCategory', dataType: 'nvarchar' },
    ],
  },
  {
    name: 'DimShipMode',
    isFact: false,
    columns: [
      { name: 'ShipModeKey', dataType: 'int' },
      { name: 'ShipMode', dataType: 'nvarchar' },
    ],
  },
  {
    name: 'FactSales',
    isFact: true,
    columns: [
      { name: 'SalesKey', dataType: 'int' },
      { name: 'OrderID', dataType: 'nvarchar' },
      { name: 'OrderDateKey', dataType: 'int' },
      { name: 'ShipDateKey', dataType: 'int' },
      { name: 'CustomerKey', dataType: 'int' },
      { name: 'ProductKey', dataType: 'int' },
      { name: 'LocationKey', dataType: 'int' },
      { name: 'ShipModeKey', dataType: 'int' },
      { name: 'SalesAmount', dataType: 'decimal' },
      { name: 'Quantity', dataType: 'int' },
      { name: 'Discount', dataType: 'decimal' },
      { name: 'Discount Band', dataType: 'nvarchar' },
      { name: 'ProfitAmount', dataType: 'decimal' },
    ],
  },
]

export function generateMockValue(dataType: DataType, columnName: string, rowIndex: number): string {
  const customerNames = ['John Smith', 'Jane Doe', 'Bob Wilson', 'Alice Brown', 'Charlie Davis', 'Eva Martinez', 'Frank Johnson', 'Grace Lee']
  const segments = ['Consumer', 'Corporate', 'Home Office', 'Small Business']
  const valueTags = ['High Value', 'Medium Value', 'Low Value', 'New Customer']
  const countries = ['United States', 'Canada', 'United Kingdom', 'Germany', 'France', 'Australia', 'Japan', 'Brazil']
  const regions = ['North', 'South', 'East', 'West', 'Central', 'Northeast', 'Southeast', 'Northwest']
  const states = ['California', 'Texas', 'New York', 'Florida', 'Illinois', 'Pennsylvania', 'Ohio', 'Georgia']
  const cities = ['New York', 'Los Angeles', 'Chicago', 'Houston', 'Phoenix', 'Philadelphia', 'San Antonio', 'San Diego']
  const products = ['Office Chair', 'Desk Lamp', 'Monitor Stand', 'Keyboard', 'Mouse Pad', 'Notebook', 'Pen Set', 'Stapler']
  const categories = ['Furniture', 'Office Supplies', 'Technology', 'Storage']
  const subCategories = ['Chairs', 'Tables', 'Paper', 'Pens', 'Phones', 'Copiers', 'Bookcases', 'Labels']
  const shipModes = ['Standard Class', 'Second Class', 'First Class', 'Same Day']
  const months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August']
  const discountBands = ['None', 'Low', 'Medium', 'High']

  switch (dataType) {
    case 'int':
      if (columnName.toLowerCase().includes('key')) {
        return String(1000 + rowIndex)
      }
      if (columnName.toLowerCase().includes('quantity')) {
        return String(Math.floor(Math.random() * 50) + 1)
      }
      return String(Math.floor(Math.random() * 1000) + 1)
    case 'tinyint':
      if (columnName.toLowerCase().includes('day')) {
        return String(Math.floor(Math.random() * 28) + 1)
      }
      if (columnName.toLowerCase().includes('month')) {
        return String(Math.floor(Math.random() * 12) + 1)
      }
      if (columnName.toLowerCase().includes('quarter')) {
        return String(Math.floor(Math.random() * 4) + 1)
      }
      return String(Math.floor(Math.random() * 100) + 1)
    case 'smallint':
      if (columnName.toLowerCase().includes('year')) {
        return String(2020 + Math.floor(Math.random() * 6))
      }
      return String(Math.floor(Math.random() * 1000) + 1)
    case 'decimal':
      if (columnName.toLowerCase().includes('discount')) {
        return (Math.random() * 0.3).toFixed(2)
      }
      return '$' + (Math.random() * 5000 + 100).toFixed(2)
    case 'date':
      const year = 2023 + Math.floor(Math.random() * 3)
      const month = String(Math.floor(Math.random() * 12) + 1).padStart(2, '0')
      const day = String(Math.floor(Math.random() * 28) + 1).padStart(2, '0')
      return `${year}-${month}-${day}`
    case 'nvarchar':
      if (columnName.toLowerCase().includes('customername')) return customerNames[rowIndex % customerNames.length]
      if (columnName.toLowerCase().includes('customerid')) return `CUST-${String(rowIndex + 1).padStart(4, '0')}`
      if (columnName.toLowerCase().includes('segment')) return segments[rowIndex % segments.length]
      if (columnName.toLowerCase().includes('value tag')) return valueTags[rowIndex % valueTags.length]
      if (columnName.toLowerCase().includes('country')) return countries[rowIndex % countries.length]
      if (columnName.toLowerCase().includes('region')) return regions[rowIndex % regions.length]
      if (columnName.toLowerCase().includes('state')) return states[rowIndex % states.length]
      if (columnName.toLowerCase().includes('city')) return cities[rowIndex % cities.length]
      if (columnName.toLowerCase().includes('postalcode')) return String(10000 + Math.floor(Math.random() * 90000))
      if (columnName.toLowerCase().includes('productname')) return products[rowIndex % products.length]
      if (columnName.toLowerCase().includes('productid')) return `PROD-${String(rowIndex + 1).padStart(4, '0')}`
      if (columnName.toLowerCase().includes('category')) return categories[rowIndex % categories.length]
      if (columnName.toLowerCase().includes('subcategory')) return subCategories[rowIndex % subCategories.length]
      if (columnName.toLowerCase().includes('shipmode')) return shipModes[rowIndex % shipModes.length]
      if (columnName.toLowerCase().includes('orderid')) return `ORD-${String(2024000 + rowIndex).padStart(7, '0')}`
      if (columnName.toLowerCase().includes('monthname')) return months[rowIndex % months.length]
      if (columnName.toLowerCase().includes('discount band')) return discountBands[rowIndex % discountBands.length]
      return `Value ${rowIndex + 1}`
    default:
      return `Value ${rowIndex + 1}`
  }
}

export function generateCalculatedMockValue(rowIndex: number): string {
  return '$' + (Math.random() * 10000 + 500).toFixed(2)
}

// Generate mock table data for relationship preview
export function generateTableMockData(tableName: string, rowCount: number = 5): Record<string, string>[] {
  const table = schemaData.find(t => t.name === tableName)
  if (!table) return []

  return Array.from({ length: rowCount }, (_, rowIndex) => {
    const row: Record<string, string> = {}
    table.columns.forEach((column) => {
      row[column.name] = generateMockValue(column.dataType, column.name, rowIndex)
    })
    return row
  })
}
