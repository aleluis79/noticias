# Noticias Argentinas

Agregador de noticias que reúne los principales titulares de los diarios argentinos en una sola interfaz. El proyecto combina una API en **.NET 8** que descarga y normaliza las portadas de cada medio con un frontend en **React + TypeScript + Vite**.

## Características

- Titulares de 13 medios argentinos en una única respuesta (hasta 50 por medio, sumando portada y secciones).
- Historial local de las últimas 3 ediciones (hoy, ayer y anteayer), con consulta por fecha y una sección propia en la portada.
- Extracción mediante HTML estático y fallback a un navegador Chromium headless para los sitios que renderizan con JavaScript.
- Caché en memoria de 5 minutos para los titulares y de 30 minutos para el cuerpo de cada nota.
- Lectura completa de la nota dentro de la propia interfaz, extrayendo y limpiando los párrafos del artículo original.
- Reproducción por voz de la nota completa con pausa y velocidad. En Chrome y el resto se usa la voz del navegador; en Firefox, una voz neuronal generada localmente por la API.
- Resumen de la nota a pedido, generado con un modelo local (`gemma4:e2b` en Ollama) y guardado en disco: una vez generado se muestra directamente y no se vuelve a resumir.
- Autor de la nota cuando el medio lo publica (se omite si la firma es solo el nombre del diario).
- Filtros por medio y por título, tema claro/oscuro/automático y control del tamaño de lectura (persistidos en `localStorage`).
- API REST documentada con Swagger en desarrollo.

## Estructura del proyecto

```
noticias/
├── api/                       # Backend .NET 8
│   ├── Controllers/           # NewsController
│   ├── Models/                # NewsArticle, ArticleContent
│   ├── Services/
│   │   ├── Parsers/           # Un parser por medio (HomePageParser como base)
│   │   ├── Summaries/         # Resúmenes a pedido con Ollama
│   │   ├── NewsAggregationService.cs
│   │   └── ArticleContentService.cs
│   ├── Tests/                 # Tests xUnit dentro del mismo proyecto
│   └── Program.cs
├── web/                       # Frontend React 19 + Vite
│   └── src/                   # App.tsx, estilos y assets
├── openspec/                  # Especificaciones y cambios del proyecto
└── .opencode/                 # Comandos y skills del agente
```

## Requisitos

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) y [pnpm](https://pnpm.io/installation)
- Chromium de Playwright (solo para el fallback de sitios con JavaScript)

## Cómo ejecutarlo

### 1. API

```bash
dotnet run --project api
```

La API queda disponible en `http://localhost:5251` y Swagger en `http://localhost:5251/swagger` cuando el entorno es `Development`.

La primera vez que el servicio necesite el navegador headless, Playwright requiere tener Chromium instalado. Después del primer build:

```bash
pwsh api/bin/Debug/net8.0/playwright.ps1 install chromium
```

> El script requiere PowerShell. Si no lo tenés, el resto de la aplicación sigue funcionando: solo se pierde el fallback para los medios que dependen de JavaScript.

### 2. Frontend

En otra terminal:

```bash
cd web
pnpm install
pnpm dev
```

El frontend queda en `http://localhost:5173`. La URL de la API está definida en `web/src/App.tsx` mediante la constante `API_BASE` (`http://localhost:5251`).

### Scripts del frontend

| Comando | Descripción |
| --- | --- |
| `pnpm dev` | Servidor de desarrollo con HMR |
| `pnpm build` | Chequeo de tipos y build de producción |
| `pnpm test` | Ejecuta los tests del frontend (Vitest + jsdom) |
| `pnpm lint` | Lint con Oxlint |
| `pnpm preview` | Vista previa del build |

En Chrome y el resto de los navegadores la reproducción usa `speechSynthesis` y las voces del dispositivo. En Firefox se usa la voz local de la API (Piper) porque las voces del sistema en Linux suenan robóticas; si la API no está disponible, vuelve sola a la voz del navegador. La reproducción no continúa con la pantalla bloqueada.

## Voz local (Firefox)

Firefox en Linux sintetiza con `speech-dispatcher`/espeak y suena robótico. Para ese navegador, la API genera el audio con **Piper** y una voz neuronal argentina, sin claves ni costos por uso.

```bash
make install-tts
```

El script descarga Piper y el modelo `es_AR-daniela-high` en `api/.tts/` (ignorado por git, unos 110 MB). La API lo detecta con la configuración por defecto; no hace falta configurar nada más. Si además instalás `ffmpeg`, el audio se sirve en MP3; si no está, se sirve WAV.

- Endpoint: `GET /api/news/audio?url=...&part=title|attribution|body&index=N`.
- Caché en disco por nota y segmento; los pedidos concurrentes comparten una sola síntesis.
- Mientras suena un párrafo, el lector precarga los dos siguientes para que el cambio no tenga espera.
- Si faltan Piper o el modelo, la API responde 503 y Firefox usa la voz del navegador como antes.
- Variables: `Tts__Enabled`, `Tts__PiperPath`, `Tts__ModelPath`, `Tts__FfmpegPath`, `Tts__CacheDirectory`, `Tts__MaxConcurrent`.
- El modelo se entrenó con el dataset [OpenSLR 61](https://www.openslr.org/61/) (Attribution-ShareAlike 4.0) y se distribuye desde [`rhasspy/piper-voices`](https://huggingface.co/rhasspy/piper-voices).

En Docker, Piper, `ffmpeg` y el modelo ya vienen en la imagen de la API, y la caché vive en el volumen `noticias-tts`.

## Resúmenes con IA local

El lector consulta si la nota ya tiene un resumen. Si no, ofrece **Generar resumen**; la API arma el texto con el título, el medio y el cuerpo extraído, y le pide al modelo local un párrafo de 3 o 4 oraciones en español. El resumen se guarda en `.summaries/` (un JSON por URL) y nunca se vuelve a generar, ni siquiera si cambia el modelo.

En desarrollo hace falta un Ollama local:

```bash
make ollama-pull    # descarga gemma4:e2b en el Ollama local
dotnet run --project api
```

La API usa `http://localhost:11434` por defecto. Variables: `Summaries__Enabled`, `Summaries__OllamaUrl`, `Summaries__Model`, `Summaries__Directory`, `Summaries__MaxInputCharacters`, `Summaries__TimeoutSeconds`, `Summaries__MaxConcurrent`.

- `GET /api/news/summary?url=...` devuelve el resumen guardado o 404.
- `POST /api/news/summary?url=...` lo genera y guarda; si ya existe, devuelve el guardado sin llamar al modelo.
- Sin Ollama o sin el modelo, el `POST` responde 503 y los resúmenes ya guardados se siguen mostrando.
- Los fallos no se guardan, así que se puede reintentar.
- El modelo pesa unos 7 GB y conviene tener al menos 16 GB de RAM si corre en CPU. La primera generación incluye la carga del modelo y puede tardar varios minutos.

## Docker

La aplicación también puede ejecutarse con Docker Compose. La imagen de la API incluye Chromium y sus dependencias, así que el fallback de Playwright funciona sin instalar nada en el host. El historial de ediciones queda en el volumen `noticias-archive` para sobrevivir a los reinicios.

Compose levanta también **Ollama** para los resúmenes. La primera vez descarga el modelo `gemma4:e2b` (unos 7 GB) en el volumen `noticias-ollama`; hasta que termina, el botón de resumen responde 503, pero la portada y las notas funcionan con normalidad. Los resúmenes generados viven en `noticias-summaries`.

```bash
docker compose up --build -d
```

- Frontend: `http://localhost:9090`
- API directa: `http://localhost:5251`

Para volver a descargar el modelo o hacerlo manualmente:

```bash
make docker-model
# o
docker compose exec ollama ollama pull gemma4:e2b
```

Ollama corre en CPU por defecto. Si el host tiene GPU NVIDIA, se puede habilitar con el bloque `deploy.resources.reservations.devices` de Compose y el NVIDIA Container Toolkit; sin eso, la generación es más lenta pero funciona.

Nginx sirve el build de Vite y redirige `/api/` a la API, por lo que el navegador usa un único origen y no hace falta CORS. Para apuntar el frontend a otra API, cambiá `VITE_API_BASE` en los `build.args` del servicio `web` (vacío significa mismo origen).

```bash
docker compose logs -f   # seguir los logs
docker compose down      # detener y eliminar los contenedores
```

## Atajos con Make

En la raíz hay un `Makefile` con los comandos más habituales:

| Comando | Qué hace |
| --- | --- |
| `make help` | Lista los objetivos disponibles |
| `make install` | Restaura paquetes .NET e instala las dependencias del frontend |
| `make dev` | Levanta la API y el frontend juntos |
| `make api` / `make web` | Levanta solo la API o solo el frontend |
| `make start` | Levanta la app en segundo plano (sobrevive al cerrar la terminal/SSH) |
| `make stop` / `make restart` | Detiene o reinicia la app en segundo plano |
| `make status` / `make logs` | Muestra si está corriendo o sigue el log en vivo |
| `make install-browser` | Instala Chromium para el fallback de Playwright |
| `make install-tts` | Descarga Piper y la voz argentina para el audio local |
| `make ollama-pull` | Descarga `gemma4:e2b` en un Ollama local |
| `make docker-model` | Descarga `gemma4:e2b` en el contenedor de Ollama |
| `make test` | Ejecuta los tests de la API y del frontend |
| `make test-api` / `make test-web` | Ejecuta solo los tests de la API o del frontend |
| `make lint` | Ejecuta Oxlint sobre el frontend |
| `make build` | Compila la API y el frontend |
| `make check` | Ejecuta lint y tests |
| `make clean` | Elimina `bin`, `obj` y `dist` |
| `make docker-build` | Construye las imágenes de Docker |
| `make docker-up` | Levanta la app con Docker en `http://localhost:9090` |
| `make docker-down` | Detiene y elimina los contenedores |
| `make docker-logs` | Sigue los logs de los contenedores |

### Correr en un servidor (segundo plano)

`make dev` queda atado a la terminal, así que al cerrar la sesión SSH se corta. Para dejarlo corriendo en un servidor:

```bash
make start    # lanza la app en segundo plano
make status   # confirma que sigue viva
make logs     # sigue la salida de API y frontend (Ctrl+C no la detiene)
make stop     # la detiene
```

`make start` lanza todo en una sesión independiente (`setsid` + `nohup`), guarda el PID en `.run/app.pid` y escribe la salida en `.run/app.log`. Por eso el proceso sigue vivo aunque cierres la terminal o se caiga la conexión SSH, y `make stop` termina también los procesos hijos (`dotnet` y `vite`).

Si abrís la app desde tu máquina, los puertos del servidor no son accesibles como `localhost`; usá un túnel SSH:

```bash
ssh -L 5173:localhost:5173 -L 5251:localhost:5251 usuario@servidor
```

Después entrá a `http://localhost:5173` en tu navegador. Tené en cuenta que `make start` no sobrevive a un reinicio del servidor: para eso conviene registrar la aplicación como servicio de `systemd` o lanzarla dentro de `tmux`/`screen`.

## API

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/api/news` | Devuelve los titulares agregados (caché de 5 minutos). |
| `GET` | `/api/news?refresh=true` | Fuerza la re-descarga de todas las fuentes. |
| `GET` | `/api/news/content?url={url}` | Extrae el autor y el cuerpo completo de una nota. Solo acepta URLs de las fuentes configuradas. |
| `GET` | `/api/news/audio?url={url}&part=title\|attribution\|body&index={n}` | Devuelve el audio local de un segmento de la nota (Firefox). Requiere `make install-tts`. |
| `GET` | `/api/news/summary?url={url}` | Devuelve el resumen guardado de una nota o 404 si todavía no existe. |
| `POST` | `/api/news/summary?url={url}` | Genera y guarda el resumen; si ya existe, devuelve el guardado sin llamar al modelo. |
| `GET` | `/api/stories` | Agrupa los titulares en historias y devuelve la cobertura por medio. Es lo que consume la portada. |
| `GET` | `/api/stories?refresh=true` | Igual que el anterior, salteando la caché. |
| `GET` | `/api/stories?date=YYYY-MM-DD` | Devuelve una edición guardada de un día anterior (hoy + 2 días). No vuelve a descargar los medios. |

Ejemplo:

```bash
curl http://localhost:5251/api/news
```

Respuesta (fragmento):

```json
[
  {
    "id": "3f2c...",
    "title": "Titular de la nota",
    "summary": "Bajada o resumen de la nota.",
    "originalUrl": "https://www.lanacion.com.ar/...",
    "sourceName": "La Nación",
    "publishedDate": "2026-11-16T10:30:00Z"
  }
]
```

La caché de titulares se puede saltear con `refresh=true`; si todas las fuentes fallan, la API devuelve la última respuesta válida que tenga en memoria.

### Historias y cobertura

`/api/stories` agrupa los titulares que reportan el mismo hecho. Dos títulos se
consideran la misma historia cuando comparten al menos tres palabras
significativas y su similitud de conjunto de palabras llega a **0.50**; si hay
duda, quedan separados. Es una regla conservadora a propósito: una fusión
equivocada afirmaría que un medio cubrió algo que no cubrió.

```bash
curl http://localhost:5251/api/stories
```

```json
{
  "generatedAt": "2026-11-16T23:03:00Z",
  "articleCount": 240,
  "storyCount": 192,
  "stories": [
    {
      "title": "El papa León XIV fue golpeado en un ojo con un decenario…",
      "sourceName": "Clarín",
      "sourceCount": 7,
      "articleCount": 7,
      "latestPublishedDate": null,
      "sources": [
        {
          "sourceName": "Clarín",
          "title": "El papa León XIV fue golpeado en un ojo…",
          "originalUrl": "https://www.clarin.com/…",
          "publishedDate": null
        }
      ]
    }
  ]
}
```

`generatedAt` es la única hora que la portada muestra: `publishedDate` solo
existe cuando el medio publica la fecha real en su JSON-LD, y nunca se rellena
con la hora de la descarga.

La portada muestra las historias con más de un medio en una matriz, y las notas
de una sola voz en la banda de breves. El lector elige qué medio leer con los
chips del panel de lectura.

### Historial de ediciones

La API guarda una edición por día en `api/.archive/` (o en el volumen de Docker)
y conserva **hoy + los 2 días anteriores**. Cada respuesta de `/api/stories`
incluye `date` y `previousDays` con los días disponibles. La portada muestra una
sección “Días anteriores” y pide una edición solo cuando el lector elige ese día.

- `GET /api/stories?date=2026-11-16` devuelve la edición guardada.
- Un día sin archivo responde 404; una fecha inválida, futura o fuera de la
  ventana responde 400.
- El historial empieza a guardarse desde el despliegue. Si la app no corrió un
  día, ese día no aparece: no se rellena retroactivamente.
- Solo se guardan los metadatos de los artículos (título, resumen, URL, medio y
  fecha de publicación cuando existe), no los cuerpos de las notas.
- La fecha de edición se calcula en `America/Argentina/Buenos_Aires`; la caché de
  5 minutos no reutiliza una edición del día anterior después de medianoche.

## Fuentes soportadas

| Medio | Sitio |
| --- | --- |
| Clarín | https://www.clarin.com |
| La Nación | https://www.lanacion.com.ar |
| Infobae | https://www.infobae.com |
| Página/12 | https://www.pagina12.com.ar |
| TN | https://tn.com.ar |
| Ámbito Financiero | https://www.ambito.com |
| El Cronista | https://www.cronista.com |
| Perfil | https://www.perfil.com |
| C5N | https://www.c5n.com |
| La Voz del Interior | https://www.lavoz.com.ar |
| El Día | https://www.eldia.com |
| 0221 | https://www.0221.com.ar |
| Seúl | https://seul.ar |

Cada parser hereda de `HomePageParser`, que intenta extraer los titulares en este orden: datos estructurados JSON-LD, tarjetas `<article>`, encabezados con enlaces y, como último recurso, cualquier enlace con forma de nota.

## Tests

Los tests están en el mismo proyecto que la API:

```bash
dotnet test api/api.csproj
```

Usan xUnit, Moq y AngleSharp, y no necesitan lanzar un navegador real.

### Banco de pruebas del agrupamiento

El umbral de agrupamiento se calibra contra capturas reales del feed, no a ojo:

```bash
# con la API corriendo, guarda un snapshot del día en api/Tests/Fixtures/clustering/
./tools/capture-clustering-snapshot.sh

dotnet test api/api.csproj --filter FullyQualifiedName~ClusteringCorpusTests
cat api/bin/Debug/net8.0/clustering-report.txt
```

`api/Tests/Fixtures/clustering/labels.json` contiene pares etiquetados a mano:
los `same` tienen que compartir historia y los `different` tienen que quedar
separados entre 0.50 y 0.60. Mover el umbral sin pasar por ese contrato rompe
los tests. Corré un snapshot por día durante unos días antes de tocarlo.

## Cómo agregar una fuente

1. Crear un parser en `api/Services/Parsers/` heredando de `HomePageParser`:

   ```csharp
   public class MiDiarioParser : HomePageParser
   {
       public override string SourceName => "Mi Diario";
       public override string BaseUrl => "https://www.midiario.com";

       // Opcional: páginas de sección que complementan la portada.
       protected override string[] SectionPaths => new[] { "/politica", "/economia" };
   }
   ```

2. Registrarlo en el arreglo `Sources` de `api/Services/NewsAggregationService.cs`.
3. Si el sitio tiene secciones que no son notas (por ejemplo, `/podcasts/`), sobrescribir `AdditionalIgnoredPathTerms` o `MaxArticles` según haga falta. Las rutas de `SectionPaths` se resuelven contra `BaseUrl` y aportan hasta 12 notas cada una.

El endpoint de contenido valida automáticamente que la URL pertenezca a alguna de las fuentes registradas, así que una fuente nueva también queda habilitada para la lectura completa.

## Notas

- El scraping depende de la estructura HTML de cada medio: si un sitio cambia su maquetación, puede ser necesario ajustar su parser.
- El endpoint `/api/news/content` solo descarga URLs de los dominios configurados para evitar que se use como proxy abierto.
- CORS está habilitado únicamente para `http://localhost:5173` y `http://127.0.0.1:5173` (puerto por defecto de Vite).
- El proyecto usa [OpenSpec](openspec/) para documentar especificaciones y cambios; los comandos disponibles para el agente están en `.opencode/prompts/`.

## Licencia

MIT.
