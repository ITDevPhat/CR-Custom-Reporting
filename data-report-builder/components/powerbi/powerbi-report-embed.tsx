'use client'

import { useEffect, useRef, useState } from 'react'
import type { PowerBIEmbedTokenResponse } from '@/lib/powerbi-api'

type PowerBIReportEmbedProps = {
  embedConfig: PowerBIEmbedTokenResponse | null
  reloadKey: number
  onEvent: (eventName: string, detail?: unknown) => void
  onError: (message: string, detail?: unknown) => void
}

export function PowerBIReportEmbed({
  embedConfig,
  reloadKey,
  onEvent,
  onError,
}: PowerBIReportEmbedProps) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let isMounted = true
    let embeddedReport: { off: (eventName: string) => void } | null = null

    async function embedReport() {
      if (!containerRef.current || !embedConfig) {
        return
      }

      setIsLoading(true)
      setError(null)

      try {
        const powerbiClient = await import('powerbi-client')
        const { models, service, factories } = powerbiClient
        const powerbi = new service.Service(
          factories.hpmFactory,
          factories.wpmpFactory,
          factories.routerFactory
        )

        powerbi.reset(containerRef.current)

        const report = powerbi.embed(containerRef.current, {
          type: 'report',
          id: embedConfig.reportId,
          embedUrl: embedConfig.embedUrl,
          accessToken: embedConfig.embedToken,
          tokenType: models.TokenType.Embed,
          permissions: models.Permissions.Read,
          settings: {
            panes: {
              filters: { visible: false, expanded: false },
              pageNavigation: { visible: true },
            },
            layoutType: models.LayoutType.Custom,
            customLayout: {
              displayOption: models.DisplayOption.FitToWidth,
            },
            background: models.BackgroundType.Transparent,
          },
        })

        embeddedReport = report

        const handle = (eventName: string) => (event: { detail?: unknown }) => {
          onEvent(eventName, event.detail)
          if (eventName === 'loaded' || eventName === 'rendered') {
            setIsLoading(false)
          }
          if (eventName === 'error') {
            const message = 'Power BI SDK emitted an error event.'
            setError(message)
            onError(message, event.detail)
            setIsLoading(false)
          }
        }

        for (const eventName of ['loaded', 'rendered', 'error', 'pageChanged', 'dataSelected', 'visualClicked']) {
          report.on(eventName, handle(eventName))
        }

        if (isMounted) {
          onEvent('embedInitialized', {
            reportId: embedConfig.reportId,
            reportName: embedConfig.reportName,
          })
        }
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Power BI SDK initialization failed.'
        setError(message)
        setIsLoading(false)
        onError(message)
      }
    }

    embedReport()

    return () => {
      isMounted = false
      if (embeddedReport) {
        for (const eventName of ['loaded', 'rendered', 'error', 'pageChanged', 'dataSelected', 'visualClicked']) {
          embeddedReport.off(eventName)
        }
      }
    }
  }, [embedConfig, reloadKey, onError, onEvent])

  return (
    <div className="relative h-full min-h-[520px] w-full overflow-hidden border border-border bg-background">
      {isLoading && (
        <div className="absolute inset-0 z-10 grid place-items-center bg-background/75 text-sm text-muted-foreground">
          Loading Power BI report...
        </div>
      )}
      {error && (
        <div className="absolute inset-x-4 top-4 z-10 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}
      {!embedConfig && (
        <div className="grid h-full min-h-[520px] place-items-center text-sm text-muted-foreground">
          Generate an embed token, then embed the report.
        </div>
      )}
      <div ref={containerRef} className="h-full min-h-[520px] w-full" />
    </div>
  )
}
