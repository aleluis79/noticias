import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useServerArticleAudio } from './useServerArticleAudio'
import { FakeAudio } from '../test/fakeAudio'
import type { SpeechRate } from './speechModel'

const baseOptions = {
  articleKey: 'https://www.clarin.com/nota',
  endpoint: 'http://localhost:5251/api/news/audio',
  title: 'El título de la nota',
  author: 'Ana Pérez',
  paragraphCount: 2,
  enabled: true,
  rate: 1 as SpeechRate,
  onError: vi.fn(),
}

afterEach(() => {
  vi.unstubAllGlobals()
  FakeAudio.reset()
})

function setup(overrides: Partial<typeof baseOptions> = {}) {
  vi.stubGlobal('Audio', FakeAudio)
  const onError = vi.fn()
  return {
    onError,
    ...renderHook(
      (props: typeof baseOptions) => useServerArticleAudio(props),
      { initialProps: { ...baseOptions, onError, ...overrides } },
    ),
  }
}

describe('useServerArticleAudio', () => {
  it('plays title, attribution and paragraphs in order', () => {
    const { result } = setup()

    act(() => result.current.play())
    expect(result.current.status).toBe('playing')
    expect(FakeAudio.instances[0].src).toContain('part=title')

    act(() => FakeAudio.instances[0].finish())
    expect(FakeAudio.instances[1].play).toHaveBeenCalled()
    expect(FakeAudio.instances[1].src).toContain('part=attribution')

    act(() => FakeAudio.instances[1].finish())
    expect(FakeAudio.instances[2].play).toHaveBeenCalled()
    expect(FakeAudio.instances[2].src).toContain('part=body')
    expect(FakeAudio.instances[2].src).toContain('index=0')

    act(() => FakeAudio.instances[2].finish())
    act(() => FakeAudio.instances[3].finish())
    expect(result.current.status).toBe('idle')
  })

  it('prefetches the next two paragraphs while the current one plays', () => {
    const { result } = setup()

    act(() => result.current.play())

    expect(FakeAudio.instances).toHaveLength(3)
    expect(FakeAudio.instances[1].src).toContain('part=attribution')
    expect(FakeAudio.instances[2].src).toContain('index=0')
    expect(FakeAudio.instances[1].load).toHaveBeenCalled()
    expect(FakeAudio.instances[2].load).toHaveBeenCalled()
    expect(FakeAudio.instances[1].play).not.toHaveBeenCalled()
    expect(FakeAudio.instances[2].play).not.toHaveBeenCalled()

    act(() => FakeAudio.instances[0].finish())
    expect(FakeAudio.instances[1].play).toHaveBeenCalled()
    expect(FakeAudio.instances).toHaveLength(4)
    expect(FakeAudio.instances[3].src).toContain('index=1')
  })

  it('pauses, resumes and stops the current audio', () => {
    const { result } = setup()

    act(() => result.current.play())
    const audio = FakeAudio.instances[0]

    act(() => result.current.pause())
    expect(result.current.status).toBe('paused')
    expect(audio.pause).toHaveBeenCalledTimes(1)

    act(() => result.current.resume())
    expect(result.current.status).toBe('playing')
    expect(audio.play).toHaveBeenCalledTimes(2)

    act(() => result.current.stop())
    expect(result.current.status).toBe('idle')
    expect(audio.removeAttribute).toHaveBeenCalledWith('src')
    expect(FakeAudio.instances[1].removeAttribute).toHaveBeenCalledWith('src')
    expect(FakeAudio.instances[2].removeAttribute).toHaveBeenCalledWith('src')
  })

  it('applies the selected speed to the current and prefetched audio', () => {
    const { result, rerender } = setup()

    act(() => result.current.play())
    expect(FakeAudio.instances[0].playbackRate).toBe(1)

    rerender({ ...baseOptions, rate: 1.5 })
    expect(FakeAudio.instances[0].playbackRate).toBe(1.5)
    expect(FakeAudio.instances[1].playbackRate).toBe(1.5)
    expect(FakeAudio.instances[2].playbackRate).toBe(1.5)
  })

  it('reports an error once and becomes unavailable', () => {
    const { result, onError } = setup()

    act(() => result.current.play())
    act(() => FakeAudio.instances[0].fail())

    expect(onError).toHaveBeenCalledTimes(1)
    expect(result.current.available).toBe(false)
    expect(result.current.status).toBe('idle')
  })

  it('stops when the article changes', () => {
    const { result, rerender } = setup()

    act(() => result.current.play())
    const audio = FakeAudio.instances[0]

    rerender({ ...baseOptions, articleKey: 'https://www.clarin.com/otra' })

    expect(audio.removeAttribute).toHaveBeenCalledWith('src')
    expect(result.current.status).toBe('idle')
  })

  it('does nothing when disabled', () => {
    const { result } = setup({ enabled: false })

    act(() => result.current.play())

    expect(FakeAudio.instances).toHaveLength(0)
    expect(result.current.status).toBe('idle')
  })
})
