import { describe, expect, it } from 'vitest'
import {
  audioSegmentUrl,
  buildAudioSegments,
  usesServerSpeech,
} from './playbackModel'

describe('usesServerSpeech', () => {
  it('detects Firefox and Firefox on iOS', () => {
    expect(
      usesServerSpeech(
        'Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0',
      ),
    ).toBe(true)
    expect(
      usesServerSpeech(
        'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) FxiOS/140.0',
      ),
    ).toBe(true)
  })

  it('keeps the browser voice everywhere else', () => {
    expect(
      usesServerSpeech(
        'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/140.0 Safari/537.36',
      ),
    ).toBe(false)
    expect(usesServerSpeech('')).toBe(false)
  })
})

describe('buildAudioSegments', () => {
  it('orders title, attribution and every paragraph', () => {
    expect(buildAudioSegments(2)).toEqual([
      { part: 'title' },
      { part: 'attribution' },
      { part: 'body', index: 0 },
      { part: 'body', index: 1 },
    ])
  })

  it('has no body segments without paragraphs', () => {
    expect(buildAudioSegments(0)).toEqual([
      { part: 'title' },
      { part: 'attribution' },
    ])
  })
})

describe('audioSegmentUrl', () => {
  it('encodes the article and segment', () => {
    const url = audioSegmentUrl(
      'http://localhost:5251/api/news/audio',
      'https://www.clarin.com/nota?x=1',
      { part: 'body', index: 2 },
    )

    expect(url).toContain('http://localhost:5251/api/news/audio?')
    expect(url).toContain(
      `url=${encodeURIComponent('https://www.clarin.com/nota?x=1')}`,
    )
    expect(url).toContain('part=body')
    expect(url).toContain('index=2')
  })

  it('includes the visible title and author for their segments', () => {
    const title = audioSegmentUrl('/api/news/audio', 'https://tn.com.ar/nota', { part: 'title' }, {
      title: 'Título visible',
    })
    const attribution = audioSegmentUrl(
      '/api/news/audio',
      'https://tn.com.ar/nota',
      { part: 'attribution' },
      { author: 'Ana Pérez' },
    )

    expect(title).toContain('title=T%C3%ADtulo+visible')
    expect(attribution).toContain('author=Ana+P%C3%A9rez')
  })

  it('does not add an index for title or attribution', () => {
    const url = audioSegmentUrl('/api/news/audio', 'https://tn.com.ar/nota', {
      part: 'title',
    })

    expect(url).toBe(
      `/api/news/audio?url=${encodeURIComponent('https://tn.com.ar/nota')}&part=title`,
    )
  })
})
