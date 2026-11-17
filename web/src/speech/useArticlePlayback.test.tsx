import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useArticlePlayback } from './useArticlePlayback'
import {
  installFakeSpeech,
  type FakeSpeechHandle,
} from '../test/fakeSpeechSynthesis'
import { FakeAudio } from '../test/fakeAudio'

const firefoxUserAgent =
  'Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0'
const chromeUserAgent =
  'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/140.0 Safari/537.36'

const baseOptions = {
  articleKey: 'https://www.clarin.com/nota',
  title: 'El título de la nota',
  author: 'Ana Pérez',
  sourceName: 'Clarín',
  paragraphs: ['Primer párrafo.', 'Segundo párrafo.'],
  endpoint: 'http://localhost:5251/api/news/audio',
  enabled: true,
}

let speech: FakeSpeechHandle | null = null

afterEach(() => {
  speech?.restore()
  speech = null
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  FakeAudio.reset()
})

function setup(userAgent: string, overrides: Partial<typeof baseOptions> = {}) {
  speech = installFakeSpeech()
  vi.stubGlobal('Audio', FakeAudio)
  vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(userAgent)

  return {
    ...speech,
    ...renderHook(
      (props: typeof baseOptions) => useArticlePlayback(props),
      { initialProps: { ...baseOptions, ...overrides } },
    ),
  }
}

describe('useArticlePlayback', () => {
  it('uses local audio on Firefox and leaves speech synthesis idle', () => {
    const { result, fake } = setup(firefoxUserAgent)

    act(() => result.current.play())

    expect(FakeAudio.instances).toHaveLength(3)
    expect(FakeAudio.instances[0].src).toContain('part=title')
    expect(fake.spoken).toHaveLength(0)
    expect(result.current.status).toBe('playing')
    expect(result.current.voices).toHaveLength(0)
  })

  it('keeps speech synthesis on Chrome without requesting audio', () => {
    const { result, fake } = setup(chromeUserAgent)

    act(() => result.current.play())

    expect(FakeAudio.instances).toHaveLength(0)
    expect(fake.spoken[0]?.text).toBe('El título de la nota')
    expect(result.current.voices.length).toBeGreaterThan(0)
  })

  it('falls back to the browser voice when local audio fails', () => {
    const { result, fake } = setup(firefoxUserAgent)

    act(() => result.current.play())
    act(() => FakeAudio.instances[0].fail())

    expect(fake.spoken[0]?.text).toBe('El título de la nota')
    expect(result.current.status).toBe('playing')
    expect(result.current.voices.length).toBeGreaterThan(0)
  })

  it('keeps play, pause and stop working with local audio', () => {
    const { result } = setup(firefoxUserAgent)

    act(() => result.current.play())
    act(() => result.current.pause())
    expect(result.current.status).toBe('paused')

    act(() => result.current.resume())
    expect(result.current.status).toBe('playing')

    act(() => result.current.stop())
    expect(result.current.status).toBe('idle')
  })

  it('applies persisted speed to local audio', () => {
    const { result } = setup(firefoxUserAgent)

    act(() => result.current.setRate(1.25))
    act(() => result.current.play())

    expect(FakeAudio.instances[0].playbackRate).toBe(1.25)
    expect(result.current.rate).toBe(1.25)
  })
})
