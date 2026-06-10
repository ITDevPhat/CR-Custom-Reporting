'use client'

import { Play, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface ActionBarProps {
  onRunReport: () => void
  onReset: () => void
  isRunning?: boolean
  canRun?: boolean
}

export function ActionBar({
  onRunReport,
  onReset,
  isRunning = false,
  canRun = true,
}: ActionBarProps) {
  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 flex items-center gap-2 bg-card border border-border rounded-lg shadow-lg p-2">
      <Button
        variant="outline"
        size="sm"
        onClick={onReset}
        className="gap-2"
      >
        <RotateCcw className="h-4 w-4" />
        Reset
      </Button>

      <Button
        size="sm"
        onClick={onRunReport}
        disabled={isRunning || !canRun}
        className="gap-2"
      >
        <Play className="h-4 w-4" />
        {isRunning ? 'Running...' : 'Run Report'}
      </Button>
    </div>
  )
}