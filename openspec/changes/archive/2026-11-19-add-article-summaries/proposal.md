# Proposal

## Why

El lector ya trae la nota completa, pero para decidir si vale la pena leerla hace falta un resumen. Generarlo a pedido con un modelo local evita depender de servicios externos, claves y costos por uso, y deja el resumen guardado para no volver a calcularlo.

## What Changes

- Nuevo endpoint `GET /api/news/summary?url=...` que devuelve el resumen guardado de una nota y `POST /api/news/summary?url=...` que lo genera y lo guarda.
- Resumen en español de 3 o 4 oraciones a partir del cuerpo ya extraído por la API, usando Ollama con `gemma4:e2b`.
- Caché persistente en disco por URL, sin vencimiento ni regeneración; el resumen se guarda aunque cambie el modelo.
- Deduplicación de generaciones concurrentes y límite de concurrencia para no saturar la CPU.
- En el lector, consulta automática del resumen al abrir la nota y botón "Generar resumen" cuando todavía no existe.
- Servicio `ollama` en `docker-compose.yml`, con descarga automática del modelo en el primer arranque, volumen de modelos y volumen de resúmenes.
- Documentación, configuración local, tests de API y de frontend.

## Capabilities

### New Capabilities

- `article-summaries`: generación a pedido, almacenamiento y consulta de resúmenes de notas con un modelo local.

### Modified Capabilities

- `news-aggregation`: el cuerpo extraído expone también el título de la página para alimentar el resumen.

## Impact

- `api/`: modelo, opciones, servicio de resúmenes, endpoint, extracción de título y tests.
- `web/`: hook, panel de resumen, integración en el lector y tests.
- `docker-compose.yml`, `web/nginx.conf`, `.gitignore`, `README.md`, `Makefile` y `DESIGN.md`.
- Modelo `gemma4:e2b` (Gemma Terms of Use, ~7.2 GB): se descarga en el primer arranque, no se versiona.
