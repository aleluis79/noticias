import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'

const emptyEdition = {
  generatedAt: '2026-11-17T10:00:00Z',
  articleCount: 0,
  storyCount: 0,
  stories: [],
}

describe('test environment', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        status: 200,
        json: async () => emptyEdition,
      })),
    )
  })

  it('renders the app with the DOM stubs in place', async () => {
    render(<App />)

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: 'Noticias Argentinas' }),
      ).toBeTruthy()
    })

    expect(Element.prototype.scrollIntoView).toBeTypeOf('function')
  })
})
