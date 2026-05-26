
'use client'

import { useEffect, useMemo, useRef, useState } from 'react'
import { loadScript, loadStylesheet } from '@/lib/load-script'

type TelerikHtml5ViewerProps = {
  executionId: string
  reportSource: string
  serviceUrl: string
}

declare global {
  interface Window {
    jQuery?: any
    $?: any
  }
}

const ASSET_BASE = '/telerik-report-viewer'

const STYLE_CANDIDATES = {
  kendoCommon: [
    `${ASSET_BASE}/styles/kendo.common.min.css`,
  ],
  kendoTheme: [
    `${ASSET_BASE}/styles/kendo.blueopal.min.css`,
  ],
  viewer: [
    `${ASSET_BASE}/styles/telerikReportViewer-20.1.26.520.min.css`,
    `${ASSET_BASE}/styles/telerikReportViewer-20.1.26.520.css`,
  ],
  icons: [
    `${ASSET_BASE}/font/font-icons.min.css`,
    `${ASSET_BASE}/font/font-icons.css`,
  ],
}

const SCRIPT_CANDIDATES = {
  jquery: [
    `${ASSET_BASE}/js/jquery.min.js`,
  ],
  kendo: [
    `${ASSET_BASE}/js/telerikReportViewer.kendo-20.1.26.520.min.js`,
  ],
  viewer: [
    `${ASSET_BASE}/js/telerikReportViewer-20.1.26.520.min.js`,
  ],
}

async function assetExists(url: string): Promise<boolean> {
  if (typeof window === 'undefined') return false

  // External fallback cannot always be HEAD-probed due CORS; let the script loader handle it.
  if (/^https?:\/\//i.test(url)) return true

  try {
    const response = await fetch(url, { method: 'HEAD', cache: 'no-store' })
    return response.ok
  } catch {
    return false
  }
}

async function resolveAsset(label: string, candidates: string[]): Promise<string> {
  for (const candidate of candidates) {
    if (await assetExists(candidate)) {
      return candidate
    }
  }

  throw new Error(
    `Missing Telerik ${label} asset. Checked: ${candidates.join(', ')}. ` +
      `Copy the matching file into public/telerik-report-viewer or adjust the viewer asset candidates.`
  )
}

export function TelerikHtml5Viewer({ executionId, reportSource, serviceUrl }: TelerikHtml5ViewerProps) {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const initializedRef = useRef(false)
  const elementId = useMemo(() => `reportViewer-${executionId.replace(/[^a-zA-Z0-9_-]/g, '-')}`, [executionId])

  useEffect(() => {
    let mounted = true

    async function init() {
      try {
        setLoading(true)
        setError(null)

        const [kendoCommonCss, kendoThemeCss, viewerCss, iconCss] = await Promise.all([
          resolveAsset('Kendo common CSS', STYLE_CANDIDATES.kendoCommon),
          resolveAsset('Kendo theme CSS', STYLE_CANDIDATES.kendoTheme),
          resolveAsset('Report Viewer CSS', STYLE_CANDIDATES.viewer),
          resolveAsset('Report Viewer icon CSS', STYLE_CANDIDATES.icons),
        ])

        await loadStylesheet(kendoCommonCss)
        await loadStylesheet(kendoThemeCss)
        await loadStylesheet(iconCss)
        await loadStylesheet(viewerCss)

        if (!window.jQuery?.fn?.jquery) {
          const jqueryJs = await resolveAsset('jQuery JS', SCRIPT_CANDIDATES.jquery)
          await loadScript(jqueryJs)
        }

        if (!window.jQuery?.fn?.jquery) {
          throw new Error(
            'jQuery is required by Telerik HTML5 Report Viewer but was not loaded. ' +
              'Place jquery.min.js under public/telerik-report-viewer/js or allow the CDN fallback.'
          )
        }

        window.$ = window.jQuery

        const kendoJs = await resolveAsset('Kendo JS', SCRIPT_CANDIDATES.kendo)
        const viewerJs = await resolveAsset('Report Viewer JS', SCRIPT_CANDIDATES.viewer)

        await loadScript(kendoJs)
        await loadScript(viewerJs)

        if (!mounted) return

        const $ = window.jQuery
        if (!$?.fn?.telerik_ReportViewer) {
          throw new Error(
            'Telerik HTML5 Report Viewer plugin was not registered. ' +
              `Loaded Kendo asset: ${kendoJs}. Loaded viewer asset: ${viewerJs}.`
          )
        }

        const selector = `#${elementId}`
        const templateUrl = `${ASSET_BASE}/templates/telerikReportViewerTemplate-20.1.26.520.html`

        if (!initializedRef.current) {
          $(selector).empty()
          $(selector).telerik_ReportViewer({
            serviceUrl,
            templateUrl,
            reportSource: {
              report: reportSource,
              parameters: {},
            },
            viewMode: 'PRINT_PREVIEW',
            pageMode: 'SINGLE_PAGE',
            scaleMode: 'FIT_PAGE_WIDTH',
            enableAccessibility: true,
            error: (_event: unknown, args: unknown) => {
              if (!mounted) return
              const message = typeof args === 'string'
                ? args
                : (args as { message?: string; error?: string })?.message
                  ?? (args as { message?: string; error?: string })?.error
                  ?? 'Telerik report viewer failed to render the preview.'
              setError(message)
              setLoading(false)
            },
            pageReady: () => {
              if (!mounted) return
              setLoading(false)
            },
            renderingEnd: () => {
              if (!mounted) return
              setLoading(false)
            },
          })

          const viewer = $(selector).data('telerik_ReportViewer')
          viewer?.viewMode?.('PRINT_PREVIEW')
          viewer?.pageMode?.('SINGLE_PAGE')
          viewer?.scale?.({ scaleMode: 'FIT_PAGE_WIDTH' })
          initializedRef.current = true
        }
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

  return (
    <div className="space-y-2">
      {loading ? (
        <div className="rounded-md border bg-muted/30 p-4 text-sm text-muted-foreground">
          Loading Telerik HTML5 Report Viewer...
        </div>
      ) : null}

      {error ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {error}
        </div>
      ) : null}

      <div
        id={elementId}
        className="h-[78vh] w-full rounded-md border bg-white"
        aria-label={`Telerik report viewer for ${executionId}`}
      />
    </div>
  )
}
