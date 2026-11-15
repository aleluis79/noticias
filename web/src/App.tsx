import { useEffect, useRef, useState } from 'react';
import type { SyntheticEvent } from 'react';
import './App.css';

// Shape returned by api/Models/NewsArticle.cs
interface NewsArticle {
  title: string;
  summary: string;
  originalUrl: string;
  sourceName: string;
  publishedDate?: string;
}

// Shape returned by GET /api/news/content
interface ArticleContent {
  url: string;
  title: string;
  paragraphs: string[];
}

type ContentState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; paragraphs: string[] };

const API_BASE = 'http://localhost:5251';

type ThemePreference = 'auto' | 'light' | 'dark';

const THEME_STORAGE_KEY = 'noticias-theme';
const NOTE_SIZE_STORAGE_KEY = 'noticias-reading-size';
const NOTE_SCALES = [1, 1.15, 1.3, 1.45] as const;

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

const shortDateFormatter = new Intl.DateTimeFormat('es-AR', {
  day: 'numeric',
  month: 'short',
});

const todayLabel = capitalize(
  fullDateFormatter.format(new Date()),
);

function capitalize(text: string) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function normalizeText(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLocaleLowerCase('es');
}

function formatArticleDate(raw?: string) {
  if (!raw) {
    return null;
  }

  const date = new Date(raw);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  const now = new Date();
  if (date.toDateString() === now.toDateString()) {
    return `${timeFormatter.format(date)} hs`;
  }

  return shortDateFormatter.format(date);
}

function App() {
  const [news, setNews] = useState<NewsArticle[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [contents, setContents] = useState<Record<string, ContentState>>({});
  const [sourceFilter, setSourceFilter] = useState('all');
  const [titleFilter, setTitleFilter] = useState('');
  const [themePreference, setThemePreference] =
    useState<ThemePreference>(initialThemePreference);
  const [noteSizeIndex, setNoteSizeIndex] =
    useState<number>(initialNoteSizeIndex);

  const requestedContents = useRef(new Set<string>());
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();

    const fetchNews = async () => {
      try {
        const response = await fetch(`${API_BASE}/api/news`, {
          signal: controller.signal,
        });
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data: NewsArticle[] = await response.json();
        setNews(data);
        setError(null);
      } catch (e) {
        if (controller.signal.aborted) {
          return;
        }
        console.error('Failed to fetch news:', e);
        setError(
          'No pudimos conectarnos con el servicio de noticias. Verificá que la API .NET esté corriendo en ' +
            API_BASE +
            '.',
        );
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    };

    void fetchNews();

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

  const refreshNews = async () => {
    setRefreshing(true);
    try {
      const response = await fetch(`${API_BASE}/api/news?refresh=true`);
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      const data: NewsArticle[] = await response.json();
      if (!mounted.current) {
        return;
      }
      setNews(data);
      setError(null);
    } catch (e) {
      console.error('Failed to refresh news:', e);
    } finally {
      if (mounted.current) {
        setRefreshing(false);
      }
    }
  };

  const retryInitialLoad = () => {
    setLoading(true);
    setError(null);
    window.location.reload();
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
        [url]: { status: 'ready', paragraphs },
      }));
    } catch (e) {
      console.error('Failed to load the article body:', e);
      requestedContents.current.delete(url);
      if (mounted.current) {
        setContents((previous) => ({ ...previous, [url]: { status: 'error' } }));
      }
    }
  };

  const handleToggle = (
    event: SyntheticEvent<HTMLDetailsElement>,
    url: string,
  ) => {
    if (event.currentTarget.open) {
      void loadContent(url);
    }
  };

  const sources = Array.from(new Set(news.map((article) => article.sourceName))).sort(
    (a, b) => a.localeCompare(b, 'es'),
  );

  const normalizedTitleFilter = normalizeText(titleFilter.trim());
  const filteredNews = news.filter((article) => {
    const matchesSource =
      sourceFilter === 'all' || article.sourceName === sourceFilter;
    const matchesTitle =
      normalizedTitleFilter === '' ||
      normalizeText(article.title).includes(normalizedTitleFilter);

    return matchesSource && matchesTitle;
  });

  const hasFilters = sourceFilter !== 'all' || titleFilter.trim() !== '';

  const clearFilters = () => {
    setSourceFilter('all');
    setTitleFilter('');
  };

  return (
    <div className="app">
      <header className="masthead">
        <p className="masthead__eyebrow">Agregador · Edición del día</p>
        <h1 className="masthead__title">Noticias Argentinas</h1>
        <p className="masthead__subtitle">
          Las principales notas de los diarios del país, reunidas en una sola
          lectura.
        </p>
        <div className="masthead__meta">
          <span className="masthead__date">{todayLabel}</span>
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
              onClick={() => void refreshNews()}
              disabled={refreshing || loading}
              data-loading={refreshing}
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
              {refreshing ? 'Actualizando…' : 'Actualizar'}
            </button>
          </div>
        </div>
      </header>

      <main>
        {loading ? (
          <div className="state" role="status">
            <span className="spinner" aria-hidden="true" />
            <p className="state__text">Reuniendo las noticias del día…</p>
          </div>
        ) : error && news.length === 0 ? (
          <div className="state">
            <h2 className="state__title">No pudimos cargar las noticias</h2>
            <p className="state__text">{error}</p>
            <button
              type="button"
              className="refresh-button state__button"
              onClick={retryInitialLoad}
            >
              Reintentar
            </button>
          </div>
        ) : news.length === 0 ? (
          <div className="state">
            <h2 className="state__title">Sin noticias por ahora</h2>
            <p className="state__text">
              No encontramos notas disponibles en este momento. Probá actualizar
              en unos minutos.
            </p>
          </div>
        ) : (
          <>
            <search className="filters" aria-label="Filtrar noticias">
              <div className="filters__field">
                <label className="filters__label" htmlFor="title-filter">
                  Buscar por título
                </label>
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

              <div className="filters__footer">
                <p className="filters__count" role="status">
                  {filteredNews.length === news.length
                    ? `${news.length} notas`
                    : `${filteredNews.length} de ${news.length} notas`}
                </p>
                {hasFilters && (
                  <button
                    type="button"
                    className="filters__clear"
                    onClick={clearFilters}
                  >
                    Limpiar filtros
                  </button>
                )}
              </div>
            </search>

            {filteredNews.length === 0 ? (
              <div className="state">
                <h2 className="state__title">Sin resultados</h2>
                <p className="state__text">
                  No encontramos notas que coincidan con el medio y el título
                  elegidos.
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
              <div className="news-grid">
                {filteredNews.map((article) => {
              const content = contents[article.originalUrl];
              const published = formatArticleDate(article.publishedDate);

              return (
                <details
                  key={article.originalUrl}
                  className="news-card"
                  onToggle={(event) =>
                    handleToggle(event, article.originalUrl)
                  }
                >
                  <summary className="news-card__summary">
                    <span className="news-card__meta">
                      <span className="news-card__source">
                        {article.sourceName}
                      </span>
                      {published && (
                        <time
                          className="news-card__time"
                          dateTime={article.publishedDate}
                        >
                          {published}
                        </time>
                      )}
                    </span>
                    <h2 className="news-card__title">{article.title}</h2>
                    {article.summary && (
                      <p className="news-card__excerpt">{article.summary}</p>
                    )}
                    <span className="news-card__toggle">
                      <span className="news-card__toggle-text--closed">
                        Leer la nota completa
                      </span>
                      <span className="news-card__toggle-text--open">
                        Cerrar nota
                      </span>
                      <svg
                        className="news-card__chevron"
                        width="18"
                        height="18"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        aria-hidden="true"
                      >
                        <path d="m6 9 6 6 6-6" />
                      </svg>
                    </span>
                  </summary>

                  <div className="news-card__body">
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
                            setNoteSizeIndex((current) =>
                              Math.max(current - 1, 0),
                            )
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
                          disabled={
                            noteSizeIndex === NOTE_SCALES.length - 1
                          }
                          aria-label="Aumentar el tamaño del texto"
                        >
                          A+
                        </button>
                      </div>
                    </div>

                    {(!content || content.status === 'loading') && (
                      <p className="news-card__status" role="status">
                        <span className="spinner" aria-hidden="true" />
                        Cargando la nota completa…
                      </p>
                    )}

                    {content?.status === 'error' && (
                      <p className="news-card__status news-card__status--error">
                        No pudimos extraer la nota completa. Podés leerla
                        directamente en {article.sourceName}.
                      </p>
                    )}

                    {content?.status === 'ready' && (
                      <div className="news-card__content">
                        {content.paragraphs.map((paragraph, index) => (
                          <p key={index}>{paragraph}</p>
                        ))}
                      </div>
                    )}

                    <a
                      href={article.originalUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="news-card__link"
                    >
                      Abrir en {article.sourceName}
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
                  </div>
                </details>
              );
                })}
              </div>
            )}
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
