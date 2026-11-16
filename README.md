# Noticias Argentinas

Agregador de noticias que reúne los principales titulares de los diarios argentinos en una sola interfaz. El proyecto combina una API en **.NET 8** que descarga y normaliza las portadas de cada medio con un frontend en **React + TypeScript + Vite**.

## Características

- Titulares de 12 medios argentinos en una única respuesta.
- Extracción mediante HTML estático y fallback a un navegador Chromium headless para los sitios que renderizan con JavaScript.
- Caché en memoria de 5 minutos para los titulares y de 30 minutos para el cuerpo de cada nota.
- Lectura completa de la nota dentro de la propia interfaz, extrayendo y limpiando los párrafos del artículo original.
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
| `pnpm lint` | Lint con Oxlint |
| `pnpm preview` | Vista previa del build |

## API

| Método | Ruta | Descripción |
| --- | --- | --- |
| `GET` | `/api/news` | Devuelve los titulares agregados (caché de 5 minutos). |
| `GET` | `/api/news?refresh=true` | Fuerza la re-descarga de todas las fuentes. |
| `GET` | `/api/news/content?url={url}` | Extrae el cuerpo completo de una nota. Solo acepta URLs de las fuentes configuradas. |

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

## Fuentes soportadas

| Medio | Sitio |
| --- | --- |
| Clarín | https://www.clarin.com |
| La Nación | https://www.lanacion.com.ar |
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

## Cómo agregar una fuente

1. Crear un parser en `api/Services/Parsers/` heredando de `HomePageParser`:

   ```csharp
   public class MiDiarioParser : HomePageParser
   {
       public override string SourceName => "Mi Diario";
       public override string BaseUrl => "https://www.midiario.com";
   }
   ```

2. Registrarlo en el arreglo `Sources` de `api/Services/NewsAggregationService.cs`.
3. Si el sitio tiene secciones que no son notas (por ejemplo, `/podcasts/`), sobrescribir `AdditionalIgnoredPathTerms` o `MaxArticles` según haga falta.

El endpoint de contenido valida automáticamente que la URL pertenezca a alguna de las fuentes registradas, así que una fuente nueva también queda habilitada para la lectura completa.

## Notas

- El scraping depende de la estructura HTML de cada medio: si un sitio cambia su maquetación, puede ser necesario ajustar su parser.
- El endpoint `/api/news/content` solo descarga URLs de los dominios configurados para evitar que se use como proxy abierto.
- CORS está habilitado únicamente para `http://localhost:5173` y `http://127.0.0.1:5173` (puerto por defecto de Vite).
- El proyecto usa [OpenSpec](openspec/) para documentar especificaciones y cambios; los comandos disponibles para el agente están en `.opencode/prompts/`.

## Licencia

ISC.
