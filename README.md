# Global Wealth Tracker

A production-grade web application for tracking Indian mutual funds and global ETFs in a single portfolio dashboard.

## Architecture

```
gwt/
├── backend/          Node.js + Express + Prisma
│   ├── prisma/       PostgreSQL schema & migrations
│   └── src/
│       ├── jobs/     node-cron daily NAV sync
│       ├── lib/      Redis cache, logger
│       ├── middleware/ JWT auth
│       ├── routes/   REST API endpoints
│       └── services/ AMFI, Yahoo Finance, Portfolio logic
└── frontend/         React + Vite + React Query + Zustand
    └── src/
        ├── components/ HoldingsTable, FundSearch, AddHoldingForm
        ├── hooks/    usePortfolio, useFundSearch (React Query)
        ├── lib/      Axios client, Zustand store, formatters
        └── pages/    PortfolioPage, AddHoldingPage, AnalyticsPage
```

## Data sources

| Region | Source | Mechanism |
|--------|--------|-----------|
| India  | AMFI NAVAll.txt | Fetched fresh daily, parsed in-process, cached in Redis 4h |
| Global | Yahoo Finance v8 | Per-ticker quote on demand, cached in Redis 4h |

## Quick start

### 1. Start infrastructure

```bash
docker-compose up -d
```

This starts PostgreSQL on `5432` and Redis on `6379`.

### 2. Backend

```bash
cd backend
cp .env.example .env        # edit DATABASE_URL, REDIS_URL
npm install
npm run db:migrate          # creates all tables
npm run dev                 # starts on :4000
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev                 # starts on :5173
```

Open http://localhost:5173

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/funds/search?q=&region=` | Live fund search (AMFI or Yahoo) |
| GET | `/api/funds/nav/:ticker?region=` | Single fund NAV |
| POST | `/api/funds/ensure` | Upsert fund into DB before adding holding |
| GET | `/api/portfolio` | Full portfolio with live NAVs |
| POST | `/api/portfolio/holdings` | Add / consolidate holding |
| PATCH | `/api/portfolio/holdings/:id` | Update holding |
| DELETE | `/api/portfolio/holdings/:id` | Remove holding |
| POST | `/api/admin/sync` | Manually trigger NAV sync job |
| GET | `/health` | Health check |

## NAV sync schedule

The `navSync.job.js` cron runs at **15:00 UTC (8:30 PM IST)** daily — shortly after Indian markets close and AMFI publishes NAVs. It:

1. Fetches all funds currently held by any user
2. For India: re-parses AMFI NAVAll.txt
3. For Global: batch-fetches from Yahoo Finance
4. Updates `fund_meta.latestNav` and `fund_meta.navDate`
5. Writes a row to `nav_history` (future XIRR calculation)
6. Busts all `portfolio:*` Redis cache keys

## Auth setup (required for production)

The middleware in `src/middleware/auth.js` has a stub — replace with:

**Clerk (recommended):**
```js
import { createClerkClient } from '@clerk/backend';
const clerk = createClerkClient({ secretKey: process.env.CLERK_SECRET_KEY });
const payload = await clerk.verifyToken(token);
req.userId = payload.sub;
```

**Auth0:**
```js
import { auth } from 'express-oauth2-jwt-bearer';
app.use(auth({ audience: process.env.AUTH0_AUDIENCE, issuerBaseURL: process.env.AUTH0_ISSUER }));
```

## V2 roadmap (from PRD)

- [ ] Live FX rate conversion (INR → USD) using Open Exchange Rates or Frankfurter API
- [ ] XIRR calculation using `nav_history` point-in-time NAVs
- [ ] Asset allocation breakdown by category (Equity / Debt / International)
- [ ] Push notifications when NAV moves >2% in a day
