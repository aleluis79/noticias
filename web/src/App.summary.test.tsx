import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const articleUrl = 'https://www.lanacion.com.ar/nota-1'

const summaryText =
  'El Gobierno anunció un nuevo plan económico para el próximo trimestre. ' +
  'La medida busca ordenar las cuentas públicas y reducir la inflación.'

const edition = {
  generatedAt: '2026-11-19T10:00:00Z',
  articleCount: 1,
  storyCount: 1,
  stories: [
    {
      id: 'story-1',
      title: 'Una historia del día',
      summary: '',
      originalUrl: articleUrl,
      sourceName: 'La Nación',
      sourceCount: 1,
      articleCount: 1,
      latestPublishedDate: null,
      sources: [
        {
          sourceName: 'La Nación',
          title: 'Una historia del día',
          summary: '',
          originalUrl: articleUrl,
          publishedDate: null,
        },
      ],
    },
  ],
}

const articleContent = {
  url: articleUrl,
  title: 'Una historia del día',
  author: 'Ana Pérez',
  paragraphs: ['Primer párrafo del cuerpo.', 'Segundo párrafo del cuerpo.'],
}

function summaryResponse() {
  return {
    url: articleUrl,
    summary: summaryText,
    model: 'gemma4:e2b',
    generatedAt: '2026-11-19T10:05:00Z',
  }
}

function mockApi(options: { stored?: boolean; generateStatus?: number } = {}) {
  const fetchMock = vi.fn(
    async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)

      if (url.includes('/api/stories')) {
        return { ok: true, status: 200, json: async () => edition }
      }

      if (url.includes('/api/news/content')) {
        return { ok: true, status: 200, json: async () => articleContent }
      }

      if (url.includes('/api/news/summary')) {
        if (init?.method === 'POST') {
          const status = options.generateStatus ?? 200
          return {
            ok: status === 200,
            status,
            json: async () => (status === 200 ? summaryResponse() : {}),
          }
        }

        return options.stored
          ? { ok: true, status: 200, json: async () => summaryResponse() }
          : { ok: false, status: 404, json: async () => ({}) }
      }

      throw new Error(`Unexpected request: ${url}`)
    },
  )

  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function openArticle() {
  render(<App />)
  fireEvent.click(
    await screen.findByRole('button', { name: 'Una historia del día' }),
  )
  await screen.findByText('Primer párrafo del cuerpo.')
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('App summary integration', () => {
  it('offers to generate the summary when none is stored', async () => {
    mockApi()

    await openArticle()

    expect(
      await screen.findByRole('button', { name: 'Generar resumen' }),
    ).toBeTruthy()
  })

  it('generates and shows the summary after pressing the button', async () => {
    const fetchMock = mockApi()

    await openArticle()
    fireEvent.click(
      await screen.findByRole('button', { name: 'Generar resumen' }),
    )

    expect(await screen.findByText(summaryText)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Generar resumen' })).toBeNull()

    const posted = fetchMock.mock.calls.some(
      ([input, init]) =>
        String(input).includes('/api/news/summary') &&
        (init as RequestInit | undefined)?.method === 'POST',
    )
    expect(posted).toBe(true)
  })

  it('shows the stored summary directly without offering generation', async () => {
    const fetchMock = mockApi({ stored: true })

    await openArticle()

    expect(await screen.findByText(summaryText)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Generar resumen' })).toBeNull()

    const posted = fetchMock.mock.calls.some(
      ([, init]) => (init as RequestInit | undefined)?.method === 'POST',
    )
    expect(posted).toBe(false)
  })

  it('shows an accessible error and a retry control when generation fails', async () => {
    mockApi({ generateStatus: 503 })

    await openArticle()
    fireEvent.click(
      await screen.findByRole('button', { name: 'Generar resumen' }),
    )

    expect(
      await screen.findByText(
        'El resumen no está disponible en este momento. Probá de nuevo más tarde.',
      ),
    ).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeTruthy()
  })

  it('does not request the stored summary again after reopening the article', async () => {
    const fetchMock = mockApi({ stored: true })

    await openArticle()
    await screen.findByText(summaryText)

    fireEvent.click(screen.getByRole('button', { name: /Volver a la portada/ }))
    await waitFor(() => {
      expect(
        screen.queryByRole('button', { name: /Volver a la portada/ }),
      ).toBeNull()
    })
    fireEvent.click(
      screen.getByRole('button', { name: 'Una historia del día' }),
    )
    await screen.findByText(summaryText)

    const summaryGets = fetchMock.mock.calls.filter(
      ([input]) => String(input).includes('/api/news/summary'),
    )
    expect(summaryGets).toHaveLength(1)
  })
})
