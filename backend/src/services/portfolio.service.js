import { prisma } from '../lib/prisma.js';
import { getBatchQuotes } from './yahoo.service.js';
import { getAmfiNav } from './amfi.service.js';
import { cacheGet, cacheSet, cacheDel, TTL } from '../lib/redis.js';
import { logger } from '../lib/logger.js';

// Get full portfolio for a user, enriched with live NAVs
export async function getPortfolio(userId) {
  const cacheKey = `portfolio:${userId}`;
  const cached = await cacheGet(cacheKey);
  if (cached) return cached;

  const holdings = await prisma.holding.findMany({
    where: { userId },
    include: { fund: true },
    orderBy: { purchaseAt: 'desc' },
  });

  if (!holdings.length) return [];

  // Batch-fetch live prices
  const indiaFunds = holdings.filter(h => h.fund.region === 'INDIA');
  const globalFunds = holdings.filter(h => h.fund.region === 'GLOBAL');

  // India: check if stored NAV is fresh (same day), else re-fetch
  const indiaNavs = {};
  for (const h of indiaFunds) {
    const fresh = await getAmfiNav(h.fund.schemeCode);
    if (fresh) indiaNavs[h.fund.id] = fresh.latestNav;
    else indiaNavs[h.fund.id] = Number(h.fund.latestNav);
  }

  // Global: batch Yahoo Finance
  const globalTickers = [...new Set(globalFunds.map(h => h.fund.ticker))];
  const globalQuotes = globalTickers.length
    ? await getBatchQuotes(globalTickers)
    : {};

  const enriched = holdings.map(h => {
    const isIndia = h.fund.region === 'INDIA';
    const liveNav = isIndia
      ? (indiaNavs[h.fund.id] ?? Number(h.fund.latestNav))
      : (globalQuotes[h.fund.ticker]?.price ?? Number(h.fund.latestNav));

    const units = Number(h.units);
    const avgCost = h.avgCost ? Number(h.avgCost) : null;
    const currentValue = units * liveNav;
    const costBasis = avgCost ? units * avgCost : null;
    const gain = costBasis ? currentValue - costBasis : null;
    const gainPct = costBasis ? (gain / costBasis) * 100 : null;

    return {
      holdingId: h.id,
      fundId: h.fund.id,
      name: h.fund.name,
      ticker: h.fund.ticker,
      amc: h.fund.amc,
      category: h.fund.category,
      region: h.fund.region,
      units,
      avgCost,
      purchaseAt: h.purchaseAt,
      liveNav,
      navDate: h.fund.navDate,
      currency: isIndia ? 'INR' : (globalQuotes[h.fund.ticker]?.currency ?? 'USD'),
      currentValue: +currentValue.toFixed(4),
      costBasis: costBasis ? +costBasis.toFixed(4) : null,
      gain: gain ? +gain.toFixed(4) : null,
      gainPct: gainPct ? +gainPct.toFixed(4) : null,
    };
  });

  await cacheSet(cacheKey, enriched, TTL.PORTFOLIO);
  return enriched;
}

// Add or consolidate a holding
export async function upsertHolding(userId, { fundId, units, avgCost, purchaseAt }) {
  // Auto-consolidation: if holding exists, merge units and weighted avg cost
  const existing = await prisma.holding.findUnique({
    where: { userId_fundId: { userId, fundId } },
  });

  let result;
  if (existing) {
    const oldUnits = Number(existing.units);
    const newUnits = oldUnits + Number(units);

    let newAvgCost = existing.avgCost ? Number(existing.avgCost) : null;
    if (avgCost && existing.avgCost) {
      // Weighted average cost
      newAvgCost = (oldUnits * Number(existing.avgCost) + Number(units) * Number(avgCost)) / newUnits;
    } else if (avgCost) {
      newAvgCost = Number(avgCost);
    }

    result = await prisma.holding.update({
      where: { userId_fundId: { userId, fundId } },
      data: {
        units: newUnits,
        avgCost: newAvgCost,
        // keep oldest purchase date as the anchor
        purchaseAt: existing.purchaseAt < new Date(purchaseAt)
          ? existing.purchaseAt
          : new Date(purchaseAt),
      },
      include: { fund: true },
    });
  } else {
    result = await prisma.holding.create({
      data: {
        userId,
        fundId,
        units: Number(units),
        avgCost: avgCost ? Number(avgCost) : null,
        purchaseAt: new Date(purchaseAt),
      },
      include: { fund: true },
    });
  }

  await cacheDel(`portfolio:${userId}`);
  return result;
}

// Update units / avg cost for an existing holding
export async function updateHolding(userId, holdingId, updates) {
  const holding = await prisma.holding.findFirst({
    where: { id: holdingId, userId },
  });
  if (!holding) throw new Error('Holding not found');

  const updated = await prisma.holding.update({
    where: { id: holdingId },
    data: {
      units: updates.units !== undefined ? Number(updates.units) : undefined,
      avgCost: updates.avgCost !== undefined ? Number(updates.avgCost) : undefined,
      purchaseAt: updates.purchaseAt ? new Date(updates.purchaseAt) : undefined,
    },
    include: { fund: true },
  });

  await cacheDel(`portfolio:${userId}`);
  return updated;
}

// Remove a holding
export async function deleteHolding(userId, holdingId) {
  const holding = await prisma.holding.findFirst({
    where: { id: holdingId, userId },
  });
  if (!holding) throw new Error('Holding not found');

  await prisma.holding.delete({ where: { id: holdingId } });
  await cacheDel(`portfolio:${userId}`);
}
