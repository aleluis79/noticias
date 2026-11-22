# Proposal

## Why

El lector ya trae el texto completo de la nota, pero pierde las fotos y los videos que el medio publica. Para muchas notas la imagen es parte central de la información. Al mismo tiempo, cargarla siempre costaría datos, atención y privacidad en un producto que hoy es deliberadamente liviano. La solución es extraer los medios como metadatos y mostrarlos solo cuando el lector lo pide.

## What Changes

- La API extrae imágenes y videos del cuerpo de la nota y los expone como metadatos opcionales en `GET /api/news/content`, con la posición que ocupan respecto de los párrafos.
- El lector muestra un control "Ver imágenes y videos" apagado por defecto; con el control apagado no se renderiza ni se descarga ningún medio.
- La preferencia se guarda en `localStorage` y, una vez activada, se mantiene para las próximas notas en ese dispositivo.
- Las imágenes se cargan directamente desde el CDN del medio, con carga diferida y sin `Referer`; si fallan, el lector conserva el pie de foto y sigue siendo legible.
- Los videos nativos se reproducen dentro del lector con controles y sin autoplay. YouTube y Vimeo se muestran como una tarjeta-enlace que abre el video en el proveedor, sin cargar iframes de terceros.
- La extracción cubre las formas reales de los medios: `<figure>` con `<figcaption>`, `<img>` con `alt`, `srcset` y atributos lazy; `og:image` o la imagen del JSON-LD como respaldo.
- Tests de API y de frontend, documentación de producto, diseño y operación.

## Capabilities

### New Capabilities

- `article-media`: extracción opcional de imágenes y videos de una nota, y presentación bajo demanda en el lector con preferencia local y degradación clara.

### Modified Capabilities

- `news-aggregation`: el contenido extraído expone también los medios y su posición, sin cambiar el contrato existente de párrafos, título, autor ni el estado de muro de registro.

## Impact

- `api/Models/ArticleContent.cs` y un nuevo modelo de medio.
- `api/Services/ArticleContentService.cs` y tests de extracción.
- `web/src/App.tsx`, estilos del lector, un módulo de medios y tests de frontend.
- `README.md`, `PRODUCT.md` y `DESIGN.md`.
- Sin cambios en voz, resumen, historial, Docker ni dependencias nuevas.
