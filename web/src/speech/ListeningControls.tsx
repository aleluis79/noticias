import { useId } from 'react'
import type { SpeechStatus } from './useArticleSpeech'
import {
  SPEECH_RATES,
  type SpeechRate,
  type VoiceLike,
} from './speechModel'

export interface ListeningControlsProps {
  status: SpeechStatus
  rate: SpeechRate
  voices: VoiceLike[]
  voiceURI: string | null
  onPlay: () => void
  onPause: () => void
  onResume: () => void
  onStop: () => void
  onRateChange: (rate: SpeechRate) => void
  onVoiceChange: (voiceURI: string) => void
}

const STATUS_TEXT: Record<SpeechStatus, string> = {
  idle: '',
  playing: 'Leyendo la nota',
  paused: 'Lectura en pausa',
}

function formatRate(rate: SpeechRate): string {
  return `${String(rate).replace('.', ',')}×`
}

function PlayIcon() {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="currentColor"
      aria-hidden="true"
    >
      <path d="M8 5v14l11-7z" />
    </svg>
  )
}

function PauseIcon() {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="currentColor"
      aria-hidden="true"
    >
      <path d="M7 5h4v14H7zM13 5h4v14h-4z" />
    </svg>
  )
}

function StopIcon() {
  return (
    <svg
      width="12"
      height="12"
      viewBox="0 0 24 24"
      fill="currentColor"
      aria-hidden="true"
    >
      <path d="M6 6h12v12H6z" />
    </svg>
  )
}

export function ListeningControls({
  status,
  rate,
  voices,
  voiceURI,
  onPlay,
  onPause,
  onResume,
  onStop,
  onRateChange,
  onVoiceChange,
}: ListeningControlsProps) {
  const labelId = useId()
  const voiceId = useId()

  const primaryLabel =
    status === 'playing' ? 'Pausar' : status === 'paused' ? 'Reanudar' : 'Escuchar'

  const handlePrimary = () => {
    if (status === 'playing') {
      onPause()
    } else if (status === 'paused') {
      onResume()
    } else {
      onPlay()
    }
  }

  return (
    <div className="listening-controls">
      <span className="listening-controls__label" id={labelId}>
        Escuchar la nota
      </span>

      <div
        className="listening-controls__group"
        role="group"
        aria-labelledby={labelId}
      >
        <button
          type="button"
          className="listening-controls__button listening-controls__button--primary"
          onClick={handlePrimary}
          aria-pressed={status !== 'idle'}
        >
          {status === 'playing' ? <PauseIcon /> : <PlayIcon />}
          {primaryLabel}
        </button>
        <button
          type="button"
          className="listening-controls__button listening-controls__button--stop"
          onClick={onStop}
          disabled={status === 'idle'}
          aria-label="Detener la lectura"
        >
          <StopIcon />
        </button>
      </div>

      <div
        className="listening-controls__rates"
        role="group"
        aria-label="Velocidad de lectura"
      >
        {SPEECH_RATES.map((option) => (
          <button
            key={option}
            type="button"
            className="listening-controls__rate"
            onClick={() => onRateChange(option)}
            aria-pressed={option === rate}
          >
            {formatRate(option)}
          </button>
        ))}
      </div>

      {voices.length > 1 && (
        <div className="listening-controls__voice">
          <label htmlFor={voiceId}>Voz</label>
          <select
            id={voiceId}
            value={voiceURI ?? voices[0]?.voiceURI ?? ''}
            onChange={(event) => onVoiceChange(event.target.value)}
          >
            {voices.map((voice) => (
              <option key={voice.voiceURI} value={voice.voiceURI}>
                {voice.name} ({voice.lang})
              </option>
            ))}
          </select>
        </div>
      )}

      <p className="visually-hidden" role="status" aria-live="polite">
        {STATUS_TEXT[status]}
      </p>
    </div>
  )
}
