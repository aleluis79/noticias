import type { SummaryState } from './useArticleSummary'

export interface SummaryPanelProps {
  state: SummaryState
  onGenerate: () => void
}

function SparkleIcon() {
  return (
    <svg
      width="15"
      height="15"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9L12 3Z" />
      <path d="M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8L19 15Z" />
    </svg>
  )
}

export function SummaryPanel({ state, onGenerate }: SummaryPanelProps) {
  if (state.status === 'loading') {
    return null
  }

  if (state.status === 'idle') {
    return (
      <div className="summary summary--empty">
        <span className="summary__label">Resumen · IA</span>
        <p className="summary__hint">
          Un resumen breve de la nota, generado en el servidor con un modelo
          local.
        </p>
        <button type="button" className="summary__button" onClick={onGenerate}>
          <SparkleIcon />
          Generar resumen
        </button>
      </div>
    )
  }

  if (state.status === 'generating') {
    return (
      <div className="summary" role="status" aria-live="polite">
        <span className="summary__label">Resumen · IA</span>
        <p className="summary__loading">
          <span className="spinner" aria-hidden="true" />
          Generando el resumen…
        </p>
        <p className="summary__note">
          La primera vez puede tardar un poco mientras el modelo se carga.
        </p>
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <div className="summary summary--error" role="status" aria-live="polite">
        <span className="summary__label">Resumen · IA</span>
        <p className="summary__text summary__text--error">{state.message}</p>
        <button type="button" className="summary__button" onClick={onGenerate}>
          Reintentar
        </button>
      </div>
    )
  }

  return (
    <section
      className="summary"
      aria-labelledby="summary-label"
      aria-live="polite"
    >
      <h3 className="summary__label" id="summary-label">
        Resumen · IA
      </h3>
      <p className="summary__text">{state.summary}</p>
      <p className="summary__note">
        Generado automáticamente. Puede contener errores.
      </p>
    </section>
  )
}
