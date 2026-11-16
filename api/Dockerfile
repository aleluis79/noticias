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

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "api.dll"]
