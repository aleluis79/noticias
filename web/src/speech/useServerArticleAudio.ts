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

/**
 * Plays locally synthesized article segments with an <audio> element. The
 * browser voice remains the fallback when the API is unavailable.
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
  const segmentsRef = useRef<AudioSegment[]>([])
  const indexRef = useRef(0)
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
  }, [rate])

  useEffect(() => {
    onErrorRef.current = onError
  }, [onError])

  const disposeAudio = useCallback(() => {
    const audio = audioRef.current
    if (!audio) {
      return
    }

    audio.pause()
    audio.removeAttribute('src')
    audio.load()
    audioRef.current = null
  }, [])

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

  const startSegment = (index: number, generation: number) => {
    if (generation !== generationRef.current) {
      return
    }

    const segment = segmentsRef.current[index]
    if (!segment || !articleKey || !endpoint) {
      setStatus('idle')
      return
    }

    const audio = new Audio()
    audio.preload = 'auto'
    audio.playbackRate = rateRef.current
    audio.src = audioSegmentUrl(endpoint, articleKey, segment, { title, author })
    audio.onended = () => {
      if (generation !== generationRef.current) {
        return
      }

      if (index + 1 < segmentsRef.current.length) {
        indexRef.current = index + 1
        startSegment(index + 1, generation)
      } else {
        setStatus('idle')
      }
    }
    audio.onerror = () => fail(generation)

    audioRef.current = audio
    setStatus('playing')
    void audio.play().catch(() => fail(generation))
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
    indexRef.current = 0
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
