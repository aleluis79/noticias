import { describe, expect, it } from 'vitest'
import {
  SHOW_MEDIA_STORAGE_KEY,
  buildReaderBlocks,
  readShowMediaPreference,
  writeShowMediaPreference,
  type ArticleMedia,
} from './mediaModel'

function image(url: string, afterParagraph: number): ArticleMedia {
  return {
    type: 'image',
    url,
    afterParagraph,
  }
}

describe('buildReaderBlocks', () => {
  it('places media before the first paragraph when the position is zero', () => {
    const blocks = buildReaderBlocks(
      ['Primer párrafo.', 'Segundo párrafo.'],
      [image('https://cdn.example.com/portada.jpg', 0)],
    )

    expect(blocks.map((block) => block.kind)).toEqual([
      'media',
      'paragraph',
      'paragraph',
    ])
    expect(blocks[0]).toMatchObject({
      kind: 'media',
      media: { url: 'https://cdn.example.com/portada.jpg' },
    })
  })

  it('places media between paragraphs', () => {
    const blocks = buildReaderBlocks(
      ['Primer párrafo.', 'Segundo párrafo.'],
      [image('https://cdn.example.com/entre.jpg', 1)],
    )

    expect(blocks.map((block) => block.kind)).toEqual([
      'paragraph',
      'media',
      'paragraph',
    ])
    expect(blocks[1]).toMatchObject({
      kind: 'media',
      media: { url: 'https://cdn.example.com/entre.jpg' },
    })
  })

  it('places media after the last paragraph', () => {
    const blocks = buildReaderBlocks(
      ['Primer párrafo.', 'Segundo párrafo.'],
      [image('https://cdn.example.com/cierre.jpg', 2)],
    )

    expect(blocks.map((block) => block.kind)).toEqual([
      'paragraph',
      'paragraph',
      'media',
    ])
  })

  it('clamps positions outside the article to the nearest end', () => {
    const blocks = buildReaderBlocks(
      ['Primer párrafo.'],
      [
        image('https://cdn.example.com/antes.jpg', -3),
        image('https://cdn.example.com/despues.jpg', 99),
      ],
    )

    expect(blocks.map((block) => block.kind)).toEqual([
      'media',
      'paragraph',
      'media',
    ])
  })

  it('keeps the source order of media that share a position', () => {
    const blocks = buildReaderBlocks(
      ['Primer párrafo.'],
      [
        image('https://cdn.example.com/uno.jpg', 1),
        image('https://cdn.example.com/dos.jpg', 1),
      ],
    )

    expect(blocks.filter((block) => block.kind === 'media')).toMatchObject([
      { media: { url: 'https://cdn.example.com/uno.jpg' } },
      { media: { url: 'https://cdn.example.com/dos.jpg' } },
    ])
  })
})

describe('show media preference', () => {
  it('defaults to disabled when nothing is stored', () => {
    expect(readShowMediaPreference()).toBe(false)
  })

  it('reads an enabled preference from localStorage', () => {
    window.localStorage.setItem(SHOW_MEDIA_STORAGE_KEY, '1')

    expect(readShowMediaPreference()).toBe(true)
  })

  it('writes the preference for the next visit', () => {
    writeShowMediaPreference(true)
    expect(window.localStorage.getItem(SHOW_MEDIA_STORAGE_KEY)).toBe('1')

    writeShowMediaPreference(false)
    expect(window.localStorage.getItem(SHOW_MEDIA_STORAGE_KEY)).toBe('0')
  })

  it('treats an unknown stored value as disabled', () => {
    window.localStorage.setItem(SHOW_MEDIA_STORAGE_KEY, 'quizas')

    expect(readShowMediaPreference()).toBe(false)
  })
})
