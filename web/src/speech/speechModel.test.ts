import { describe, expect, it } from 'vitest'
import {
  buildSpeechQueue,
  parseSpeechRate,
  pickSpanishVoice,
  SPEECH_RATES,
  speechChunkSizeFor,
} from './speechModel'

describe('buildSpeechQueue', () => {
  it('speaks the title, then the attribution, then the paragraphs in order', () => {
    const queue = buildSpeechQueue({
      title: 'El gobierno anunció medidas',
      author: 'Ana Pérez',
      sourceName: 'La Nación',
      paragraphs: ['Primer párrafo.', 'Segundo párrafo.'],
    })

    expect(queue.map((chunk) => chunk.text)).toEqual([
      'El gobierno anunció medidas',
      'Por Ana Pérez, La Nación.',
      'Primer párrafo.',
      'Segundo párrafo.',
    ])
    expect(queue.map((chunk) => chunk.paragraphIndex)).toEqual([
      null,
      null,
      0,
      1,
    ])
  })

  it('attributes the outlet alone when the article has no visible author', () => {
    const queue = buildSpeechQueue({
      title: 'Título',
      author: null,
      sourceName: 'Clarín',
      paragraphs: ['Cuerpo.'],
    })

    expect(queue[1]).toEqual({ text: 'Clarín.', paragraphIndex: null })
  })

  it('splits long paragraphs into chunks of at most 240 characters', () => {
    const sentence = 'Una oración con bastante contenido para la prueba. '
    const paragraph = sentence.repeat(8).trim()

    const queue = buildSpeechQueue({
      title: 'Título',
      author: null,
      sourceName: 'Infobae',
      paragraphs: [paragraph],
    })

    const body = queue.filter((chunk) => chunk.paragraphIndex === 0)
    expect(body.length).toBeGreaterThan(1)
    for (const chunk of body) {
      expect(chunk.text.length).toBeLessThanOrEqual(240)
      expect(chunk.paragraphIndex).toBe(0)
    }
    expect(body.map((chunk) => chunk.text).join(' ')).toBe(paragraph)
  })

  it('splits long unpunctuated text without cutting words', () => {
    const paragraph = 'palabra '.repeat(60).trim()

    const queue = buildSpeechQueue({
      title: 'Título',
      author: null,
      sourceName: 'TN',
      paragraphs: [paragraph],
    })

    const body = queue.filter((chunk) => chunk.paragraphIndex === 0)
    expect(body.length).toBeGreaterThan(1)
    for (const chunk of body) {
      expect(chunk.text.length).toBeLessThanOrEqual(240)
      expect(chunk.text.startsWith('palabra')).toBe(true)
      expect(chunk.text.endsWith('palabra')).toBe(true)
    }
  })

  it('drops empty titles, attributions and paragraphs', () => {
    const queue = buildSpeechQueue({
      title: '   ',
      author: '   ',
      sourceName: '',
      paragraphs: ['', '   ', 'Solo esto.'],
    })

    expect(queue).toEqual([{ text: 'Solo esto.', paragraphIndex: 2 }])
  })

  it('accepts a larger chunk size for engines that do not truncate', () => {
    const paragraph = 'Una oración de prueba con contenido suficiente. '.repeat(12).trim()

    const defaultQueue = buildSpeechQueue({
      title: 'Título',
      author: null,
      sourceName: 'TN',
      paragraphs: [paragraph],
    })
    const largeQueue = buildSpeechQueue({
      title: 'Título',
      author: null,
      sourceName: 'TN',
      paragraphs: [paragraph],
      maxChunk: 1000,
    })

    expect(
      defaultQueue.filter((chunk) => chunk.paragraphIndex === 0).length,
    ).toBeGreaterThan(1)
    expect(
      largeQueue.filter((chunk) => chunk.paragraphIndex === 0),
    ).toHaveLength(1)
  })
})

describe('speechChunkSizeFor', () => {
  it('uses longer fragments on Firefox', () => {
    expect(
      speechChunkSizeFor(
        'Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0',
      ),
    ).toBe(1000)
  })

  it('keeps the conservative fragment size elsewhere', () => {
    expect(
      speechChunkSizeFor(
        'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/140.0 Safari/537.36',
      ),
    ).toBe(240)
    expect(speechChunkSizeFor('')).toBe(240)
  })
})

describe('pickSpanishVoice', () => {
  const voices = [
    { name: 'English', lang: 'en-US', voiceURI: 'en-us' },
    { name: 'Spain', lang: 'es-ES', voiceURI: 'es-es' },
    { name: 'Latin', lang: 'es-419', voiceURI: 'es-419' },
    { name: 'Argentina', lang: 'es-AR', voiceURI: 'es-ar' },
  ]

  it('keeps the saved voice when it is still available', () => {
    expect(pickSpanishVoice(voices, 'es-es')?.voiceURI).toBe('es-es')
  })

  it('prefers Argentina, then Latin America, then Spain', () => {
    expect(pickSpanishVoice(voices, null)?.voiceURI).toBe('es-ar')
    expect(pickSpanishVoice(voices.slice(0, 3), null)?.voiceURI).toBe('es-419')
    expect(pickSpanishVoice(voices.slice(0, 2), null)?.voiceURI).toBe('es-es')
  })

  it('ignores voices that are not Spanish', () => {
    expect(pickSpanishVoice([voices[0]], null)).toBeNull()
    expect(pickSpanishVoice([], null)).toBeNull()
  })

  it('falls back when the saved voice no longer exists', () => {
    expect(pickSpanishVoice(voices, 'de-DE')?.voiceURI).toBe('es-ar')
  })
})

describe('parseSpeechRate', () => {
  it('accepts the supported rates', () => {
    expect(SPEECH_RATES).toEqual([1, 1.25, 1.5])
    expect(parseSpeechRate('1')).toBe(1)
    expect(parseSpeechRate('1.25')).toBe(1.25)
    expect(parseSpeechRate('1.5')).toBe(1.5)
  })

  it('falls back to normal speed for missing or invalid values', () => {
    expect(parseSpeechRate(null)).toBe(1)
    expect(parseSpeechRate('')).toBe(1)
    expect(parseSpeechRate('2')).toBe(1)
    expect(parseSpeechRate('rápido')).toBe(1)
  })
})
