'use client'

import { Save, Copy, Trash2, Eye } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'

interface ReportHeaderProps {
  reportTitle: string
  setReportTitle: (title: string) => void
  reportDescription: string
  setReportDescription: (desc: string) => void
  selectedFieldsCount: number
  lastRunTime: string | null
  onClearFields: () => void
  previewMode: boolean
  setPreviewMode: (mode: boolean) => void
  onSaveDraft: () => void
  onDuplicate: () => void
}

export function ReportHeader({
  reportTitle,
  setReportTitle,
  reportDescription,
  setReportDescription,
  selectedFieldsCount,
  lastRunTime,
  onClearFields,
  previewMode,
  setPreviewMode,
  onSaveDraft,
  onDuplicate,
}: ReportHeaderProps) {
  return (
    <div className="h-16 border-b border-border bg-card px-4 flex items-center justify-between">
      <div className="flex items-center gap-4 flex-1">
        <Input
          value={reportTitle}
          onChange={(e) => setReportTitle(e.target.value)}
          className="max-w-[250px] font-medium"
          placeholder="Report title..."
        />
        <Input
          value={reportDescription}
          onChange={(e) => setReportDescription(e.target.value)}
          className="max-w-[300px] text-sm"
          placeholder="Add report description..."
        />
      </div>
      
      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" className="gap-2" onClick={onSaveDraft}>
          <Save className="h-4 w-4" />
          Save Draft
        </Button>
        <Button variant="outline" size="sm" className="gap-2" onClick={onDuplicate}>
          <Copy className="h-4 w-4" />
          Duplicate
        </Button>
        <Button 
          variant="outline" 
          size="sm" 
          className="gap-2"
          onClick={onClearFields}
        >
          <Trash2 className="h-4 w-4" />
          Clear Fields
        </Button>
        <Button 
          variant={previewMode ? "default" : "outline"} 
          size="sm" 
          className="gap-2"
          onClick={() => setPreviewMode(!previewMode)}
        >
          <Eye className="h-4 w-4" />
          Preview Mode
        </Button>
        
        <Separator orientation="vertical" className="h-6 mx-2" />
        
        <div className="flex items-center gap-3 text-sm">
          <Badge variant="outline" className="font-normal">
            Selected fields: {selectedFieldsCount}
          </Badge>
          <span className="text-muted-foreground">
            Last run: {lastRunTime || 'Not run yet'}
          </span>
        </div>
      </div>
    </div>
  )
}
