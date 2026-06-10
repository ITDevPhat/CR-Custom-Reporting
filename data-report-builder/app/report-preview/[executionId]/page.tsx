'use client'


import Link from 'next/link'
import { useParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { ArrowLeft, FileSpreadsheet, FileText, FileType, Table2 } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { TelerikHtml5Viewer } from '@/components/report-preview/telerik-html5-viewer'
import {
  API_BASE,
  getReportPreviewReference,
  downloadReportExecution,
  triggerBlobDownload,
  type ExportFormat,
  type ReportPreviewReference,
} from '@/lib/report-executions-api'


export default function ReportPreviewPage() {
  const params = useParams<{ executionId: string }>()
  const executionId = params.executionId
  const [preview, setPreview] = useState<ReportPreviewReference | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function load() {
      try {
        const ref = await getReportPreviewReference(executionId)
        setPreview(ref)
      } catch {
        setError('Unable to open report preview')
        toast.error('Unable to open report preview')
      }
    }
    void load()
  }, [executionId])

  const download = async (format: ExportFormat) => {
    const { blob, filename } = await downloadReportExecution(executionId, format)
    triggerBlobDownload(blob, filename)
  }

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <Button asChild variant="outline">
          <Link href="/report-runs"><ArrowLeft className="h-4 w-4 mr-2" />Back to My Reports</Link>
        </Button>
        <div className="flex flex-wrap items-center justify-end gap-2">
          <Button variant="outline" onClick={() => void download('PDF')}><FileText className="h-4 w-4 mr-1" />PDF</Button>
          <Button variant="outline" onClick={() => void download('XLSX')}><Table2 className="h-4 w-4 mr-1" />XLSX</Button>
          <Button variant="outline" onClick={() => void download('CSV')}><FileSpreadsheet className="h-4 w-4 mr-1" />CSV</Button>
          <Button variant="outline" onClick={() => void download('DOCX')}><FileType className="h-4 w-4 mr-1" />DOCX</Button>
          <Button variant="outline" onClick={() => void download('PPTX')}><FileType className="h-4 w-4 mr-1" />PPTX</Button>
          <Button variant="outline" onClick={() => void download('RTF')}><FileType className="h-4 w-4 mr-1" />RTF</Button>
          <Button variant="outline" onClick={() => void download('TIFF')}><FileType className="h-4 w-4 mr-1" />TIFF</Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Report Preview - {executionId}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {error ? <p className="text-sm text-red-600">{error}</p> : null}
          {preview ? (
            <div className="text-sm space-y-1">
              <p><strong>Report Source:</strong> {preview.reportSource}</p>
              <p><strong>Status:</strong> {preview.status}</p>
              <p><strong>Artifact Available:</strong> {String(preview.artifactAvailable)}</p>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Loading preview reference...</p>
          )}
          {preview ? (
            <TelerikHtml5Viewer
              executionId={executionId}
              reportSource={preview.reportSource}
              serviceUrl={`${API_BASE}/api/reports`}
            />
          ) : null}
        </CardContent>
      </Card>
    </div>
  )
}
