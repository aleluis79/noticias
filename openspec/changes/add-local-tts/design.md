# Design

## Context

Ver `proposal.md`. La API ya extrae y cachea el cuerpo de cada nota y valida que la URL pertenezca a una fuente configurada. El lector ya tiene una cola de reproducción por voz y controles estables. Firefox en Linux delega la voz al sistema y no puede mejorar desde el navegador; Chrome no necesita cambios.

## Goals / Non-Goals

**Goals:**

- Audio neuronal `es-AR` sin costo por uso y sin salir del servidor del proyecto.
- Arranque rápido: un segmento por título, atribución y párrafo, no toda la nota de una vez.
- Fallback transparente a `speechSynthesis` si la API falla.
- Caché persistente y generación deduplicada.

**Non-Goals:**

- Cambiar la reproducción de Chrome, Safari o móviles no-Firefox.
- Clonar voces, elegir múltiples voces o entrenar modelos.
- Reproducción con pantalla bloqueada en esta iteración.

## Decisions

### 1. Piper con voz `es_AR-daniela-high`

Piper es local, CPU-only, sin claves y con una voz argentina de 22 kHz. Se usa el binario standalone 2023.11.14-2 y el modelo de `rhasspy/piper-voices` (OpenSLR 61, CC BY-SA 4.0). Alternativas: Azure/Google (mejor voz, pero costo y clave) y Kokoro (más pesado). El modelo se descarga en la instalación, no se versiona.

### 2. Segmentos por título, atribución y índice de párrafo

`part=title|attribution|body&index=N`. Permite empezar a sonar en segundos, cachear por párrafo y reutilizar la extracción existente. El texto de la atribución lo arma la API con el autor visible y el nombre del medio derivado del host, sin confiar en texto del cliente.

### 3. Caché en disco y deduplicación en memoria

Clave SHA-256 de URL + segmento + modelo. Archivo `.mp3` (o `.wav` si no hay `ffmpeg`). `ConcurrentDictionary<string, Task<...>>` comparte una generación entre pedidos concurrentes; un `SemaphoreSlim` limita el CPU. Las respuestas usan `enableRangeProcessing` y `Cache-Control`.

### 4. `ffmpeg` opcional para MP3

Si `ffmpeg` existe se sirve MP3 64 kbps; si no, WAV del propio Piper. Así el desarrollo local funciona sin instalar nada extra y Docker sirve MP3. El tipo de contenido acompaña al archivo.

### 5. Solo Firefox, con fallback

`usesServerSpeech(userAgent)` detecta Firefox/FxiOS. El hook de audio local reproduce segmentos con `new Audio()` y expone la misma interfaz que `useArticleSpeech`; un facade elige motor. Ante `error` del audio o respuesta no exitosa, llama a `speech.play()` y sigue con la voz del navegador. Chrome mantiene el camino actual sin pedir audio.

### 6. TDD

Primero tests puros y de servicio con un `IProcessRunner` falso; luego tests del hook de audio con un `Audio` falso y del facade con navegador Firefox/Chrome simulados. Ningún código de producción antes de su test rojo, salvo configuración de tooling.

## Risks / Trade-offs

- **CPU y latencia** → segmentos por párrafo, caché y límite de concurrencia; RTF medido ~0.27 en el equipo de desarrollo.
- **Modelo de 109 MB en la imagen** → se descarga en build/instalación, no en git; se puede cambiar a `medium` si el tamaño pesa más que la calidad.
- **Licencia CC BY-SA del dataset** → atribución en README; el modelo se distribuye como dependencia descargada, no como código del repo.
- **Autoplay entre segmentos** → el primer `play()` nace de un gesto del usuario; si un navegador bloquea la continuidad, el error dispara el fallback a voz del navegador.
- **`ffmpeg` ausente** → WAV más pesado pero reproducible; Docker lo incluye y dev funciona sin él.
- **Firefox con voces excelentes (Windows/macOS)** → igual usa la voz local; es consistente y se puede desactivar con configuración.

## Migration Plan

Sin migración de datos. La función es aditiva: si `Tts:Enabled` es false o faltan binarios/modelo, la API responde 503 y Firefox usa la voz del navegador como antes. Rollback: desactivar la opción o quitar el endpoint/hook.

## Open Questions

Ninguna que afecte specs, enfoque o tareas.
