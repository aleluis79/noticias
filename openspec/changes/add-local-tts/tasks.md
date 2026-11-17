# Tasks

## 1. API: texto, caché y proceso

- [x] 1.1 Escribir en rojo tests de `TtsTextBuilder` (título, atribución con autor visible y medio derivado del host, párrafo por índice, texto vacío) y de la clave de caché; verificar el rojo.
- [x] 1.2 Implementar `TtsTextBuilder` y la clave de caché en `api/Services/Tts/` hasta poner en verde; verificar con `dotnet test`.
- [x] 1.3 Escribir en rojo tests de `PiperTtsService` con un `IProcessRunner` falso: deshabilitado, modelo ausente, generación MP3, fallback WAV, caché en disco y error del proceso; verificar el rojo.
- [x] 1.4 Implementar `ITextToSpeechService`, `PiperTtsService`, `IProcessRunner` y `TtsOptions` hasta poner en verde; verificar con `dotnet test`.

## 2. API: endpoint

- [x] 2.1 Escribir en rojo tests del endpoint de audio (URL no soportada, parte inválida, índice fuera de rango, TTS deshabilitado, respuesta exitosa con content type y caché); verificar el rojo.
- [x] 2.2 Implementar `GET /api/news/audio` en `NewsController`, registrar DI y configuración; verificar con `dotnet test`.

## 3. Frontend: modelo y hook de audio

- [x] 3.1 Escribir en rojo tests de `usesServerSpeech`, `buildAudioSegments` y `audioSegmentUrl`; verificar el rojo.
- [x] 3.2 Implementar las funciones puras en `web/src/speech/playbackModel.ts`; verificar con `pnpm test`.
- [x] 3.3 Escribir en rojo tests de `useServerArticleAudio` con un `Audio` falso: reproducir segmentos en orden, pausa/reanudar/stop, velocidad, error y cancelación al cambiar de nota; verificar el rojo.
- [x] 3.4 Implementar el hook hasta poner en verde; verificar con `pnpm test`.

## 4. Frontend: integración y fallback

- [x] 4.1 Escribir en rojo tests de `useArticlePlayback`: Firefox usa audio local, Chrome usa `speechSynthesis`, error de audio cae a la voz del navegador y los controles conservan estado; verificar el rojo.
- [x] 4.2 Implementar el facade y cablearlo en `App.tsx`; verificar con `pnpm test`, `pnpm lint` y `pnpm build`.
- [x] 4.3 Ajustar los tests de integración de `App` para Firefox y verificar que el control sigue apareciendo con la nota lista.

## 5. Instalación y despliegue

- [x] 5.1 Crear `tools/install-tts.sh` (Piper + modelo `es_AR-daniela-high`), target `make install-tts`, `.gitignore` y configuración de desarrollo; verificar ejecutando el script y `curl` al endpoint con una nota real.
- [x] 5.2 Actualizar `api/Dockerfile` y `docker-compose.yml` con ffmpeg, Piper, modelo y volumen de caché; verificar `docker compose build api` y una reproducción en Firefox.

## 6. Documentación y verificación

- [x] 6.1 Documentar en `README.md` y `PRODUCT.md` el TTS local, la instalación, la atribución del modelo y el alcance solo-Firefox; verificar que los comandos coinciden con el código.
- [x] 6.2 Ejecutar `dotnet test`, `pnpm test`, `pnpm lint`, `pnpm build` y `openspec validate add-local-tts --strict`; verificar que todo pasa.
- [x] 6.3 Probar en Firefox: abrir una nota, escuchar, pausar, reanudar, detener, cambiar velocidad y confirmar el fallback apagando el TTS; registrar el resultado.

## Workflow follow-up

- Archivar el cambio con `openspec archive add-local-tts` cuando la revisión esté aprobada.
- Verificar las specs principales `local-tts` y `article-speech`.
