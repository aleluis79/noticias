import { useState } from 'react'
import type { ArticleMedia } from './mediaModel'

const PROVIDER_LABELS: Record<string, string> = {
  youtube: 'YouTube',
  vimeo: 'Vimeo',
}

export function ArticleMediaBlock({ media }: { media: ArticleMedia }) {
  if (media.type === 'image') {
    return <ArticleImage media={media} />
  }

  if (media.type === 'video') {
    return <ArticleNativeVideo media={media} />
  }

  return <ArticleVideoCard media={media} />
}

function ArticleImage({ media }: { media: ArticleMedia }) {
  const [failed, setFailed] = useState(false)
  const caption = media.caption?.trim() || null
  const alt = media.alt?.trim() || caption || ''

  if (failed) {
    return caption ? (
      <p className="reader__media-fallback">{caption}</p>
    ) : null
  }

  return (
    <figure className="reader__media">
      <img
        src={media.url}
        alt={alt}
        width={media.width ?? undefined}
        height={media.height ?? undefined}
        loading="lazy"
        decoding="async"
        referrerPolicy="no-referrer"
        onError={() => setFailed(true)}
      />
      {caption && <figcaption>{caption}</figcaption>}
    </figure>
  )
}

function ArticleNativeVideo({ media }: { media: ArticleMedia }) {
  const caption = media.caption?.trim() || null

  return (
    <figure className="reader__media">
      <video
        controls
        preload="none"
        playsInline
        poster={media.posterUrl ?? undefined}
        aria-label={caption ?? 'Video de la nota'}
      >
        <source src={media.url} />
      </video>
      {caption && <figcaption>{caption}</figcaption>}
    </figure>
  )
}

function ArticleVideoCard({ media }: { media: ArticleMedia }) {
  const caption = media.caption?.trim() || null
  const provider = media.provider
    ? PROVIDER_LABELS[media.provider] ?? 'el proveedor'
    : 'el proveedor'

  return (
    <div className="reader__media-card">
      <a
        href={media.url}
        target="_blank"
        rel="noopener noreferrer"
        className="reader__media-link"
      >
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="currentColor"
          aria-hidden="true"
        >
          <path d="M8 5v14l11-7z" />
        </svg>
        Ver video en {provider}
      </a>
      {caption && (
        <span className="reader__media-card-caption">{caption}</span>
      )}
    </div>
  )
}
