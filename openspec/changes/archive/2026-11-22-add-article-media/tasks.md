# Tasks

## 1. Contrato y tests de extracción

- [x] 1.1 Agregar `ArticleMedia` y la lista `Media` en `ArticleContent`, y verificar que la solución compila con `dotnet build api/api.csproj`.
- [x] 1.2 Escribir tests rojos de extracción de una `<figure>` con `<figcaption>` y verificar que fallan porque todavía no existe la extracción de medios.
- [x] 1.3 Escribir tests rojos de `<img>` con `alt`, `srcset`, URLs relativas y atributos lazy; verificar que fallan por la misma razón.
- [x] 1.4 Escribir tests rojos de descarte de pixeles, logos, placeholders y esquemas no HTTP; verificar que fallan.
- [x] 1.5 Escribir tests rojos de fallback `og:image` e imagen del JSON-LD; verificar que fallan.
- [x] 1.6 Escribir tests rojos de video nativo, YouTube y Vimeo; verificar que fallan.
- [x] 1.7 Escribir tests rojos de posición (`AfterParagraph`) y de nota sin medios; verificar que fallan.

## 2. Implementación de extracción

- [x] 2.1 Refactorizar la selección de cuerpo para conservar el contenedor y verificar que todos los tests existentes de párrafos siguen pasando.
- [x] 2.2 Implementar el recorrido ordenado que asigna `AfterParagraph` y verificar los tests de posición.
- [x] 2.3 Implementar la extracción de figure, img, srcset, lazy y resolución de URLs y verificar los tests de imágenes.
- [x] 2.4 Implementar los filtros de ruido, dimensiones y esquemas y verificar los tests de descarte.
- [x] 2.5 Implementar el fallback de Open Graph y JSON-LD y verificar sus tests.
- [x] 2.6 Implementar video nativo y detección de YouTube/Vimeo, conservando solo esos iframes en la limpieza, y verificar sus tests.
- [x] 2.7 Implementar deduplicación y límite de medios y verificar los tests correspondientes.
- [x] 2.8 Verificar que `GetArticleContentAsync` devuelve medios en la respuesta y que una nota sin medios devuelve lista vacía.

## 3. Frontend: modelo y tests

- [x] 3.1 Escribir tests rojos de `buildReaderBlocks` para medios antes del primer párrafo, entre párrafos y al final; verificar que fallan.
- [x] 3.2 Implementar `buildReaderBlocks` y verificar que los tests pasan.
- [x] 3.3 Escribir tests rojos de la preferencia `noticias-show-media`: valor inicial apagado, activación y lectura desde `localStorage`.
- [x] 3.4 Implementar la lectura, escritura y valor inicial de la preferencia y verificar sus tests.

## 4. Frontend: componentes de medios

- [x] 4.1 Escribir tests rojos del componente de imagen: figura, pie, `alt`, dimensiones y estado de error.
- [x] 4.2 Implementar el componente de imagen y verificar que los tests pasan.
- [x] 4.3 Escribir tests rojos de video nativo y de tarjeta de YouTube/Vimeo: controles, `preload="none"` y enlace externo sin iframe.
- [x] 4.4 Implementar el componente de video y verificar que los tests pasan.

## 5. Frontend: integración en el lector

- [x] 5.1 Escribir tests rojos del lector: sin medios no hay control; con medios y preferencia apagada no se renderiza ninguna imagen.
- [x] 5.2 Implementar el control y el renderizado condicional y verificar esos tests.
- [x] 5.3 Escribir tests rojos de activación, orden de medios, persistencia y degradación de imagen rota.
- [x] 5.4 Implementar la integración completa y verificar esos tests.
- [x] 5.5 Ejecutar `pnpm test` y `pnpm lint` en `web/` y verificar que pasan.

## 6. Diseño y documentación

- [x] 6.1 Agregar los estilos de medios al lector respetando 68ch, 16px, sin sombra nueva y con pie en sans, y verificar visualmente en claro y oscuro.
- [x] 6.2 Documentar el componente "Article Media" en `DESIGN.md` y verificar que describe el comportamiento implementado.
- [x] 6.3 Actualizar `README.md` y `PRODUCT.md` con medios opcionales, preferencia local, video y privacidad, y verificar que no contradicen la implementación.

## 7. Verificación final

- [x] 7.1 Ejecutar `make test` y verificar que API y frontend pasan completos.
- [x] 7.2 Ejecutar `make lint` y `make build` y verificar que no hay errores.
- [x] 7.3 Revisar una nota real de Clarín, Infobae y La Nación con medios apagados y encendidos y verificar posición, pie, peso y degradación.

## Workflow follow-up

- Archivar el cambio con `openspec archive add-article-media` después de la revisión.
- Verificar el resultado archivado y que las specs principales quedaron actualizadas.
