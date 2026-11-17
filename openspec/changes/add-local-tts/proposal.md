# Proposal

## Why

En Firefox sobre Linux la síntesis del navegador depende de `speech-dispatcher` y espeak/MBROLA: suena robótica y se corta. Chrome ya suena bien y no necesita cambios. La app puede generar audio neuronal en el propio servidor con Piper, sin claves ni costos por uso, y usarlo solo en Firefox.

## What Changes

- Nueva API `GET /api/news/audio?url=...&part=title|attribution|body&index=N` que devuelve el audio de un segmento de la nota.
- Síntesis local con Piper (`es_AR-daniela-high`) ejecutada por la API; sin servicios externos ni API keys.
- Caché en disco por URL, segmento y modelo, con deduplicación de pedidos concurrentes.
- Conversión a MP3 con `ffmpeg` cuando está disponible; WAV como fallback local.
- En Firefox, el lector reproduce esos segmentos con un elemento `<audio>` y mantiene los mismos controles; si la API no está disponible, cae a la voz del navegador.
- Chrome, Safari y el resto siguen usando `speechSynthesis` sin cambios.
- Script `tools/install-tts.sh` y target `make install-tts` para preparar Piper en desarrollo; la imagen Docker lo incluye.

## Capabilities

### New Capabilities

- `local-tts`: síntesis de voz local en la API, por segmentos de nota, con caché y degradación controlada.

### Modified Capabilities

- `article-speech`: se agrega el motor de audio local para Firefox y su fallback a la voz del navegador.

## Impact

- `api/`: nuevo servicio Piper, opciones de configuración, endpoint, dependencias del proceso y tests.
- `api/Dockerfile` y `docker-compose.yml`: Piper, modelo, ffmpeg y volumen de caché.
- `web/`: detección de Firefox, cola de segmentos de audio y fallback; Chrome sin cambios.
- `tools/install-tts.sh`, `Makefile`, `README.md`: instalación local y documentación.
- Modelo `es_AR-daniela-high` (OpenSLR 61, CC BY-SA 4.0): se descarga en la instalación, no se versiona.
