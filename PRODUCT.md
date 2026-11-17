# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Lectores argentinos que siguen la actualidad a diario y quieren ponerse al día con los principales diarios del país sin abrir cada sitio. Revisan la portada desde el celular o la computadora, eligen una nota y esperan poder leerla completa en el mismo lugar. _(Confirmado en entrevista.)_

## Product Purpose

Reunir los titulares de los principales medios argentinos en una sola interfaz y permitir leer la nota completa ahí mismo. El éxito es que el lector encuentre y termine de leer una nota del día sin cambiar de sitio.

## Positioning

La app no solo agrega titulares: extrae y limpia el cuerpo del artículo original, de modo que la lectura completa ocurre dentro de la interfaz. El enlace al medio queda como opción, no como requisito para leer. _(Confirmado en entrevista.)_

## Operating Context

- Uso cotidiano de actualización: abrir la portada, escanear titulares, filtrar por medio o por texto del título, leer una nota y volver.
- Sesiones breves en celular y sesiones más largas en escritorio.
- Tema (automático, claro u oscuro), tamaño de lectura y reproducción por voz ajustables y persistidos en el dispositivo.
- La app corre con la API .NET 8 y el frontend React/Vite del repo, o con Docker Compose / `make start` en un servidor.

## Capabilities and Constraints

- 13 medios argentinos: Clarín, La Nación, Infobae, Página/12, TN, Ámbito Financiero, El Cronista, Perfil, C5N, La Voz del Interior, El Día, 0221 y Seúl; hasta 50 titulares por medio, combinando la portada con las secciones declaradas de cada medio. El README es la fuente de verdad de la cobertura actual.
- Historial local de las últimas 3 ediciones (hoy, ayer y anteayer). Se guarda en disco, solo con metadatos, y la portada lo muestra en una sección “Días anteriores”. El historial empieza desde el despliegue: si la app no corrió un día, ese día no existe.
- Extracción por HTML estático con fallback a Chromium headless. La estructura de cada medio puede cambiar y romper su parser; en ese caso la nota se indica como no disponible y se ofrece el original.
- `GET /api/news` cachea 5 minutos; `GET /api/news/content` acepta solo URLs de las fuentes configuradas (para no ser un proxy abierto) y cachea el cuerpo 30 minutos.
- Sin cuentas de usuario ni backend de preferencias: tema, tamaño de lectura, velocidad y voz viven en `localStorage`. _(Confirmado en entrevista.)_
- La nota completa se puede escuchar con pausa y velocidad. Chrome, Safari y el resto usan la voz del navegador (`speechSynthesis`), con elección de voz en español; Firefox usa una voz neuronal local generada por la API con Piper (`es_AR-daniela-high`).
- En Firefox el texto de la nota viaja al servidor del proyecto, donde se sintetiza y se cachea; no se envía a servicios de terceros. En el resto de los navegadores no sale del dispositivo.
- La reproducción se detiene al cerrar la nota o cambiar de medio; no sigue con la pantalla bloqueada. Si la voz local falla, Firefox vuelve solo a la voz del navegador. La calidad de la voz del navegador depende de las voces instaladas en cada dispositivo.
- El autor se muestra solo cuando firma una persona; se omite si coincide con el nombre del medio o es una firma genérica.
- La lectura no reemplaza al medio: siempre se ofrece "Abrir en <medio>", se atribuye medio y autor, y no se reescribe ni se resume con IA. _(Confirmado en entrevista.)_
- Los filtros combinan medio y texto del título; no hay búsqueda sobre el cuerpo ni historial de lectura.
- No se inventan notas, autores, métricas de uso ni testimonios.
- No se estableció un estándar de accesibilidad ni requisitos específicos; hoy existen control de tamaño de lectura y tema automático.

## Brand Commitments

- Nombre del producto: "Noticias Argentinas".
- La interfaz está escrita en español rioplatense ("Verificá", "Probá", "elegí"). Es el idioma y registro actuales del producto.

## Evidence on Hand

- `README.md`: funcionalidad, fuentes, endpoints, despliegue y atajos de Make.
- `openspec/specs/news-aggregation/spec.md` y el cambio archivado `openspec/changes/archive/2026-11-15-news-aggregator-argentina/`: especificación vigente.
- `api/Tests/`: tests xUnit de parsers, agregación y extracción de contenido.
- No hay testimonios, clientes, prensa ni métricas de uso; no fabricarlos.

## Product Principles

- **La nota se lee acá:** cada decisión protege la lectura completa dentro de la app.
- **El medio manda:** atribución visible, enlace al original y cero reescritura del contenido.
- **Ponerse al día rápido:** escanear, filtrar y abrir una nota con el menor número de pasos.
- **Sin fricción ni cuentas:** preferencias locales; nada que registrar para leer.
- **Honestidad sobre el scraping:** si un medio falla o cambia, se dice y se ofrece el original.
