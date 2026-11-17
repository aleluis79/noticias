import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

function story(
  id: string,
  title: string,
  sourceName: string,
  originalUrl: string,
) {
  return {
    id,
    title,
    summary: '',
    originalUrl,
    sourceName,
    sourceCount: 1,
    articleCount: 1,
    latestPublishedDate: null,
    sources: [
      {
        sourceName,
        title,
        summary: '',
        originalUrl,
        publishedDate: null,
      },
    ],
  }
}

const todayStory = story(
  'today-1',
  'Una historia de hoy',
  'Clarín',
  'https://www.clarin.com/hoy',
)

const yesterdayClarin = story(
  'yesterday-1',
  'Una historia de ayer de Clarín',
  'Clarín',
  'https://www.clarin.com/ayer-clarin',
)

const yesterdayLaNacion = story(
  'yesterday-2',
  'Una historia de ayer de La Nación',
  'La Nación',
  'https://www.lanacion.com.ar/ayer-nacion',
)

function edition(
  date: string,
  stories: ReturnType<typeof story>[],
  previousDays: { date: string; articleCount: number; storyCount: number }[],
) {
  return {
    date,
    generatedAt: '2026-11-17T10:00:00Z',
    articleCount: stories.length,
    storyCount: stories.length,
    stories,
    previousDays,
  }
}

function mockFetch(options?: { historyFails?: boolean; noPreviousDays?: boolean }) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)

    if (url.includes('/api/news/content')) {
      return {
        ok: true,
        status: 200,
        json: async () => ({
          url: 'https://www.clarin.com/ayer-clarin',
          title: 'Una historia de ayer de Clarín',
          author: 'Ana Pérez',
          paragraphs: ['Primer párrafo de la nota de ayer.'],
        }),
      }
    }

    if (url.includes('/api/stories?date=')) {
      if (options?.historyFails) {
        return { ok: false, status: 500, json: async () => ({}) }
      }

      return {
        ok: true,
        status: 200,
        json: async () =>
          edition('2026-11-16', [yesterdayClarin, yesterdayLaNacion], []),
      }
    }

    if (url.includes('/api/stories')) {
      return {
        ok: true,
        status: 200,
        json: async () =>
          edition(
            '2026-11-17',
            [todayStory],
            options?.noPreviousDays
              ? []
              : [
                  { date: '2026-11-16', articleCount: 2, storyCount: 2 },
                  { date: '2026-11-15', articleCount: 1, storyCount: 1 },
                ],
          ),
      }
    }

    throw new Error(`Unexpected request: ${url}`)
  })

  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('edition history', () => {
  beforeEach(() => {
    mockFetch()
  })

  it('loads a previous day and opens its note in the reader', async () => {
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Una historia de hoy')).toBeTruthy()
    })

    expect(
      screen.getByRole('heading', { name: 'Días anteriores' }),
    ).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: /Ayer · 16 de noviembre/ }))

    await waitFor(() => {
      expect(screen.getByText('Una historia de ayer de Clarín')).toBeTruthy()
    })

    fireEvent.click(screen.getByText('Una historia de ayer de Clarín'))

    await waitFor(() => {
      expect(
        screen.getByRole('heading', {
          name: 'Una historia de ayer de Clarín',
          level: 2,
        }),
      ).toBeTruthy()
    })

    await waitFor(() => {
      expect(screen.getByText('Primer párrafo de la nota de ayer.')).toBeTruthy()
    })
  })

  it('filters the previous day with the same medium filter', async () => {
    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Una historia de hoy')).toBeTruthy()
    })

    fireEvent.click(screen.getByRole('button', { name: /Ayer · 16 de noviembre/ }))

    await waitFor(() => {
      expect(screen.getByText('Una historia de ayer de La Nación')).toBeTruthy()
    })

    fireEvent.change(screen.getByLabelText('Medio'), {
      target: { value: 'La Nación' },
    })

    await waitFor(() => {
      expect(screen.queryByText('Una historia de ayer de Clarín')).toBeNull()
      expect(screen.getByText('Una historia de ayer de La Nación')).toBeTruthy()
    })
  })

  it('does not render the section when there are no previous days', async () => {
    mockFetch({ noPreviousDays: true })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Una historia de hoy')).toBeTruthy()
    })

    expect(screen.queryByText('Días anteriores')).toBeNull()
  })

  it('shows a retry message when a previous day fails', async () => {
    mockFetch({ historyFails: true })

    render(<App />)

    await waitFor(() => {
      expect(screen.getByText('Una historia de hoy')).toBeTruthy()
    })

    fireEvent.click(screen.getByRole('button', { name: /Ayer · 16 de noviembre/ }))

    await waitFor(() => {
      expect(
        screen.getByText('No pudimos cargar las notas de ese día.'),
      ).toBeTruthy()
      expect(screen.getByRole('button', { name: 'Reintentar' })).toBeTruthy()
    })

    expect(screen.getByText('Una historia de hoy')).toBeTruthy()
  })
})
