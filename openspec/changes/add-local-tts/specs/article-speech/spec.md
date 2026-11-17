# Spec Delta

## ADDED Requirements

### Requirement: Firefox SHALL prefer locally synthesized audio

On Firefox, playback SHALL use the local audio API for the article title, attribution and body, and MUST fall back to the browser voice when the local API is unavailable.

#### Scenario: Firefox uses local audio
- **WHEN** playback starts in Firefox and the local API is available
- **THEN** the reader plays local audio segments instead of using speech synthesis

#### Scenario: Local audio fails
- **WHEN** a local audio segment fails to load
- **THEN** the reader continues with the browser voice for the current article

#### Scenario: Other browsers keep the browser voice
- **WHEN** playback starts outside Firefox
- **THEN** the reader uses speech synthesis without requesting local audio

## MODIFIED Requirements

### Requirement: Playback SHALL be controllable

The reader SHALL let the user pause, resume and stop playback at any time, and SHALL announce the current playback state. Controls MUST behave the same with local audio and with the browser voice.

#### Scenario: Pause and resume
- **WHEN** the user pauses an active playback and later resumes it
- **THEN** speech stops and continues from the current point instead of restarting the article

#### Scenario: Stop
- **WHEN** the user stops an active or paused playback
- **THEN** speech stops and the control returns to its idle state
