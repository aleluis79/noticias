# Proposal

## Why

La portada hoy toma como máximo 20 titulares de la home de cada medio. Esa portada
prioriza grandes títulos, pero deja afuera notas importantes que cada medio publica
en sus secciones (política, economía, sociedad, ciudad, etc.) y que no llegan al
bloque principal. El lector que usa la app para ponerse al día termina abriendo el
sitio del medio para encontrar esas notas.

## What Changes

- Cada medio pasa a aportar hasta **50 titulares** en lugar de 20.
- Además de la portada de cada medio, la API descarga un conjunto acotado de
  secciones editoriales propias de ese medio (por ejemplo, política, economía y
  sociedad) y suma sus notas al mismo listado, sin duplicados.
- La portada del medio mantiene prioridad: sus notas aparecen primero; las
  secciones completan el cupo con notas que no estaban en la home.
- Los medios con estructura de secciones distinta (Página/12, La Voz, El Día,
  0221, El Cronista, Seúl) declaran sus propias rutas; los que no tienen secciones
  útiles siguen usando solo su portada.
- Se ajusta el límite interno de artículos por historia para que una nota de
  sección no quede fuera de una cobertura ya agrupada.
- No se agregan medios nuevos ni se cambia el contrato público de `/api/news` o
  `/api/stories`: la respuesta mantiene la misma forma, con más artículos.

## Capabilities

### New Capabilities
<!-- Ninguna: la ampliación de cobertura pertenece a la capacidad existente. -->

### Modified Capabilities
- `news-aggregation`: la recuperación por medio deja de limitarse a la home y a 20
  titulares; ahora incluye secciones declaradas y un cupo mayor por medio, con
  deduplicación y prioridad de la portada.

## Impact

- `api/Services/Parsers/HomePageParser.cs`: límite por página, extracción
  reutilizable con tope configurable y declaración de secciones.
- `api/Services/Parsers/IParser.cs` y los 13 parsers concretos: nuevas rutas de
  sección por medio.
- `api/Services/NewsAggregationService.cs`: descarga de home + secciones,
  concurrencia, deduplicación, cupo por medio y logging.
- `api/Services/Grouping/StoryGroupingService.cs`: tope de artículos por historia.
- `api/Tests/NewsAggregationServiceTests.cs`, `HomePageParserTests.cs` y tests de
  agrupamiento: nuevos escenarios de secciones, deduplicación y cupo.
- `README.md` y `PRODUCT.md`: cobertura real por medio.
- Sin cambios en el frontend: ya pagina las historias y los breves.
