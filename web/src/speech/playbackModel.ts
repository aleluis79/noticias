export type AudioSegmentPart = 'title' | 'attribution' | 'body'

export interface AudioSegment {
  part: AudioSegmentPart
  index?: number
}

/**
 * Firefox on Linux delegates speech to speech-dispatcher, which sounds robotic
 * and cuts off with local voices. It gets server-synthesized audio instead.
 */
export function usesServerSpeech(userAgent: string): boolean {
  return /firefox|fxios/i.test(userAgent)
}

export function buildAudioSegments(paragraphCount: number): AudioSegment[] {
  const segments: AudioSegment[] = [{ part: 'title' }, { part: 'attribution' }]

  for (let index = 0; index < paragraphCount; index += 1) {
    segments.push({ part: 'body', index })
  }

  return segments
}

export interface AudioSegmentMetadata {
  title?: string
  author?: string | null
}

export function audioSegmentUrl(
  endpoint: string,
  articleUrl: string,
  segment: AudioSegment,
  metadata: AudioSegmentMetadata = {},
): string {
  const params = new URLSearchParams()
  params.set('url', articleUrl)
  params.set('part', segment.part)
  if (segment.index !== undefined) {
    params.set('index', String(segment.index))
  }
  if (segment.part === 'title' && metadata.title) {
    params.set('title', metadata.title)
  }
  if (segment.part === 'attribution' && metadata.author) {
    params.set('author', metadata.author)
  }

  return `${endpoint.replace(/\/+$/, '')}?${params.toString()}`
}
