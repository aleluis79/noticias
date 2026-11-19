import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const emptyEdition = {
  generatedAt: '2026-11-17T10:00:00Z',
  articleCount: 0,
  storyCount: 0,
  stories: [],
}

const originalScrollY = Object.getOwnPropertyDescriptor(window, 'scrollY')
let scrollY = 0

function scrollTo(value: number) {
  scrollY = value
  fireEvent.scroll(window)
}

describe('back to top button', () => {
  beforeEach(() => {
    scrollY = 0
    Object.defineProperty(window, 'scrollY', {
      configurable: true,
      get: () => scrollY,
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({
        ok: true,
        status: 200,
        json: async () => emptyEdition,
      })),
    )
    vi.mocked(window.scrollTo).mockClear()
  })

  afterEach(() => {
    if (originalScrollY) {
      Object.defineProperty(window, 'scrollY', originalScrollY)
    }
  })

  it('appears after scrolling down and only hides near the top', async () => {
    render(<App />)
    await screen.findByRole('heading', { name: 'Noticias Argentinas' })

    expect(
      screen.queryByRole('button', {
        name: 'Volver al inicio de la página',
      }),
    ).toBeNull()

    scrollTo(900)
    await screen.findByRole('button', {
      name: 'Volver al inicio de la página',
    })

    scrollTo(120)
    await waitFor(() => {
      expect(
        screen.queryByRole('button', {
          name: 'Volver al inicio de la página',
        }),
      ).toBeNull()
    })
  })

  it('scrolls to the top and moves focus to the masthead', async () => {
    render(<App />)
    await screen.findByRole('heading', { name: 'Noticias Argentinas' })

    scrollTo(900)
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Volver al inicio de la página',
      }),
    )

    expect(window.scrollTo).toHaveBeenCalledWith({
      top: 0,
      behavior: 'smooth',
    })
    expect(document.activeElement).toBe(
      screen.getByRole('heading', { name: 'Noticias Argentinas' }),
    )
  })
})
