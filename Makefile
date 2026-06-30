# Hawkeye Trading Bot — developer convenience targets.
#
# Thin wrappers around the Docker Compose stack in deploy/. Run from the repo root.
# Compose reads deploy/.env automatically; copy deploy/.env.example to deploy/.env first.

COMPOSE = docker compose -f deploy/docker-compose.yml

.DEFAULT_GOAL := help

.PHONY: help up down build logs ps clean db migrate psql verify

help: ## List available targets
	@grep -hE '^[a-zA-Z_-]+:.*?## ' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "} {printf "  \033[36m%-10s\033[0m %s\n", $$1, $$2}'

## --- Docker stack ---

up: ## Build and start the whole stack (detached)
	$(COMPOSE) up -d --build

down: ## Stop and remove containers
	$(COMPOSE) down

build: ## Build all images
	$(COMPOSE) build

logs: ## Follow logs from all services
	$(COMPOSE) logs -f

ps: ## Show service status
	$(COMPOSE) ps

clean: ## Stop the stack and drop volumes (wipes the TimescaleDB data)
	$(COMPOSE) down -v

## --- DB / migrations ---

db: ## Start just the TimescaleDB service (detached)
	$(COMPOSE) up -d timescale-db

migrate: ## Run the one-shot migrators for both modules (waits for healthy DB, then exits)
	$(COMPOSE) up --build marketdata-migrations strategy-migrations

psql: ## Open a psql shell in the running TimescaleDB container (creds from deploy/.env)
	@set -a; [ -f deploy/.env ] && . ./deploy/.env; set +a; \
		docker exec -it timescale-db psql -U "$${POSTGRES_USER:-hawkeye}" -d "$${POSTGRES_DB:-hawkeye}"

## --- Verify-mode run ---

verify: ## Run the marketdata loader in verify mode (re-scans and upserts mismatches)
	HTB_VERIFY=true $(COMPOSE) up -d --build marketdata-loader
