import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import './App.css';

// Shapes returned by GET /api/stories (api/Models/NewsStory.cs)
interface StorySource {
  sourceName: string;
  title: string;
  summary: string;
  originalUrl: string;
  publishedDate?: string | null;
}

interface Story {
  id: string;
  title: string;
  summary: string;
  originalUrl: string;
  sourceName: string;
  sourceCount: number;
  articleCount: number;
  latestPublishedDate?: string | null;
  sources: StorySource[];
}

interface StoriesResponse {
  generatedAt: string;
  articleCount: number;
  storyCount: number;
  stories: Story[];
}

// Shape returned by GET /api/news/content
interface ArticleContent {
  url: string;
  title: string;
  author?: string;
  paragraphs: string[];
}

type ContentState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; paragraphs: string[]; author: string | null };

// Empty in Docker builds so requests go through the nginx reverse proxy.
const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5251';

type ThemePreference = 'auto' | 'light' | 'dark';

const THEME_STORAGE_KEY = 'noticias-theme';
const NOTE_SIZE_STORAGE_KEY = 'noticias-reading-size';
const NOTE_SCALES = [1, 1.15, 1.3, 1.45] as const;

const COMPARE_PAGE = 12;
const BREVES = 6;
const PAGE_SIZE = 18;

function parseThemePreference(value: string | null): ThemePreference {
  return value === 'light' || value === 'dark' ? value : 'auto';
}

function readThemePreference(): ThemePreference {
  try {
    return parseThemePreference(
      window.localStorage.getItem(THEME_STORAGE_KEY),
    );
  } catch {
    return 'auto';
  }
}

const initialThemePreference = readThemePreference();

function parseNoteSizeIndex(value: string | null): number | null {
  if (value === null || value === '') {
    return null;
  }

  const index = Number(value);
  return Number.isInteger(index) && index >= 0 && index < NOTE_SCALES.length
    ? index
    : null;
}

function readNoteSizeIndex(): number {
  try {
    return (
      parseNoteSizeIndex(
        window.localStorage.getItem(NOTE_SIZE_STORAGE_KEY),
      ) ?? 0
    );
  } catch {
    return 0;
  }
}

const initialNoteSizeIndex = readNoteSizeIndex();

const fullDateFormatter = new Intl.DateTimeFormat('es-AR', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

const timeFormatter = new Intl.DateTimeFormat('es-AR', {
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

const todayLabel = capitalize(fullDateFormatter.format(new Date()));

const shortDateFormatter = new Intl.DateTimeFormat('es-AR', {
  day: 'numeric',
  month: 'short',
});

const shortDateLabel = shortDateFormatter
  .format(new Date())
  .replace('.', '')
  .toUpperCase();

function capitalize(text: string) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function normalizeText(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLocaleLowerCase('es');
}

/** The only time the front page shows: when the edition was fetched. */
function editionTime(raw?: string) {
  if (!raw) {
    return null;
  }

  const date = new Date(raw);
  return Number.isNaN(date.getTime()) ? null : `${timeFormatter.format(date)} hs`;
}

/**
 * Singletons arrive grouped by outlet because the API fetches source by
 * source. Round-robin keeps one outlet from filling the whole breves band.
 */
/** Outlets covering a story, in API order, one entry each. */
function outletsOf(story: Story): string[] {
  return Array.from(new Set(story.sources.map((source) => source.sourceName)));
}

function interleaveBySource(stories: Story[]): Story[] {
  const buckets = new Map<string, Story[]>();
  for (const story of stories) {
    const bucket = buckets.get(story.sourceName);
    if (bucket) {
      bucket.push(story);
    } else {
      buckets.set(story.sourceName, [story]);
    }
  }

  const interleaved: Story[] = [];
  let added = true;
  while (added) {
    added = false;
    for (const bucket of buckets.values()) {
      const next = bucket.shift();
      if (next) {
        interleaved.push(next);
        added = true;
      }
    }
  }

  return interleaved;
}

const GENERIC_AUTHORS = new Set([
  'redaccion',
  'staff',
  'newsroom',
  'agencia',
  'agencias',
  'editorial',
]);

/**
 * The byline is only useful when a person signs the note. Sources that publish
 * the outlet itself as the author (e.g. "LA NACION" or "0221") add no information.
 */
function visibleAuthor(author: string | null | undefined, sourceName: string) {
  if (!author) {
    return null;
  }

  const normalizedAuthor = normalizeText(author).trim();
  if (!normalizedAuthor || GENERIC_AUTHORS.has(normalizedAuthor)) {
    return null;
  }

  const normalizedSource = normalizeText(sourceName).trim();
  if (
    normalizedAuthor === normalizedSource ||
    normalizedAuthor.startsWith(`${normalizedSource} `)
  ) {
    return null;
  }

  return author;
}

function App() {
  const [edition, setEdition] = useState<StoriesResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshError, setRefreshError] = useState<string | null>(null);

  const [openStoryId, setOpenStoryId] = useState<string | null>(null);
  const [openSourceUrl, setOpenSourceUrl] = useState<string | null>(null);
  const [contents, setContents] = useState<Record<string, ContentState>>({});

  const [sourceFilter, setSourceFilter] = useState('all');
  const [titleFilter, setTitleFilter] = useState('');
  const [visibleExtra, setVisibleExtra] = useState(PAGE_SIZE);
  const [visibleCompare, setVisibleCompare] = useState(COMPARE_PAGE);

  const [themePreference, setThemePreference] =
    useState<ThemePreference>(initialThemePreference);
  const [noteSizeIndex, setNoteSizeIndex] =
    useState<number>(initialNoteSizeIndex);

  const requestedContents = useRef(new Set<string>());
  const mounted = useRef(true);
  const readerRef = useRef<HTMLElement | null>(null);
  const openerRef = useRef<HTMLButtonElement | null>(null);
  const openScrollRef = useRef(0);
  const moreCompareRef = useRef<HTMLButtonElement | null>(null);
  const moreStoriesRef = useRef<HTMLButtonElement | null>(null);
  const pendingScrollRef = useRef<number | null>(null);

  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();

    const fetchStories = async () => {
      try {
        const response = await fetch(`${API_BASE}/api/stories`, {
          signal: controller.signal,
        });
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data: StoriesResponse = await response.json();
        setEdition(data);
        setError(null);
      } catch (e) {
        if (controller.signal.aborted) {
          return;
        }
        console.error('Failed to fetch the edition:', e);
        setError(
          'No pudimos conectarnos con el servicio de noticias. Probá de nuevo en unos minutos.',
        );
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    };

    void fetchStories();

    return () => {
      mounted.current = false;
      controller.abort();
    };
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    const colorSchemeQuery = window.matchMedia('(prefers-color-scheme: dark)');

    const applyTheme = () => {
      const resolved =
        themePreference === 'auto'
          ? colorSchemeQuery.matches
            ? 'dark'
            : 'light'
          : themePreference;

      root.dataset.theme = resolved;
    };

    applyTheme();

    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, themePreference);
    } catch {
      // Storage can be unavailable (private mode); the theme still applies.
    }

    if (themePreference !== 'auto') {
      return;
    }

    colorSchemeQuery.addEventListener('change', applyTheme);
    return () => colorSchemeQuery.removeEventListener('change', applyTheme);
  }, [themePreference]);

  useEffect(() => {
    document.documentElement.style.setProperty(
      '--note-scale',
      String(NOTE_SCALES[noteSizeIndex]),
    );

    try {
      window.localStorage.setItem(
        NOTE_SIZE_STORAGE_KEY,
        String(noteSizeIndex),
      );
    } catch {
      // Storage can be unavailable (private mode); the size still applies.
    }
  }, [noteSizeIndex]);

  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key === THEME_STORAGE_KEY) {
        setThemePreference(parseThemePreference(event.newValue));
        return;
      }

      if (event.key === NOTE_SIZE_STORAGE_KEY) {
        const index = parseNoteSizeIndex(event.newValue);
        if (index !== null) {
          setNoteSizeIndex(index);
        }
      }
    };

    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  const loadEdition = async (refresh: boolean) => {
    if (refresh) {
      setRefreshing(true);
      setRefreshError(null);
    } else {
      setLoading(true);
      setError(null);
    }

    try {
      const response = await fetch(
        `${API_BASE}/api/stories${refresh ? '?refresh=true' : ''}`,
      );
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      const data: StoriesResponse = await response.json();
      if (!mounted.current) {
        return;
      }
      setEdition(data);
      setError(null);
    } catch (e) {
      console.error('Failed to refresh the edition:', e);
      if (!mounted.current) {
        return;
      }
      if (refresh) {
        setRefreshError(
          'No pudimos actualizar la edición. Seguimos mostrando la que ya teníamos.',
        );
      } else {
        setError(
          'No pudimos conectarnos con el servicio de noticias. Probá de nuevo en unos minutos.',
        );
      }
    } finally {
      if (mounted.current) {
        if (refresh) {
          setRefreshing(false);
        } else {
          setLoading(false);
        }
      }
    }
  };

  const loadContent = async (url: string) => {
    if (requestedContents.current.has(url)) {
      return;
    }

    requestedContents.current.add(url);
    setContents((previous) => ({ ...previous, [url]: { status: 'loading' } }));

    try {
      const response = await fetch(
        `${API_BASE}/api/news/content?url=${encodeURIComponent(url)}`,
      );
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data: ArticleContent = await response.json();
      const paragraphs = data.paragraphs ?? [];
      const author = data.author?.trim() || null;

      if (!mounted.current) {
        return;
      }

      if (paragraphs.length === 0) {
        requestedContents.current.delete(url);
        setContents((previous) => ({ ...previous, [url]: { status: 'error' } }));
        return;
      }

      setContents((previous) => ({
        ...previous,
        [url]: { status: 'ready', paragraphs, author },
      }));
    } catch (e) {
      console.error('Failed to load the article body:', e);
      requestedContents.current.delete(url);
      if (mounted.current) {
        setContents((previous) => ({ ...previous, [url]: { status: 'error' } }));
      }
    }
  };

  const stories = edition?.stories ?? [];

  const sources = Array.from(
    new Set(stories.flatMap((story) => story.sources.map((s) => s.sourceName))),
  ).sort((a, b) => a.localeCompare(b, 'es'));

  const normalizedTitleFilter = normalizeText(titleFilter.trim());
  const filteredStories = stories.filter((story) => {
    const matchesSource =
      sourceFilter === 'all' ||
      story.sources.some((source) => source.sourceName === sourceFilter);
    const matchesTitle =
      normalizedTitleFilter === '' ||
      normalizeText(story.title).includes(normalizedTitleFilter) ||
      story.sources.some((source) =>
        normalizeText(source.title).includes(normalizedTitleFilter),
      );

    return matchesSource && matchesTitle;
  });

  const clustered = filteredStories.filter((story) => story.sourceCount > 1);
  const singleSource = interleaveBySource(
    filteredStories.filter((story) => story.sourceCount === 1),
  );

  const matrixStories = clustered.slice(0, visibleCompare);
  const breves = singleSource.slice(0, BREVES);
  const extraStories = singleSource.slice(BREVES).slice(0, visibleExtra);

  const hasMoreCompare = clustered.length > matrixStories.length;
  const hasMore = singleSource.length - BREVES > visibleExtra;
  const hasFilters = sourceFilter !== 'all' || titleFilter.trim() !== '';
  const filteredArticleCount = filteredStories.reduce(
    (total, story) => total + story.articleCount,
    0,
  );
  const totalArticleCount = edition?.articleCount ?? 0;

  const clearFilters = () => {
    setSourceFilter('all');
    setTitleFilter('');
  };

  const openStory = (story: Story, opener: HTMLButtonElement) => {
    openerRef.current = opener;
    openScrollRef.current = window.scrollY;
    const first = story.sources[0];
    setOpenStoryId(story.id);
    setOpenSourceUrl(first.originalUrl);
    void loadContent(first.originalUrl);
    window.requestAnimationFrame(() => {
      readerRef.current?.scrollIntoView({ block: 'start' });
      readerRef.current?.focus({ preventScroll: true });
    });
  };

  const closeStory = () => {
    pendingScrollRef.current = openScrollRef.current;
    setOpenStoryId(null);
    setOpenSourceUrl(null);
  };

  // The reader is removed before the browser can clamp the restored offset,
  // so the scroll and the focus land after the DOM update, not before it.
  useLayoutEffect(() => {
    if (openStoryId !== null || pendingScrollRef.current === null) {
      return;
    }

    window.scrollTo({ top: pendingScrollRef.current });
    pendingScrollRef.current = null;
    openerRef.current?.focus({ preventScroll: true });
  }, [openStoryId]);

  useEffect(() => {
    if (!openStoryId) {
      return;
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') {
        return;
      }

      const target = event.target as HTMLElement | null;
      if (
        target?.closest('input, select, textarea, [contenteditable="true"]')
      ) {
        return;
      }

      closeStory();
    };

    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  });

  const selectSource = (url: string) => {
    setOpenSourceUrl(url);
    void loadContent(url);
  };

  const openStoryData = openStoryId
    ? stories.find((story) => story.id === openStoryId) ?? null
    : null;
  const openSource =
    openStoryData?.sources.find((source) => source.originalUrl === openSourceUrl) ??
    openStoryData?.sources[0] ??
    null;
  const openOutlets: [string, StorySource[]][] = [];
  if (openStoryData) {
    const byOutlet = new Map<string, StorySource[]>();
    for (const source of openStoryData.sources) {
      const versions = byOutlet.get(source.sourceName);
      if (versions) {
        versions.push(source);
      } else {
        byOutlet.set(source.sourceName, [source]);
      }
    }
    openOutlets.push(...byOutlet.entries());
  }

  const openSourceVersions = openStoryData && openSource
    ? openStoryData.sources.filter(
        (source) => source.sourceName === openSource.sourceName,
      )
    : [];

  const openContent = openSource ? contents[openSource.originalUrl] : undefined;
  const openAuthor =
    openContent?.status === 'ready'
      ? visibleAuthor(openContent.author, openSource?.sourceName ?? '')
      : null;

  const editionLabel = editionTime(edition?.generatedAt);

  return (
    <div className="app">
      <a className="skip-link" href="#edicion">
        Ir a las historias del día
      </a>

      <header className="masthead">
        <div className="masthead__top">
          <h1 className="masthead__title">Noticias Argentinas</h1>
          <p className="masthead__meta">
            <span className="masthead__date">{todayLabel}</span>
            <span className="masthead__date-short">{shortDateLabel}</span>
            {editionLabel && (
              <span className="masthead__updated">
                Actualizado {editionLabel}
              </span>
            )}
          </p>
        </div>
        <p className="masthead__subtitle">
          Los titulares de los medios del país, con la nota completa en el mismo
          lugar.
        </p>
        <div className="masthead__actions">
          <div className="theme-control">
            <label className="theme-control__label" htmlFor="theme-select">
              Tema
            </label>
            <select
              id="theme-select"
              name="theme"
              className="theme-control__select"
              value={themePreference}
              onChange={(event) =>
                setThemePreference(event.target.value as ThemePreference)
              }
            >
              <option value="auto">Automático</option>
              <option value="light">Claro</option>
              <option value="dark">Oscuro</option>
            </select>
          </div>
          <button
            type="button"
            className="refresh-button"
            onClick={() => void loadEdition(true)}
            disabled={refreshing || loading}
            data-loading={refreshing}
            aria-label="Actualizar las noticias"
            aria-busy={refreshing}
          >
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
              <path d="M21 12a9 9 0 1 1-3-6.7" />
              <path d="M21 3v6h-6" />
            </svg>
            {refreshing ? 'Actualizando…' : (
              <span className="refresh-button__label">Actualizar</span>
            )}
          </button>
        </div>
      </header>

      {refreshError && (
        <div className="notice" role="status">
          <p className="notice__text">{refreshError}</p>
          <button
            type="button"
            className="notice__close"
            onClick={() => setRefreshError(null)}
            aria-label="Cerrar el aviso"
          >
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              aria-hidden="true"
            >
              <path d="M18 6 6 18" />
              <path d="m6 6 12 12" />
            </svg>
          </button>
        </div>
      )}

      <main>
        {loading ? (
          <div className="state" role="status">
            <span className="spinner" aria-hidden="true" />
            <p className="state__text">Reuniendo las noticias del día…</p>
          </div>
        ) : error && stories.length === 0 ? (
          <div className="state">
            <h2 className="state__title">No pudimos cargar las noticias</h2>
            <p className="state__text">{error}</p>
            <button
              type="button"
              className="refresh-button state__button"
              onClick={() => void loadEdition(false)}
            >
              Reintentar
            </button>
          </div>
        ) : stories.length === 0 ? (
          <div className="state">
            <h2 className="state__title">Sin noticias por ahora</h2>
            <p className="state__text">
              No encontramos notas disponibles en este momento. Probá actualizar
              en unos minutos.
            </p>
          </div>
        ) : (
          <>
            {openStoryData && openSource && (
              <section
                className="reader"
                ref={(node) => {
                  readerRef.current = node;
                }}
                tabIndex={-1}
                aria-labelledby="reader-title"
              >
                <div className="reader__bar">
                  <button
                    type="button"
                    className="reader__back"
                    onClick={closeStory}
                  >
                    ← Volver a la portada
                  </button>
                  <span className="reader__count">
                    {openStoryData.sourceCount === 1
                      ? 'Una sola voz'
                      : `${openStoryData.sourceCount} medios`}
                  </span>
                </div>

                {openStoryData.sources.length > 1 && (
                  <div
                    className="reader__sources"
                    role="group"
                    aria-label="Elegir el medio para leer esta historia"
                  >
                    {openOutlets.map(([outlet, versions]) => (
                      <button
                        key={outlet}
                        type="button"
                        className="chip chip--action"
                        aria-pressed={
                          openSource.sourceName === outlet
                        }
                        onClick={() => selectSource(versions[0].originalUrl)}
                      >
                        {outlet}
                      </button>
                    ))}
                  </div>
                )}

                {openSourceVersions.length > 1 && (
                  <p className="reader__versions">
                    <span>Otras notas de {openSource.sourceName}:</span>
                    {openSourceVersions
                      .filter(
                        (version) =>
                          version.originalUrl !== openSource.originalUrl,
                      )
                      .map((version) => (
                        <button
                          key={version.originalUrl}
                          type="button"
                          className="reader__version"
                          onClick={() => selectSource(version.originalUrl)}
                        >
                          {version.title}
                        </button>
                      ))}
                  </p>
                )}

                <h2 id="reader-title" className="reader__title">
                  {openSource.title}
                </h2>

                <div className="reading-controls">
                  <span className="reading-controls__label">
                    Tamaño del texto
                  </span>
                  <div
                    className="reading-controls__group"
                    role="group"
                    aria-label="Tamaño del texto de la nota"
                  >
                    <button
                      type="button"
                      className="reading-controls__button"
                      onClick={() =>
                        setNoteSizeIndex((current) => Math.max(current - 1, 0))
                      }
                      disabled={noteSizeIndex === 0}
                      aria-label="Reducir el tamaño del texto"
                    >
                      A−
                    </button>
                    <span
                      className="reading-controls__value"
                      aria-live="polite"
                      aria-atomic="true"
                    >
                      {Math.round(NOTE_SCALES[noteSizeIndex] * 100)}%
                    </span>
                    <button
                      type="button"
                      className="reading-controls__button"
                      onClick={() =>
                        setNoteSizeIndex((current) =>
                          Math.min(current + 1, NOTE_SCALES.length - 1),
                        )
                      }
                      disabled={noteSizeIndex === NOTE_SCALES.length - 1}
                      aria-label="Aumentar el tamaño del texto"
                    >
                      A+
                    </button>
                  </div>
                </div>

                {(!openContent || openContent.status === 'loading') && (
                  <p className="reader__status" role="status">
                    <span className="spinner" aria-hidden="true" />
                    Cargando la nota completa…
                  </p>
                )}

                {openContent?.status === 'error' && (
                  <p className="reader__error" role="status">
                    No pudimos traer la nota completa de {openSource.sourceName}.
                    Podés leerla en el medio original.
                  </p>
                )}

                {openAuthor && (
                  <p className="reader__author">
                    <span className="reader__author-label">Por</span>{' '}
                    {openAuthor}
                  </p>
                )}

                {openContent?.status === 'ready' && (
                  <div className="reader__content">
                    {openContent.paragraphs.map((paragraph, index) => (
                      <p key={index}>{paragraph}</p>
                    ))}
                  </div>
                )}

                <a
                  href={openSource.originalUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="reader__link"
                >
                  Abrir en {openSource.sourceName}
                  <svg
                    width="14"
                    height="14"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden="true"
                  >
                    <path d="M14 3h7v7" />
                    <path d="M10 14 21 3" />
                    <path d="M21 14v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5" />
                  </svg>
                </a>
              </section>
            )}

            <search className="filters" aria-label="Filtrar historias">
              <div className="filters__field">
                <label className="filters__label" htmlFor="title-filter">
                  Buscar por título
                </label>
                <div className="filters__search">
                  <input
                    id="title-filter"
                    name="title"
                    type="search"
                    className="filters__input"
                    placeholder="Milei, inflación, Boca…"
                    value={titleFilter}
                    onChange={(event) => setTitleFilter(event.target.value)}
                    autoComplete="off"
                    spellCheck={false}
                    enterKeyHint="search"
                  />
                  {titleFilter !== '' && (
                    <button
                      type="button"
                      className="filters__search-clear"
                      onClick={() => setTitleFilter('')}
                      aria-label="Borrar la búsqueda"
                    >
                      <svg
                        width="14"
                        height="14"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        aria-hidden="true"
                      >
                        <path d="M18 6 6 18" />
                        <path d="m6 6 12 12" />
                      </svg>
                    </button>
                  )}
                </div>
              </div>

              <div className="filters__field">
                <label className="filters__label" htmlFor="source-filter">
                  Medio
                </label>
                <select
                  id="source-filter"
                  name="source"
                  className="filters__select"
                  value={sourceFilter}
                  onChange={(event) => setSourceFilter(event.target.value)}
                >
                  <option value="all">Todos los medios</option>
                  {sources.map((source) => (
                    <option key={source} value={source}>
                      {source}
                    </option>
                  ))}
                </select>
              </div>

              {hasFilters && (
                <div className="filters__footer">
                  <button
                    type="button"
                    className="filters__clear"
                    onClick={clearFilters}
                  >
                    Limpiar filtros
                  </button>
                </div>
              )}
            </search>

            <div id="edicion">
              {filteredStories.length === 0 ? (
                <div className="state">
                  <h2 className="state__title">Sin resultados</h2>
                  <p className="state__text">
                    No encontramos historias que coincidan con{' '}
                    {sourceFilter !== 'all' && titleFilter.trim() !== ''
                      ? 'el medio y el título elegidos'
                      : sourceFilter !== 'all'
                        ? 'el medio elegido'
                        : 'el título elegido'}
                    .
                  </p>
                  <button
                    type="button"
                    className="refresh-button state__button"
                    onClick={clearFilters}
                  >
                    Limpiar filtros
                  </button>
                </div>
              ) : (
                <>
                  {matrixStories.length > 0 && (
                    <section className="matrix" aria-labelledby="matrix-title">
                      <div className="section-head">
                        <h2 id="matrix-title" className="section-head__title">
                          Las historias del día
                        </h2>
                        <p className="section-head__note">
                          {hasFilters
                            ? `${filteredStories.length} de ${stories.length} historias`
                            : `${clustered.length} con más de un medio · ${totalArticleCount} notas`}
                        </p>
                      </div>

                      <div className="matrix__legend" aria-hidden="true">
                        <span className="matrix__legend-spacer" />
                        <div className="matrix__legend-items">
                          {sources.map((source) => (
                            <span key={source} className="matrix__legend-item">
                              {source}
                            </span>
                          ))}
                        </div>
                      </div>

                      <ol className="matrix__rows">
                        {matrixStories.map((story) => (
                          <li key={story.id} className="matrix__row">
                            <div className="matrix__story">
                              <h3 className="matrix__headline">
                                <button
                                  type="button"
                                  className="matrix__open"
                                  onClick={(event) =>
                                    openStory(story, event.currentTarget)
                                  }
                                >
                                  {story.title}
                                </button>
                              </h3>
                              <p className="matrix__coverage">
                                <span className="matrix__attribution">
                                  Según {story.sourceName}
                                </span>
                                <span aria-hidden="true"> · </span>
                                {story.sourceCount} medios
                                <span className="visually-hidden">
                                  : {outletsOf(story).join(', ')}
                                </span>
                              </p>
                            </div>
                            <div className="matrix__dots">
                              {sources.map((source) => {
                                const covers = story.sources.some(
                                  (item) => item.sourceName === source,
                                );
                                return (
                                  <span
                                    key={source}
                                    className={
                                      covers ? 'dot dot--on' : 'dot'
                                    }
                                    title={
                                      covers
                                        ? `${source}: cubre esta historia`
                                        : `${source}: no la cubre`
                                    }
                                  />
                                );
                              })}
                            </div>
                            <p className="matrix__outlets">
                              {(() => {
                                const outlets = outletsOf(story);
                                const shown = outlets.slice(0, 4);
                                const rest = outlets.length - shown.length;
                                return (
                                  <>
                                    {shown.join(' · ')}
                                    {rest > 0 ? ` · +${rest}` : ''}
                                  </>
                                );
                              })()}
                            </p>
                          </li>
                        ))}
                      </ol>

                      {hasMoreCompare && (
                        <button
                          type="button"
                          className="more__button"
                          ref={moreCompareRef}
                          onClick={() => {
                            setVisibleCompare(
                              (current) => current + COMPARE_PAGE,
                            );
                            window.requestAnimationFrame(() =>
                              moreCompareRef.current?.scrollIntoView({
                                block: 'nearest',
                              }),
                            );
                          }}
                        >
                          Ver más historias comparadas
                        </button>
                      )}
                    </section>
                  )}

                  {breves.length > 0 && (
                    <section className="breves" aria-labelledby="breves-title">
                      <div className="section-head">
                        <h2 id="breves-title" className="section-head__title">
                          Breves · una sola voz
                        </h2>
                        <p className="section-head__note">
                          {hasFilters
                            ? `${filteredArticleCount} de ${totalArticleCount} notas`
                            : 'Notas que solo trae un medio'}
                        </p>
                      </div>
                      <ul className="breves__grid">
                        {breves.map((story) => (
                          <li key={story.id} className="brief">
                            <p className="brief__source">
                              {story.sourceName}
                            </p>
                            <h3 className="brief__title">
                              <button
                                type="button"
                                className="brief__open"
                                onClick={(event) =>
                                  openStory(story, event.currentTarget)
                                }
                              >
                                {story.title}
                              </button>
                            </h3>
                          </li>
                        ))}
                      </ul>
                    </section>
                  )}

                  {extraStories.length > 0 && (
                    <section className="more" aria-labelledby="more-title">
                      <div className="section-head">
                        <h2 id="more-title" className="section-head__title">
                          El resto de la edición
                        </h2>
                        <p className="section-head__note">
                          {filteredStories.length} historias en total
                        </p>
                      </div>
                      <ul className="more__list">
                        {extraStories.map((story) => (
                          <li key={story.id} className="more__item">
                            <div className="more__meta">
                              <span className="more__source">
                                {story.sourceName}
                              </span>
                              {story.sourceCount > 1 && (
                                <span className="more__count">
                                  {story.sourceCount} medios
                                </span>
                              )}
                            </div>
                            <h3 className="more__title">
                              <button
                                type="button"
                                className="more__open"
                                onClick={(event) =>
                                  openStory(story, event.currentTarget)
                                }
                              >
                                {story.title}
                              </button>
                            </h3>
                          </li>
                        ))}
                      </ul>
                      {hasMore && (
                        <button
                          type="button"
                          className="more__button"
                          ref={moreStoriesRef}
                          onClick={() => {
                            setVisibleExtra((current) => current + PAGE_SIZE);
                            window.requestAnimationFrame(() =>
                              moreStoriesRef.current?.scrollIntoView({
                                block: 'nearest',
                              }),
                            );
                          }}
                        >
                          Ver más historias
                        </button>
                      )}
                    </section>
                  )}
                </>
              )}
            </div>
          </>
        )}
      </main>

      {sources.length > 0 && (
        <footer className="site-footer">
          Fuentes: {sources.join(' · ')}
        </footer>
      )}
    </div>
  );
}

export default App;
