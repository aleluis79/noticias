import { usesServerSpeech } from './playbackModel'
import {
  useArticleSpeech,
  type ArticleSpeech,
  type UseArticleSpeechOptions,
} from './useArticleSpeech'
import { useServerArticleAudio } from './useServerArticleAudio'

export interface UseArticlePlaybackOptions extends UseArticleSpeechOptions {
  /** Base URL of the local audio endpoint, e.g. /api/news/audio. */
  endpoint: string | null
}

/**
 * Picks the playback engine: local server audio on Firefox, the browser voice
 * everywhere else. A local audio failure falls back to the browser voice.
 */
export function useArticlePlayback(
  options: UseArticlePlaybackOptions,
): ArticleSpeech {
  const userAgent =
    typeof navigator === 'undefined' ? '' : navigator.userAgent
  const prefersServerAudio = usesServerSpeech(userAgent)

  const speech = useArticleSpeech(options)
  const serverAudio = useServerArticleAudio({
    articleKey: options.articleKey,
    endpoint: options.endpoint,
    title: options.title,
    author: options.author,
    paragraphCount: options.paragraphs.length,
    enabled: options.enabled && prefersServerAudio,
    rate: speech.rate,
    onError: speech.play,
  })

  if (!prefersServerAudio || !serverAudio.available || !serverAudio.supported) {
    return speech
  }

  return {
    supported: true,
    status: serverAudio.status,
    rate: speech.rate,
    voices: [],
    voiceURI: null,
    setRate: speech.setRate,
    setVoiceURI: () => {},
    play: serverAudio.play,
    pause: serverAudio.pause,
    resume: serverAudio.resume,
    stop: serverAudio.stop,
  }
}
