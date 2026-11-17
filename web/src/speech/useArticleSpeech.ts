import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  buildSpeechQueue,
  parseSpeechRate,
  pickSpanishVoice,
  SPEECH_RATE_STORAGE_KEY,
  SPEECH_VOICE_STORAGE_KEY,
  speechChunkSizeFor,
  type SpeechChunk,
  type SpeechRate,
  type VoiceLike,
} from './speechModel'

export type SpeechStatus = 'idle' | 'playing' | 'paused'

export interface UseArticleSpeechOptions {
  articleKey: string | null
  title: string
  author: string | null
  sourceName: string
  paragraphs: string[]
  enabled: boolean
}

export interface ArticleSpeech {
  supported: boolean
  status: SpeechStatus
  rate: SpeechRate
  voices: VoiceLike[]
  voiceURI: string | null
  setRate: (rate: SpeechRate) => void
  setVoiceURI: (voiceURI: string | null) => void
  play: () => void
  pause: () => void
  resume: () => void
  stop: () => void
}

const SPANISH_LANGUAGE = /^es(-|$)/i

function readStorage(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null
  }
}

function writeStorage(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value)
  } catch {
    // Storage can be unavailable (private mode); playback still works.
  }
}

function getSynthesis(): SpeechSynthesis | null {
  if (typeof window === 'undefined') {
    return null
  }

  return window.speechSynthesis ?? null
}

function isSpeechSupported(): boolean {
  return (
    getSynthesis() !== null &&
    typeof window.SpeechSynthesisUtterance === 'function'
  )
}

function toVoiceLike(voice: SpeechSynthesisVoice): VoiceLike {
  return {
    name: voice.name,
    lang: voice.lang,
    voiceURI: voice.voiceURI,
  }
}

export function useArticleSpeech({
  articleKey,
  title,
  author,
  sourceName,
  paragraphs,
  enabled,
}: UseArticleSpeechOptions): ArticleSpeech {
  const [supported] = useState(isSpeechSupported)
  const [status, setStatus] = useState<SpeechStatus>('idle')
  const [rate, setRateState] = useState<SpeechRate>(() =>
    parseSpeechRate(readStorage(SPEECH_RATE_STORAGE_KEY)),
  )
  const [savedVoiceURI, setSavedVoiceURI] = useState<string | null>(() =>
    readStorage(SPEECH_VOICE_STORAGE_KEY),
  )
  const [voices, setVoices] = useState<VoiceLike[]>([])

  const statusRef = useRef(status)
  const rateRef = useRef(rate)

  const nativeVoicesRef = useRef<SpeechSynthesisVoice[]>([])
  const queueRef = useRef<SpeechChunk[]>([])
  const indexRef = useRef(0)
  const sessionRef = useRef(0)

  const selectedVoice = useMemo(
    () => pickSpanishVoice(voices, savedVoiceURI),
    [voices, savedVoiceURI],
  )
  const selectedVoiceRef = useRef<VoiceLike | null>(selectedVoice)

  useEffect(() => {
    statusRef.current = status
  }, [status])

  useEffect(() => {
    rateRef.current = rate
  }, [rate])

  useEffect(() => {
    selectedVoiceRef.current = selectedVoice
  }, [selectedVoice])

  useEffect(() => {
    if (!supported) {
      return
    }

    const synthesis = getSynthesis()
    if (!synthesis) {
      return
    }

    const updateVoices = () => {
      const available = synthesis.getVoices()
      nativeVoicesRef.current = available
      setVoices(
        available
          .filter((voice) => SPANISH_LANGUAGE.test(voice.lang))
          .map(toVoiceLike),
      )
    }

    updateVoices()
    synthesis.addEventListener('voiceschanged', updateVoices)
    return () => synthesis.removeEventListener('voiceschanged', updateVoices)
  }, [supported])

  useEffect(() => {
    writeStorage(SPEECH_RATE_STORAGE_KEY, String(rate))
  }, [rate])

  useEffect(() => {
    if (savedVoiceURI) {
      writeStorage(SPEECH_VOICE_STORAGE_KEY, savedVoiceURI)
    }
  }, [savedVoiceURI])

  const stop = useCallback(() => {
    sessionRef.current += 1
    queueRef.current = []
    indexRef.current = 0
    getSynthesis()?.cancel()
    setStatus('idle')
  }, [])

  // Cancel anything still queued when the reader unmounts.
  useEffect(() => {
    return () => {
      sessionRef.current += 1
      getSynthesis()?.cancel()
    }
  }, [])

  const speakFrom = (index: number, session: number) => {
    const synthesis = getSynthesis()
    const chunk = queueRef.current[index]

    if (!synthesis || !chunk) {
      setStatus('idle')
      return
    }

    const utterance = new window.SpeechSynthesisUtterance(chunk.text)
    const selectedVoice = selectedVoiceRef.current
    utterance.lang = selectedVoice?.lang ?? 'es-AR'
    utterance.rate = rateRef.current
    if (selectedVoice) {
      const nativeVoice = nativeVoicesRef.current.find(
        (voice) => voice.voiceURI === selectedVoice.voiceURI,
      )
      if (nativeVoice) {
        utterance.voice = nativeVoice
      }
    }

    utterance.onend = () => {
      if (session !== sessionRef.current) {
        return
      }

      indexRef.current = index + 1
      if (indexRef.current >= queueRef.current.length) {
        setStatus('idle')
      } else {
        speakFrom(indexRef.current, session)
      }
    }

    utterance.onerror = () => {
      if (session !== sessionRef.current) {
        return
      }

      // "canceled" and "interrupted" come from our own stop/cancel calls.
      setStatus('idle')
    }

    synthesis.speak(utterance)
  }

  const play = () => {
    if (!supported) {
      return
    }

    const synthesis = getSynthesis()
    if (!synthesis) {
      return
    }

    if (statusRef.current === 'paused') {
      synthesis.resume()
      setStatus('playing')
      return
    }

    const userAgent =
      typeof navigator === 'undefined' ? '' : navigator.userAgent
    const queue = buildSpeechQueue({
      title,
      author,
      sourceName,
      paragraphs,
      maxChunk: speechChunkSizeFor(userAgent),
    })
    if (queue.length === 0) {
      return
    }

    sessionRef.current += 1
    const session = sessionRef.current
    queueRef.current = queue
    indexRef.current = 0
    synthesis.cancel()
    setStatus('playing')
    speakFrom(0, session)
  }

  const pause = () => {
    if (!supported || statusRef.current !== 'playing') {
      return
    }

    getSynthesis()?.pause()
    setStatus('paused')
  }

  const resume = () => {
    if (!supported || statusRef.current !== 'paused') {
      return
    }

    getSynthesis()?.resume()
    setStatus('playing')
  }

  const setRate = (nextRate: SpeechRate) => {
    setRateState(nextRate)
  }

  const setVoiceURI = (voiceURI: string | null) => {
    setSavedVoiceURI(voiceURI)
  }

  const previousKey = useRef(articleKey)
  useEffect(() => {
    if (previousKey.current === articleKey) {
      return
    }

    previousKey.current = articleKey
    stop()
  }, [articleKey, stop])

  const wasEnabled = useRef(enabled)
  useEffect(() => {
    if (wasEnabled.current && !enabled) {
      stop()
    }
    wasEnabled.current = enabled
  }, [enabled, stop])

  return {
    supported,
    status,
    rate,
    voices,
    voiceURI: selectedVoice?.voiceURI ?? null,
    setRate,
    setVoiceURI,
    play,
    pause,
    resume,
    stop,
  }
}
