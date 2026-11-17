import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ListeningControls } from './ListeningControls'
import type { SpeechRate, VoiceLike } from './speechModel'

const voices: VoiceLike[] = [
  { name: 'Argentina', lang: 'es-AR', voiceURI: 'es-ar' },
  { name: 'España', lang: 'es-ES', voiceURI: 'es-es' },
]

function renderControls(overrides: Partial<Parameters<typeof ListeningControls>[0]> = {}) {
  const handlers = {
    onPlay: vi.fn(),
    onPause: vi.fn(),
    onResume: vi.fn(),
    onStop: vi.fn(),
    onRateChange: vi.fn(),
    onVoiceChange: vi.fn(),
  }

  render(
    <ListeningControls
      status="idle"
      rate={1 as SpeechRate}
      voices={voices}
      voiceURI="es-ar"
      {...handlers}
      {...overrides}
    />,
  )

  return handlers
}

describe('ListeningControls', () => {
  it('shows the listening label and the idle primary action', () => {
    renderControls()

    expect(screen.getByText('Escuchar la nota')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Escuchar' })).toBeTruthy()
    expect(
      screen.getByRole('button', { name: 'Detener la lectura' }),
    ).toHaveProperty('disabled', true)
  })

  it('alternates play, pause and resume from the primary button', () => {
    const idle = renderControls()
    fireEvent.click(screen.getByRole('button', { name: 'Escuchar' }))
    expect(idle.onPlay).toHaveBeenCalledTimes(1)

    cleanup()
    const playing = renderControls({ status: 'playing' })
    fireEvent.click(screen.getByRole('button', { name: 'Pausar' }))
    expect(playing.onPause).toHaveBeenCalledTimes(1)

    cleanup()
    const paused = renderControls({ status: 'paused' })
    fireEvent.click(screen.getByRole('button', { name: 'Reanudar' }))
    expect(paused.onResume).toHaveBeenCalledTimes(1)
  })

  it('marks the current rate and reports changes', () => {
    const handlers = renderControls({ rate: 1.25 })

    expect(
      screen.getByRole('button', { name: '1,25×' }).getAttribute('aria-pressed'),
    ).toBe('true')
    expect(
      screen.getByRole('button', { name: '1×' }).getAttribute('aria-pressed'),
    ).toBe('false')

    fireEvent.click(screen.getByRole('button', { name: '1,5×' }))
    expect(handlers.onRateChange).toHaveBeenCalledWith(1.5)
  })

  it('only offers the voice selector when there is a choice', () => {
    renderControls()
    expect(screen.getByLabelText('Voz')).toBeTruthy()

    cleanup()
    renderControls({ voices: [voices[0]] })
    expect(screen.queryByLabelText('Voz')).toBeNull()
  })

  it('reports voice changes', () => {
    const handlers = renderControls()

    fireEvent.change(screen.getByLabelText('Voz'), {
      target: { value: 'es-es' },
    })

    expect(handlers.onVoiceChange).toHaveBeenCalledWith('es-es')
  })

  it('announces the playback state', () => {
    renderControls({ status: 'playing' })
    expect(screen.getByText('Leyendo la nota')).toBeTruthy()

    cleanup()
    renderControls({ status: 'paused' })
    expect(screen.getByText('Lectura en pausa')).toBeTruthy()
  })

  it('stops playback from the stop button', () => {
    const handlers = renderControls({ status: 'playing' })

    fireEvent.click(screen.getByRole('button', { name: 'Detener la lectura' }))

    expect(handlers.onStop).toHaveBeenCalledTimes(1)
  })

  it('exposes accessible names for every control', () => {
    renderControls({ status: 'playing' })

    for (const button of screen.getAllByRole('button')) {
      const name =
        button.getAttribute('aria-label') ??
        button.textContent?.replace(/\s+/g, ' ').trim()
      expect(name).toBeTruthy()
    }

    expect(
      screen.getByRole('group', { name: 'Escuchar la nota' }),
    ).toBeTruthy()
    expect(
      screen.getByRole('group', { name: 'Velocidad de lectura' }),
    ).toBeTruthy()
    expect(screen.getByLabelText('Voz')).toBeTruthy()
  })

  it('reflects the pressed state and enables stop only while active', () => {
    renderControls({ status: 'idle' })

    expect(
      screen.getByRole('button', { name: 'Escuchar' }).getAttribute('aria-pressed'),
    ).toBe('false')
    expect(
      screen.getByRole('button', { name: 'Detener la lectura' }),
    ).toHaveProperty('disabled', true)

    cleanup()
    renderControls({ status: 'playing' })

    expect(
      screen.getByRole('button', { name: 'Pausar' }).getAttribute('aria-pressed'),
    ).toBe('true')
    expect(
      screen.getByRole('button', { name: 'Detener la lectura' }),
    ).toHaveProperty('disabled', false)
  })
})
