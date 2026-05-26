'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { loadScript, loadStylesheet } from '@/lib/load-script'

type TelerikHtml5ViewerProps = {
  executionId: string
  reportSource: string
  serviceUrl: string
}

declare global {
  interface Window { jQuery?: any }
}

export function TelerikHtml5Viewer({ executionId, reportSource, serviceUrl }: TelerikHtml5ViewerProps) {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const initializedRef = useRef(false)
  const elementId = useMemo(() => `reportViewer-${executionId}`, [executionId])

  useEffect(() => {
    let mounted = true
    const base = '/telerik-report-viewer'
    const jquery = `${base}/js/jquery.min.js`
    const kendo = `${base}/js/kendo.all.min.js`
    const viewerJs = `${base}/js/telerikReportViewer.kendo.min.js`
    const viewerCss = `${base}/css/telerikReportViewer.min.css`
    const kendoCss = `${base}/css/kendo.common.min.css`
    const kendoThemeCss = `${base}/css/kendo.default.min.css`

    async function init() {
      try {
        await loadStylesheet(kendoCss)
        await loadStylesheet(kendoThemeCss)
        await loadStylesheet(viewerCss)
        await loadScript(jquery)
        await loadScript(kendo)
        await loadScript(viewerJs)

        if (!mounted) return
        const $ = window.jQuery
        if (!$?.fn?.telerik_ReportViewer) {
          throw new Error('Telerik viewer assets not found. Extract Telerik_Reporting_20.1.26.520_Assets.zip into public/telerik-report-viewer.')
        }

        if (!initializedRef.current) {
          $(`#${elementId}`).telerik_ReportViewer({
            serviceUrl,
            reportSource: { report: reportSource, parameters: {} },
            viewMode: 'INTERACTIVE',
            scaleMode: 'FIT_PAGE_WIDTH',
            enableAccessibility: true,
          })
          initializedRef.current = true
        }
        setLoading(false)
      } catch (e) {
        if (!mounted) return
        setError((e as Error).message)
        setLoading(false)
      }
    }
    void init()

    return () => {
      mounted = false
      const $ = window.jQuery
      if ($?.fn?.telerik_ReportViewer) {
        const widget = $(`#${elementId}`).data('telerik_ReportViewer')
        widget?.dispose?.()
      }
      initializedRef.current = false
    }
  }, [elementId, reportSource, serviceUrl])

  if (loading) return <div className="text-sm text-muted-foreground">Loading Telerik viewer...</div>
  if (error) return <div className="text-sm text-red-600">{error}</div>
  return <div id={elementId} className="h-[78vh] w-full border rounded-md bg-white" />
}
