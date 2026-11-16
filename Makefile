.DEFAULT_GOAL := help

DOTNET ?= dotnet
PNPM ?= pnpm
API_PROJECT ?= api/api.csproj
API_URL ?= http://localhost:5251
WEB_DIR ?= web

.PHONY: help install install-browser dev api web test test-api lint build build-api build-web check clean

help: ## Muestra esta ayuda
	@awk 'BEGIN {FS = ":.*## "; printf "\nUso:\n  make <objetivo>\n\nObjetivos:\n"} /^[a-zA-Z_-]+:.*?## / { printf "  \033[36m%-18s\033[0m %s\n", $$1, $$2 }' $(MAKEFILE_LIST)
	@echo ""

install: ## Instala las dependencias de la API y del frontend
	$(DOTNET) restore $(API_PROJECT)
	cd $(WEB_DIR) && $(PNPM) install

install-browser: ## Instala Chromium para el fallback de Playwright
	$(DOTNET) build $(API_PROJECT) --nologo
	pwsh api/bin/Debug/net8.0/playwright.ps1 install chromium

dev: ## Levanta la API y el frontend juntos
	@echo "API: $(API_URL) | Web: http://localhost:5173"
	@$(DOTNET) run --project $(API_PROJECT) & \
	api_pid=$$!; \
	trap 'kill $$api_pid 2>/dev/null' INT TERM EXIT; \
	cd $(WEB_DIR) && $(PNPM) dev; \
	kill $$api_pid 2>/dev/null

api: ## Levanta solo la API en http://localhost:5251
	$(DOTNET) run --project $(API_PROJECT)

web: ## Levanta solo el frontend en http://localhost:5173
	cd $(WEB_DIR) && $(PNPM) dev

test: test-api ## Ejecuta todos los tests

test-api: ## Ejecuta los tests de la API
	$(DOTNET) test $(API_PROJECT) --nologo

lint: ## Ejecuta Oxlint sobre el frontend
	cd $(WEB_DIR) && $(PNPM) lint

build: build-api build-web ## Compila la API y el frontend

build-api: ## Compila la API
	$(DOTNET) build $(API_PROJECT) --nologo

build-web: ## Compila el frontend
	cd $(WEB_DIR) && $(PNPM) build

check: lint test ## Ejecuta lint y tests

clean: ## Elimina los artefactos de build (bin, obj, dist)
	$(DOTNET) clean $(API_PROJECT) --nologo
	rm -rf $(WEB_DIR)/dist
