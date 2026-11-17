import { useCallback, useEffect, useRef, useState } from 'react'
import type { SpeechStatus } from './useArticleSpeech'
import type { SpeechRate } from './speechModel'
import {
  audioSegmentUrl,
  buildAudioSegments,
  type AudioSegment,
} from './playbackModel'

export interface UseServerArticleAudioOptions {
  articleKey: string | null
  endpoint: string | null
  title: string
  author: string | null
  paragraphCount: number
  enabled: boolean
  rate: SpeechRate
  onError: () => void
}

export interface ServerArticleAudio {
  available: boolean
  supported: boolean
  status: SpeechStatus
  play: () => void
  pause: () => void
  resume: () => void
  stop: () => void
}

/** How many upcoming segments are generated while the current one plays. */
const PREFETCH_AHEAD = 2

function releaseAudio(audio: HTMLAudioElement) {
  audio.onended = null
  audio.onerror = null
  audio.pause()
  audio.removeAttribute('src')
  audio.load()
}

/**
 * Plays locally synthesized article segments with <audio> elements. The next
 * segments are fetched while the current one plays, so paragraphs start with
 * no gap. The browser voice remains the fallback when the API is unavailable.
 */
export function useServerArticleAudio({
  articleKey,
  endpoint,
  title,
  author,
  paragraphCount,
  enabled,
  rate,
  onError,
}: UseServerArticleAudioOptions): ServerArticleAudio {
  const [supported] = useState(() => typeof Audio !== 'undefined')
  const [available, setAvailable] = useState(true)
  const [status, setStatus] = useState<SpeechStatus>('idle')

  const audioRef = useRef<HTMLAudioElement | null>(null)
  const prefetchedRef = useRef(new Map<number, HTMLAudioElement>())
  const segmentsRef = useRef<AudioSegment[]>([])
  const generationRef = useRef(0)
  const statusRef = useRef(status)
  const rateRef = useRef(rate)
  const onErrorRef = useRef(onError)

  useEffect(() => {
    statusRef.current = status
  }, [status])

  useEffect(() => {
    rateRef.current = rate
    if (audioRef.current) {
      audioRef.current.playbackRate = rate
    }
    for (const audio of prefetchedRef.current.values()) {
      audio.playbackRate = rate
    }
  }, [rate])

  useEffect(() => {
    onErrorRef.current = onError
  }, [onError])

  const releasePrefetched = useCallback(() => {
    for (const audio of prefetchedRef.current.values()) {
      releaseAudio(audio)
    }
    prefetchedRef.current.clear()
  }, [])

  const disposeAudio = useCallback(() => {
    if (audioRef.current) {
      releaseAudio(audioRef.current)
      audioRef.current = null
    }
    releasePrefetched()
  }, [releasePrefetched])

  const fail = (generation: number) => {
    if (generation !== generationRef.current) {
      return
    }

    generationRef.current += 1
    disposeAudio()
    setStatus('idle')
    setAvailable(false)
    onErrorRef.current()
  }

  const createAudio = (index: number): HTMLAudioElement | null => {
    const segment = segmentsRef.current[index]
    if (!segment || !articleKey || !endpoint) {
      return null
    }

    const audio = new Audio()
    audio.preload = 'auto'
    audio.playbackRate = rateRef.current
    audio.src = audioSegmentUrl(endpoint, articleKey, segment, { title, author })
    return audio
  }

  const prefetch = (fromIndex: number) => {
    const wanted = new Set<number>()

    for (let offset = 1; offset <= PREFETCH_AHEAD; offset += 1) {
      const index = fromIndex + offset
      if (index >= segmentsRef.current.length) {
        break
      }

      wanted.add(index)
      if (!prefetchedRef.current.has(index)) {
        const audio = createAudio(index)
        if (audio) {
          audio.load()
          prefetchedRef.current.set(index, audio)
        }
      }
    }

    for (const [index, audio] of prefetchedRef.current) {
      if (!wanted.has(index)) {
        releaseAudio(audio)
        prefetchedRef.current.delete(index)
      }
    }
  }

  const startSegment = (index: number, generation: number) => {
    if (generation !== generationRef.current) {
      return
    }

    const segment = segmentsRef.current[index]
    if (!segment) {
      disposeAudio()
      setStatus('idle')
      return
    }

    let audio = prefetchedRef.current.get(index)
    if (audio) {
      prefetchedRef.current.delete(index)
    } else {
      audio = createAudio(index) ?? undefined
    }

    if (!audio) {
      setStatus('idle')
      return
    }

    audio.onended = () => {
      if (generation !== generationRef.current) {
        return
      }

      releaseAudio(audio as HTMLAudioElement)
      audioRef.current = null

      if (index + 1 < segmentsRef.current.length) {
        startSegment(index + 1, generation)
      } else {
        disposeAudio()
        setStatus('idle')
      }
    }
    audio.onerror = () => fail(generation)

    audioRef.current = audio
    setStatus('playing')
    void audio.play().catch(() => fail(generation))
    prefetch(index)
  }

  const play = () => {
    if (!supported || !enabled || !articleKey || !endpoint) {
      return
    }

    if (statusRef.current === 'paused' && audioRef.current) {
      const generation = generationRef.current
      setStatus('playing')
      void audioRef.current.play().catch(() => fail(generation))
      return
    }

    generationRef.current += 1
    const generation = generationRef.current
    segmentsRef.current = buildAudioSegments(paragraphCount)
    disposeAudio()
    startSegment(0, generation)
  }

  const pause = () => {
    if (statusRef.current !== 'playing') {
      return
    }

    audioRef.current?.pause()
    setStatus('paused')
  }

  const resume = () => {
    if (statusRef.current !== 'paused' || !audioRef.current) {
      return
    }

    const generation = generationRef.current
    setStatus('playing')
    void audioRef.current.play().catch(() => fail(generation))
  }

  const stop = useCallback(() => {
    generationRef.current += 1
    disposeAudio()
    setStatus('idle')
  }, [disposeAudio])

  const previousKey = useRef(articleKey)
  useEffect(() => {
    if (previousKey.current === articleKey) {
      return
    }

    previousKey.current = articleKey
    stop()
    setAvailable(true)
  }, [articleKey, stop])

  const wasEnabled = useRef(enabled)
  useEffect(() => {
    if (wasEnabled.current && !enabled) {
      stop()
    }
    wasEnabled.current = enabled
  }, [enabled, stop])

  useEffect(() => {
    return () => {
      generationRef.current += 1
      disposeAudio()
    }
  }, [disposeAudio])

  return {
    available,
    supported,
    status,
    play,
    pause,
    resume,
    stop,
  }
}
