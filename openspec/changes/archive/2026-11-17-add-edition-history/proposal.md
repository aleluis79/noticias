# Proposal

## Why

La app solo muestra la edición del día. Cuando el lector vuelve al día siguiente,
las notas de ayer ya no están: las portadas y secciones se renuevan y esos
titulares se pierden. El lector que se pone al día necesita poder revisar lo
importante de los últimos días sin volver a abrir cada medio.

## What Changes

- La API guarda en disco cada edición diaria que descarga correctamente, con fecha
  de Argentina, y conserva **hoy + los 2 días anteriores** (3 días en total).
- La edición de hoy se actualiza durante el día; las ediciones pasadas quedan
  congeladas y se pueden consultar aunque los sitios ya no muestren esas notas.
- `GET /api/stories` sigue devolviendo la edición de hoy y suma `date` y un
  resumen `previousDays` con los días disponibles.
- `GET /api/stories?date=YYYY-MM-DD` devuelve una edición guardada de un día
  anterior. Si el día no existe, responde 404; si la fecha es inválida o futura,
  400.
- La portada web agrega una sección **“Días anteriores”** con los días
  disponibles. Al elegir un día se cargan sus historias y se pueden leer completas
  en el mismo lector que las de hoy.
- El historial empieza a guardarse desde el despliegue. No se inventan días
  faltantes ni se rellenan retroactivamente: si la app no corrió un día, ese día
  no aparece.
- Se agrega un volumen de Docker para que el historial sobreviva a los reinicios.
- No se guardan cuerpos de notas, solo los mismos metadatos que ya devuelve la
  API (título, resumen, URL, medio y fecha de publicación cuando existe).
- **BREAKING**: ninguno. Los campos nuevos son aditivos y `/api/news` mantiene su
  comportamiento actual.

## Capabilities

### New Capabilities
- `edition-history`: guarda ediciones diarias de la agregación, las conserva por
  una ventana corta, las expone por fecha y las presenta en una sección separada
  de la portada.

### Modified Capabilities
<!-- Ninguna: la agregación actual sigue igual; el historial es una capacidad nueva. -->

## Impact

- Nuevo servicio de almacenamiento diario y configuración de archivo
  (`api/Services/Archive/`).
- `api/Services/NewsAggregationService.cs`: registrar la edición al descargar y
  alinear la caché con el cambio de fecha argentina.
- `api/Controllers/NewsController.cs` y `api/Models/`: fecha de edición, resumen
  de días anteriores y consulta por fecha.
- `api/Program.cs`: registro del servicio y configuración.
- `web/src/App.tsx` y estilos: sección “Días anteriores” con carga por día y
  lectura reutilizando el lector actual.
- `docker-compose.yml`, `api/Dockerfile`, `.gitignore`: volumen y ruta del
  historial.
- `README.md` y `PRODUCT.md`: endpoints, retención, límites y operación.
- Tests de API, almacenamiento y frontend para guardado, retención, fechas,
  historial vacío y carga de un día.
