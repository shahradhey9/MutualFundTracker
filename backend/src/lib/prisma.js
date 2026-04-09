/**
 * Prisma singleton
 *
 * Node.js caches modules, so importing this file always returns the same
 * PrismaClient instance. This prevents the "too many connections" error that
 * occurs when each service file calls `new PrismaClient()` independently.
 *
 * In development, hot-reload would create a new instance on every file change,
 * so we attach the instance to globalThis to survive module re-evaluation.
 */
import { PrismaClient } from '@prisma/client';
import { logger } from './logger.js';

const globalForPrisma = globalThis;

export const prisma =
  globalForPrisma.prisma ??
  new PrismaClient({
    log: [
      { emit: 'event', level: 'query' },
      { emit: 'event', level: 'error' },
      { emit: 'event', level: 'warn' },
    ],
  });

if (process.env.NODE_ENV === 'development') {
  // Log slow queries in development
  prisma.$on('query', (e) => {
    if (e.duration > 200) {
      logger.warn(`Slow query (${e.duration}ms): ${e.query}`);
    }
  });
}

prisma.$on('error', (e) => logger.error('Prisma error:', e));

globalForPrisma.prisma = prisma;
