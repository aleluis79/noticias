.DEFAULT_GOAL := help

DOTNET ?= dotnet
PNPM ?= pnpm
API_PROJECT ?= api/api.csproj
API_URL ?= http://localhost:5251
WEB_DIR ?= web
WEB_URL ?= http://localhost:5173
RUN_DIR ?= $(CURDIR)/.run
PID_FILE ?= $(RUN_DIR)/app.pid
LOG_FILE ?= $(RUN_DIR)/app.log

.PHONY: help install install-browser dev api web start stop restart status logs test test-api lint build build-api build-web check clean

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

start: ## Levanta la app en segundo plano (sobrevive a cerrar SSH)
	@mkdir -p "$(RUN_DIR)"
	@if [ -f "$(PID_FILE)" ] && kill -0 "$$(cat "$(PID_FILE)")" 2>/dev/null; then \
		echo "La aplicación ya está corriendo (PID $$(cat "$(PID_FILE)"))."; \
		echo "Usá 'make logs' para ver la salida o 'make stop' para detenerla."; \
		exit 0; \
	fi; \
	echo "Iniciando la aplicación en segundo plano..."; \
	setsid nohup $(MAKE) --no-print-directory dev > "$(LOG_FILE)" 2>&1 < /dev/null & \
	echo $$! > "$(PID_FILE)"; \
	sleep 2; \
	if kill -0 "$$(cat "$(PID_FILE)")" 2>/dev/null; then \
		echo "Aplicación corriendo (PID $$(cat "$(PID_FILE)"))."; \
		echo "API: $(API_URL) | Web: $(WEB_URL)"; \
		echo "Logs: $(LOG_FILE)   (make logs)"; \
	else \
		echo "La aplicación no pudo iniciarse. Últimas líneas del log:"; \
		tail -n 20 "$(LOG_FILE)"; \
		rm -f "$(PID_FILE)"; \
		exit 1; \
	fi

stop: ## Detiene la app que corre en segundo plano
	@if [ ! -f "$(PID_FILE)" ]; then \
		echo "No hay ninguna instancia registrada en $(PID_FILE)."; \
		exit 0; \
	fi; \
	pid=$$(cat "$(PID_FILE)"); \
	if kill -0 "$$pid" 2>/dev/null; then \
		kill -TERM -"$$pid" 2>/dev/null || kill -TERM "$$pid" 2>/dev/null; \
		n=0; \
		while [ $$n -lt 20 ] && kill -0 "$$pid" 2>/dev/null; do sleep 0.5; n=$$((n + 1)); done; \
		if kill -0 "$$pid" 2>/dev/null; then \
			kill -KILL -"$$pid" 2>/dev/null || kill -KILL "$$pid" 2>/dev/null; \
			echo "La aplicación no respondió al cierre ordenado; se forzó la detención."; \
		else \
			echo "Aplicación detenida."; \
		fi; \
	else \
		echo "La instancia registrada (PID $$pid) ya no está corriendo."; \
	fi; \
	rm -f "$(PID_FILE)"

restart: stop start ## Reinicia la app en segundo plano

status: ## Muestra si la app está corriendo en segundo plano
	@if [ -f "$(PID_FILE)" ] && kill -0 "$$(cat "$(PID_FILE)")" 2>/dev/null; then \
		echo "Corriendo (PID $$(cat "$(PID_FILE)"))."; \
		echo "API: $(API_URL) | Web: $(WEB_URL)"; \
	else \
		echo "Detenida."; \
	fi

logs: ## Sigue el log de la app en segundo plano (Ctrl+C para salir)
	@if [ ! -f "$(LOG_FILE)" ]; then echo "Todavía no hay logs en $(LOG_FILE)."; exit 0; fi
	@tail -f "$(LOG_FILE)"

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
