import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ArticleMediaBlock } from './ArticleMediaBlock'
import type { ArticleMedia } from './mediaModel'

function image(overrides: Partial<ArticleMedia> = {}): ArticleMedia {
  return {
    type: 'image',
    url: 'https://cdn.example.com/foto.jpg',
    afterParagraph: 0,
    ...overrides,
  }
}

describe('ArticleMediaBlock', () => {
  it('renders an image with its caption, dimensions and lazy loading', () => {
    render(
      <ArticleMediaBlock
        media={image({
          caption: 'La hinchada durante el partido.',
          alt: 'El estadio lleno',
          width: 1200,
          height: 800,
        })}
      />,
    )

    const img = screen.getByRole('img', { name: 'El estadio lleno' })
    expect(img.getAttribute('src')).toBe('https://cdn.example.com/foto.jpg')
    expect(img.getAttribute('width')).toBe('1200')
    expect(img.getAttribute('height')).toBe('800')
    expect(img.getAttribute('loading')).toBe('lazy')
    expect(img.getAttribute('decoding')).toBe('async')
    expect(img.getAttribute('referrerpolicy')).toBe('no-referrer')
    expect(screen.getByText('La hinchada durante el partido.')).toBeTruthy()
  })

  it('uses the caption as alternative text when the source publishes no alt', () => {
    render(
      <ArticleMediaBlock
        media={image({ caption: 'Una foto sin texto alternativo.' })}
      />,
    )

    expect(
      screen.getByRole('img', { name: 'Una foto sin texto alternativo.' }),
    ).toBeTruthy()
  })

  it('hides a broken image and keeps its caption', () => {
    render(
      <ArticleMediaBlock
        media={image({ caption: 'El pie que sobrevive al error.' })}
      />,
    )

    fireEvent.error(screen.getByRole('img'))

    expect(screen.queryByRole('img')).toBeNull()
    expect(screen.getByText('El pie que sobrevive al error.')).toBeTruthy()
  })

  it('renders a native video with controls, poster and no preload', () => {
    const { container } = render(
      <ArticleMediaBlock
        media={{
          type: 'video',
          provider: 'native',
          url: 'https://www.infobae.com/video.mp4',
          posterUrl: 'https://www.infobae.com/poster.jpg',
          caption: 'El video de la nota.',
          afterParagraph: 1,
        }}
      />,
    )

    const video = container.querySelector('video')
    expect(video).not.toBeNull()
    expect(video?.hasAttribute('controls')).toBe(true)
    expect(video?.getAttribute('preload')).toBe('none')
    expect(video?.getAttribute('poster')).toBe('https://www.infobae.com/poster.jpg')
    expect(video?.hasAttribute('autoplay')).toBe(false)
    expect(video?.querySelector('source')?.getAttribute('src')).toBe(
      'https://www.infobae.com/video.mp4',
    )
    expect(screen.getByText('El video de la nota.')).toBeTruthy()
  })

  it('renders a YouTube card as an external link without an iframe', () => {
    const { container } = render(
      <ArticleMediaBlock
        media={{
          type: 'videoEmbed',
          provider: 'youtube',
          url: 'https://www.youtube.com/watch?v=abc123XYZ',
          caption: 'La conferencia completa.',
          afterParagraph: 2,
        }}
      />,
    )

    const link = screen.getByRole('link', { name: /Ver video en YouTube/i })
    expect(link.getAttribute('href')).toBe('https://www.youtube.com/watch?v=abc123XYZ')
    expect(link.getAttribute('target')).toBe('_blank')
    expect(link.getAttribute('rel')).toBe('noopener noreferrer')
    expect(container.querySelector('iframe')).toBeNull()
    expect(screen.getByText('La conferencia completa.')).toBeTruthy()
  })

  it('renders a Vimeo card as an external link', () => {
    render(
      <ArticleMediaBlock
        media={{
          type: 'videoEmbed',
          provider: 'vimeo',
          url: 'https://vimeo.com/987654',
          afterParagraph: 0,
        }}
      />,
    )

    const link = screen.getByRole('link', { name: /Ver video en Vimeo/i })
    expect(link.getAttribute('href')).toBe('https://vimeo.com/987654')
  })
})
