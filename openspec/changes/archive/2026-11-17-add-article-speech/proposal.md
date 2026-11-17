# Proposal

## Why

Hoy la nota completa solo se puede leer. Quien prefiere escuchar mientras cocina, viaja o descansa no tiene forma de consumir el contenido sin salir de la app, y el texto ya extraído en `openContent.paragraphs` está listo para verbalizarse. Agregar reproducción por voz amplía el acceso sin tocar la extracción ni reescribir el contenido.

## What Changes

- Nuevo control "Escuchar la nota" en el panel de lectura, disponible cuando la nota terminó de cargar.
- Reproducción, pausa, reanudación y detención con la voz del navegador (`speechSynthesis`), sin backend ni servicios externos.
- Lectura en orden de título, atribución (autor y medio) y párrafos del cuerpo.
- Velocidad de lectura (1×, 1,25×, 1,5×) y voz en español elegibles, persistidas en `localStorage` junto al tema y el tamaño de lectura.
- La lectura se detiene al cerrar la nota, cambiar de medio o de versión de la nota, y al desmontar el lector.
- Si el navegador no expone la API de síntesis de voz, el control no se muestra y la lectura sigue funcionando igual.

## Capabilities

### New Capabilities

- `article-speech`: reproducción por voz de la nota cargada en el lector, con controles de reproducción, preferencias locales de voz y velocidad, y detención consistente al cambiar de nota.

### Modified Capabilities

<!-- Ninguna. La extracción y la lectura no cambian sus requisitos. -->

## Impact

- `web/src/App.tsx`: estado, controles y ciclo de vida de la lectura en voz alta.
- `web/src/App.css`: estilos del bloque de escucha con los tokens existentes.
- Nuevo `web/src/useArticleSpeech.ts` y utilidades puras de cola/voces.
- `web/package.json`: se suma Vitest, jsdom y Testing Library para TDD del hook y los controles.
- `README.md` y `PRODUCT.md`: documentan la capacidad y las preferencias locales.
- Sin cambios en la API .NET ni en los parsers.
