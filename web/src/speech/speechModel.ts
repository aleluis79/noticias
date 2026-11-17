/** Longest spoken fragment; short enough to pause promptly and avoid truncation. */
export const MAX_SPEECH_CHUNK = 240

/**
 * Firefox delegates synthesis to the OS. Its engines handle long utterances
 * without Chrome's truncation, and fewer boundaries mean fewer cut-offs when
 * speech-dispatcher generates the audio.
 */
export const FIREFOX_MAX_SPEECH_CHUNK = 1000

export const SPEECH_RATE_STORAGE_KEY = 'noticias-speech-rate'
export const SPEECH_VOICE_STORAGE_KEY = 'noticias-speech-voice'

export const SPEECH_RATES = [1, 1.25, 1.5] as const
export type SpeechRate = (typeof SPEECH_RATES)[number]

export interface VoiceLike {
  name: string
  lang: string
  voiceURI: string
}

/** Higher is preferred when the user has no saved voice. */
function spanishVoiceRank(lang: string): number {
  const normalized = lang.replace('_', '-').toLowerCase()

  if (normalized === 'es-ar') {
    return 100
  }
  if (normalized.startsWith('es-419')) {
    return 80
  }
  if (normalized.startsWith('es-')) {
    return 60
  }
  if (normalized === 'es') {
    return 40
  }

  return 0
}

export function pickSpanishVoice(
  voices: readonly VoiceLike[],
  savedVoiceURI: string | null,
): VoiceLike | null {
  const spanishVoices = voices.filter(
    (voice) => spanishVoiceRank(voice.lang) > 0,
  )

  if (savedVoiceURI) {
    const saved = spanishVoices.find(
      (voice) => voice.voiceURI === savedVoiceURI,
    )
    if (saved) {
      return saved
    }
  }

  const [best] = [...spanishVoices].sort(
    (a, b) => spanishVoiceRank(b.lang) - spanishVoiceRank(a.lang),
  )

  return best ?? null
}

export function speechChunkSizeFor(userAgent: string): number {
  return /firefox/i.test(userAgent)
    ? FIREFOX_MAX_SPEECH_CHUNK
    : MAX_SPEECH_CHUNK
}

export function parseSpeechRate(value: string | null): SpeechRate {
  if (value === null || value === '') {
    return 1
  }

  const rate = Number(value)
  return SPEECH_RATES.includes(rate as SpeechRate) ? (rate as SpeechRate) : 1
}

export interface SpeechQueueInput {
  title: string
  author?: string | null
  sourceName: string
  paragraphs: string[]
  maxChunk?: number
}

export interface SpeechChunk {
  text: string
  /** Body paragraph being spoken, or null for the title and attribution. */
  paragraphIndex: number | null
}

function normalize(value: string | null | undefined): string {
  return value?.replace(/\s+/g, ' ').trim() ?? ''
}

function splitSentences(text: string): string[] {
  if (typeof Intl.Segmenter === 'function') {
    const segmenter = new Intl.Segmenter('es', { granularity: 'sentence' })
    return Array.from(segmenter.segment(text), (part) => part.segment)
  }

  return text.split(/(?<=[.!?…])\s+/)
}

function splitLongSentence(sentence: string, maxChunk: number): string[] {
  const pieces: string[] = []
  let rest = sentence

  while (rest.length > maxChunk) {
    const window = rest.slice(0, maxChunk + 1)
    const cut = window.lastIndexOf(' ')
    const index = cut > 0 ? cut : maxChunk
    pieces.push(rest.slice(0, index).trim())
    rest = rest.slice(index).trim()
  }

  if (rest) {
    pieces.push(rest)
  }

  return pieces
}

function chunkParagraph(
  text: string,
  paragraphIndex: number,
  maxChunk: number,
): SpeechChunk[] {
  const chunks: SpeechChunk[] = []
  let current = ''

  const push = () => {
    if (current) {
      chunks.push({ text: current, paragraphIndex })
      current = ''
    }
  }

  for (const rawSentence of splitSentences(text)) {
    const sentence = normalize(rawSentence)
    if (!sentence) {
      continue
    }

    if (sentence.length > maxChunk) {
      push()
      for (const piece of splitLongSentence(sentence, maxChunk)) {
        chunks.push({ text: piece, paragraphIndex })
      }
      continue
    }

    const candidate = current ? `${current} ${sentence}` : sentence
    if (candidate.length <= maxChunk) {
      current = candidate
    } else {
      push()
      current = sentence
    }
  }

  push()
  return chunks
}

/**
 * Spoken sequence for an article: title, attribution, then each paragraph.
 * Long paragraphs are split so pause and stop take effect immediately.
 */
export function buildSpeechQueue({
  title,
  author,
  sourceName,
  paragraphs,
  maxChunk = MAX_SPEECH_CHUNK,
}: SpeechQueueInput): SpeechChunk[] {
  const queue: SpeechChunk[] = []
  const cleanTitle = normalize(title)
  const cleanAuthor = normalize(author)
  const cleanSource = normalize(sourceName)

  if (cleanTitle) {
    queue.push({ text: cleanTitle, paragraphIndex: null })
  }

  if (cleanAuthor && cleanSource) {
    queue.push({
      text: `Por ${cleanAuthor}, ${cleanSource}.`,
      paragraphIndex: null,
    })
  } else if (cleanAuthor) {
    queue.push({ text: `Por ${cleanAuthor}.`, paragraphIndex: null })
  } else if (cleanSource) {
    queue.push({ text: `${cleanSource}.`, paragraphIndex: null })
  }

  paragraphs.forEach((paragraph, index) => {
    const clean = normalize(paragraph)
    if (clean) {
      queue.push(...chunkParagraph(clean, index, maxChunk))
    }
  })

  return queue
}
