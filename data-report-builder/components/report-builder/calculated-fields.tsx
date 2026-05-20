'use client'

import { useState } from 'react'
import { Calculator, Sigma } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { toast } from 'sonner'
import {
  type CalculatedField,
  type MetricFunction,
  schemaData,
} from '@/lib/schema-data'

const metricFunctions: MetricFunction[] = ['SUM', 'AVG', 'MIN', 'MAX', 'COUNT', 'COUNT DISTINCT']

interface CreateMetricModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (field: CalculatedField) => void
}

export function CreateMetricModal({ open, onOpenChange, onSave }: CreateMetricModalProps) {
  const [name, setName] = useState('')
  const [aggregationFunction, setAggregationFunction] = useState<MetricFunction>('SUM')

  const handleSave = () => {
    if (!name.trim()) {
      toast.error('Please enter a metric name')
      return
    }

    const newMetric: CalculatedField = {
      id: `metric-${Date.now()}`,
      name: name.trim(),
      type: 'metric',
      aggregationFunction,
    }

    onSave(newMetric)
    setName('')
    setAggregationFunction('SUM')
    onOpenChange(false)
    toast.success(`Metric "${name}" created`)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[400px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Calculator className="h-5 w-5 text-cyan-600" />
            Create Metric
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="metricName">Metric Name</Label>
            <Input
              id="metricName"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g., Total"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="aggregationFunction">Aggregation Function</Label>
            <Select value={aggregationFunction} onValueChange={(v) => setAggregationFunction(v as MetricFunction)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {metricFunctions.map((fn) => (
                  <SelectItem key={fn} value={fn}>
                    {fn}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSave}>Create Metric</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

interface CreateMeasureModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSave: (field: CalculatedField) => void
}

export function CreateMeasureModal({ open, onOpenChange, onSave }: CreateMeasureModalProps) {
  const [name, setName] = useState('')
  const [aggregationFunction, setAggregationFunction] = useState<MetricFunction>('SUM')
  const [sourceTable, setSourceTable] = useState('FactSales')
  const [sourceColumn, setSourceColumn] = useState('SalesAmount')

  const selectedTable = schemaData.find(t => t.name === sourceTable)
  const availableColumns = selectedTable?.columns || []

  const handleSave = () => {
    if (!name.trim()) {
      toast.error('Please enter a measure name')
      return
    }

    const newMeasure: CalculatedField = {
      id: `measure-${Date.now()}`,
      name: name.trim(),
      type: 'measure',
      aggregationFunction,
      sourceTable,
      sourceColumn,
    }

    onSave(newMeasure)
    setName('')
    setAggregationFunction('SUM')
    setSourceTable('FactSales')
    setSourceColumn('SalesAmount')
    onOpenChange(false)
    toast.success(`Measure "${name}" created`)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[450px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sigma className="h-5 w-5 text-orange-600" />
            Create Measure
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="measureName">Measure Name</Label>
            <Input
              id="measureName"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g., Total Sales"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="measureAggregation">Aggregation Function</Label>
            <Select value={aggregationFunction} onValueChange={(v) => setAggregationFunction(v as MetricFunction)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {metricFunctions.map((fn) => (
                  <SelectItem key={fn} value={fn}>
                    {fn}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="sourceTable">Source Table</Label>
            <Select value={sourceTable} onValueChange={(v) => {
              setSourceTable(v)
              const table = schemaData.find(t => t.name === v)
              if (table && table.columns.length > 0) {
                setSourceColumn(table.columns[0].name)
              }
            }}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {schemaData.map((table) => (
                  <SelectItem key={table.name} value={table.name}>
                    {table.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="sourceColumn">Source Column</Label>
            <Select value={sourceColumn} onValueChange={setSourceColumn}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {availableColumns.map((col) => (
                  <SelectItem key={col.name} value={col.name}>
                    {col.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSave}>Create Measure</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
