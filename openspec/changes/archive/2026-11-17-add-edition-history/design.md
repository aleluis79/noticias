# Design

## Context

Ver `proposal.md` para la motivación. Hoy `NewsAggregationService` guarda una
edición en memoria durante 5 minutos y `/api/stories` la agrupa en el momento. No
hay persistencia: al cambiar el día, la edición anterior desaparece. El frontend
solo conoce la respuesta de `/api/stories` y ya tiene un lector que funciona con
cualquier `NewsStory`.

El nuevo historial no reemplaza esa agregación: agrega una capa de persistencia
diaria y una consulta por fecha.

## Goals / Non-Goals

**Goals:**

- Guardar una edición por día en Argentina y conservar hoy + 2 días anteriores.
- Servir una edición pasada sin volver a descargar los medios.
- Mostrar los días disponibles en una sección separada, sin inflar la carga
  inicial de la portada.
- Mantener el lector, los filtros y las voces actuales para las notas históricas.

**Non-Goals:**

- Rellenar días anteriores al despliegue o días en que la app no corrió.
- Guardar cuerpos de notas, historial de lectura o preferencias.
- Cambiar `/api/news`, el agrupamiento o el umbral de similitud.
- Agregar autenticación o endpoints públicos nuevos fuera de `/api/stories`.

## Decisions

### 1. Snapshot diario en disco, no en base de datos

Cada edición se guarda como un JSON por fecha:

```json
{
  "date": "2026-11-17",
  "capturedAt": "2026-11-17T22:36:53Z",
  "articles": [ ... ]
}
```

La ruta sale de `Archive:Directory` (por defecto `.archive` dentro del content
root de la API) y el nombre es `YYYY-MM-DD.json`. Se escribe a un archivo temporal
y se reemplaza de forma atómica. Un fallo de escritura se registra y no afecta la
respuesta del día.

**Alternativa descartada:** base de datos. Para 3 días de metadatos, un archivo
por día es más simple de operar y de respaldar.

**Alternativa descartada:** guardar las historias ya agrupadas. Ataría el archivo
al algoritmo de agrupamiento; guardar artículos permite reagrupar si cambia el
código.

### 2. Fecha de edición con reloj y zona horaria inyectables

Se agrega `EditionClock`, que usa `TimeProvider` y la zona
`America/Argentina/Buenos_Aires` (con fallback a UTC−3 si el sistema no trae
tzdata). Expone `Today` y `Now`.

La caché de 5 minutos pasa a incluir la fecha de edición. Si el reloj cambia de
día, la caché anterior no se usa como edición de hoy.

**Alternativa descartada:** usar `DateTime.Now` directo. No permite probar el
cambio de día y depende de la zona del servidor.

### 3. Guardar después de una descarga fresca, no en cada request

`NewsAggregationService` guarda la edición inmediatamente después de una
agregación fresca con al menos un artículo, antes de publicar la caché. Si la
descarga falla por completo y se devuelve el último snapshot en memoria, no se
escribe nada.

**Alternativa descartada:** guardar desde el controlador. Escribiría en cada
request de `/api/stories`, incluso cuando la respuesta viene de caché.

### 4. Consulta por fecha dentro de `/api/stories`

`StoriesResponse` suma:

- `date`: fecha de la edición (`YYYY-MM-DD`).
- `previousDays`: resúmenes de los días guardados, del más reciente al más viejo.

`GET /api/stories?date=YYYY-MM-DD` devuelve una edición pasada. La consulta
histórica no dispara descargas y valida la fecha antes de tocar el disco:

- fecha inválida, futura o fuera de la ventana → 400;
- fecha válida sin archivo → 404;
- fecha existente → 200 con la misma forma que la edición de hoy.

Se eligió un parámetro sobre un endpoint nuevo porque el recurso sigue siendo
“la edición de historias” y el cliente reutiliza el mismo tipo de respuesta.

**Alternativa descartada:** `/api/stories/history`. Obligaba a duplicar el modelo
y a que el frontend mantuviera dos clientes distintos para lo mismo.

### 5. Caché de historias agrupadas en memoria

`EditionHistoryService` compone el almacenamiento con `StoryGroupingService` y
cachea por fecha la edición ya agrupada. Los días pasados son inmutables; el día
de hoy se sigue sirviendo por la ruta actual. En el primer request después de
arrancar se leen y agrupan como máximo dos días, y luego se reutilizan.

**Alternativa descartada:** agrupar en cada request. Con 625 artículos por día,
cada consulta costaría cientos de milisegundos sin necesidad.

**Alternativa descartada:** guardar `storyCount` en el archivo. El resumen
quedaría viejo si cambia el algoritmo de agrupamiento.

### 6. Sección “Días anteriores” con carga por día

El frontend usa `previousDays` para dibujar una sección visible después de la
edición actual, con un botón por fecha (“Ayer”, “Antes de ayer” o la fecha
completa).

- No se descarga historial al cargar la portada: el lector elige un día y ahí se
  pide `GET /api/stories?date=...`.
- El estado de la sección es independiente del estado de hoy: un error de
  historial no rompe la edición actual y ofrece reintentar.
- Las historias del día elegido se muestran en una lista compacta con medio y
  cantidad de medios; al abrirlas se reutiliza el lector existente.
- Los filtros de medio y título se aplican también a las historias históricas.
- Si el día cargado trae medios que hoy no aparecen, se suman al selector de
  medios.

Se descartó cargar automáticamente los dos días al abrir la portada: duplicaría
el peso inicial en celular y el lector suele querer un día puntual.

### 7. Operación y persistencia

- Local: `api/.archive/`, ignorado por git.
- Docker: volumen `noticias-archive` montado en `/var/lib/noticias-archive` y
  `Archive__Directory` apuntando ahí.
- Retención: al guardar, se eliminan los archivos fuera de la ventana de 3 días.
- Un archivo corrupto se registra y se omite; no tumba el endpoint.
- El historial solo contiene metadatos: no hay cuerpos de notas ni datos del
  lector.

## Risks / Trade-offs

- **La app debe correr todos los días para tener historial** → es una limitación
  aceptada; la UI muestra solo los días existentes y el README lo explica.
- **Primer request tras un reinicio agrupa dos días** → se hace una sola vez y
  queda en memoria; son como máximo dos ediciones.
- **Crecimiento del disco** → retención de 3 archivos de ~400 KB; el volumen de
  Docker evita perderlos al recrear el contenedor.
- **Cambio de día a mitad de una descarga** → la fecha se toma al iniciar la
  agregación y se guarda con esa fecha; la próxima request después de medianoche
  vuelve a descargar.
- **Archivo corrupto o disco lleno** → se registra y el historial se comporta
  como si ese día faltara; la edición de hoy sigue funcionando.
- **Notas viejas con URL caída** → el lector muestra el error actual y ofrece
  “Abrir en el medio”, igual que con cualquier nota de hoy.
- **Payload del historial** → cada día se pide solo al seleccionarlo, no en la
  carga inicial.

## Migration Plan

1. Desplegar la API con el volumen de historial. El historial empieza vacío y se
   completa desde el primer día.
2. Verificar que `/api/stories` incluya `date` y que, al día siguiente,
   `previousDays` tenga la fecha anterior.
3. Para probar sin esperar un día, copiar el snapshot de hoy a la fecha de ayer
   en el directorio de archivo y consultar `/api/stories?date=...`.
4. Rollback: detener la API y volver a la versión anterior. Los archivos quedan en
   el volumen sin afectar la operación.
