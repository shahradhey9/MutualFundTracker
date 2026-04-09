/**
 * NAV Sync Job
 *
 * Runs daily at 8:30 PM IST (15:00 UTC) — shortly after Indian markets close.
 * For each fund held by any user:
 *   - India: fetch from AMFI NAVAll.txt
 *   - Global: fetch from Yahoo Finance
 * Updates fund_meta.latestNav and writes a row to nav_history for XIRR (V2).
 */

import cron from 'node-cron';
import { prisma } from '../lib/prisma.js';
import { fetchAllAmfiNavs } from '../services/amfi.service.js';
import { getBatchQuotes } from '../services/yahoo.service.js';
import { cacheDel } from '../lib/redis.js';
import { logger } from '../lib/logger.js';

async function runNavSync() {
  logger.info('[NavSync] Starting daily NAV sync...');
  const started = Date.now();

  try {
    // Get all funds currently held by any user
    const heldFunds = await prisma.fundMeta.findMany({
      where: { holdings: { some: {} } }, // only funds with at least one holding
    });

    if (!heldFunds.length) {
      logger.info('[NavSync] No held funds found — skipping');
      return;
    }

    const indiaFunds = heldFunds.filter(f => f.region === 'INDIA');
    const globalFunds = heldFunds.filter(f => f.region === 'GLOBAL');

    // ── India ──────────────────────────────────────────────────────────────
    if (indiaFunds.length) {
      logger.info(`[NavSync] Syncing ${indiaFunds.length} Indian funds from AMFI...`);
      const allAmfi = await fetchAllAmfiNavs();
      const amfiMap = Object.fromEntries(allAmfi.map(f => [f.schemeCode, f]));

      const indiaUpdates = indiaFunds
        .filter(f => f.schemeCode && amfiMap[f.schemeCode])
        .map(f => {
          const fresh = amfiMap[f.schemeCode];
          const navDate = fresh.navDate
            ? (() => { try { return new Date(fresh.navDate.split('-').reverse().join('-')); } catch { return new Date(); } })()
            : new Date();
          return { fund: f, nav: fresh.latestNav, navDate };
        });

      await Promise.all(
        indiaUpdates.map(({ fund, nav, navDate }) =>
          prisma.fundMeta.update({
            where: { id: fund.id },
            data: { latestNav: nav, navDate },
          })
        )
      );

      // Write nav_history rows (upsert — idempotent)
      const today = new Date(); today.setHours(0, 0, 0, 0);
      await prisma.$transaction(
        indiaUpdates.map(({ fund, nav, navDate }) =>
          prisma.navHistory.upsert({
            where: { fundId_navDate: { fundId: fund.id, navDate: navDate } },
            update: { nav },
            create: { fundId: fund.id, nav, navDate },
          })
        )
      );

      logger.info(`[NavSync] India: updated ${indiaUpdates.length} funds`);
    }

    // ── Global ─────────────────────────────────────────────────────────────
    if (globalFunds.length) {
      logger.info(`[NavSync] Syncing ${globalFunds.length} global funds from Yahoo Finance...`);
      const tickers = globalFunds.map(f => f.ticker);
      const quotes = await getBatchQuotes(tickers);

      const globalUpdates = globalFunds
        .filter(f => quotes[f.ticker])
        .map(f => ({ fund: f, quote: quotes[f.ticker] }));

      await Promise.all(
        globalUpdates.map(({ fund, quote }) => {
          const navDate = new Date(quote.navDate);
          return prisma.fundMeta.update({
            where: { id: fund.id },
            data: { latestNav: quote.price, navDate },
          });
        })
      );

      const today = new Date(); today.setHours(0, 0, 0, 0);
      await prisma.$transaction(
        globalUpdates.map(({ fund, quote }) => {
          const navDate = new Date(quote.navDate);
          return prisma.navHistory.upsert({
            where: { fundId_navDate: { fundId: fund.id, navDate } },
            update: { nav: quote.price },
            create: { fundId: fund.id, nav: quote.price, navDate },
          });
        })
      );

      logger.info(`[NavSync] Global: updated ${globalUpdates.length} funds`);
    }

    // Bust all portfolio caches so next request gets fresh data
    const users = await prisma.user.findMany({ select: { id: true } });
    await Promise.all(users.map(u => cacheDel(`portfolio:${u.id}`)));

    const elapsed = ((Date.now() - started) / 1000).toFixed(1);
    logger.info(`[NavSync] Done in ${elapsed}s`);
  } catch (err) {
    logger.error('[NavSync] Failed:', err);
  }
}

// Schedule: 8:30 PM IST = 15:00 UTC
export function startNavSyncJob() {
  cron.schedule('0 15 * * *', runNavSync, { timezone: 'UTC' });
  logger.info('[NavSync] Scheduled: daily at 15:00 UTC (8:30 PM IST)');
}

// Also export so it can be triggered manually via an admin endpoint
export { runNavSync };
