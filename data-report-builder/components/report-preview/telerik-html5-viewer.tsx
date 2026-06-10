
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
    telerikReportViewer?: any
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
    `${ASSET_BASE}/js/telerikReportViewer-20.1.26.520.js`,
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

async function loadFirstAvailableScript(label: string, candidates: string[]): Promise<string> {
  const failures: string[] = []

  for (const candidate of candidates) {
    try {
      if (/^https?:\/\//i.test(candidate) || await assetExists(candidate)) {
        await loadScript(candidate)
        return candidate
      }
    } catch (e) {
      failures.push(`${candidate}: ${(e as Error).message}`)
    }
  }

  throw new Error(
    `Missing Telerik ${label} asset. Checked: ${candidates.join(', ')}.` +
      (failures.length ? ` Load failures: ${failures.join(' | ')}` : '')
  )
}

function normalizeServiceUrl(serviceUrl: string): string {
  return serviceUrl.replace(/\/+$/, '')
}

export function TelerikHtml5Viewer({ executionId, reportSource, serviceUrl }: TelerikHtml5ViewerProps) {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const initializedRef = useRef(false)
  const loadingWatchdogRef = useRef<number | null>(null)
  const elementId = useMemo(() => `reportViewer-${executionId.replace(/[^a-zA-Z0-9_-]/g, '-')}`, [executionId])

  useEffect(() => {
    let mounted = true
    const finishLoading = () => {
      if (!mounted) return
      setLoading(false)
    }

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
        const normalizedServiceUrl = normalizeServiceUrl(serviceUrl)
        const viewerScriptCandidates = [
          `${normalizedServiceUrl}/resources/js/telerikReportViewer`,
          ...SCRIPT_CANDIDATES.viewer,
        ]

        await loadScript(kendoJs)
        const viewerJs = await loadFirstAvailableScript('Report Viewer JS', viewerScriptCandidates)

        if (!mounted) return

        const $ = window.jQuery
        const telerikReportViewer = window.telerikReportViewer
        if (!$?.fn?.telerik_ReportViewer || !telerikReportViewer) {
          throw new Error(
            'Telerik HTML5 Report Viewer plugin was not registered. ' +
              `Loaded Kendo asset: ${kendoJs}. Loaded viewer asset: ${viewerJs}.`
          )
        }

        const selector = `#${elementId}`

        if (!initializedRef.current) {
          if (loadingWatchdogRef.current) {
            window.clearInterval(loadingWatchdogRef.current)
          }

          $(selector).empty()
          $(selector).telerik_ReportViewer({
            serviceUrl: `${normalizedServiceUrl}/`,
            reportSource: {
              report: reportSource,
              parameters: {},
            },
            viewMode: telerikReportViewer.ViewModes.INTERACTIVE,
            scaleMode: telerikReportViewer.ScaleModes.FIT_PAGE_WIDTH ?? telerikReportViewer.ScaleModes.FIT_PAGE,
            scale: 1.0,
            enableAccessibility: false,
            error: (_event: unknown, args: unknown) => {
              if (!mounted) return
              const message = typeof args === 'string'
                ? args
                : (args as { message?: string; error?: string })?.message
                  ?? (args as { message?: string; error?: string })?.error
                  ?? 'Telerik report viewer failed to render the preview.'
              setError(message)
              finishLoading()
            },
            renderingBegin: () => {
              if (!mounted) return
              setError(null)
            },
            pageReady: () => {
              finishLoading()
            },
            renderingEnd: () => {
              finishLoading()
            },
          })

          loadingWatchdogRef.current = window.setInterval(() => {
            if (!mounted) return

            const viewerElement = document.getElementById(elementId)
            const hasPagesArea = !!viewerElement?.querySelector('[data-role="telerik_ReportViewer_PagesArea"]')
            const hasRenderedPage = !!viewerElement?.querySelector('.trv-page-wrapper, .trv-report-page, .trv-page-container')
            const hasToolbar = !!viewerElement?.querySelector('[data-role="telerik_ReportViewer_Toolbar"]')

            if (hasRenderedPage || (hasToolbar && hasPagesArea)) {
              finishLoading()
              if (loadingWatchdogRef.current) {
                window.clearInterval(loadingWatchdogRef.current)
                loadingWatchdogRef.current = null
              }
            }
          }, 500)

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
      if (loadingWatchdogRef.current) {
        window.clearInterval(loadingWatchdogRef.current)
        loadingWatchdogRef.current = null
      }
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
        className="h-[78vh] w-full overflow-hidden rounded-md border bg-white font-sans"
        aria-label={`Telerik report viewer for ${executionId}`}
      />
    </div>
  )
}
