# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**Global Wealth Tracker (GWT)** — a portfolio tracker for Indian mutual funds (AMFI) and global ETFs (Yahoo Finance/NASDAQ). Users add holdings, the app fetches live NAVs and shows P&L.

**Deployed on Render:** ASP.NET Core backend (Docker), React/Vite frontend (static), PostgreSQL 16 (managed).

---

## Commands

### Frontend (`/frontend`)
```bash
npm run dev        # Dev server on port 5173
npm run build      # Production build → dist/
npm run preview    # Preview production build locally
```

### Backend — .NET (`/backend-dotnet/src/GWT.Api`)
```bash
dotnet run                                # Dev server (auto-runs EF migrations on start)
dotnet build                              # Compile solution
dotnet publish -c Release                 # Production publish

# EF Core migrations (run from backend-dotnet/)
dotnet ef migrations add <Name> --project src/GWT.Infrastructure --startup-project src/GWT.Api
dotnet ef database update --project src/GWT.Infrastructure --startup-project src/GWT.Api
```

### Local infrastructure (Docker)
```bash
make up            # Start PostgreSQL:16 + Redis:7 containers
make down          # Stop containers
make setup         # Full first-time setup (up + install + migrate + seed)
make dev-backend   # Start Node.js legacy backend (port 4000)
make dev-frontend  # Start frontend dev server (port 5173)
```

### Backend — Node.js legacy (`/backend`) — not deployed, kept for reference
```bash
npm run dev        # Nodemon dev server (port 4000)
npm run db:migrate # Prisma migrations
npm run db:seed    # Load demo data (demo@gwt.dev / demo1234)
npm test           # Integration tests (server must be running)
```

---

## Architecture

### Deployed stack
The **ASP.NET Core backend** (`/backend-dotnet`) is what runs in production. The Node.js/Express backend (`/backend`) is a legacy implementation — same API contract, same DB schema, kept for reference but not deployed.

### Frontend data flow
```
React Query (usePortfolio / useFundSearch)
  → Axios (src/lib/api.js) — injects Bearer JWT from localStorage['gwt_token']
  → ASP.NET Core API (/api/*)
  → PostgreSQL via EF Core
```

### Backend Clean Architecture (`/backend-dotnet/src/`)

```
GWT.Api           — Controllers, middleware, Program.cs, Serilog config
GWT.Application   — Services, DTOs, validators (FluentValidation), interfaces
GWT.Infrastructure — EF Core DbContext + migrations, repositories, external clients, background jobs
GWT.Domain        — Entities, enums (Region: INDIA / GLOBAL)
```

DI wiring: `Program.cs` calls `services.AddApplication()` then `services.AddInfrastructure(config)`. Application registers scoped services. Infrastructure registers DbContext, Redis, repositories, typed HttpClients, and the hosted background service.

**Scoped vs Singleton matters:**
- `IAmfiService`, `IFxService`, `INasdaqService` — typed HttpClients (transient)
- `IYahooFinanceService` — **singleton** with a dedicated long-lived `HttpClient` + `CookieContainer`. Yahoo Finance requires a crumb tied to session cookies; `IHttpClientFactory` would rotate the handler and invalidate the crumb.
- `ICacheService` — singleton (Redis is thread-safe)
- All repositories and application services — scoped

### NAV data pipeline

**India (AMFI):**
1. `AmfiService.FetchAllNavsAsync` downloads `NAVAll.txt` (~2MB), parses ~7000 Growth-plan funds, caches in static `_memCache` (30-min TTL) and Redis.
2. `ForceRefreshAsync` resets `_memCacheExpiry = DateTime.MinValue` to bypass TTL unconditionally.
3. `BulkUpsertFundsAsync` (`INSERT … ON CONFLICT DO UPDATE`) persists to `fund_meta`.

**Global (Yahoo Finance):**
1. `YahooFinanceService` fetches a crumb at startup via `WarmUpAsync`, then uses it for all subsequent batch quote requests.
2. `FetchAndCacheGlobalNavsAsync` fetches quotes and updates in-memory `_navCache`.
3. `UpdateNavBatchAsync` persists prices to `fund_meta`.

**Background service (`NavSyncBackgroundService`):**
- Runs one loop per exchange timezone (IST / NY / London / Paris), each waiting until local midnight.
- Runs a separate `RunNavCacheRefreshLoopAsync` every 30 minutes that refreshes both India and Global caches in parallel with independent error handling via `RunWithGuardAsync`.
- After each refresh, calls `portfolioSvc.InvalidateAllCaches()` so stale pre-computed portfolio values are evicted.

### Caching layers (in priority order)

| Layer | India (AMFI) | Global (Yahoo) | Portfolio |
|---|---|---|---|
| 1. Process memory | `_memCache` (30-min TTL) | `_navCache` (in-memory) | `_memCache` per user (5-min TTL) |
| 2. Redis | `amfi:navall` (30 min) | keyed by ticker | `portfolio:{userId}` (1 min, Node.js only) |
| 3. DB (`fund_meta`) | fallback | fallback | always read on cache miss |
| 4. Live source | AMFI URL | Yahoo Finance API | — |

`FundService.SearchAsync` checks memory cache first, then DB, then live source — in that order.

### Portfolio page performance
`PortfolioService.GetPortfolioAsync` uses the per-user static `_memCache`. It reads Global NAVs from `_yahoo.GetGlobalNavSnapshot()` (in-memory, no HTTP) and India NAVs from `fund_meta.LatestNav` (stored by background refresh). No live HTTP calls on the portfolio hot path.

### Key gotcha — Npgsql 8 DateTimeKind
`DateTime.TryParseExact` with `DateTimeStyles.None` produces `Kind=Unspecified`. Npgsql 8+ throws when writing `Unspecified` to a `timestamp with time zone` column. Always use `DateTime.SpecifyKind(dt, DateTimeKind.Utc)` before persisting any date parsed from AMFI data.

### Postgres URL conversion
Render supplies the connection string as a `postgres://user:pass@host/db` URL. `DependencyInjection.ConvertPostgresUrl` converts it to Npgsql key=value format with `SSL Mode=Require;Trust Server Certificate=true` automatically.

### Startup sequence (`Program.cs`)
1. Apply EF Core migrations synchronously (blocking — schema must be correct before any request).
2. Start accepting HTTP requests (`app.StartAsync()`).
3. Fire-and-forget background task: fetch AMFI + Yahoo crumb + NASDAQ ETF catalogue in parallel, then bulk-seed `fund_meta` if records are sparse.
4. `NavSyncBackgroundService` starts independently via hosted service.

---

## Environment configuration

**Backend .NET** (appsettings.json / env overrides):
```
ConnectionStrings__DefaultConnection   Postgres DSN or postgres:// URL
ConnectionStrings__Redis               Redis DSN (optional; abortConnect=false)
Jwt__Secret                            HS256 signing key (required)
Jwt__Issuer / Jwt__Audience            gwt-api / gwt-client
AllowedOrigins__0                      CORS origin (e.g. https://gwt-frontend.onrender.com)
AdminKey                               Admin endpoint key
```

**Frontend** (`.env` or Render env vars):
```
VITE_API_URL    Backend API base URL (default: http://localhost:4000/api)
```

## Currency exchange rate update
Get the latest currency exchange rate for each currency against USD and update corresponding values

## Unit Tests
Create unit tests for each feature for existing and any new feature is getting added.
All unit tests should be passed before each deployment.
