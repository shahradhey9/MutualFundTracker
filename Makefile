.PHONY: up down install migrate seed dev test

# Start PostgreSQL + Redis
up:
	docker-compose up -d
	@echo "Waiting for Postgres to be ready..."
	@sleep 3

# Stop infra
down:
	docker-compose down

# Install all deps
install:
	cd backend && npm install
	cd frontend && npm install

# Run DB migrations
migrate:
	cd backend && npm run db:migrate

# Seed demo data
seed:
	cd backend && npm run db:seed

# Start both API server and frontend dev server (requires tmux or run in two terminals)
dev-backend:
	cd backend && npm run dev

dev-frontend:
	cd frontend && npm run dev

# Full first-time setup
setup: up install migrate seed
	@echo ""
	@echo "✅ Setup complete. Now run:"
	@echo "   make dev-backend   (in one terminal)"
	@echo "   make dev-frontend  (in another terminal)"
	@echo ""
	@echo "   Demo login: demo@gwt.dev / demo1234"

# Run integration tests (server must be running)
test:
	cd backend && npm test
