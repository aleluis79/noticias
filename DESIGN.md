---
name: Noticias Argentinas
description: Una primera plana de papel cálido que muestra qué medios cubren cada historia y deja leer la nota completa sin salir.
colors:
  warm-paper: "#f6f2ea"
  newsprint: "#fffdf8"
  shaded-paper: "#efe9dd"
  ink-black: "#1b1814"
  sepia-ink: "#6b6255"
  press-red: "#ad2b20"
  press-red-deep: "#8a2018"
  press-red-wash: "#f6e3df"
  paper-rule: "#e3dbcd"
  rule-strong: "#cabfae"
typography:
  display:
    fontFamily: "'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia, 'Times New Roman', serif"
    fontSize: "clamp(2rem, 1.4rem + 2.4vw, 2.9rem)"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "-0.035em"
  headline:
    fontFamily: "'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia, 'Times New Roman', serif"
    fontSize: "clamp(1.08rem, 1rem + 0.35vw, 1.28rem)"
    fontWeight: 600
    lineHeight: 1.16
    letterSpacing: "-0.014em"
  reading-title:
    fontFamily: "'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia, 'Times New Roman', serif"
    fontSize: "clamp(1.5rem, 1.2rem + 1.2vw, 2.1rem)"
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: "-0.025em"
  brief:
    fontFamily: "'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia, 'Times New Roman', serif"
    fontSize: "1rem"
    fontWeight: 600
    lineHeight: 1.22
    letterSpacing: "-0.005em"
  body:
    fontFamily: "'Iowan Old Style', 'Palatino Linotype', Palatino, Georgia, 'Times New Roman', serif"
    fontSize: "calc(1.06rem * var(--note-scale, 1))"
    fontWeight: 400
    lineHeight: 1.7
  lede:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif"
    fontSize: "0.95rem"
    fontWeight: 400
    lineHeight: 1.55
  label:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif"
    fontSize: "0.72rem"
    fontWeight: 700
    lineHeight: 1.5
    letterSpacing: "0.14em"
  action:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', sans-serif"
    fontSize: "0.78rem"
    fontWeight: 650
    lineHeight: 1.5
    letterSpacing: "0.06em"
rounded:
  pill: "999px"
  panel: "16px"
  field: "10px"
  notice: "6px"
  rule: "3px"
spacing:
  micro: "0.35rem"
  xs: "0.6rem"
  sm: "0.9rem"
  md: "1.2rem"
  lg: "1.6rem"
  xl: "2.2rem"
  page: "3.5rem"
components:
  source-chip:
    backgroundColor: "{colors.press-red-wash}"
    textColor: "{colors.press-red}"
    typography: "{typography.label}"
    rounded: "{rounded.pill}"
    padding: "0.35rem 0.7rem"
  source-chip-selected:
    backgroundColor: "{colors.press-red}"
    textColor: "{colors.newsprint}"
    typography: "{typography.label}"
    rounded: "{rounded.pill}"
    padding: "0.4rem 0.85rem"
    height: "2.75rem"
  filter-field:
    backgroundColor: "{colors.newsprint}"
    textColor: "{colors.ink-black}"
    rounded: "{rounded.field}"
    padding: "0.55rem 0.85rem"
    height: "2.75rem"
  pill-button:
    backgroundColor: "transparent"
    textColor: "{colors.ink-black}"
    typography: "{typography.action}"
    rounded: "{rounded.pill}"
    padding: "0.45rem 0.95rem"
    height: "2.75rem"
  matrix-dot:
    backgroundColor: "transparent"
    rounded: "{rounded.pill}"
    size: "0.55rem"
  matrix-dot-covered:
    backgroundColor: "{colors.press-red}"
    rounded: "{rounded.pill}"
    size: "0.55rem"
  reader-panel:
    backgroundColor: "{colors.newsprint}"
    rounded: "{rounded.panel}"
    padding: "1.4rem 1.35rem 1.7rem"
  reading-control-pill:
    backgroundColor: "{colors.newsprint}"
    rounded: "{rounded.pill}"
    padding: "0.2rem"
    height: "3rem"
---

# Design System: Noticias Argentinas

## Overview

**Creative North Star: "La Primera Plana"**

El sistema se comporta como la primera plana de una edición impresa sobre papel cálido: una cabecera con filete rojo, un cuerpo de historias ordenado por importancia y tipografía con serif para todo lo que se lee. No hay marco de aplicación, barra lateral ni widgets. La página entera es la edición del día.

La diferencia con un diario es que la edición es **comparada**. La portada abre con una matriz de historias por medio —las historias en filas, los trece medios en columnas, un punto por cada medio que cubre la historia— y las notas que trae un solo medio caen a una banda de breves. La pluralidad deja de ser una afirmación del texto y se vuelve geometría: quién cubre qué se lee antes de leer nada.

El tono es sereno y autoritativo, con densidad media. El rojo de imprenta aparece en dosis pequeñas y en tres trabajos concretos: filete de cabecera, etiquetas de cobertura y foco. La lectura manda sobre la interfaz: al abrir una historia, el panel de lectura ocupa el ancho completo, limita la medida a 68ch y el serif sube de tamaño con el control de lectura. El sistema evita explícitamente el look de feed de red social: nada de scroll infinito, métricas de engagement, avatares ni botones de reacción.

**Key Characteristics:**
- Papel cálido en dos tonos y tinta casi negra; sin blanco puro ni negro puro.
- Serif editorial para títulos y cuerpo; sans en versalitas espaciadas para la interfaz.
- Un solo acento, el Rojo Imprenta, usado en superficies pequeñas y en puntos de dato.
- Separación por filetes de 1px; una sola sombra, reservada al panel de lectura.
- Controles en forma de píldora con objetivos táctiles de 2.75rem como piso.
- La cobertura de una historia es un dato visible, no un texto de marketing.

## Colors

Una paleta de papel y tinta: cremas cálidos, un casi-negro con temperatura marrón y un único rojo de imprenta que funciona como señal.

### Primary
- **Rojo Imprenta** (#ad2b20): el acento único. Filete de 3px sobre la cabecera, etiqueta de cobertura ("7 medios cubren esta historia"), chips de medio, puntos de cobertura de la matriz, etiquetas de acción y anillo de foco. En oscuro se invierte a un coral legible (#ff8073) para conservar contraste sobre el papel nocturno.
- **Rojo Imprenta Profundo** (#8a2018): solo para hover de enlaces de texto y para profundizar el acento en claro. No se usa como fondo de botones.

### Neutral
- **Papel Cálido** (#f6f2ea): fondo de la página y del documento en tema claro. El cream, no el blanco, es la base.
- **Papel de Tapa** (#fffdf8): superficie de campos, controles y panel de lectura.
- **Papel Sombreado** (#efe9dd): tono intermedio de la escala de papel, disponible para superficies secundarias. La portada actual no lo usa como fondo de fila: la separación se hace con filetes.
- **Tinta Negra** (#1b1814): texto principal, titulares y filetes de foco. Es un negro cálido, no `#000`.
- **Tinta Sepia** (#6b6255): resúmenes, metadatos, contadores, placeholders y leyenda de la matriz. El segundo nivel de lectura.
- **Filete Papel** (#e3dbcd): bordes de fila, separadores y líneas punteadas internas.
- **Filete Reforzado** (#cabfae): borde de controles interactivos y de la leyenda de la matriz.

Los equivalentes oscuros viven en `.impeccable/design.json` y en `web/src/index.css` bajo `[data-theme='dark']`; no se inventan acá valores nuevos. El tema se resuelve por tokens, nunca por overrides locales.

### Named Rules
**La Regla del Rojo Único.** El acento ocupa menos del 10% del **área** de cualquier pantalla. Los puntos de cobertura son señal de dato y no cuentan como relleno: un punto de 9px no compite con una superficie. Lo que sigue prohibido es un botón primario relleno o cualquier bloque grande teñido de rojo.

**La Regla sin Blanco Puro.** Ningún fondo usa `#fff`. El papel más claro es Papel de Tapa (#fffdf8); el fondo de página es siempre Papel Cálido.

## Typography

**Display Font:** Iowan Old Style / Palatino (con Georgia, Times New Roman, serif como fallback)
**Body Font:** Iowan Old Style / Palatino (con Georgia, Times New Roman, serif)
**Label/Mono Font:** system UI sans (system-ui, Segoe UI, Roboto, Helvetica Neue)

**Character:** Un serif de libro con eje de contraste suave para todo lo que se lee —cabecera, titulares de historia, breves, cuerpo de nota— y una sans neutra en versalitas espaciadas para lo que se opera. La mezcla imita la jerarquía de un diario: el contenido tiene voz; los controles tienen oficio.

### Hierarchy
- **Display** (700, `clamp(2rem, 1.4rem + 2.4vw, 2.9rem)`, 1): solo el título "Noticias Argentinas" en la cabecera.
- **Reading Title** (700, `clamp(1.5rem, 1.2rem + 1.2vw, 2.1rem)`, 1.1): el titular del medio elegido dentro del panel de lectura.
- **Headline** (600, `clamp(1.08rem, 1rem + 0.35vw, 1.28rem)`, 1.16): las historias de la matriz. `text-wrap: balance`.
- **Brief** (600, 1rem, 1.22): las notas de un solo medio en la banda de breves y en el resto de la edición.
- **Body** (400, `1.06rem × --note-scale`, 1.7): párrafos de la nota extraída, en serif y a 68ch máximo. La escala va de 100% a 145% en cuatro pasos.
- **Lede** (400, 0.95rem, 1.55): el subtítulo de la cabecera. Es el único texto de lectura en sans.
- **Label** (700, 0.72rem, tracking 0.14em, versalitas): etiquetas de campo, "Tema", leyenda de la matriz, cobertura y metadatos de breves.

### Named Rules
**La Regla de las Dos Voces.** Serif para contenido (títulos, cuerpo, breves); sans para interfaz (etiquetas, campos, botones). No se cruzan: un botón nunca se compone en serif y un párrafo nunca en versalitas.

**La Regla del Titular Equilibrado.** Todo titular usa `text-wrap: balance` y tracking negativo; todo párrafo usa `text-wrap: pretty`. El rag importa tanto como el tamaño.

**La Regla del Piso Legible.** Ninguna etiqueta funcional baja de 11px. La versalita más chica del sistema es 0.72rem (11.5px); por debajo, la etiqueta deja de ser legible y se vuelve textura.

## Layout

Un contenedor único de `min(100%, 1120px)` centrado, con 1.25rem de gutter lateral y 3.5rem de respiro al pie. La portada es una sucesión vertical de cabecera, filtros, matriz, breves y resto de la edición.

- **Cabecera:** título a la izquierda y metadatos a la derecha (fecha completa y "Actualizado HH:MM hs"), subtítulo debajo, acciones alineadas a la izquierda y filete de cierre. Un filete rojo de 3px la corona, inset 1.25rem respecto del contenedor.
- **Filtros:** grilla de dos columnas `minmax(0, 1.5fr) minmax(0, 1fr)` (búsqueda por título y medio), con una fila inferior de ancho completo para el contador y "Limpiar filtros".
- **Matriz:** `grid-template-columns: minmax(0, 1fr) minmax(0, 24rem)`. La columna derecha reparte los 13 medios en partes iguales con separación de 0.2rem, para que leyenda y puntos compartan la misma geometría. Las etiquetas de medio se rotan en vertical y crecen hacia arriba desde la fila.
- **Breves:** tres columnas con 2.2rem de separación, intercaladas por medio para que un solo diario no ocupe la banda.
- **Resto de la edición:** lista de filas a ancho completo, paginada de a 18.
- **Panel de lectura:** ocupa el ancho completo del contenedor, arriba de la matriz, con `68ch` de medida centrada para cuerpo, autor y controles.
- **Responsive (≤860px):** la matriz abandona los puntos y muestra los medios que cubren cada historia como chips; la leyenda desaparece.
- **Responsive (≤640px):** una sola columna, gutter de 1rem, cabecera compacta (subtítulo oculto, tema y actualizar en una fila, actualizar solo con ícono), filtros apilados y breves a una columna.

## Elevation & Depth

El sistema es plano con una sola elevación real. La jerarquía se construye con capas tonales de papel y filetes de 1px; la matriz no tiene sombra ni tarjetas flotantes. El panel de lectura es la única superficie elevada: aparece por encima de la portada y su sombra lo declara como una capa distinta, no como decoración.

### Shadow Vocabulary
- **Ambient Panel (claro)** (`box-shadow: 0 1px 2px rgb(27 24 20 / 5%), 0 18px 40px -28px rgb(27 24 20 / 45%)`): solo en el panel de lectura.
- **Ambient Panel (oscuro)** (`box-shadow: 0 1px 2px rgb(0 0 0 / 35%), 0 20px 44px -28px rgb(0 0 0 / 85%)`): misma función sobre papel nocturno.

### Named Rules
**La Regla del Filete Primero.** Si hace falta separar dos bloques, se usa un filete de 1px (o punteado hacia adentro de una fila), no una sombra nueva. Solo existe una sombra en todo el sistema, y pertenece al panel de lectura.

## Shapes

Lenguaje de formas mixto y deliberado: rectángulos de esquina suave para el contenido y píldoras completas para lo accionable. El panel de lectura usa 16px; los campos de texto, 10px; botones, selects, chips y grupos segmentados usan `999px`; el aviso de error, 6px. El filete de cabecera tiene 3px de alto y esquinas inferiores de 3px. Los separadores externos son continuos; los internos del panel de lectura, punteados. La matriz no tiene contenedores: filas separadas por filetes.

**La Regla de la Píldora Accionable.** Todo lo que se toca es una píldora; todo lo que se lee es un rectángulo de 16px. Un botón cuadrado o una tarjeta con radio de píldora rompe el sistema.

## Components

### Buttons
- **Shape:** píldora completa (`999px`), borde de 1px Filete Reforzado, altura mínima de 2.75rem (44px).
- **Primary:** no existe botón relleno de acento. La acción principal es una píldora fantasma: fondo transparente, texto Tinta Negra, 0.78rem/650 en versalitas con tracking 0.06em, ícono de 15px a la izquierda.
- **Hover / Focus:** borde y texto pasan a Rojo Imprenta con fondo Rojo Lavado; el foco usa outline de 2px Rojo Imprenta con offset 2px.
- **Enlaces de acción:** "Limpiar filtros" y "← Volver a la portada" son texto rojo en versalitas, sin borde ni fondo, con subrayado solo en hover.
- **En móvil:** "Actualizar" se reduce a ícono sin perder su nombre accesible.

### Chips
- **Style:** chip estático en píldora, fondo Rojo Lavado, texto Rojo Imprenta, 0.72rem/700 en versalitas con tracking 0.12em, padding `0.35rem 0.7rem`.
- **Selectable:** el chip de medio dentro del panel de lectura es un botón de 2.75rem de alto. El medio activo invierte a fondo Rojo Imprenta con texto Papel de Tapa; el resto queda en lavado.
- **State:** en la matriz, los chips de cobertura son solo lectura; no compiten con el titular.

### Matrix Rows
- **Corner Style:** sin radio; filas separadas por filete de 1px.
- **Headline:** serif Headline como botón a ancho completo con objetivo de 2.75rem, para que el nombre accesible sea el titular y no la fila entera.
- **Coverage:** etiqueta en versalitas rojas ("7 medios cubren esta historia"), debajo del titular.
- **Dots:** 12 columnas de puntos de 0.55rem; cubierto en Rojo Imprenta, no cubierto con borde Filete Reforzado. En móvil se sustituyen por chips con el nombre del medio.

### Cards / Containers
- **Corner Style:** el panel de lectura usa 16px; no hay tarjetas en la portada.
- **Background:** Papel de Tapa sobre Papel Cálido.
- **Shadow Strategy:** sombra ambiental única (ver Elevation & Depth).
- **Border:** 1px Filete Reforzado en el panel; Filete Papel en las filas.
- **Internal Padding:** `1.4rem 1.35rem 1.7rem` en el panel; `0.95rem 0` por fila de matriz.

### Inputs / Fields
- **Style:** fondo Papel de Tapa, borde 1px Filete Reforzado, radio 10px, altura mínima 2.75rem, texto de 1rem, placeholder Tinta Sepia al 100%.
- **Focus:** outline de 2px Rojo Imprenta con offset 2px; el borde no cambia de color en foco.
- **Hover:** el borde pasa a Tinta Sepia. No hay estado de error: la búsqueda filtra en vivo y el vacío se comunica con un estado dedicado.

### Reading Controls (firma)
Grupo segmentado en píldora con borde Filete Reforzado: botón "A−", valor porcentual en vivo (tabular, 0.76rem/650) y botón "A+". Cada botón mide 2.75rem cuadrados; el hover tiñe el fondo de Rojo Lavado y el texto de Rojo Imprenta; en los extremos de la escala el botón se deshabilita con opacidad 0.35. El valor se anuncia con `aria-live="polite"`.

### Article Body
El cuerpo extraído se compone en serif a `calc(1.06rem × --note-scale)`, interlineado 1.7, párrafos separados por 1.05em, 68ch de medida y `overflow-wrap: break-word`. Antes del cuerpo, el autor se muestra en 0.84rem/600 con la etiqueta "Por" en versalitas rojas; solo aparece cuando firma una persona. Al pie, el enlace "Abrir en <medio>" cierra el panel como píldora con ícono externo de 14px.

### Summary Panel
El resumen ocupa el mismo ancho de lectura (68ch) y vive entre los controles de voz y el autor. Es un rectángulo de 16px sobre Papel Sombreado, con filete de 1px, sin sombra. La etiqueta "Resumen · IA" va en versalitas rojas; el texto, en serif a `calc(1.02rem × --note-scale)` con interlineado 1.6. Una nota en Tinta Sepia aclara que se generó automáticamente y puede contener errores.

Cuando todavía no hay resumen, el panel muestra una píldora fantasma "Generar resumen" con ícono de destello y una línea de contexto. Mientras genera, reemplaza el botón por el spinner y el texto "Generando el resumen…". Si falla, el borde pasa a Rojo Imprenta, se explica el motivo y la píldora cambia a "Reintentar". El resumen guardado nunca vuelve a ofrecer generación.

### States
Los estados vacíos y de error son bloques centrados con título serif de 1.6rem, texto de 0.95rem a 48ch y, cuando corresponde, una píldora de reintento. El error de extracción es un bloque con borde rojo de 1px y `role="status"`. El aviso de actualización fallida usa el mismo recurso, arriba, y queda pegado al borde superior de la ventana para que se vea desde cualquier punto de la edición. El spinner es un anillo de 1.1rem con borde Filete Reforzado y sector superior rojo, acompañado siempre de texto ("Reuniendo las noticias del día…", "Cargando la nota completa…").

## Do's and Don'ts

### Do:
- **Do** usar serif para títulos y cuerpo, y sans en versalitas espaciadas para etiquetas y controles.
- **Do** mantener el fondo en Papel Cálido y las superficies en Papel de Tapa; el contraste entre ambos es la separación base.
- **Do** reservar el Rojo Imprenta para señales pequeñas: filete, cobertura, chips, puntos, foco.
- **Do** separar bloques con filetes de 1px o punteados antes de recurrir a una sombra.
- **Do** respetar el mínimo táctil de 2.75rem en todo lo interactivo, incluidos los botones de texto.
- **Do** limitar el cuerpo de la nota a 68ch y centrarlo, con el control de lectura en cuatro pasos (100–145%).
- **Do** usar `text-wrap: balance` en titulares y `pretty` en párrafos.
- **Do** conservar el anillo de foco de 2px con offset 2px en todo control interactivo.
- **Do** decir la cobertura con precisión: "7 medios cubren esta historia" solo si son siete.

### Don't:
- **Don't** rellenar botones o superficies grandes con el rojo de acento; no existe un botón primario sólido en este sistema.
- **Don't** introducir blanco puro (`#fff`), negro puro (`#000`) ni grises fríos: rompen la temperatura del papel.
- **Don't** agregar una segunda familia tipográfica ni mezclar serif en la interfaz.
- **Don't** usar radios mayores a 16px en superficies ni esquinas rectas en elementos accionables.
- **Don't** sumar sombras nuevas ni elevar filas de la matriz; la única sombra es la del panel de lectura.
- **Don't** convertir la portada en un feed de red social: nada de scroll infinito, avatares, contadores de interacción, botones de reacción ni perfiles.
- **Don't** usar versalitas ni tracking amplio en el cuerpo de la nota.
- **Don't** bajar etiquetas funcionales de 11px.
- **Don't** mostrar loaders sin texto que explique qué se está cargando.
