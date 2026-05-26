/**
 * [NEW FILE] Report Runs Page
 *
 * This page displays a list of report executions fetched from GET /api/report-executions.
 * Mock data is used ONLY as an isolated fallback for local UI preview when the backend is unavailable.
 *
 * Created: 2024
 */
'use client'

import { useState, useCallback, useEffect } from 'react'
import useSWR from 'swr'
import Link from 'next/link'
import { toast } from 'sonner'
import {
  FileText,
  Table2,
  FileSpreadsheet,
  FileType,
  RefreshCw,
  ArrowLeft,
  Download,
  Search,
  Loader2,
  AlertCircle,
  Copy,
  Eye,
  RotateCcw,
  X,
  CheckCircle2,
  Clock,
  XCircle,
  AlertTriangle,
  HardDrive,
  Cloud,
  Info,
  AlertOctagon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { Skeleton } from '@/components/ui/skeleton'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import {
  getReportExecutions,
  getReportExecution,
  downloadReportExecution,
  triggerBlobDownload,
  type ReportExecution,
  type ReportExecutionDetail,
  type ReportExecutionStatus,
  type ReportExecutionsResponse,
  type StorageMode,
  type ExportFormat,
} from '@/lib/report-executions-api'
import { cn } from '@/lib/utils'

// --- Utility functions ---

function formatDuration(durationMs?: number): string {
  if (durationMs === undefined || durationMs === null) return '-'
  if (durationMs < 1000) return `${durationMs} ms`
  return `${(durationMs / 1000).toFixed(1)}s`
}

function formatRowCount(count?: number): string {
  if (count === undefined || count === null) return '-'
  return count.toLocaleString()
}

function formatDateTime(isoString?: string): string {
  if (!isoString) return '-'
  const date = new Date(isoString)
  return date.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function getStatusColor(status: ReportExecutionStatus) {
  switch (status) {
    case 'Completed':
      return 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300'
    case 'Processing':
      return 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300'
    case 'Requested':
      return 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300'
    case 'Failed':
      return 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300'
    case 'ArtifactMissing':
      return 'bg-orange-100 text-orange-700 dark:bg-orange-900/50 dark:text-orange-300'
    case 'Expired':
      return 'bg-slate-200 text-slate-500 dark:bg-slate-700 dark:text-slate-400'
  }
}

function getStatusIcon(status: ReportExecutionStatus) {
  switch (status) {
    case 'Completed':
      return <CheckCircle2 className="h-3 w-3" />
    case 'Processing':
      return <Loader2 className="h-3 w-3 animate-spin" />
    case 'Requested':
      return <Clock className="h-3 w-3" />
    case 'Failed':
      return <XCircle className="h-3 w-3" />
    case 'ArtifactMissing':
      return <AlertTriangle className="h-3 w-3" />
    case 'Expired':
      return <Clock className="h-3 w-3" />
  }
}

function getStorageIcon(mode: StorageMode) {
  return mode === 'S3' ? (
    <Cloud className="h-3 w-3" />
  ) : (
    <HardDrive className="h-3 w-3" />
  )
}

function canDownload(execution: ReportExecution): boolean {
  return execution.status === 'Completed' && execution.artifactAvailable === true
}
function canPreview(execution: ReportExecution): boolean {
  return execution.status === 'Completed' && execution.artifactAvailable === true
}

function getDownloadDisabledReason(execution: ReportExecution): string {
  if (execution.status === 'Processing') {
    return 'Report is still processing'
  }
  if (execution.status === 'Requested') {
    return 'Report has not started yet'
  }
  if (execution.status === 'Failed') {
    return 'Report execution failed'
  }
  if (execution.status === 'ArtifactMissing') {
    return 'Artifact is missing'
  }
  if (execution.status === 'ArtifactCorrupted') {
    return 'Artifact is corrupted'
  }
  if (execution.status === 'ArtifactVersionMismatch') {
    return 'Artifact version is incompatible'
  }
  if (execution.status === 'Expired') {
    return 'Artifact has expired'
  }
  if (!execution.artifactAvailable) {
    return 'Artifact not available'
  }
  return 'Download not available'
}

// --- Mock Mode Banner ---

function MockModeBanner() {
  return (
    <Alert className="border-amber-500 bg-amber-50 dark:bg-amber-950/30 dark:border-amber-700">
      <AlertOctagon className="h-4 w-4 text-amber-600 dark:text-amber-500" />
      <AlertTitle className="text-amber-800 dark:text-amber-400">Development Preview Mode</AlertTitle>
      <AlertDescription className="text-amber-700 dark:text-amber-500">
        Backend API is unavailable. Showing mock report executions for UI testing.
      </AlertDescription>
    </Alert>
  )
}

// --- Stats Cards ---

function StatsCards({ executions }: { executions: ReportExecution[] }) {
  const total = executions.length
  const completed = executions.filter((e) => e.status === 'Completed').length
  const processing = executions.filter(
    (e) => e.status === 'Processing' || e.status === 'Requested'
  ).length
  const failed = executions.filter((e) => e.status === 'Failed').length
  const artifactsAvailable = executions.filter((e) => e.artifactAvailable).length

  const stats = [
    { label: 'Total Runs', value: total, icon: FileText },
    { label: 'Completed', value: completed, icon: CheckCircle2, color: 'text-green-600' },
    { label: 'Processing', value: processing, icon: Loader2, color: 'text-blue-600' },
    { label: 'Failed', value: failed, icon: XCircle, color: 'text-red-600' },
    { label: 'Artifacts', value: artifactsAvailable, icon: Download, color: 'text-slate-600' },
  ]

  return (
    <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
      {stats.map((stat) => (
        <Card key={stat.label} className="bg-card">
          <CardContent className="pt-4 pb-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs text-muted-foreground font-medium">{stat.label}</p>
                <p className={cn('text-2xl font-semibold mt-1', stat.color)}>{stat.value}</p>
              </div>
              <stat.icon className={cn('h-5 w-5 text-muted-foreground', stat.color)} />
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

// --- Download Button ---

function DownloadButton({
  execution,
  format,
  icon: Icon,
  label,
  isMockMode,
}: {
  execution: ReportExecution
  format: ExportFormat
  icon: React.ComponentType<{ className?: string }>
  label: string
  isMockMode: boolean
}) {
  const [isLoading, setIsLoading] = useState(false)
  const enabled = canDownload(execution)

  const handleDownload = async () => {
    if (!enabled) return

    try {
      setIsLoading(true)
      toast.info(`Preparing ${format} download...`)
      
      const { blob, filename } = await downloadReportExecution(
        execution.executionId,
        format,
        isMockMode
      )
      triggerBlobDownload(blob, filename)
      toast.success('Download started')
    } catch {
      toast.error('Download failed. Please try again.')
    } finally {
      setIsLoading(false)
    }
  }

  const tooltipContent = enabled
    ? `Download as ${label}`
    : getDownloadDisabledReason(execution)

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            variant="outline"
            size="sm"
            disabled={!enabled || isLoading}
            onClick={handleDownload}
            className="h-7 px-2 text-xs"
          >
            {isLoading ? (
              <Loader2 className="h-3 w-3 animate-spin" />
            ) : (
              <Icon className="h-3 w-3" />
            )}
            <span className="ml-1 hidden sm:inline">{label}</span>
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <p>{tooltipContent}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}

// --- Execution Detail Sheet ---

function ExecutionDetailSheet({
  open,
  onOpenChange,
  executionId,
  isMockMode,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  executionId: string | null
  isMockMode: boolean
}) {
  const [detail, setDetail] = useState<ReportExecutionDetail | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open || !executionId) {
      setDetail(null)
      setError(null)
      return
    }

    async function loadDetail() {
      try {
        setIsLoading(true)
        setError(null)
        const data = await getReportExecution(executionId!, isMockMode)
        setDetail(data)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load details')
      } finally {
        setIsLoading(false)
      }
    }

    loadDetail()
  }, [open, executionId, isMockMode])

  const copyToClipboard = (value: string, label: string) => {
    navigator.clipboard.writeText(value)
    switch (label) {
      case 'Execution ID':
        toast.success('Execution ID copied')
        break
      case 'Artifact Key':
        toast.success('Artifact key copied')
        break
      case 'Query Fingerprint':
        toast.success('Query fingerprint copied')
        break
      default:
        toast.success(`${label} copied`)
    }
  }

  const DetailRow = ({
    label,
    value,
    copyable = false,
  }: {
    label: string
    value?: string | number | null
    copyable?: boolean
  }) => {
    if (value === undefined || value === null || value === '') return null
    const stringValue = String(value)

    return (
      <div className="flex items-start justify-between gap-4 py-2">
        <span className="text-sm text-muted-foreground shrink-0">{label}</span>
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-right break-all">{stringValue}</span>
          {copyable && (
            <Button
              variant="ghost"
              size="sm"
              className="h-6 w-6 p-0 shrink-0"
              onClick={() => copyToClipboard(stringValue, label)}
            >
              <Copy className="h-3 w-3" />
            </Button>
          )}
        </div>
      </div>
    )
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>Execution Details</SheetTitle>
          <SheetDescription>
            View detailed information about this report execution.
          </SheetDescription>
        </SheetHeader>

        {isLoading && (
          <div className="space-y-4 mt-6">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="flex justify-between">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="h-4 w-32" />
              </div>
            ))}
          </div>
        )}

        {error && (
          <Alert variant="destructive" className="mt-6">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Error</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {detail && !isLoading && (
          <ScrollArea className="h-[calc(100vh-180px)] mt-6 pr-4">
            <div className="space-y-1">
              <DetailRow label="Execution ID" value={detail.executionId} copyable />
              <DetailRow label="Report ID" value={detail.reportId} />
              <DetailRow label="Report Name" value={detail.reportName} />
              <DetailRow label="Template ID" value={detail.templateId} />
              <Separator className="my-2" />
              <DetailRow label="Status" value={detail.status} />
              <DetailRow label="Row Count" value={formatRowCount(detail.rowCount)} />
              <DetailRow label="Duration" value={formatDuration(detail.durationMs)} />
              <Separator className="my-2" />
              <DetailRow label="Artifact Key" value={detail.artifactKey} copyable />
              <DetailRow
                label="Artifact Available"
                value={detail.artifactAvailable ? 'Yes' : 'No'}
              />
              <DetailRow label="Storage Mode" value={detail.storageMode} />
              <Separator className="my-2" />
              <DetailRow label="Query Fingerprint" value={detail.queryFingerprint} copyable />
              <DetailRow label="Semantic Model Version" value={detail.semanticModelVersion} />
              <Separator className="my-2" />
              <DetailRow label="Created At" value={formatDateTime(detail.createdAtUtc)} />
              <DetailRow label="Completed At" value={formatDateTime(detail.completedAtUtc)} />

              {detail.errorMessage && (
                <>
                  <Separator className="my-2" />
                  <div className="py-2">
                    <span className="text-sm text-muted-foreground block mb-1">Error Message</span>
                    <Alert variant="destructive" className="mt-1">
                      <AlertDescription className="text-xs">{detail.errorMessage}</AlertDescription>
                    </Alert>
                  </div>
                </>
              )}

              {detail.compiledSql && (
                <>
                  <Separator className="my-2" />
                  <div className="py-2">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-sm text-muted-foreground">Compiled SQL</span>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="h-6 px-2"
                        onClick={() => copyToClipboard(detail.compiledSql!, 'SQL')}
                      >
                        <Copy className="h-3 w-3 mr-1" />
                        Copy
                      </Button>
                    </div>
                    <pre className="text-xs bg-muted p-3 rounded-md overflow-x-auto whitespace-pre-wrap font-mono">
                      {detail.compiledSql}
                    </pre>
                  </div>
                </>
              )}

              {canDownload(detail) && (
                <>
                  <Separator className="my-2" />
                  <div className="py-2">
                    <span className="text-sm text-muted-foreground block mb-2">
                      Download Formats
                    </span>
                    <div className="flex flex-wrap gap-2">
                      <DownloadButton
                        execution={detail}
                        format="PDF"
                        icon={FileText}
                        label="PDF"
                        isMockMode={isMockMode}
                      />
                      <DownloadButton
                        execution={detail}
                        format="XLSX"
                        icon={Table2}
                        label="XLSX"
                        isMockMode={isMockMode}
                      />
                      <DownloadButton
                        execution={detail}
                        format="CSV"
                        icon={FileSpreadsheet}
                        label="CSV"
                        isMockMode={isMockMode}
                      />
                      <DownloadButton
                        execution={detail}
                        format="DOCX"
                        icon={FileType}
                        label="DOCX"
                        isMockMode={isMockMode}
                      />
                    </div>
                  </div>
                </>
              )}

              <Separator className="my-2" />
              <div className="py-2">
                <TooltipProvider>
                  <Tooltip>
                    <TooltipTrigger asChild>
                      <Button variant="outline" size="sm" disabled className="w-full">
                        <RotateCcw className="h-3 w-3 mr-2" />
                        Re-run Report
                      </Button>
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>Re-run will be available after report definition persistence is implemented.</p>
                    </TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              </div>
            </div>
          </ScrollArea>
        )}
      </SheetContent>
    </Sheet>
  )
}

// --- Skeleton Table ---

function TableSkeleton() {
  return (
    <div className="space-y-3">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="flex items-center gap-4 py-3">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-16" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-24" />
          <div className="flex gap-2 ml-auto">
            <Skeleton className="h-7 w-12" />
            <Skeleton className="h-7 w-12" />
            <Skeleton className="h-7 w-12" />
          </div>
        </div>
      ))}
    </div>
  )
}

// --- Main Page Component ---

export default function ReportRunsPage() {
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const [storageFilter, setStorageFilter] = useState<string>('all')
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedExecutionId, setSelectedExecutionId] = useState<string | null>(null)
  const [detailSheetOpen, setDetailSheetOpen] = useState(false)

  // Fetch executions from the real API (with mock fallback in dev)
  const {
    data,
    error,
    isLoading,
    isValidating,
    mutate,
  } = useSWR<ReportExecutionsResponse>('report-executions', getReportExecutions, {
    revalidateOnFocus: false,
    refreshInterval: 0, // Will be dynamically set based on active runs
  })

  const executions = data?.executions ?? []
  const isMockMode = data?.isMockData ?? false

  // Auto-refresh when there are active runs (only in real mode)
  const hasActiveRuns = !isMockMode && executions.some(
    (e) => e.status === 'Requested' || e.status === 'Processing'
  )

  useSWR(
    hasActiveRuns ? 'report-executions-polling' : null,
    getReportExecutions,
    {
      refreshInterval: 5000,
      onSuccess: (newData) => {
        mutate(newData, false)
      },
    }
  )

  const handleRefresh = useCallback(async () => {
    try {
      await mutate()
      toast.success('Report executions refreshed')
    } catch {
      toast.error('Unable to refresh report executions')
    }
  }, [mutate])

  const handleViewDetails = useCallback((executionId: string) => {
    setSelectedExecutionId(executionId)
    setDetailSheetOpen(true)
  }, [])

  // Filter executions
  const filteredExecutions = executions.filter((execution) => {
    // Status filter
    if (statusFilter !== 'all' && execution.status !== statusFilter) {
      return false
    }

    // Storage filter
    if (storageFilter !== 'all' && execution.storageMode !== storageFilter) {
      return false
    }

    // Search filter
    if (searchQuery) {
      const query = searchQuery.toLowerCase()
      const matchesId = execution.executionId.toLowerCase().includes(query)
      const matchesName = execution.reportName?.toLowerCase().includes(query)
      if (!matchesId && !matchesName) {
        return false
      }
    }

    return true
  })

  // Sort by created date descending
  const sortedExecutions = [...filteredExecutions].sort((a, b) => {
    return new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime()
  })

  return (
    <div className="min-h-screen bg-background">
      {/* Header */}
      <div className="border-b bg-card">
        <div className="container max-w-7xl mx-auto py-6 px-4">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">My Reports</h1>
              <p className="text-sm text-muted-foreground mt-1">
                View previous report executions and download available artifacts in multiple formats.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" asChild>
                <Link href="/">
                  <ArrowLeft className="h-4 w-4 mr-2" />
                  Back to Builder
                </Link>
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={handleRefresh}
                disabled={isValidating}
              >
                <RefreshCw className={cn('h-4 w-4 mr-2', isValidating && 'animate-spin')} />
                Refresh
              </Button>
            </div>
          </div>
        </div>
      </div>

      <div className="container max-w-7xl mx-auto py-6 px-4 space-y-6">
        {/* Mock Mode Banner */}
        {isMockMode && <MockModeBanner />}

        {/* Stats Cards */}
        {executions.length > 0 && <StatsCards executions={executions} />}

        {/* Filters */}
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Report Executions</CardTitle>
            <CardDescription>
              {hasActiveRuns && (
                <span className="inline-flex items-center gap-1 text-blue-600 dark:text-blue-400">
                  <Loader2 className="h-3 w-3 animate-spin" />
                  Auto-refreshing for active runs
                </span>
              )}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex flex-col sm:flex-row gap-3 mb-4">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search by name or execution ID..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-9"
                />
                {searchQuery && (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="absolute right-1 top-1/2 -translate-y-1/2 h-6 w-6 p-0"
                    onClick={() => setSearchQuery('')}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                )}
              </div>
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger className="w-full sm:w-[160px]">
                  <SelectValue placeholder="Status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Statuses</SelectItem>
                  <SelectItem value="Completed">Completed</SelectItem>
                  <SelectItem value="Processing">Processing</SelectItem>
                  <SelectItem value="Requested">Requested</SelectItem>
                  <SelectItem value="Failed">Failed</SelectItem>
                  <SelectItem value="ArtifactMissing">Artifact Missing</SelectItem>
                  <SelectItem value="Expired">Expired</SelectItem>
                </SelectContent>
              </Select>
              <Select value={storageFilter} onValueChange={setStorageFilter}>
                <SelectTrigger className="w-full sm:w-[140px]">
                  <SelectValue placeholder="Storage" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Storage</SelectItem>
                  <SelectItem value="Local">Local</SelectItem>
                  <SelectItem value="S3">S3</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Loading State */}
            {isLoading && <TableSkeleton />}

            {/* Error State (only in production when no mock fallback) */}
            {error && !isLoading && !isMockMode && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>Unable to load report executions</AlertTitle>
                <AlertDescription className="mt-2">
                  {error instanceof Error ? error.message : 'An error occurred'}
                  <Button
                    variant="outline"
                    size="sm"
                    className="ml-4"
                    onClick={handleRefresh}
                  >
                    Retry
                  </Button>
                </AlertDescription>
              </Alert>
            )}

            {/* Empty State */}
            {!isLoading && !error && sortedExecutions.length === 0 && (
              <div className="text-center py-12">
                <FileText className="h-12 w-12 mx-auto text-muted-foreground mb-4" />
                <h3 className="text-lg font-medium mb-2">No report runs yet</h3>
                <p className="text-sm text-muted-foreground mb-4">
                  {searchQuery || statusFilter !== 'all' || storageFilter !== 'all'
                    ? 'No executions match your filters.'
                    : 'Run a report to see it here.'}
                </p>
                <Button asChild>
                  <Link href="/">Build a Report</Link>
                </Button>
              </div>
            )}

            {/* Table */}
            {!isLoading && sortedExecutions.length > 0 && (
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[120px]">Execution ID</TableHead>
                      <TableHead>Report Name</TableHead>
                      <TableHead className="w-[100px]">Status</TableHead>
                      <TableHead className="w-[80px] text-right">Rows</TableHead>
                      <TableHead className="w-[140px]">Created</TableHead>
                      <TableHead className="w-[80px] text-right">Duration</TableHead>
                      <TableHead className="w-[80px]">Artifact</TableHead>
                      <TableHead className="w-[70px]">Storage</TableHead>
                      <TableHead className="w-[220px] text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sortedExecutions.map((execution) => (
                      <TableRow key={execution.executionId}>
                        <TableCell className="font-mono text-xs">
                          {execution.executionId.slice(0, 8)}...
                        </TableCell>
                        <TableCell className="font-medium">
                          {execution.reportName || '-'}
                          {execution.errorMessage && execution.status === 'Failed' && (
                            <TooltipProvider>
                              <Tooltip>
                                <TooltipTrigger>
                                  <Info className="h-3 w-3 ml-2 text-red-500 inline" />
                                </TooltipTrigger>
                                <TooltipContent className="max-w-[300px]">
                                  <p className="text-xs">{execution.errorMessage}</p>
                                </TooltipContent>
                              </Tooltip>
                            </TooltipProvider>
                          )}
                        </TableCell>
                        <TableCell>
                          <Badge
                            variant="secondary"
                            className={cn(
                              'text-xs font-medium inline-flex items-center gap-1',
                              getStatusColor(execution.status)
                            )}
                          >
                            {getStatusIcon(execution.status)}
                            {execution.status}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right font-mono text-xs">
                          {formatRowCount(execution.rowCount)}
                        </TableCell>
                        <TableCell className="text-xs">
                          {formatDateTime(execution.createdAtUtc)}
                        </TableCell>
                        <TableCell className="text-right font-mono text-xs">
                          {formatDuration(execution.durationMs)}
                        </TableCell>
                        <TableCell>
                          <Badge
                            variant="outline"
                            className={cn(
                              'text-xs',
                              execution.artifactAvailable
                                ? 'border-green-500 text-green-600'
                                : 'border-slate-300 text-slate-500'
                            )}
                          >
                            {execution.artifactAvailable ? 'Available' : 'Missing'}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className="text-xs gap-1">
                            {getStorageIcon(execution.storageMode)}
                            {execution.storageMode}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex items-center justify-end gap-1">
                            <TooltipProvider>
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <Button
                                    asChild
                                    variant="outline"
                                    size="sm"
                                    className="h-7 px-2 text-xs"
                                    disabled={!canPreview(execution)}
                                  >
                                    <Link href={`/report-preview/${encodeURIComponent(execution.executionId)}`}>
                                      <Eye className="h-3 w-3" />
                                      <span className="ml-1 hidden sm:inline">Preview</span>
                                    </Link>
                                  </Button>
                                </TooltipTrigger>
                                <TooltipContent>
                                  <p>{canPreview(execution) ? 'Preview Report' : getDownloadDisabledReason(execution)}</p>
                                </TooltipContent>
                              </Tooltip>
                            </TooltipProvider>
                            <DownloadButton
                              execution={execution}
                              format="PDF"
                              icon={FileText}
                              label="PDF"
                              isMockMode={isMockMode}
                            />
                            <DownloadButton
                              execution={execution}
                              format="XLSX"
                              icon={Table2}
                              label="XLSX"
                              isMockMode={isMockMode}
                            />
                            <DownloadButton
                              execution={execution}
                              format="CSV"
                              icon={FileSpreadsheet}
                              label="CSV"
                              isMockMode={isMockMode}
                            />
                            <TooltipProvider>
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    className="h-7 px-2"
                                    onClick={() => handleViewDetails(execution.executionId)}
                                  >
                                    <Info className="h-3 w-3" />
                                  </Button>
                                </TooltipTrigger>
                                <TooltipContent>
                                  <p>View Details</p>
                                </TooltipContent>
                              </Tooltip>
                            </TooltipProvider>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Detail Sheet */}
      <ExecutionDetailSheet
        open={detailSheetOpen}
        onOpenChange={setDetailSheetOpen}
        executionId={selectedExecutionId}
        isMockMode={isMockMode}
      />
    </div>
  )
}
