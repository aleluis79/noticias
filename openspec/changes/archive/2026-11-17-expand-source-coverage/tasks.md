# Tasks

## 1. Base del parser: topes y secciones

- [x] 1.1 Agregar `SectionUrls` a `IParser` y `Parse(string rawHtml, int? maxArticles = null)`, manteniendo compatibilidad con las llamadas actuales. Verificar con `dotnet build api/api.csproj`.
- [x] 1.2 En `HomePageParser`, subir el tope de portada a 30, agregar `SectionMaxArticles = 12`, `SectionPaths` virtual y usarlo en la recolección, el fallback de anchors y `Materialize`. Verificar con `dotnet test api/api.csproj --filter FullyQualifiedName~HomePageParserTests`.
- [x] 1.3 Agregar tests de `HomePageParser` para el tope explícito (`Parse(html, 1)`) y para `SectionUrls` resueltas contra `BaseUrl`. Verificar que pasen en `dotnet test api/api.csproj --filter FullyQualifiedName~HomePageParserTests`.

## 2. Secciones por medio

- [x] 2.1 Declarar `SectionPaths` en Clarín, La Nación, Infobae, Página/12, TN, Ámbito, El Cronista, Perfil, C5N, La Voz, El Día y 0221 con las rutas del design; dejar Seúl sin secciones. Verificar con un test que recorra los 13 parsers y compare las rutas esperadas.
- [x] 2.2 Documentar `SectionPaths` en la sección "Cómo agregar una fuente" del `README.md` y verificar que el ejemplo compile conceptualmente con lo que expone `HomePageParser`.
- [x] 2.3 Sumar la sección de autos donde existe (`/autos` en Clarín, La Nación, Infobae, TN, Ámbito, El Cronista, Perfil y C5N; `/vehiculos` en El Día) y actualizar el test de catálogo de parsers. Verificar con `dotnet test api/api.csproj --filter "FullyQualifiedName~ParserCatalogTests|FullyQualifiedName~NewsAggregationServiceTests"`.

## 3. Descarga y mezcla en la API

- [x] 3.1 En `NewsAggregationService`, descargar la portada y las secciones declaradas, parsear cada sección con `SectionMaxArticles`, subir `MaxParallelFetches` a 8 y mantener el fallback a navegador solo para la portada. Verificar con `dotnet build api/api.csproj`.
- [x] 3.2 Implementar la mezcla por medio: portada primero, secciones después, deduplicación por ruta canónica o título normalizado y corte en 50. Verificar con tests unitarios de mezcla (misma nota en portada y sección, URL con query distinta, tope de 50).
- [x] 3.3 Actualizar `NewsAggregationServiceTests` para generar HTML distinto por ruta y cubrir: portada + secciones, sección caída sin perder la portada, caché con el nuevo número de pedidos y fallback a navegador. Verificar con `dotnet test api/api.csproj --filter FullyQualifiedName~NewsAggregationServiceTests`.
- [x] 3.4 Actualizar `README.md` (hasta 50 por medio) y `PRODUCT.md` (cobertura por portada + secciones). Verificar que las cifras coincidan con los constantes implementados.

## 4. Agrupación de historias

- [x] 4.1 Subir `MaxArticlesPerStory` a 26 y actualizar el comentario que explica el límite. Verificar con `dotnet test api/api.csproj --filter FullyQualifiedName~StoryGroupingServiceTests`.
- [x] 4.2 Agregar un test de agrupación con 13 medios y dos versiones por medio (26 artículos) para confirmar que no se corta la historia. Verificar con el mismo filtro de tests.

## 5. Verificación de integración

- [x] 5.1 Ejecutar `dotnet test api/api.csproj` completo y `pnpm --dir web test` para confirmar que no hay regresiones.
- [x] 5.2 Levantar la API y ejecutar `curl "http://localhost:5251/api/stories?refresh=true"`; verificar que cada medio aporte más de 20 artículos cuando tenga secciones disponibles, que no haya URLs duplicadas por medio y que el tiempo de respuesta sea razonable. Registrar el resultado en el resumen de implementación.
