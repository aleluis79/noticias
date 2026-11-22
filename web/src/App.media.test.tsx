import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { SHOW_MEDIA_STORAGE_KEY } from './media/mediaModel'

const articleUrl = 'https://www.clarin.com/nota-con-fotos_0_abc.html'

const edition = {
  generatedAt: '2026-11-22T10:00:00Z',
  articleCount: 1,
  storyCount: 1,
  stories: [
    {
      id: 'story-media',
      title: 'Una historia con fotos',
      summary: '',
      originalUrl: articleUrl,
      sourceName: 'Clarín',
      sourceCount: 1,
      articleCount: 1,
      latestPublishedDate: null,
      sources: [
        {
          sourceName: 'Clarín',
          title: 'Una historia con fotos',
          summary: '',
          originalUrl: articleUrl,
          publishedDate: null,
        },
      ],
    },
  ],
}

const paragraphs = [
  'Primer párrafo del cuerpo de la nota.',
  'Segundo párrafo del cuerpo de la nota.',
]

function articleContent(
  media: Array<Record<string, unknown>> = [],
) {
  return {
    url: articleUrl,
    title: 'Una historia con fotos',
    author: 'Ana Pérez',
    paragraphs,
    media,
  }
}

function ok(body: unknown) {
  return { ok: true, status: 200, json: async () => body }
}

function mockApi(media: Array<Record<string, unknown>> = []) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)

    if (url.includes('/api/stories')) {
      return ok(edition)
    }

    if (url.includes('/api/news/content')) {
      return ok(articleContent(media))
    }

    if (url.includes('/api/news/summary')) {
      return { ok: false, status: 404, json: async () => ({}) }
    }

    if (url.includes('/api/news/audio')) {
      return { ok: false, status: 503, json: async () => ({}) }
    }

    throw new Error(`Unexpected request: ${url}`)
  })

  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function openArticle() {
  const view = render(<App />)
  fireEvent.click(
    await screen.findByRole('button', { name: 'Una historia con fotos' }),
  )
  await screen.findByText(paragraphs[0])
  return view
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('App media integration', () => {
  it('does not show a media control when the article has no media', async () => {
    mockApi()

    await openArticle()

    expect(screen.queryByRole('button', { name: /Ver .*imagen/i })).toBeNull()
    expect(screen.queryAllByRole('img')).toHaveLength(0)
  })

  it('keeps media hidden until the reader enables it', async () => {
    mockApi([
      {
        type: 'image',
        url: 'https://cdn.example.com/foto.jpg',
        caption: 'Pie de la foto.',
        alt: 'Una foto de la nota',
        afterParagraph: 0,
      },
    ])

    await openArticle()

    expect(
      screen.getByRole('button', { name: 'Ver 1 imagen' }),
    ).toBeTruthy()
    expect(screen.queryAllByRole('img')).toHaveLength(0)
  })

  it('shows media inline and persists the preference when enabled', async () => {
    mockApi([
      {
        type: 'image',
        url: 'https://cdn.example.com/entre.jpg',
        caption: 'Pie de la foto.',
        alt: 'Una foto de la nota',
        afterParagraph: 1,
      },
    ])

    const { container } = await openArticle()
    fireEvent.click(screen.getByRole('button', { name: 'Ver 1 imagen' }))

    const img = await screen.findByRole('img', {
      name: 'Una foto de la nota',
    })
    expect(img.getAttribute('src')).toBe('https://cdn.example.com/entre.jpg')
    expect(screen.getByText('Pie de la foto.')).toBeTruthy()
    expect(window.localStorage.getItem(SHOW_MEDIA_STORAGE_KEY)).toBe('1')

    const blocks = container.querySelectorAll('.reader__content > *')
    expect(Array.from(blocks).map((block) => block.tagName)).toEqual([
      'P',
      'FIGURE',
      'P',
    ])
  })

  it('restores the enabled preference from localStorage', async () => {
    window.localStorage.setItem(SHOW_MEDIA_STORAGE_KEY, '1')
    mockApi([
      {
        type: 'image',
        url: 'https://cdn.example.com/preferida.jpg',
        caption: 'Foto recordada.',
        afterParagraph: 0,
      },
    ])

    await openArticle()

    expect(
      await screen.findByRole('img', { name: 'Foto recordada.' }),
    ).toBeTruthy()
  })

  it('keeps the caption when an image fails to load', async () => {
    mockApi([
      {
        type: 'image',
        url: 'https://cdn.example.com/rota.jpg',
        caption: 'El pie que sobrevive.',
        alt: 'La foto que no carga',
        afterParagraph: 0,
      },
    ])

    await openArticle()
    fireEvent.click(screen.getByRole('button', { name: 'Ver 1 imagen' }))

    const img = await screen.findByRole('img', {
      name: 'La foto que no carga',
    })
    fireEvent.error(img)

    expect(screen.queryAllByRole('img')).toHaveLength(0)
    expect(screen.getByText('El pie que sobrevive.')).toBeTruthy()
  })

  it('offers a YouTube video as a link and never renders an iframe', async () => {
    mockApi([
      {
        type: 'videoEmbed',
        provider: 'youtube',
        url: 'https://www.youtube.com/watch?v=abc123XYZ',
        caption: 'La conferencia completa.',
        afterParagraph: 1,
      },
    ])

    const { container } = await openArticle()
    fireEvent.click(
      screen.getByRole('button', { name: 'Ver 1 video' }),
    )

    const link = await screen.findByRole('link', {
      name: /Ver video en YouTube/i,
    })
    expect(link.getAttribute('href')).toBe(
      'https://www.youtube.com/watch?v=abc123XYZ',
    )
    expect(container.querySelector('iframe')).toBeNull()
    expect(screen.getByText('La conferencia completa.')).toBeTruthy()
  })
})
