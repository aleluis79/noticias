# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY api.csproj ./
RUN dotnet restore api.csproj

COPY . ./
RUN dotnet publish api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage: the Playwright image already ships the Chromium browser and
# every system dependency the headless fallback needs.
FROM mcr.microsoft.com/playwright/dotnet:v1.43.0-jammy AS final
WORKDIR /app

# Local speech synthesis: Piper + FFmpeg. The voice model is downloaded at build
# time (about 110 MB) and never committed to the repository.
ARG PIPER_VERSION=2023.11.14-2
ARG TTS_VOICE=es_AR-daniela-high
ARG TTS_VOICE_URL=https://huggingface.co/rhasspy/piper-voices/resolve/main/es/es_AR/daniela/high/es_AR-daniela-high.onnx

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /opt/piper /opt/piper/voices /var/cache/noticias-tts /var/lib/noticias-archive \
    && curl -fsSL "https://github.com/rhasspy/piper/releases/download/${PIPER_VERSION}/piper_linux_x86_64.tar.gz" \
        | tar -xz -C /opt \
    && curl -fsSL -o "/opt/piper/voices/${TTS_VOICE}.onnx" "${TTS_VOICE_URL}" \
    && curl -fsSL -o "/opt/piper/voices/${TTS_VOICE}.onnx.json" "${TTS_VOICE_URL}.json"

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    Tts__Enabled=true \
    Tts__PiperPath=/opt/piper/piper \
    Tts__ModelPath=/opt/piper/voices/es_AR-daniela-high.onnx \
    Tts__FfmpegPath=/usr/bin/ffmpeg \
    Tts__CacheDirectory=/var/cache/noticias-tts \
    Archive__Directory=/var/lib/noticias-archive

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "api.dll"]
