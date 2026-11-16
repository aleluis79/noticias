#!/usr/bin/env bash
# Captura un snapshot del feed para el banco de pruebas de agrupamiento.
#
#   ./tools/capture-clustering-snapshot.sh [API_BASE]
#
# Escribe api/Tests/Fixtures/clustering/<fecha>.json con un artículo por
# titular, que es lo único que necesita StoryGroupingService. Corré uno por
# día durante unos días antes de volver a mover el umbral.
set -euo pipefail

API_BASE="${1:-http://localhost:5251}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$ROOT/api/Tests/Fixtures/clustering"
STAMP="$(date -u +%Y-%m-%d)"
OUT="$OUT_DIR/$STAMP.json"

mkdir -p "$OUT_DIR"

if [ -f "$OUT" ]; then
  echo "Ya existe $OUT. Borralo si querés reemplazar la captura de hoy."
  exit 1
fi

curl -sf --max-time 120 "$API_BASE/api/stories" > /tmp/clustering-raw.json

python3 - "$OUT" <<'PY'
import json, sys

raw = json.load(open('/tmp/clustering-raw.json'))
articles = []
seen = set()
for story in raw['stories']:
    for a in story['sources']:
        key = (a['sourceName'], a['title'])
        if key in seen:
            continue
        seen.add(key)
        articles.append({'sourceName': a['sourceName'], 'title': a['title']})

json.dump(
    {'capturedAt': raw['generatedAt'], 'articleCount': len(articles), 'articles': articles},
    open(sys.argv[1], 'w'),
    ensure_ascii=False,
    indent=1,
)
print(f'{len(articles)} artículos en {sys.argv[1]}')
PY

rm -f /tmp/clustering-raw.json
