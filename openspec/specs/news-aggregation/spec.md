# news-aggregation Specification

## Purpose
This capability provides a unified, normalized, and centralized view of news content retrieved from a predefined list of Argentine media outlets (Clarín, La Nación, Infobae, Página/12, TN, Ámbito Financiero, El Cronista, Perfil, C5N, La Voz del Interior, El Día, 0221 y Seúl).

## Requirements

### Requirement: News articles SHALL be retrieved from specified Argentine sources
The system SHALL retrieve and normalize news articles from a predefined list of Argentine sources, reading each outlet's home page and the section pages declared for that outlet.

#### Scenario: Successful retrieval and normalization
- **WHEN** a request is made for news from a supported source
- **THEN** the system returns a list of normalized articles, each containing title, summary, original URL, and source name.

#### Scenario: Section pages are part of the retrieval
- **WHEN** a supported source declares one or more section pages
- **THEN** the system retrieves those section pages in addition to the outlet's home page and normalizes their articles with the same fields.

### Requirement: Original source link MUST be provided
Every retrieved news article MUST include a direct, functional link to its original source website.

#### Scenario: Original link is present
- **WHEN** an article is successfully retrieved
- **THEN** the article object contains an `originalUrl` field pointing to the source site.

### Requirement: Each source SHALL contribute up to 50 articles
The system SHALL return no more than 50 articles per source after merging its home page and section pages.

#### Scenario: Source with several section pages
- **WHEN** a source's home page and declared sections together expose more than 50 usable articles
- **THEN** the response includes at most 50 articles for that source.

#### Scenario: Source with a single page
- **WHEN** a source has no declared sections
- **THEN** its home page can contribute up to the same per-source limit.

### Requirement: Home page articles MUST keep priority over section articles
When a source contributes both home page and section articles, the home page articles MUST appear before the section articles in the merged list.

#### Scenario: Merge order preserves the home page
- **WHEN** the same source returns articles from its home page and from a section page
- **THEN** every home page article appears before the section articles in that source's output.

### Requirement: Repeated articles MUST appear only once per source
The system MUST deduplicate articles retrieved from the home page and sections of the same source, treating URLs that differ only in query string, fragment, or trailing slash as the same article.

#### Scenario: Headline repeated on the home and a section
- **WHEN** a note is linked from both the home page and a section page of the same source
- **THEN** the source contributes that note only once.

### Requirement: A failing section MUST NOT discard the rest of the source
If a section page cannot be fetched or parsed, the system MUST still return the articles obtained from the home page and from the other sections of that source.

#### Scenario: Section request fails
- **WHEN** one declared section page of a source is unavailable or returns unparseable HTML
- **THEN** the response still includes the articles from that source's home page and successful sections, and the failure is logged.

### Requirement: The public response shape MUST remain unchanged
The `/api/news` and `/api/stories` responses MUST keep their current fields and types while returning the expanded coverage.

#### Scenario: Client reads an expanded edition
- **WHEN** the expanded coverage is returned by `/api/news` or `/api/stories`
- **THEN** each article keeps the same fields as before and the counts reflect the additional articles.
