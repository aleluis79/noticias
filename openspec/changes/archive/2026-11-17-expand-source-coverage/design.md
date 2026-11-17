# Design

## Context

Ver `proposal.md` para la motivación. Hoy `NewsAggregationService` descarga una sola
URL por medio y cada parser hereda de `HomePageParser`, que corta en 20 artículos y
deduplica por URL y título dentro de esa página. La portada del frontend ya pagina
historias y breves, así que el límite real de cobertura está en la API.

Las rutas de sección se verificaron contra las portadas reales de los 13 medios.
Algunas rutas intuitivas devuelven 404 y por eso cada medio declara las suyas.

## Goals / Non-Goals

**Goals:**

- Aumentar la cobertura por medio sin cambiar la forma de las respuestas.
- Priorizar la portada y completar con secciones, sin duplicados.
- Mantener el costo de descarga acotado y predecible.
- Dejar la configuración de secciones junto al parser del medio.

**Non-Goals:**

- Agregar medios nuevos o cambiar el frontend.
- Reemplazar el scraping por RSS u otra fuente.
- Cambiar la caché de 5 minutos ni la agrupación por similitud.
- Hacer fallback a navegador para cada sección: el fallback sigue siendo para la
  portada, que es la que detecta sitios renderizados con JavaScript.

## Decisions

### 1. Secciones declaradas en cada parser

`IParser` expone `SectionUrls` y `HomePageParser` agrega
`protected virtual string[] SectionPaths`. Cada parser concreto sobrescribe solo
sus rutas, igual que hoy hace con `AdditionalIgnoredPathTerms`.

| Medio | Secciones |
| --- | --- |
| Clarín | `/politica`, `/economia`, `/sociedad`, `/autos` |
| La Nación | `/politica`, `/economia`, `/el-mundo`, `/autos` |
| Infobae | `/politica`, `/economia`, `/sociedad`, `/autos` |
| Página/12 | `/el-pais`, `/economia`, `/sociedad` |
| TN | `/politica`, `/economia`, `/sociedad`, `/autos` |
| Ámbito Financiero | `/politica`, `/economia`, `/mundo`, `/autos` |
| El Cronista | `/economia-politica`, `/finanzas-mercados`, `/tecnologia`, `/autos` |
| Perfil | `/politica`, `/economia`, `/sociedad`, `/autos` |
| C5N | `/politica`, `/economia`, `/sociedad`, `/autos` |
| La Voz del Interior | `/politica`, `/negocios`, `/ciudadanos` |
| El Día | `/politica-y-economia`, `/la-ciudad`, `/el-mundo`, `/vehiculos` |
| 0221 | `/la-plata`, `/provincia`, `/policiales` |
| Seúl | ninguna (su portada ya es una selección corta) |

**Alternativa descartada:** guardar las secciones en el arreglo `Sources` del
servicio. Se descartó porque las rutas dependen de la estructura de cada sitio y
el resto de esas particularidades ya vive en el parser.

### 2. Tres topes distintos

- `DefaultMaxArticles = 30`: artículos que puede aportar la página principal.
- `SectionMaxArticles = 12`: artículos que puede aportar cada sección.
- `MaxArticlesPerSource = 50`: tope final después de mezclar todo.

`IParser.Parse` acepta un tope opcional (`int? maxArticles = null`). La portada se
parsea con el tope del parser y cada sección con `SectionMaxArticles`. Así la
portada no consume sola los 50 lugares y las secciones siempre aportan.

**Alternativa descartada:** subir un único límite a 50 y parsear todas las páginas
igual. La portada puede devolver 50 por sí sola y dejar las secciones sin efecto.

### 3. Orden y deduplicación al mezclar

El resultado por medio se arma como: portada primero, luego secciones en el orden
declarado. Se descarta un artículo si ya existe otro con la misma ruta canónica
(`GetLeftPart(UriPartial.Path)` sin barra final) o con el mismo título
normalizado. La comparación de títulos reutiliza la normalización actual
(minúsculas, sin acentos ni puntuación).

Luego se recorta a `MaxArticlesPerSource`. Esto cumple la prioridad de la portada
y el tope de 50 sin depender del orden en que terminen las descargas.

### 4. Descarga concurrente con un solo límite global

La portada se descarga primero para conservar el fallback a navegador actual. Las
secciones se descargan en paralelo, pero todas pasan por el mismo
`SemaphoreSlim` global. Se sube `MaxParallelFetches` de 4 a 8 para compensar el
mayor volumen sin abrir 50 conexiones al mismo tiempo.

Una sección que falla se registra y se omite; no invalida la portada ni las demás
secciones. No se lanza el navegador para secciones: si el HTML estático de una
sección no alcanza, se pierde esa sección, no la cobertura de la portada.

**Alternativa descartada:** descargar las cuatro páginas de cada medio en paralelo
sin límite global. Multiplica los picos contra sitios chicos y aumenta el riesgo de
bloqueo.

### 5. Agrupación de historias

`StoryGroupingService.MaxArticlesPerStory` pasa de 12 a 26: permite que los 13
medios aporten hasta dos versiones de la misma historia (portada + sección) sin que
el tope corte una cobertura real. No se toca el umbral de similitud de 0.50.

### 6. Pruebas y documentación

- `NewsAggregationServiceTests` pasa a generar HTML distinto por ruta, para que la
  portada y las secciones no colisionen por URL. Se agregan casos de deduplicación,
  tope de 50, sección caída y conteo de pedidos.
- `HomePageParserTests` cubre el tope opcional y la resolución de `SectionUrls`.
- `StoryGroupingTests` cubre una historia con más de 12 artículos.
- `README.md`, `PRODUCT.md` y la tabla de fuentes documentan "hasta 50 por medio" y
  `SectionPaths` como parte de cómo agregar una fuente.

## Risks / Trade-offs

- **Más pedidos por refresco (13 portadas + 45 secciones)** → límite global de 8 y
  caché de 5 minutos existente; si un medio bloquea, sus secciones se omiten sin
  afectar a los demás.
- **HTML de sección distinto al de la portada** → el parser base ya filtra por
  host, ruta, longitud de slug y títulos de navegación; las rutas elegidas se
  verificaron contra los sitios reales. Los tests usan HTML sintético, así que la
  validación final es un smoke test con `curl /api/stories`.
- **Duplicados con URLs distintas (AMP, tracking)** → la deduplicación por ruta
  canónica y título normalizado cubre los casos habituales; una nota con dos URLs
  realmente distintas puede aparecer dos veces, igual que hoy dentro de una página.
- **Agrupación O(n²) con hasta 650 artículos** → el volumen sigue siendo bajo para
  el algoritmo actual; se medirá el tiempo del endpoint en el smoke test.
- **Secciones con 404 o cambios de maquetación** → se registran y se omiten; la
  portada sigue funcionando. Ajustar `SectionPaths` no requiere tocar el servicio.

## Migration Plan

1. Desplegar la API con los nuevos topes y secciones; el frontend no necesita
   cambios ni migración de datos.
2. Verificar `GET /api/news?refresh=true` y `GET /api/stories?refresh=true`:
   cantidad por medio, ausencia de duplicados y tiempo de respuesta.
3. Rollback: revertir el cambio de código; las respuestas vuelven a la portada y
   20 por medio sin intervención sobre datos.
