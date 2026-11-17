# Spec Delta

## Purpose

Genera audio de voz en el propio servidor a partir de las notas extraídas, sin depender de servicios externos ni de las voces instaladas en el dispositivo.

## ADDED Requirements

### Requirement: API SHALL synthesize article segments locally

The API SHALL expose an endpoint that returns audio for a title, an attribution or a body paragraph of a supported article, synthesized with a local text-to-speech engine and without calling external speech services.

#### Scenario: Body segment
- **WHEN** a client requests the audio of a valid body paragraph of a supported article
- **THEN** the API returns an audio response with the spoken paragraph

#### Scenario: Title and attribution
- **WHEN** a client requests the title or attribution segment
- **THEN** the API returns audio for that text, including the outlet attribution

### Requirement: Synthesized audio MUST be cached

Generated audio MUST be stored and reused by URL, segment and voice model, and identical concurrent requests MUST share one generation.

#### Scenario: Repeated request
- **WHEN** the same segment is requested twice
- **THEN** the second response is served from the cache without running the speech engine again

#### Scenario: Concurrent requests
- **WHEN** two clients request the same uncached segment at the same time
- **THEN** both receive the same audio and the engine runs once

### Requirement: Audio endpoint MUST reject invalid input

The endpoint MUST accept only supported article URLs, known segment names and body indexes within the extracted article, and MUST return 503 when local speech is disabled or unavailable.

#### Scenario: Unsupported URL
- **WHEN** the URL does not belong to a configured outlet
- **THEN** the API returns 400 and does not run the engine

#### Scenario: Invalid body index
- **WHEN** the body index is outside the extracted paragraphs
- **THEN** the API returns 400 and does not run the engine

#### Scenario: TTS unavailable
- **WHEN** local speech is disabled or the engine fails
- **THEN** the API returns 503 so the client can fall back

### Requirement: Audio responses MUST be playable and cacheable

Audio responses MUST use an audio content type and permit HTTP range requests and client caching.

#### Scenario: Playable response
- **WHEN** a segment is returned successfully
- **THEN** the response has an `audio/*` content type, supports ranges and carries a cache header
