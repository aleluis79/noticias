#!/usr/bin/env bash
set -euo pipefail

# Downloads the Piper runtime and the Argentine Spanish voice used by the API.
# Everything lands under api/.tts, which is gitignored.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="${TTS_DIR:-$ROOT_DIR/api/.tts}"
PIPER_VERSION="${PIPER_VERSION:-2023.11.14-2}"
VOICE="${TTS_VOICE:-es_AR-daniela-high}"

PIPER_URL="https://github.com/rhasspy/piper/releases/download/${PIPER_VERSION}/piper_linux_x86_64.tar.gz"
VOICE_URL="https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_AR/daniela/high/${VOICE}.onnx"
VOICE_CONFIG_URL="${VOICE_URL}.json"

if [[ "$(uname -s)" != "Linux" || "$(uname -m)" != "x86_64" ]]; then
  echo "Este script instala el binario de Piper para Linux x86_64." >&2
  echo "En otro sistema, descargá Piper y el modelo, y configurá Tts__PiperPath y Tts__ModelPath." >&2
  exit 1
fi

for command in curl tar; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Falta $command. Instalalo y volvé a ejecutar el script." >&2
    exit 1
  fi
done

mkdir -p "$DEST/voices"

if [[ ! -x "$DEST/piper/piper" ]]; then
  echo "Descargando Piper ${PIPER_VERSION}…"
  curl -fsSL "$PIPER_URL" | tar -xz -C "$DEST"
else
  echo "Piper ya está instalado en $DEST/piper."
fi

if [[ ! -f "$DEST/voices/${VOICE}.onnx" ]]; then
  echo "Descargando la voz ${VOICE} (unos 110 MB)…"
  curl -fsSL -o "$DEST/voices/${VOICE}.onnx" "$VOICE_URL"
  curl -fsSL -o "$DEST/voices/${VOICE}.onnx.json" "$VOICE_CONFIG_URL"
else
  echo "La voz ${VOICE} ya está instalada."
fi

mkdir -p "$DEST/cache"

echo
echo "TTS local listo en $DEST"
echo "La API lo detecta con la configuración por defecto de appsettings.json."
echo "Si usás otras rutas, definí Tts__PiperPath, Tts__ModelPath y Tts__CacheDirectory."
echo "Opcional: instalá ffmpeg para servir MP3 en lugar de WAV."
