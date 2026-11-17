# Design

## Context

Ver `proposal.md` para la motivación. El lector ya tiene el cuerpo limpio en `openContent.paragraphs` dentro de `web/src/App.tsx`, y el resto de las preferencias (tema y tamaño) se persisten en `localStorage` con lectura tolerante a errores. El frontend no tiene hoy runner de tests; la API usa xUnit, pero el cambio es exclusivamente del cliente.

Restricciones relevantes:

- React 19 + TypeScript + Vite 8, sin librerías de estado ni de UI.
- Sin cuentas ni backend de preferencias (ver `PRODUCT.md`).
- El diseño visual usa tokens de `DESIGN.md`: píldoras para acciones, versalitas para etiquetas, targets de 2.75rem, sin sombras nuevas.
- La lectura en voz alta debe funcionar sin API ni claves.

## Goals / Non-Goals

**Goals:**

- Aislar la síntesis de voz en un hook y en funciones puras, para testearlas sin navegador real.
- Mantener la UI del lector como única superficie de control.
- Garantizar que cambiar o cerrar la nota nunca deje audio huérfano.
- Dejar la suite de tests del frontend ejecutable con `pnpm test`.

**Non-Goals:**

- Reproducción con pantalla bloqueada o en segundo plano (requiere audio real y un servicio de TTS).
- Descarga o compartición del audio.
- Selección de voces por idioma distinto de español.
- Cambios en la API .NET.

## Decisions

### 1. `speechSynthesis` del navegador, no TTS en servidor

Se usa la Web Speech API. Es gratis, no requiere claves, mantiene el texto en el dispositivo y no agrega latencia de red. La alternativa (Azure/Google/ElevenLabs) da voz consistente y reproducción en segundo plano, pero suma costo, secretos y un endpoint que habría que proteger; se descarta para esta iteración.

Consecuencia: la calidad de voz depende del sistema operativo. Se mitiga con selección de voz y fallback por locale.

### 2. Tres capas: modelo puro, hook y componente

- `web/src/speech/speechModel.ts`: sin React ni DOM. Arma la cola de fragmentos, ordena y elige voces, parsea/valida velocidad y claves de preferencia.
- `web/src/speech/useArticleSpeech.ts`: máquina de estados `idle | playing | paused`, habla la cola, escucha `end`/`error`, pausa, reanuda, detiene y limpia al desmontar o al cambiar `articleKey`.
- `web/src/speech/ListeningControls.tsx`: componente presentacional; recibe estado y callbacks, no conoce `speechSynthesis`.

Así los tests puros cubren la lógica frágil (orden, fallback de voces, chunking) sin mocks de navegador, y el hook se testea con un doble de `speechSynthesis` que dispara eventos de forma determinista.

### 3. Cola por oración con índice de párrafo

Cada párrafo se divide en oraciones de hasta ~240 caracteres (sin cortar palabras) y cada fragmento conserva su índice de párrafo. Motivos:

- Evita el corte de utterances largas en Chrome.
- Hace que pausar/detener sea inmediato.
- Permite resaltar el párrafo activo más adelante sin cambiar el modelo.
- Permite reanudar desde el fragmento actual si el navegador pierde el estado de pausa.

El texto hablado se compone de título, atribución (`Por <autor>, <medio>` o solo el medio) y fragmentos del cuerpo.

### 4. Máquina de estados explícita y detención por contexto

`play()` pasa a `playing`; `pause()` a `paused`; `resume()` a `playing`; `stop()` cancela y vuelve a `idle`. Un `useEffect` sobre `articleKey` y `enabled` llama a `cancel()` y resetea. Un `useEffect` de cleanup cancela al desmontar. Los errores `canceled` e `interrupted` no se muestran como fallo porque son consecuencia de nuestras propias cancelaciones.

### 5. Preferencias locales con fallback

Claves `noticias-speech-rate` y `noticias-speech-voice` (URI de voz). Velocidades permitidas: `1`, `1.25`, `1.5`; cualquier valor inválido cae a `1`. La voz guardada se usa solo si sigue existiendo; si no, se elige por ranking: `es-AR` > `es-419` > otro `es-*`. Si no hay voces en español, se usa la voz por defecto con `lang = 'es-AR'` para que el motor elija. La lista se lee al montar y se refresca con `voiceschanged`.

### 6. TDD estricto con Vitest + jsdom

Se agregan `vitest`, `jsdom` y `@testing-library/react` como devDependencies y un script `pnpm test`. Orden de trabajo:

1. Tests de `speechModel` (rojo) → implementación mínima (verde).
2. Tests del hook con un `FakeSpeechSynthesis` (rojo) → hook (verde).
3. Tests de `ListeningControls` (rojo) → componente (verde).
4. Tests de integración de `App` con `fetch` mockeado (rojo) → cableado (verde).

Ningún archivo de producción se escribe antes de su test rojo, salvo configuración de tooling. El doble de síntesis implementa `speak`, `cancel`, `pause`, `resume`, `getVoices`, `addEventListener('voiceschanged')` y expone helpers para disparar `onend`/`onerror`; así se testea el orden real de la cola.

`App` necesita stubs de `matchMedia`, `scrollTo`, `scrollIntoView` y `requestAnimationFrame` en el setup de jsdom.

### 7. Ubicación y estilo del control

El bloque se ubica debajo de `.reading-controls`, dentro del panel de lectura, con el mismo ancho de 68ch. Usa una etiqueta en versalitas "Escuchar la nota", un botón principal tipo píldora (Escuchar / Pausar / Reanudar), un botón Detener, un grupo segmentado de velocidad y un `<select>` de voz cuando hay más de una. El estado se anuncia con `aria-live="polite"`. No se agregan sombras ni colores nuevos.

## Risks / Trade-offs

- **Pausa/reanudación poco confiable en iOS** → al reanudar, si el motor no está reproduciendo, se reencola el fragmento actual en vez de reiniciar toda la nota.
- **Voces de escritorio Linux robóticas o sin español** → ranking de fallback, selector visible y ausencia de promesas de calidad; el usuario puede elegir otra voz del sistema.
- **Chrome corta utterances largas** → fragmentos por oración.
- **La voz elegida no existe en otro dispositivo** → se ignora y se elige la mejor disponible, manteniendo el resto de las preferencias.
- **La lista de voces llega tarde o vacía** → se reintenta con `voiceschanged` y se permite reproducir con la voz por defecto.
- **Pruebas de audio reales no automatizables** → el doble de `speechSynthesis` verifica la secuencia y los estados; la calidad de voz queda como verificación manual.
- **`speechSynthesis` no existe en jsdom** → el setup define el doble solo cuando el test lo instala; los tests de "no soportado" lo eliminan explícitamente.

## Migration Plan

No hay migración de datos. El cambio es aditivo y se despliega con el frontend. Rollback: quitar el bloque de escucha y el hook; no hay estado en servidor ni formato persistido crítico (las claves nuevas pueden quedar sin uso).

## Open Questions

Ninguna que afecte specs, enfoque o tareas.
