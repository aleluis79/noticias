# Design

## Context

Ver `proposal.md`. La API ya extrae y cachea el cuerpo de cada nota, valida que la URL pertenezca a una fuente configurada y guarda ediciones en disco con escritura atómica. El lector ya tiene un panel por nota y consulta el contenido al abrirlo. La app corre con Docker Compose y el frontend habla a la API a través de nginx.

## Goals / Non-Goals

**Goals:**

- Resumen breve en español generado y guardado a pedido, sin costo por uso.
- Mostrar el resumen guardado directamente al abrir la nota.
- No volver a resumir una URL, ni siquiera si cambia el modelo.
- Funcionar con Ollama local y degradar con un 503 claro cuando no está disponible.

**Non-Goals:**

- Resumir automáticamente toda la edición o todos los titulares.
- Regenerar o editar un resumen existente.
- Elegir modelo o prompts desde la interfaz.
- Traducir el resumen a otros idiomas.

## Decisions

### 1. `gemma4:e2b` con Ollama

Ollama es local, sin claves y con API HTTP simple. `gemma4:e2b` tiene contexto de 128k y está pensado para equipos modestos. Alternativas: servicios en la nube (mejor latencia, pero costo y privacidad) y modelos más grandes (mejor calidad, más RAM y tiempo). Se configura con `Summaries:Model` y se puede cambiar sin tocar código.

### 2. Un resumen por URL, guardado para siempre

La clave del archivo es `sha256(url)` y el JSON guarda `url`, `summary`, `model` y `generatedAt`. No se usa el modelo en la clave: el requisito es no volver a resumir la misma nota. Los archivos viven en `.summaries/` (o en el volumen `noticias-summaries`).

### 3. Generación a pedido y deduplicada

`GET` solo lee el archivo. `POST` genera si falta. Un `ConcurrentDictionary<string, Task<...>>` comparte la generación entre pedidos simultáneos y un `SemaphoreSlim(MaxConcurrent)` limita el uso de CPU. Los fallos no se guardan: se pueden reintentar.

### 4. Prompt acotado y salida validada

Se usa el título (cuando la página lo expone), el medio derivado del host y el cuerpo truncado a `MaxInputCharacters`. El prompt pide 3 o 4 oraciones neutras, sin inventar datos. La llamada a `/api/generate` usa `stream:false`, `think:false`, `temperature:0.2`, `num_predict:400` y `num_ctx:8192`. Se normaliza la salida y se exige un largo mínimo para descartar respuestas vacías.

### 5. Docker Compose con descarga automática

Se agregan `ollama` (volumen de modelos) y `ollama-pull` (servicio de un solo uso que ejecuta `ollama pull gemma4:e2b`). La API espera a que Ollama esté healthy y arranca sin esperar la descarga: hasta que el modelo esté listo, el `POST` responde 503 y la app sigue funcionando. El `GET` de resúmenes existentes nunca depende de Ollama.

### 6. TDD

Primero tests del servicio con un `HttpMessageHandler` falso (caché, persistencia, deduplicación, truncado y errores) y del controlador; después tests del hook y del panel con `fetch` simulado. Ningún código de producción antes de su test rojo, salvo configuración de tooling.

## Risks / Trade-offs

- **Latencia en CPU** → modelo chico, entrada acotada, un solo resumen a la vez y caché; nginx y la API usan un timeout amplio solo para este endpoint.
- **Descarga de ~7.2 GB en el primer arranque** → volumen persistente y un servicio de pull separado; se documenta el requisito de RAM y disco.
- **Calidad del resumen** → prompt explícito, salida validada y aviso en la interfaz de que puede contener errores. No se usa para afirmar hechos nuevos.
- **Cuerpo no extraíble** → el `POST` responde 404 y el lector mantiene la nota original.
- **Ollama caído** → 503; los resúmenes ya generados se siguen mostrando.

## Migration Plan

Sin migración. La función es aditiva: si `Summaries:Enabled` es false o Ollama falta, el lector muestra el botón, el `POST` devuelve 503 y la app sigue funcionando. Rollback: desactivar la opción o quitar el endpoint y el panel.

## Open Questions

Ninguna que afecte specs, enfoque o tareas.
