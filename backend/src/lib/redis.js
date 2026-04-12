import Redis from 'ioredis';
import { logger } from './logger.js';

let client = null;

export function getRedis() {
  if (client) return client;

  client = new Redis(process.env.REDIS_URL || 'redis://localhost:6379', {
    lazyConnect: true,
    maxRetriesPerRequest: 1,
    enableOfflineQueue: false,
  });

  client.on('error', (err) => {
    logger.warn(`Redis error (cache disabled): ${err.message}`);
    client = null; // fall through to DB on next call
  });

  return client;
}

// TTLs in seconds
export const TTL = {
  NAV: 4 * 60 * 60,              // 4 hours — NAV only updates once a day
  SEARCH: 24 * 60 * 60,          // 24 hours — fund catalogue is stable
  SEARCH_ENRICHED: 4 * 60 * 60,  // 4 hours — enriched results include NAV prices
  PORTFOLIO: 60,                  // 1 minute — user-specific, short TTL
};

export async function cacheGet(key) {
  try {
    const r = getRedis();
    if (!r) return null;
    const val = await r.get(key);
    return val ? JSON.parse(val) : null;
  } catch {
    return null;
  }
}

export async function cacheSet(key, value, ttl) {
  try {
    const r = getRedis();
    if (!r) return;
    await r.set(key, JSON.stringify(value), 'EX', ttl);
  } catch {
    // cache miss is acceptable — just skip
  }
}

export async function cacheDel(key) {
  try {
    const r = getRedis();
    if (!r) return;
    await r.del(key);
  } catch {}
}
