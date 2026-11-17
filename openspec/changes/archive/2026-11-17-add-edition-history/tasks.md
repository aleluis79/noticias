# Tasks

## 1. Reloj de edición y almacenamiento diario

- [x] 1.1 Crear `EditionClock` con `TimeProvider`, zona `America/Argentina/Buenos_Aires` y fallback UTC−3; exponer `Today` y `Now`. Verificar con tests de fecha en horario argentino y cambio de día.
- [x] 1.2 Crear `ArchiveOptions` (`Directory`, retención de 3 días, zona horaria) y `DailyEditionStore` que guarde un JSON atómico por fecha, lo lea, lo elimine fuera de la ventana y tolere archivos corruptos. Verificar con tests usando un directorio temporal.
- [x] 1.3 Guardar en el snapshot solo metadatos de artículo y verificar con un test que el JSON no contenga cuerpos ni datos de lectura.

## 2. Integración con la agregación

- [x] 2.1 Inyectar `EditionClock` y `DailyEditionStore` en `NewsAggregationService`, guardar la edición después de una descarga fresca con artículos y no guardar cuando se devuelve el último snapshot por fallo total. Verificar con tests de servicio usando un store falso.
- [x] 2.2 Incluir la fecha de edición en la entrada de caché y forzar una descarga nueva cuando cambia el día argentino. Verificar con un test que simule 23:59 y 00:01.
- [x] 2.3 Actualizar la construcción de `NewsAggregationService` en `Program.cs` y en los tests existentes. Verificar con `dotnet build api/api.csproj`.

## 3. API de historial

- [x] 3.1 Agregar `Date` y `PreviousDays` a `StoriesResponse`, crear el modelo de resumen de día y devolver los días guardados ordenados del más reciente al más viejo en `GET /api/stories`. Verificar con tests de controlador.
- [x] 3.2 Implementar `EditionHistoryService` con caché de ediciones agrupadas por fecha y lectura de snapshots sin volver a descargar medios. Verificar con tests de carga de un día y de reutilización de caché.
- [x] 3.3 Soportar `GET /api/stories?date=YYYY-MM-DD` con 200 para un día guardado, 404 para un día ausente y 400 para fecha inválida, futura o fuera de la ventana. Verificar con tests de controlador para cada caso.
- [x] 3.4 Verificar que `/api/news` no cambie y que la edición de hoy siga respondiendo con la misma forma más los campos nuevos. Verificar con tests de contrato de respuesta.

## 4. Sección “Días anteriores” en la web

- [x] 4.1 Extender los tipos del frontend y cargar la edición de un día con `GET /api/stories?date=...` sin tocar la edición actual. Verificar con tests de `App` que la portada sigue visible si el historial falla.
- [x] 4.2 Renderizar la sección “Días anteriores” con un selector por fecha disponible, etiquetas “Ayer” y “Antes de ayer”, estados de carga, error y reintento. Verificar con tests de renderizado.
- [x] 4.3 Permitir abrir historias históricas en el lector existente, reutilizando medios, versiones, lectura completa y voz. Verificar con un test que abre una historia de ayer y muestra su título en el lector.
- [x] 4.4 Aplicar los filtros de medio y título a las historias históricas y sumar al selector los medios que solo aparecen en el día cargado. Verificar con tests de filtrado.
- [x] 4.5 Agregar estilos de la sección respetando el sistema visual existente y verificar que la sección no aparezca cuando `previousDays` está vacío.

## 5. Persistencia, Docker y documentación

- [x] 5.1 Ignorar `api/.archive/` en git, agregar el volumen `noticias-archive` en `docker-compose.yml` y configurar `Archive__Directory` en el Dockerfile. Verificar con `docker compose config`.
- [x] 5.2 Documentar en `README.md` el parámetro `date`, la sección de la portada, la retención de 3 días y la limitación de que el historial empieza desde el despliegue. Verificar que los ejemplos de `curl` funcionen.
- [x] 5.3 Actualizar `PRODUCT.md` con la nueva capacidad y su límite operativo. Verificar que no contradiga el README.

## 6. Verificación de integración

- [x] 6.1 Ejecutar `dotnet test api/api.csproj` completo y `pnpm --dir web test` para confirmar que no hay regresiones.
- [x] 6.2 Levantar la API, crear un snapshot de prueba para la fecha de ayer y verificar `GET /api/stories` (con `previousDays`), `GET /api/stories?date=ayer` y los casos 400/404. Registrar los resultados.
