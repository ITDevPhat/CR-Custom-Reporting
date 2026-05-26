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
  const elementId = useMemo(() => 'reportViewer', [])

  async function pickExistingPath(candidates: string[], kind: 'script' | 'style'): Promise<string> {
    for (const candidate of candidates) {
      try {
        const res = await fetch(candidate, { method: 'HEAD' })
        if (res.ok) return candidate
      } catch {
        // continue
      }
    }
    throw new Error(`Missing ${kind} asset. Checked: ${candidates.join(', ')}`)
  }

  useEffect(() => {
    let mounted = true
    const base = '/telerik-report-viewer'

    async function init() {
      try {
        // Script/CSS order follows Telerik HTML5 viewer standard bootstrap:
        // jQuery -> Kendo -> viewer JS, and Kendo styles -> viewer styles.
        const jquery = await pickExistingPath(
          [`${base}/js/jquery.min.js`, `${base}/js/jquery-3.7.1.min.js`, `${base}/js/jquery-3.6.0.min.js`],
          'script'
        )
        const kendoJs = await pickExistingPath(
          [`${base}/js/kendo.all.min.js`, `${base}/js/kendo.web.min.js`],
          'script'
        )
        const viewerJs = await pickExistingPath(
          [`${base}/js/telerikReportViewer.kendo.min.js`, `${base}/js/telerikReportViewer.min.js`],
          'script'
        )
        const kendoCommonCss = await pickExistingPath(
          [`${base}/styles/kendo.common.min.css`, `${base}/styles/kendo.default.min.css`],
          'style'
        )
        const kendoThemeCss = await pickExistingPath(
          [`${base}/styles/kendo.default.min.css`, `${base}/styles/kendo.bootstrap.min.css`],
          'style'
        )
        const viewerCss = await pickExistingPath(
          [`${base}/styles/telerikReportViewer.min.css`, `${base}/styles/telerikReportViewer.css`],
          'style'
        )

        await loadStylesheet(kendoCommonCss)
        await loadStylesheet(kendoThemeCss)
        await loadStylesheet(viewerCss)
        await loadScript(jquery)
        await loadScript(kendoJs)
        await loadScript(viewerJs)

        if (!mounted) return
        const $ = window.jQuery
        if (!$?.fn?.telerik_ReportViewer) {
          throw new Error('Telerik viewer assets not found. Extract Telerik_Reporting_20.1.26.520_Assets.zip into public/telerik-report-viewer.')
        }

        if (!initializedRef.current) {
          window.jQuery('#reportViewer').telerik_ReportViewer({
            serviceUrl: `${serviceUrl}`,
            reportSource: { report: `execution:${executionId}`, parameters: {} },
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
        const widget = $('#reportViewer').data('telerik_ReportViewer')
        widget?.dispose?.()
      }
      initializedRef.current = false
    }
  }, [elementId, executionId, reportSource, serviceUrl])

  if (loading) return <div className="text-sm text-muted-foreground">Loading Telerik viewer...</div>
  if (error) return <div className="text-sm text-red-600">{error}</div>
  return <div id={elementId} className="h-[78vh] w-full border rounded-md bg-white" />
}
