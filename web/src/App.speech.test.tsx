import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import {
  installFakeSpeech,
  uninstallSpeech,
  type FakeSpeechHandle,
} from './test/fakeSpeechSynthesis'
import { FakeAudio } from './test/fakeAudio'

const articleUrl = 'https://www.lanacion.com.ar/nota-1'
const otherUrl = 'https://www.clarin.com/nota-2'

function storyFor(sourceCount: number) {
  const sources = [
    {
      sourceName: 'La Nación',
      title: 'Una historia del día',
      summary: '',
      originalUrl: articleUrl,
      publishedDate: null,
    },
  ]

  if (sourceCount > 1) {
    sources.push({
      sourceName: 'Clarín',
      title: 'La misma historia',
      summary: '',
      originalUrl: otherUrl,
      publishedDate: null,
    })
  }

  return {
    id: 'story-1',
    title: 'Una historia del día',
    summary: '',
    originalUrl: articleUrl,
    sourceName: 'La Nación',
    sourceCount,
    articleCount: sourceCount,
    latestPublishedDate: null,
    sources,
  }
}

function editionFor(sourceCount: number) {
  return {
    generatedAt: '2026-11-17T10:00:00Z',
    articleCount: sourceCount,
    storyCount: 1,
    stories: [storyFor(sourceCount)],
  }
}

const content = {
  url: articleUrl,
  title: 'Una historia del día',
  author: 'Ana Pérez',
  paragraphs: ['Primer párrafo del cuerpo.', 'Segundo párrafo del cuerpo.'],
}

function mockApi(sourceCount = 1) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)

    if (url.includes('/api/stories')) {
      return {
        ok: true,
        status: 200,
        json: async () => editionFor(sourceCount),
      }
    }

    if (url.includes('/api/news/content')) {
      return {
        ok: true,
        status: 200,
        json: async () => ({
          ...content,
          url: url.includes(encodeURIComponent(otherUrl)) ? otherUrl : articleUrl,
        }),
      }
    }

    throw new Error(`Unexpected request: ${url}`)
  })

  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

let speech: FakeSpeechHandle | null = null

beforeEach(() => {
  speech = installFakeSpeech()
})

afterEach(() => {
  speech?.restore()
  speech = null
  uninstallSpeech()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  FakeAudio.reset()
})

async function openStoryAtArticle() {
  mockApi()
  render(<App />)

  const headline = await screen.findByRole('button', {
    name: 'Una historia del día',
  })
  fireEvent.click(headline)

  await screen.findByText('Primer párrafo del cuerpo.')
}

describe('App speech integration', () => {
  it('offers playback only after the article body is ready', async () => {
    await openStoryAtArticle()

    expect(screen.getByRole('button', { name: 'Escuchar' })).toBeTruthy()
  })

  it('speaks the title and body when the user presses Escuchar', async () => {
    await openStoryAtArticle()

    fireEvent.click(screen.getByRole('button', { name: 'Escuchar' }))

    expect(speech?.fake.spoken[0]?.text).toBe('Una historia del día')
    expect(screen.getByRole('button', { name: 'Pausar' })).toBeTruthy()
  })

  it('stops playback when the reader is closed', async () => {
    await openStoryAtArticle()

    fireEvent.click(screen.getByRole('button', { name: 'Escuchar' }))
    const cancelsBefore = speech?.fake.cancelCalls ?? 0
    fireEvent.click(screen.getByRole('button', { name: /Volver a la portada/ }))

    expect(speech?.fake.cancelCalls ?? 0).toBeGreaterThan(cancelsBefore)
  })

  it('stops playback when another outlet is selected', async () => {
    mockApi(2)
    render(<App />)

    fireEvent.click(
      await screen.findByRole('button', { name: 'Una historia del día' }),
    )
    await screen.findByText('Primer párrafo del cuerpo.')

    fireEvent.click(screen.getByRole('button', { name: 'Escuchar' }))
    const cancelsBefore = speech?.fake.cancelCalls ?? 0

    fireEvent.click(screen.getByRole('button', { name: 'Clarín' }))

    await waitFor(() => {
      expect(speech?.fake.cancelCalls ?? 0).toBeGreaterThan(cancelsBefore)
    })
  })

  it('does not request the API when playback starts', async () => {
    const fetchMock = mockApi()
    render(<App />)

    fireEvent.click(
      await screen.findByRole('button', { name: 'Una historia del día' }),
    )
    await screen.findByText('Primer párrafo del cuerpo.')

    const callsBefore = fetchMock.mock.calls.length
    fireEvent.click(screen.getByRole('button', { name: 'Escuchar' }))

    expect(fetchMock.mock.calls.length).toBe(callsBefore)
  })

  it('uses local audio on Firefox instead of speech synthesis', async () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(
      'Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0',
    )
    vi.stubGlobal('Audio', FakeAudio)
    await openStoryAtArticle()

    fireEvent.click(screen.getByRole('button', { name: 'Escuchar' }))

    expect(FakeAudio.instances).toHaveLength(3)
    expect(FakeAudio.instances[0].src).toContain('/api/news/audio')
    expect(FakeAudio.instances[0].src).toContain('part=title')
    expect(speech?.fake.spoken).toHaveLength(0)
  })

  it('hides the control when the browser has no speech synthesis', async () => {
    uninstallSpeech()
    mockApi()
    render(<App />)

    fireEvent.click(
      await screen.findByRole('button', { name: 'Una historia del día' }),
    )
    await screen.findByText('Primer párrafo del cuerpo.')

    expect(screen.queryByRole('button', { name: 'Escuchar' })).toBeNull()
    expect(screen.queryByText('Escuchar la nota')).toBeNull()
  })
})
