# Spec Delta

## ADDED Requirements

### Requirement: Extracted content SHALL expose the article title

The article body extraction SHALL include the page title when the source publishes one, preferring structured metadata (`og:title`, `twitter:title`) over the visible heading and the document title.

#### Scenario: Structured title available
- **WHEN** an article page publishes an Open Graph or Twitter title
- **THEN** the extracted content includes that title

#### Scenario: No published title
- **WHEN** the page does not publish a usable title
- **THEN** the extracted content keeps working with an empty title
