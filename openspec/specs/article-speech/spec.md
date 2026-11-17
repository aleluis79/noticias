# article-speech Specification

## Purpose
Permite escuchar en voz alta la nota cargada en el lector, con controles de reproducción y preferencias de voz y velocidad guardadas en el dispositivo.

## Requirements

### Requirement: Reader SHALL offer speech playback for a loaded article

When the full body of the selected article is available, the reader SHALL show a speech control. While the body is loading or failed, the reader SHALL NOT offer playback.

#### Scenario: Control appears when the body is ready
- **WHEN** the reader shows an article whose paragraphs finished loading
- **THEN** a visible "Escuchar la nota" control is available

#### Scenario: Control is withheld without a body
- **WHEN** the article body is still loading or could not be extracted
- **THEN** the speech control is not shown or is disabled

### Requirement: Playback SHALL read the article in order

Playback SHALL verbalize the article title first, then the author and outlet attribution when present, then the body paragraphs in their displayed order, using Spanish language settings.

#### Scenario: Title, attribution and body are read in order
- **WHEN** the user starts playback on a ready article with an author
- **THEN** the spoken sequence is title, author and outlet, then each paragraph in order

### Requirement: Playback SHALL be controllable

The reader SHALL let the user pause, resume and stop playback at any time, and SHALL announce the current playback state.

#### Scenario: Pause and resume
- **WHEN** the user pauses an active playback and later resumes it
- **THEN** speech stops and continues from the current point instead of restarting the article

#### Scenario: Stop
- **WHEN** the user stops an active or paused playback
- **THEN** speech stops and the control returns to its idle state

### Requirement: Playback MUST stop when the article context changes

Playback MUST stop when the reader closes, when the user switches outlet or article version, and when the reader unmounts.

#### Scenario: Closing the reader stops speech
- **WHEN** the user closes the reader while an article is being read
- **THEN** speech stops immediately and does not continue in the background

#### Scenario: Switching outlet stops speech
- **WHEN** the user selects a different outlet or version of the same story during playback
- **THEN** speech stops and the control resets for the newly selected article

### Requirement: Voice and rate preferences MUST persist locally

The reader SHALL let the user choose a reading rate and a Spanish voice, and MUST persist both choices on the device without sending them to a server.

#### Scenario: Preferences survive a reload
- **WHEN** the user chooses a rate and a voice and reloads the page
- **THEN** the same rate and voice are selected again

#### Scenario: Missing saved voice falls back
- **WHEN** the saved voice is unavailable on the current device
- **THEN** the reader selects the best available Spanish voice and keeps playback working

#### Scenario: Playback stays on the device
- **WHEN** the user starts, pauses or stops playback
- **THEN** no article text is sent to the API or to a third-party speech service

### Requirement: Unsupported browsers MUST keep the reader usable

When the browser does not expose speech synthesis, the reader MUST NOT show a broken control and the article remains readable.

#### Scenario: No speech synthesis support
- **WHEN** the browser lacks the speech synthesis API
- **THEN** the speech control is absent and reading, filters and theme controls keep working
