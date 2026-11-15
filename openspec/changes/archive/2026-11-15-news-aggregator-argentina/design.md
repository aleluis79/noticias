# Design

## Context

The project is initialized with an empty .NET 11 API and a React web application template. The design must integrate a news aggregation service to pull data from specific Argentine media outlets. See proposal.md for the motivation and scope.

## Goals / Non-Goals

**Goals:**
- Implement a robust backend API (.NET 11) capable of fetching, normalizing, and serving news data from the defined Argentine sources.
- Develop a React frontend to consume the unified API and display aggregated news articles with original source links.
- Adhere strictly to SDD OpenSpec and TDD principles throughout implementation.

**Non-Goals:**
- Implementing the full, production-grade scraping logic for all sites in this planning phase. Focus is on the architecture and contracts (specs).
- Handling authentication/user accounts (unless required by a specific source).

## Decisions

**Backend API:** Use .NET 11 as provided by the template. This leverages existing project setup and targets modern C# features.
**Frontend:** Use React as provided by the template. This allows for a modern, component-based UI capable of displaying dynamic, aggregated content.
**Data Acquisition:** Initial approach will be HTTP requests combined with targeted, resilient parsing logic (e.g., HTML parsing libraries) tailored to each site, as dedicated APIs for all sources are unlikely to exist. This requires careful TDD to validate data integrity from various site formats.

## Risks / Trade-offs

[Scraping Fragility] → Target websites frequently change layouts, requiring maintenance. Mitigation: Design parsers as isolated, testable modules, ensuring changes are localized and easily updated.
[Rate Limiting] → Aggressive scraping may lead to IP blocking. Mitigation: Implement staggered fetching, respect `robots.txt` (where applicable), and use robust retry logic with exponential backoff.

## Migration Plan

1. Implement the backend API endpoints (via TDD).
2. Implement the news fetching and normalization service (via TDD).
3. Implement the React frontend components to consume the unified API (via TDD).
4. Deploy the integrated system following standard CI/CD pipelines.

## Open Questions

None that materially affect the specs or the core approach. The specific implementation details of the parsers will be refined during the TDD implementation phase.