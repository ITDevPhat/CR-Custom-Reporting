'use client'

import { Database, Network, RefreshCw, User } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { toast } from 'sonner'

interface GlobalToolbarProps {
  onOpenRelationshipManagement: () => void
  onConnectSource: () => void
  onRefreshMetadata: () => void
  onClearSource: () => void
  connectedSource: string | null
  hasDataset: boolean
}

export function GlobalToolbar({
  onOpenRelationshipManagement,
  onConnectSource,
  onRefreshMetadata,
  onClearSource,
  connectedSource,
  hasDataset,
}: GlobalToolbarProps) {
  return (
    <header className="h-14 border-b border-border bg-card px-4 flex items-center justify-between">
      <div className="flex items-center gap-2">
        <div>
          <h1 className="text-lg font-semibold text-foreground">Data Report Builder</h1>
          <p className="text-xs text-muted-foreground">Drag fields to build custom reports</p>
        </div>
      </div>
      
      <div className="flex items-center gap-2">
        <Button 
          variant="outline" 
          size="sm" 
          className="gap-2"
          onClick={onConnectSource}
        >
          <Database className="h-4 w-4" />
          Connect Source
        </Button>
        <Button 
          variant="outline" 
          size="sm" 
          className="gap-2"
          onClick={onOpenRelationshipManagement}
          disabled={!hasDataset}
        >
          <Network className="h-4 w-4" />
          Relationship Management
        </Button>
        <Button 
          variant="outline" 
          size="sm" 
          className="gap-2"
          disabled={!hasDataset}
          onClick={() => {
            if (!hasDataset) {
              toast.info('No source connected')
              return
            }
            onRefreshMetadata()
            toast.success('Metadata refreshed')
          }}
        >
          <RefreshCw className="h-4 w-4" />
          Refresh Metadata
        </Button>
      </div>
      
      <div className="flex items-center gap-3">
        {connectedSource ? (
          <Badge variant="default" className="text-xs gap-1.5">
            <Database className="h-3 w-3" />
            Connected: {connectedSource}
          </Badge>
        ) : (
          <Badge variant="secondary" className="text-xs">
            No source connected
          </Badge>
        )}
        {hasDataset && (
          <Button variant="ghost" size="sm" onClick={onClearSource}>
            Clear Source
          </Button>
        )}
        <Badge variant="secondary" className="text-xs">Prototype</Badge>
        <Button variant="ghost" size="icon" className="rounded-full">
          <User className="h-5 w-5" />
        </Button>
      </div>
    </header>
  )
}
