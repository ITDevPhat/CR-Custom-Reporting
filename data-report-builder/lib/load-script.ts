const scriptPromises = new Map<string, Promise<void>>()
const stylePromises = new Map<string, Promise<void>>()

export function loadScript(src: string): Promise<void> {
  if (typeof window === 'undefined') return Promise.resolve()
  if (scriptPromises.has(src)) return scriptPromises.get(src)!

  const existing = document.querySelector(`script[src="${src}"]`) as HTMLScriptElement | null
  if (existing?.dataset.loaded === 'true') return Promise.resolve()

  const promise = new Promise<void>((resolve, reject) => {
    const script = existing ?? document.createElement('script')
    script.src = src
    script.async = true
    script.onload = () => {
      script.dataset.loaded = 'true'
      resolve()
    }
    script.onerror = () => reject(new Error(`Failed to load script: ${src}`))
    if (!existing) document.body.appendChild(script)
  })
  scriptPromises.set(src, promise)
  return promise
}

export function loadStylesheet(href: string): Promise<void> {
  if (typeof window === 'undefined') return Promise.resolve()
  if (stylePromises.has(href)) return stylePromises.get(href)!

  const existing = document.querySelector(`link[href="${href}"]`) as HTMLLinkElement | null
  if (existing?.dataset.loaded === 'true') return Promise.resolve()

  const promise = new Promise<void>((resolve, reject) => {
    const link = existing ?? document.createElement('link')
    link.rel = 'stylesheet'
    link.href = href
    link.onload = () => {
      link.dataset.loaded = 'true'
      resolve()
    }
    link.onerror = () => reject(new Error(`Failed to load stylesheet: ${href}`))
    if (!existing) document.head.appendChild(link)
  })
  stylePromises.set(href, promise)
  return promise
}
