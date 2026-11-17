# edition-history Specification

## Purpose
Conserva las ediciones diarias de la agregación durante una ventana corta y permite consultarlas por fecha desde la API y la portada, para que las notas importantes de los últimos días sigan disponibles aunque los medios ya no las muestren.

## Requirements

### Requirement: A successful daily edition MUST be persisted
After a fresh aggregation returns at least one article, the system MUST save that edition to durable local storage, dated in Argentina time.

#### Scenario: First fetch of the day
- **WHEN** a fresh aggregation returns articles for the first time on a given Argentina date
- **THEN** the system stores an edition for that date with the retrieved article metadata.

#### Scenario: Later refresh on the same day
- **WHEN** a fresh aggregation succeeds again on the same Argentina date
- **THEN** the stored edition for that date is replaced with the newer articles.

#### Scenario: Aggregation fails completely
- **WHEN** no source returns articles and the API serves a previous in-memory snapshot
- **THEN** the stored edition for the day is not replaced by an empty edition.

### Requirement: The system MUST retain today and the two previous days
The stored history MUST include the current edition plus at most the two previous dates, and older editions MUST be removed.

#### Scenario: A fourth day is saved
- **WHEN** an edition is saved on a new date and three older editions already exist
- **THEN** only the current date and the two immediately previous dates remain stored.

#### Scenario: The app did not run for several days
- **WHEN** the service starts after several days without running
- **THEN** only the days that were actually persisted are offered, without inventing or backfilling missing dates.

### Requirement: Editions MUST roll over at midnight in Argentina time
The edition date MUST be calculated in the `America/Argentina/Buenos_Aires` time zone, and a cached edition from a previous date MUST NOT be served as today's edition.

#### Scenario: Request after midnight
- **WHEN** the Argentina date changes while an edition from the previous date is still cached
- **THEN** the next request fetches and serves a new edition for the new date.

### Requirement: The current edition response MUST list the available previous days
`GET /api/stories` MUST keep returning today's edition and MUST add its `date` plus a `previousDays` list of stored summaries, newest first.

#### Scenario: Previous days are available
- **WHEN** the API responds with today's edition and stored editions exist for yesterday and the day before
- **THEN** `previousDays` contains both dates with their article and story counts.

#### Scenario: No previous days are available
- **WHEN** there are no stored editions before today
- **THEN** `previousDays` is empty and the current edition response remains valid.

### Requirement: A previous edition MUST be retrievable by date
`GET /api/stories?date=YYYY-MM-DD` MUST return the stored edition for that date without fetching the sources again.

#### Scenario: Stored date requested
- **WHEN** the client requests a date that exists in the stored history
- **THEN** the response contains that date, its stories, and its article and story counts.

#### Scenario: Unknown date requested
- **WHEN** the client requests a valid past date that is not stored
- **THEN** the API responds with 404.

#### Scenario: Invalid or future date requested
- **WHEN** the client requests a malformed date, a future date, or a date outside the retention window
- **THEN** the API responds with 400.

### Requirement: The front end MUST present previous days in a separate section
When previous days are available, the front end SHALL show a distinct "Días anteriores" section with one selector per available date.

#### Scenario: Reader selects a previous day
- **WHEN** the reader selects an available previous date
- **THEN** that day's stories load in the section and can be opened in the existing article reader.

#### Scenario: Medium or title filter applied
- **WHEN** the reader filters by medium or title
- **THEN** the previous day's stories are filtered with the same criteria as today's stories.

#### Scenario: No previous days
- **WHEN** the API returns an empty previous-day list
- **THEN** the section is not rendered.

#### Scenario: Previous-day request fails
- **WHEN** loading a previous day fails
- **THEN** the section shows a retry message and today's edition remains usable.

### Requirement: Stored history MUST contain only edition metadata
The stored editions MUST contain the same article metadata returned by the API and MUST NOT include article bodies or reading-history data.

#### Scenario: Inspecting a stored edition
- **WHEN** a stored edition is read back
- **THEN** it contains title, summary, original URL, source name, and optional publication date, but no article body.
