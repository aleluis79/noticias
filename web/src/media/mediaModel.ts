export const SHOW_MEDIA_STORAGE_KEY = 'noticias-show-media'

export interface ArticleMedia {
  type: 'image' | 'video' | 'videoEmbed'
  url: string
  posterUrl?: string | null
  caption?: string | null
  alt?: string | null
  provider?: string | null
  width?: number | null
  height?: number | null
  afterParagraph: number
}

export type ReaderBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'media'; media: ArticleMedia }

export function buildReaderBlocks(
  paragraphs: string[],
  media: ArticleMedia[],
): ReaderBlock[] {
  const byPosition = new Map<number, ArticleMedia[]>()

  for (const item of media) {
    const rawPosition = Number.isFinite(item.afterParagraph)
      ? Math.trunc(item.afterParagraph)
      : 0
    const position = Math.min(Math.max(rawPosition, 0), paragraphs.length)
    const group = byPosition.get(position)

    if (group) {
      group.push(item)
    } else {
      byPosition.set(position, [item])
    }
  }

  const blocks: ReaderBlock[] = []

  for (let index = 0; index <= paragraphs.length; index += 1) {
    if (index > 0) {
      blocks.push({ kind: 'paragraph', text: paragraphs[index - 1] })
    }

    for (const item of byPosition.get(index) ?? []) {
      blocks.push({ kind: 'media', media: item })
    }
  }

  return blocks
}

export function readShowMediaPreference(): boolean {
  try {
    return window.localStorage.getItem(SHOW_MEDIA_STORAGE_KEY) === '1'
  } catch {
    return false
  }
}

export function writeShowMediaPreference(value: boolean): void {
  try {
    window.localStorage.setItem(SHOW_MEDIA_STORAGE_KEY, value ? '1' : '0')
  } catch {
    // A blocked or full storage must not break the reader.
  }
}
