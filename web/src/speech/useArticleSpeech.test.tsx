import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useArticleSpeech } from './useArticleSpeech'
import {
  SPEECH_RATE_STORAGE_KEY,
  SPEECH_VOICE_STORAGE_KEY,
} from './speechModel'
import {
  installFakeSpeech,
  uninstallSpeech,
  type FakeSpeechHandle,
} from '../test/fakeSpeechSynthesis'

const baseProps = {
  articleKey: 'https://www.lanacion.com.ar/nota',
  title: 'El título de la nota',
  author: 'Ana Pérez',
  sourceName: 'La Nación',
  paragraphs: ['Primer párrafo.', 'Segundo párrafo.'],
  enabled: true,
}

let handle: FakeSpeechHandle | null = null

afterEach(() => {
  handle?.restore()
  handle = null
  uninstallSpeech()
})

function setup(overrides: Partial<typeof baseProps> = {}) {
  handle = installFakeSpeech()
  return {
    ...handle,
    ...renderHook(
      (props: typeof baseProps) => useArticleSpeech(props),
      { initialProps: { ...baseProps, ...overrides } },
    ),
  }
}

describe('useArticleSpeech', () => {
  it('starts idle and exposes the supported Spanish voices', () => {
    const { result } = setup()

    expect(result.current.supported).toBe(true)
    expect(result.current.status).toBe('idle')
    expect(result.current.voices.map((voice) => voice.voiceURI)).toEqual([
      'es-es',
      'es-419',
      'es-ar',
    ])
  })

  it('speaks the queue in order and advances on each end event', () => {
    const { result, fake } = setup()

    act(() => result.current.play())

    expect(result.current.status).toBe('playing')
    expect(fake.spoken.map((utterance) => utterance.text)).toEqual([
      'El título de la nota',
    ])

    act(() => fake.finish())
    expect(fake.spoken.at(-1)?.text).toBe('Por Ana Pérez, La Nación.')

    act(() => fake.finish())
    expect(fake.spoken.at(-1)?.text).toBe('Primer párrafo.')
  })

  it('returns to idle when the last fragment ends', () => {
    const { result, fake } = setup()

    act(() => result.current.play())
    for (let i = 0; i < 4; i += 1) {
      act(() => fake.finish())
    }

    expect(result.current.status).toBe('idle')
  })

  it('pauses and resumes without restarting the queue', () => {
    const { result, fake } = setup()

    act(() => result.current.play())
    act(() => result.current.pause())

    expect(result.current.status).toBe('paused')
    expect(fake.pauseCalls).toBe(1)

    act(() => result.current.resume())

    expect(result.current.status).toBe('playing')
    expect(fake.resumeCalls).toBe(1)
    expect(fake.spoken).toHaveLength(1)
  })

  it('stops playback and cancels the engine', () => {
    const { result, fake } = setup()

    act(() => result.current.play())
    act(() => result.current.stop())

    expect(result.current.status).toBe('idle')
    expect(fake.cancelCalls).toBeGreaterThanOrEqual(1)
  })

  it('ignores a canceled error from our own cancellation', () => {
    const { result, fake } = setup()

    act(() => result.current.play())
    act(() => fake.fail('canceled'))

    expect(result.current.status).toBe('idle')
  })

  it('reports no support and does nothing without the speech API', () => {
    uninstallSpeech()
    const { result } = renderHook(() => useArticleSpeech(baseProps))

    expect(result.current.supported).toBe(false)
    act(() => result.current.play())
    expect(result.current.status).toBe('idle')
  })

  it('stops when the article key changes', () => {
    const { result, fake, rerender } = setup()

    act(() => result.current.play())
    const cancelsBefore = fake.cancelCalls
    rerender({ ...baseProps, articleKey: 'https://www.clarin.com/otra' })

    expect(fake.cancelCalls).toBeGreaterThan(cancelsBefore)
    expect(result.current.status).toBe('idle')
  })

  it('stops when the article is no longer enabled', () => {
    const { result, fake, rerender } = setup()

    act(() => result.current.play())
    const cancelsBefore = fake.cancelCalls
    rerender({ ...baseProps, enabled: false })

    expect(fake.cancelCalls).toBeGreaterThan(cancelsBefore)
    expect(result.current.status).toBe('idle')
  })

  it('cancels the engine when the reader unmounts', () => {
    const { result, fake, unmount } = setup()

    act(() => result.current.play())
    const cancelsBefore = fake.cancelCalls
    unmount()

    expect(fake.cancelCalls).toBeGreaterThan(cancelsBefore)
  })

  it('reads the saved rate and voice and applies them to the utterance', () => {
    window.localStorage.setItem(SPEECH_RATE_STORAGE_KEY, '1.25')
    window.localStorage.setItem(SPEECH_VOICE_STORAGE_KEY, 'es-419')
    const { result, fake } = setup()

    act(() => result.current.play())

    expect(result.current.rate).toBe(1.25)
    expect(fake.current()?.rate).toBe(1.25)
    expect(fake.current()?.voice?.voiceURI).toBe('es-419')
  })

  it('persists rate and voice changes', () => {
    const { result } = setup()

    act(() => result.current.setRate(1.5))
    act(() => result.current.setVoiceURI('es-es'))

    expect(window.localStorage.getItem(SPEECH_RATE_STORAGE_KEY)).toBe('1.5')
    expect(window.localStorage.getItem(SPEECH_VOICE_STORAGE_KEY)).toBe('es-es')
  })

  it('falls back to the best Spanish voice when the saved one is gone', () => {
    window.localStorage.setItem(SPEECH_VOICE_STORAGE_KEY, 'voz-inexistente')
    const { result, fake } = setup()

    act(() => result.current.play())

    expect(result.current.voiceURI).toBe('es-ar')
    expect(fake.current()?.voice?.voiceURI).toBe('es-ar')
  })

  it('assigns the native voice object instead of a copy', () => {
    const { result, fake } = setup()
    const nativeVoice = fake.getVoices().find(
      (voice) => voice.voiceURI === 'es-ar',
    )

    act(() => result.current.play())

    expect(fake.current()?.voice).toBe(nativeVoice)
  })

  it('uses the selected voice language instead of a hardcoded locale', () => {
    window.localStorage.setItem(SPEECH_VOICE_STORAGE_KEY, 'es-419')
    const { result, fake } = setup()

    act(() => result.current.play())

    expect(fake.current()?.lang).toBe('es-419')
  })

  it('uses longer fragments on Firefox to avoid cut-off speech', () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(
      'Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0',
    )
    const paragraph = 'Una oración de prueba con contenido suficiente. '.repeat(12).trim()
    const { result, fake } = setup({ paragraphs: [paragraph] })

    act(() => result.current.play())
    act(() => fake.finish())
    act(() => fake.finish())

    const body = fake.spoken.filter((utterance) =>
      utterance.text.startsWith('Una oración'),
    )
    expect(body).toHaveLength(1)
  })
})
