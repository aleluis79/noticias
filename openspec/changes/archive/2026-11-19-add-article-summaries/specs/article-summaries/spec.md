# Spec Delta

## Purpose

Genera y conserva un resumen breve de cada nota a pedido, usando un modelo de lenguaje local, para que el lector pueda entender la nota antes de leerla completa.

## ADDED Requirements

### Requirement: Reader SHALL offer an on-demand summary

When an article is open and no summary is stored, the reader SHALL offer a control to generate it. When a summary is already stored, the reader SHALL show it directly without offering generation.

#### Scenario: No stored summary
- **WHEN** the user opens an article that has no summary
- **THEN** the reader shows a "Generar resumen" control

#### Scenario: Stored summary
- **WHEN** the user opens an article whose summary was already generated
- **THEN** the reader shows the summary and does not offer to generate it again

### Requirement: API SHALL generate and store summaries

The API SHALL expose an endpoint to generate the summary of a supported article from its extracted body, and MUST store it so later requests return the stored text without running the model again.

#### Scenario: First generation
- **WHEN** a client requests the summary of a supported article that has no stored summary
- **THEN** the API generates a short Spanish summary and returns it

#### Scenario: Repeated request
- **WHEN** the summary of the same article is requested again, by GET or POST
- **THEN** the API returns the stored summary without calling the model

#### Scenario: Restart
- **WHEN** the API restarts and the summary is requested again
- **THEN** the stored summary is still available

### Requirement: Summaries MUST be stored per article and never regenerated

Stored summaries MUST be keyed by article URL and MUST remain valid regardless of model configuration changes.

#### Scenario: Model change
- **WHEN** the configured model changes after a summary was stored
- **THEN** the stored summary is returned instead of generating a new one

### Requirement: Summary endpoint MUST reject invalid input and degrade clearly

The endpoint MUST accept only supported article URLs and MUST return 404 when the article body cannot be extracted, 503 when the local model is disabled or unavailable, and 502 when generation fails.

#### Scenario: Unsupported URL
- **WHEN** the URL does not belong to a configured outlet
- **THEN** the API returns 400 and does not call the model

#### Scenario: No article body
- **WHEN** the article body cannot be extracted
- **THEN** the API returns 404 and does not call the model

#### Scenario: Model unavailable
- **WHEN** the local model is disabled, missing or unreachable
- **THEN** the API returns 503 and stored summaries remain readable

#### Scenario: Generation failure
- **WHEN** the model returns an empty or unusable response
- **THEN** the API returns 502 and stores nothing so the user can retry

### Requirement: Concurrent requests MUST share one generation

Two simultaneous generation requests for the same article MUST trigger the model only once and return the same summary.

#### Scenario: Concurrent generation
- **WHEN** two clients request the summary of the same uncached article at the same time
- **THEN** both receive the same summary and the model runs once

### Requirement: Reader SHALL announce summary state

The reader MUST show loading, generated, and error states with accessible text, and MUST let the user retry after a failure.

#### Scenario: Generation in progress
- **WHEN** the summary is being generated
- **THEN** the reader shows a loading message and keeps the article readable

#### Scenario: Generation error
- **WHEN** generation fails
- **THEN** the reader shows an explanatory message and a retry control
