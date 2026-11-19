# Tasks

## 1. API: modelo, opciones y almacenamiento

- [x] 1.1 Escribir en rojo tests de `ArticleSummaryService` con un `HttpMessageHandler` falso: persistencia en disco, no re-llamar al modelo, deduplicación, truncado de entrada y errores; verificar el rojo.
- [x] 1.2 Implementar `ArticleSummary`, `SummaryOptions`, `IArticleSummaryService` y `ArticleSummaryService`; verificar con `dotnet test`.

## 2. API: endpoint y título

- [x] 2.1 Escribir en rojo tests del controlador de resúmenes (400, 404, 503, 502 y 200) y de la extracción de título; verificar el rojo.
- [x] 2.2 Implementar `SummariesController`, la extracción de título en `ArticleContentService`, el registro DI y la configuración; verificar con `dotnet test`.

## 3. Frontend: hook y panel

- [x] 3.1 Escribir en rojo tests del hook y del panel: 200 muestra directo, 404 muestra botón, POST genera, error permite reintentar y cambiar de URL consulta otro resumen; verificar el rojo.
- [x] 3.2 Implementar `useArticleSummary` y `SummaryPanel`; verificar con `pnpm test`.
- [x] 3.3 Integrar el panel en `App.tsx` y agregar los estilos en `App.css`; verificar con `pnpm test`, `pnpm lint` y `pnpm build`.

## 4. Docker e infraestructura

- [x] 4.1 Agregar `ollama` y `ollama-pull` a `docker-compose.yml`, el volumen de resúmenes y las variables de la API.
- [x] 4.2 Ampliar el timeout de nginx solo para `/api/news/summary`, ignorar `.summaries/` y agregar el target de Make para el pull manual.

## 5. Documentación y verificación

- [x] 5.1 Documentar la función, los endpoints, la configuración, los requisitos de RAM y la primera descarga en `README.md` y `DESIGN.md`.
- [x] 5.2 Ejecutar `dotnet test`, `pnpm test`, `pnpm lint`, `pnpm build` y `openspec validate add-article-summaries --strict`; verificar que todo pasa.

## Workflow follow-up

- Archivar el cambio con `openspec archive add-article-summaries` cuando la revisión esté aprobada.
- Verificar la spec principal `article-summaries`.
