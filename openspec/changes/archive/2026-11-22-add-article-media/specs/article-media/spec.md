# Spec Delta

## Purpose

Permite que el lector vea las imágenes y los videos que acompañan una nota, de forma opcional y bajo demanda, sin convertir la lectura en una galería ni cargar medios que el usuario no pidió.

## ADDED Requirements

### Requirement: Extracted content SHALL expose optional media

The article content response SHALL include an optional list of media items found in the article body, without changing the existing paragraphs, title, author or registration-wall fields.

#### Scenario: Article with inline media
- **WHEN** the extracted article contains one or more images or videos in its body
- **THEN** the response includes a media list describing each one

#### Scenario: Article without media
- **WHEN** the article body has no usable images or videos
- **THEN** the response includes an empty media list and the article remains readable

#### Scenario: Existing clients
- **WHEN** a client ignores the media list
- **THEN** it still receives the same paragraphs and the article renders as before

### Requirement: Media SHALL include a position relative to paragraphs

Each media item SHALL indicate how many extracted paragraphs precede it, so the reader can interleave media without altering paragraph indexes.

#### Scenario: Image after the second paragraph
- **WHEN** an image appears in the body after the second extracted paragraph
- **THEN** its position is two and the reader shows it after that paragraph

#### Scenario: Image before the first paragraph
- **WHEN** an image appears before any extracted paragraph
- **THEN** its position is zero and the reader shows it before the body text

### Requirement: Media extraction MUST exclude unsafe or irrelevant resources

The API MUST include only absolute HTTP or HTTPS URLs and MUST discard tracking pixels, logos, placeholders, advertising and media with obviously tiny dimensions.

#### Scenario: Tracking pixel inside the body
- **WHEN** the body contains a one-by-one tracking image
- **THEN** that image is not included in the media list

#### Scenario: Relative image source
- **WHEN** an image uses a relative URL
- **THEN** the API resolves it against the article URL before returning it

#### Scenario: Unsupported scheme
- **WHEN** a media source uses a scheme other than HTTP or HTTPS
- **THEN** it is not included in the media list

### Requirement: Reader SHALL keep media hidden until requested

The reader SHALL NOT render or download any image or video until the user explicitly enables media for the article, and SHALL offer a control only when the article has media.

#### Scenario: First visit with media available
- **WHEN** the user opens an article that has media and the preference is off
- **THEN** the reader shows a control to view the media and renders no image or video

#### Scenario: Article without media
- **WHEN** the article has no media
- **THEN** the reader does not show the media control

#### Scenario: User enables media
- **WHEN** the user activates the media control
- **THEN** the images and video cards appear inline in the article

### Requirement: Reader SHALL persist the media preference

The reader SHALL remember the user's media preference on the device, defaulting to off when no preference was stored.

#### Scenario: No stored preference
- **WHEN** the user opens the reader for the first time
- **THEN** media stays hidden

#### Scenario: Stored preference enabled
- **WHEN** the user previously enabled media and opens another article on the same device
- **THEN** that article shows its media without asking again

### Requirement: Images SHALL be presented with captions and accessible text

Displayed images SHALL use figure semantics, preserve the caption or alternative text published by the outlet, reserve their aspect ratio when dimensions are known, and load lazily.

#### Scenario: Image with caption
- **WHEN** an image includes a caption
- **THEN** the reader shows the image and the caption below it

#### Scenario: Image without caption
- **WHEN** an image has no caption but has alternative text
- **THEN** the reader uses the alternative text as the accessible name

#### Scenario: Broken image
- **WHEN** an image cannot be loaded
- **THEN** the reader hides the broken image and keeps the article readable

### Requirement: Video playback MUST avoid automatic third-party embeds

Native video files SHALL play inside the reader with controls and without autoplay; YouTube and Vimeo videos SHALL be offered as a link to the provider and MUST NOT load a third-party iframe automatically.

#### Scenario: Native video
- **WHEN** the article contains a native video file
- **THEN** the reader offers a video player with controls and does not preload the file

#### Scenario: YouTube video
- **WHEN** the article embeds a YouTube video
- **THEN** the reader shows a card that opens the video on YouTube without loading YouTube content in the page

#### Scenario: Vimeo video
- **WHEN** the article embeds a Vimeo video
- **THEN** the reader shows a card that opens the video on Vimeo without loading Vimeo content in the page
