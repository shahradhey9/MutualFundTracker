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

const app = express();
const PORT = process.env.PORT || 4000;

// ── Security & parsing ───────────────────────────────────────────────────────
app.use(helmet());
app.use(cors({
  origin: process.env.FRONTEND_ORIGIN || 'http://localhost:5173',
  credentials: true,
}));
app.use(express.json({ limit: '100kb' }));

// ── Rate limiting ────────────────────────────────────────────────────────────
const limiter = rateLimit({
  windowMs: 60 * 1000,
  max: 120,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: 'Too many requests — please slow down.' },
});
app.use('/api/', limiter);

// ── Routes ───────────────────────────────────────────────────────────────────
app.use('/api/auth',      authRouter);
app.use('/api/funds',     fundsRouter);
app.use('/api/portfolio', portfolioRouter);
app.use('/api/admin',     adminRouter);

app.get('/health', async (_, res) => {
  try {
    await prisma.$queryRaw`SELECT 1`;
    res.json({ status: 'ok', db: 'connected', ts: new Date().toISOString() });
  } catch {
    res.status(503).json({ status: 'error', db: 'disconnected' });
  }
});

// ── 404 handler ──────────────────────────────────────────────────────────────
app.use((req, res) => {
  res.status(404).json({ error: `Route not found: ${req.method} ${req.path}` });
});

// ── Global error handler ─────────────────────────────────────────────────────
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

// ── Start ────────────────────────────────────────────────────────────────────
app.listen(PORT, () => {
  logger.info(`API server listening on :${PORT} [${process.env.NODE_ENV || 'development'}]`);
  startNavSyncJob();
});

// Graceful shutdown
process.on('SIGTERM', async () => {
  logger.info('SIGTERM received — shutting down');
  await prisma.$disconnect();
  process.exit(0);
});
