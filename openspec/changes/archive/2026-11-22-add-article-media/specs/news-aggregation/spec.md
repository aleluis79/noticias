# Spec Delta

## ADDED Requirements

### Requirement: Extracted content SHALL expose media from the article body

The article body extraction SHALL return the images, native videos and recognized YouTube or Vimeo embeds that belong to the note, using the caption or alternative text published by the source when available.

#### Scenario: Figure with caption
- **WHEN** the body contains a figure with an image and a caption
- **THEN** the extracted content includes the image with that caption

#### Scenario: Image with alternative text
- **WHEN** the body contains an image with alternative text but no figure caption
- **THEN** the extracted content includes the image with that alternative text

#### Scenario: Open Graph fallback
- **WHEN** the body has no usable inline image but the page publishes an Open Graph image
- **THEN** the extracted content includes that image as the lead media

#### Scenario: Recognized video embed
- **WHEN** the body contains a YouTube or Vimeo embed
- **THEN** the extracted content includes a video item with its provider and original watch URL

### Requirement: Media extraction MUST NOT alter paragraph extraction

Adding media metadata MUST preserve the existing paragraph count, order, filtering and text so title, author, speech and summaries keep working unchanged.

#### Scenario: Paragraphs after media extraction
- **WHEN** the same article is extracted with media support
- **THEN** its paragraphs are identical to the previous extraction behavior

#### Scenario: Speech indexes
- **WHEN** the reader requests the audio of a body paragraph by index
- **THEN** that index still points to the same paragraph
