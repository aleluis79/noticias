import { useEffect, useRef, useState } from 'react'

export interface ArticleSummaryResponse {
  url: string
  summary: string
  model?: string
  generatedAt?: string
}

export type SummaryState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'generating' }
  | { status: 'ready'; summary: string }
  | { status: 'error'; message: string }

export interface UseArticleSummaryResult {
  state: SummaryState
  generate: () => void
}

const IDLE: SummaryState = { status: 'idle' }
const LOADING: SummaryState = { status: 'loading' }

const GENERIC_ERROR = 'No pudimos generar el resumen. Probá de nuevo.'

class SummaryRequestError extends Error {}

export function summaryErrorMessage(status: number): string {
  if (status === 404) {
    return 'No pudimos extraer el contenido de esta nota para resumirla.'
  }

  if (status === 503) {
    return 'El resumen no está disponible en este momento. Probá de nuevo más tarde.'
  }

  return GENERIC_ERROR
}

function summaryEndpoint(endpoint: string, url: string): string {
  return `${endpoint}?url=${encodeURIComponent(url)}`
}

/**
 * Loads the stored summary of the current article and generates it on demand.
 * A generated summary stays in memory for the session; the API keeps it on disk
 * so reopening the article shows it directly.
 */
export function useArticleSummary(
  articleUrl: string | null,
  endpoint: string,
): UseArticleSummaryResult {
  const [states, setStates] = useState<Record<string, SummaryState>>({})
  const requested = useRef(new Set<string>())
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  useEffect(() => {
    if (!articleUrl || requested.current.has(articleUrl)) {
      return
    }

    const url = articleUrl
    requested.current.add(url)
    setStates((previous) => ({
      ...previous,
      [url]: { status: 'loading' },
    }))

    const load = async () => {
      try {
        const response = await fetch(summaryEndpoint(endpoint, url))

        if (response.status === 404) {
          if (mounted.current) {
            setStates((previous) => ({ ...previous, [url]: IDLE }))
          }
          return
        }

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }

        const data: ArticleSummaryResponse = await response.json()
        const summary = data.summary?.trim()
        if (!summary) {
          throw new Error('Resumen vacío')
        }

        if (mounted.current) {
          setStates((previous) => ({
            ...previous,
            [url]: { status: 'ready', summary },
          }))
        }
      } catch {
        // The button is still offered if the stored summary could not be checked.
        requested.current.delete(url)
        if (mounted.current) {
          setStates((previous) => ({ ...previous, [url]: IDLE }))
        }
      }
    }

    void load()
  }, [articleUrl, endpoint])

  const generate = () => {
    if (!articleUrl) {
      return
    }

    const url = articleUrl
    setStates((previous) => ({
      ...previous,
      [url]: { status: 'generating' },
    }))

    const request = async () => {
      try {
        const response = await fetch(summaryEndpoint(endpoint, url), {
          method: 'POST',
        })

        if (!response.ok) {
          throw new SummaryRequestError(summaryErrorMessage(response.status))
        }

        const data: ArticleSummaryResponse = await response.json()
        const summary = data.summary?.trim()
        if (!summary) {
          throw new SummaryRequestError(GENERIC_ERROR)
        }

        if (mounted.current) {
          setStates((previous) => ({
            ...previous,
            [url]: { status: 'ready', summary },
          }))
        }
      } catch (error) {
        if (!mounted.current) {
          return
        }

        const message =
          error instanceof SummaryRequestError ? error.message : GENERIC_ERROR
        setStates((previous) => ({
          ...previous,
          [url]: { status: 'error', message },
        }))
      }
    }

    void request()
  }

  const state = articleUrl ? states[articleUrl] ?? LOADING : IDLE

  return { state, generate }
}
