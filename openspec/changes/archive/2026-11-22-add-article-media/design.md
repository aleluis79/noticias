# Design

## Context

Ver `proposal.md`. La API ya descarga la nota, la limpia con AngleSharp y elige el contenedor del cuerpo para devolver párrafos. El lector ya cachea el contenido por URL, tiene preferencias de tema y tamaño de lectura en `localStorage`, y usa los índices de párrafo para la voz y el resumen. Cualquier diseño de medios tiene que convivir con esos índices.

La extracción actual se apoya en tres formas reales:

- Clarín publica imágenes sueltas con `<img alt="...">` dentro de `#cuerpo`.
- Infobae usa `<figure>` y `<figcaption>`.
- La Nación publica la imagen principal en `og:image` con URLs firmadas y su HTML inicial es un esqueleto que se completa con JavaScript.

## Goals / Non-Goals

**Goals:**

- Extraer medios con posición sin tocar la extracción de párrafos ni sus índices.
- Cargar imágenes solo cuando el lector lo pide, y recordar esa decisión en el dispositivo.
- Soportar imágenes, video nativo y enlaces a YouTube/Vimeo con degradación clara.
- Mantener el lector liviano: sin galería, sin iframes automáticos y sin descargas en el servidor.

**Non-Goals:**

- Mostrar miniaturas en la portada, la matriz o las breves.
- Descargar o cachear imágenes en la API (queda como fase posterior si aparece hotlink bloqueado).
- Reproducir YouTube o Vimeo dentro de la app.
- Extraer medios de páginas que no tengan cuerpo legible o que estén detrás de un muro de registro.
- Cambiar el contrato de voz, resumen o historial.

## Decisions

### 1. `media` como lista aparte de `paragraphs`

`ArticleContent` conserva `paragraphs` y suma `Media`. Cada medio tiene `AfterParagraph`, que cuenta párrafos extraídos, no nodos del DOM. Así la voz sigue pidiendo `body&index=N` con el mismo significado y el resumen sigue usando el mismo texto.

Alternativa descartada: convertir el cuerpo en una lista de bloques. Es más expresiva, pero obliga a migrar la voz, el resumen y los tests de índices. El beneficio no compensa el riesgo.

### 2. Una sola pasada de selección de cuerpo

La selección actual (`BestParagraphs`) devuelve solo párrafos. Se refactoriza para devolver también el contenedor elegido. Sobre ese contenedor se recorre el DOM en orden y se cuentan los párrafos aceptados; cuando aparece un medio, se registra con el contador actual. La misma función de aceptación de párrafos se usa para ambos, así no se desincronizan.

Si el cuerpo sale del JSON-LD (sin contenedor DOM), no hay posiciones: se devuelven los párrafos y, si existe, la imagen Open Graph como único medio en la posición cero.

### 3. Extracción tolerante a las formas de cada medio

Se reconocen `figure`, `picture`, `img`, `video` y iframes de video conocidos. Para cada uno:

- `figcaption` como pie; si no hay, `alt`.
- `src`, `srcset`, `data-src`, `data-srcset`, `data-original` y `data-lazy-src` como fuentes posibles.
- URLs relativas y protocol-relative resueltas contra la URL de la nota.
- Dimensiones de los atributos `width`/`height` para reservar el aspecto.
- Deduplicación por host + ruta, sin query, para no repetir la misma foto en varios tamaños.

Se descartan hosts y rutas de tracking, logos, placeholders, publicidad, avatares y sprites; se descartan esquemas que no sean HTTP/HTTPS y medios con dimensiones declaradas menores a 200 px.

### 4. Fallback de imagen principal

Si no hay imágenes inline, se busca `og:image` y luego la imagen del JSON-LD. Se agrega en la posición cero. Si la URL parece un logo, un placeholder o una imagen de sharing, se descarta. Esto cubre a La Nación, donde el cuerpo se arma con JavaScript y la imagen canónica está en metadatos.

### 5. Video sin terceros automáticos

Los `<video>` nativos se devuelven con `Url`, `PosterUrl` y `Provider: "native"`. YouTube y Vimeo se detectan desde el iframe y se devuelven con `Provider` y la URL de reproducción pública, no la URL de embed. El lector los muestra como tarjeta-enlace.

Alternativa descartada: insertar el iframe del proveedor. Ahorra un clic, pero carga cookies, scripts y tracking de terceros y contradice el tono liviano del producto.

### 6. Carga directa con `no-referrer`

Las imágenes se piden al CDN del medio con `referrerPolicy="no-referrer"` y `loading="lazy"`. Evita sumar un proxy, caché de binarios y una superficie SSRF en la fase 1. La prueba con La Nación confirmó que una URL firmada completa responde 200 sin referer. Si aparecen bloqueos, la API puede empezar a devolver URLs proxeadas sin cambiar el frontend.

### 7. Preferencia única y persistida

Una sola preferencia `noticias-show-media` con `'1'`/`'0'` controla imágenes y videos. El valor inicial es `'0'`. Se lee al iniciar, como el tema y el tamaño de lectura, para evitar un parpadeo. El control solo aparece cuando el contenido está listo y hay medios.

### 8. TDD estricto

Cada comportamiento se escribe primero como test rojo:

- API: extracción de figure, img con alt, srcset, relativas, fallback Open Graph, filtros, orden, video y nota sin medios.
- Frontend: preferencia apagada, activación, persistencia, posición, pie, imagen rota, video nativo y tarjeta de YouTube.
- Después se implementa lo mínimo para ponerlo en verde y se ejecutan `make test` y `make lint`.

## Risks / Trade-offs

- **Hotlink 403** → se conserva el pie de foto y se ofrece el enlace al medio; si es sistemático, se implementa el proxy.
- **URLs firmadas que expiran** → la caché de contenido es de 30 minutos; si una imagen falla, el bloque se degrada y la nota sigue legible.
- **CLS por imágenes sin dimensiones** → se usan `width`/`height` cuando existen; si no, se acepta un salto menor antes que recortar la imagen.
- **Peso en móvil** → preferencia apagada por defecto, carga diferida y límite de 12 medios.
- **Privacidad** → la carga directa revela la IP del lector al CDN; queda documentado y el proxy es la mitigación futura.
- **Pérdida de medios en notas recuperadas por feed** → se intenta extraer del HTML del feed; si no, se documenta la limitación.
- **Iframes que no son video** → se siguen eliminando en la limpieza; solo sobreviven los proveedores reconocidos.

## Migration Plan

Cambio aditivo. El campo `media` es una lista vacía para notas sin medios y los clientes viejos lo ignoran. No hay migración de datos ni cambios de configuración. Rollback: quitar el renderizado de medios en el lector; la API puede seguir devolviendo la lista sin efecto.
