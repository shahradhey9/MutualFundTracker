import 'dotenv/config';
import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import rateLimit from 'express-rate-limit';

import { logger } from './lib/logger.js';
import { prisma } from './lib/prisma.js';
import { startNavSyncJob } from './jobs/navSync.job.js';

import authRouter      from './routes/auth.routes.js';
import fundsRouter     from './routes/funds.routes.js';
import portfolioRouter from './routes/portfolio.routes.js';
import adminRouter     from './routes/admin.routes.js';

const app  = express();
const PORT = process.env.PORT || 4000;

// REQUIRED on Render/Railway — they sit behind a proxy that sets X-Forwarded-For
// Without this, express-rate-limit throws a ValidationError and crashes requests
app.set('trust proxy', 1);

// ── Security & parsing ────────────────────────────────────────────────────────
app.use(helmet({ crossOriginEmbedderPolicy: false }));
app.use(cors({
  origin: process.env.FRONTEND_ORIGIN || 'http://localhost:5173',
  credentials: true,
  methods: ['GET', 'POST', 'PATCH', 'DELETE', 'OPTIONS'],
}));
app.use(express.json({ limit: '100kb' }));

// ── Rate limiting ─────────────────────────────────────────────────────────────
app.use('/api/', rateLimit({
  windowMs: 60 * 1000,
  max: 200,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: 'Too many requests — please slow down.' },
}));

// ── Routes ────────────────────────────────────────────────────────────────────
app.use('/api/auth',      authRouter);
app.use('/api/funds',     fundsRouter);
app.use('/api/portfolio', portfolioRouter);
app.use('/api/admin',     adminRouter);

// ── Health check ──────────────────────────────────────────────────────────────
app.get('/health', async (_, res) => {
  try {
    await prisma.$queryRaw`SELECT 1`;
    res.json({ status: 'ok', db: 'connected', ts: new Date().toISOString() });
  } catch {
    res.status(503).json({ status: 'error', db: 'disconnected' });
  }
});

// ── 404 ───────────────────────────────────────────────────────────────────────
app.use((req, res) => {
  res.status(404).json({ error: `Not found: ${req.method} ${req.path}` });
});

// ── Global error handler ──────────────────────────────────────────────────────
app.use((err, req, res, _next) => {
  const status = err.status || 500;
  if (status >= 500) logger.error(err);
  else logger.warn(`${status} — ${err.message} [${req.method} ${req.path}]`);
  res.status(status).json({
    error: process.env.NODE_ENV === 'production' && status === 500
      ? 'Internal server error'
      : err.message,
  });
});

// ── Start ─────────────────────────────────────────────────────────────────────
app.listen(PORT, () => {
  logger.info(`GWT API :${PORT} [${process.env.NODE_ENV || 'development'}]`);
  startNavSyncJob();
});

process.on('SIGTERM', async () => {
  logger.info('SIGTERM — shutting down');
  await prisma.$disconnect();
  process.exit(0);
});
