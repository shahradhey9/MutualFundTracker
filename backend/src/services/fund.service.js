import { prisma } from '../lib/prisma.js';
import { searchAmfi, getAmfiNav } from './amfi.service.js';
import { searchYahoo, getYahooQuote } from './yahoo.service.js';
import { cacheGet, cacheSet, TTL } from '../lib/redis.js';
import { logger } from '../lib/logger.js';
import axios from 'axios';

// In-memory fallback for enriched search results (used when Redis is unavailable)
const _enrichedMem = new Map(); // key -> { data, expiry }

function memGet(key) {
  const entry = _enrichedMem.get(key);
  if (!entry) return null;
  if (Date.now() > entry.expiry) { _enrichedMem.delete(key); return null; }
  return entry.data;
}

function memSet(key, data, ttlSeconds) {
  _enrichedMem.set(key, { data, expiry: Date.now() + ttlSeconds * 1000 });
}

// Search funds — routes to correct data source by region
export async function searchFunds(query, region) {
  if (region === 'INDIA') {
    const results = await searchAmfi(query);
    return results.map(f => ({
      id: f.id,
      region: 'INDIA',
      name: f.name,
      amc: f.amc,
      ticker: f.ticker,
      schemeCode: f.schemeCode,
      category: f.category,
      latestNav: f.latestNav,
      navDate: f.navDate,
      currency: 'INR',
    }));
  }

  if (region === 'GLOBAL') {
    // Check enriched cache (search + quotes combined) to avoid 10 Yahoo API calls per search
    const enrichedKey = `yf:search-enriched:${query.toLowerCase()}`;
    const memCached = memGet(enrichedKey);
    if (memCached) return memCached;
    const redisCached = await cacheGet(enrichedKey);
    if (redisCached) { memSet(enrichedKey, redisCached, TTL.SEARCH_ENRICHED); return redisCached; }

    const results = await searchYahoo(query);
    if (!results || results.length === 0) {
      return [];
    }

    const top10 = results.slice(0, 10);
    const tickers = top10.map(f => f.ticker);
    const quotes = {};

    if (tickers.length > 0) {
      try {
        // --- BATCH FETCH QUOTES ---
        // Use Yahoo's v7 batch quote endpoint to get all prices in a single network request.
        const url = `https://query1.finance.yahoo.com/v7/finance/quote?symbols=${tickers.join(',')}`;
        const { data } = await axios.get(url, {
          timeout: 10000,
          headers: { 'User-Agent': 'GlobalWealthTracker/1.0' },
        });

        if (data?.quoteResponse?.result) {
          for (const quote of data.quoteResponse.result) {
            quotes[quote.symbol] = {
              price: quote.regularMarketPrice,
              currency: quote.currency,
              // Yahoo provides time as a unix epoch timestamp (seconds)
              navDate: quote.regularMarketTime ? new Date(quote.regularMarketTime * 1000) : null,
            };
          }
        }
      } catch (error) {
        logger.warn(`Yahoo batch quote fetch failed for query "${query}": ${error.message}`);
        // Proceed without prices if batch fetch fails; the UI can handle null NAVs.
      }
    }

    const enriched = top10.map(fund => ({
      ...fund,
      latestNav: quotes[fund.ticker]?.price ?? null,
      currency: quotes[fund.ticker]?.currency ?? 'USD',
      navDate: quotes[fund.ticker]?.navDate ? quotes[fund.ticker].navDate.toISOString() : null,
    }));

    // Cache enriched results so repeat searches skip the 10 quote API calls
    memSet(enrichedKey, enriched, TTL.SEARCH_ENRICHED);
    await cacheSet(enrichedKey, enriched, TTL.SEARCH_ENRICHED);
    return enriched;
  }

  throw new Error('Invalid region. Must be INDIA or GLOBAL');
}

// Ensure a fund exists in fund_meta table before creating a holding
// This is the "upsert on selection" pattern — fund catalogue grows organically
export async function ensureFundExists(fundData) {
  const { id, region, name, amc, ticker, schemeCode, category, isin } = fundData;

  // Fetch latest NAV at time of selection
  let latestNav = null;
  let navDate = null;

  if (region === 'INDIA' && schemeCode) {
    const nav = await getAmfiNav(schemeCode);
    latestNav = nav?.latestNav ?? null;
    navDate = nav?.navDate ? new Date(nav.navDate) : null;
  } else if (region === 'GLOBAL') {
    const quote = await getYahooQuote(ticker).catch(() => null);
    latestNav = quote?.price ?? null;
    navDate = quote?.navDate ? new Date(quote.navDate) : null;
  }

  return prisma.fundMeta.upsert({
    where: { ticker },
    update: {
      latestNav: latestNav ?? undefined,
      navDate: navDate ?? undefined,
      name, amc, category,
    },
    create: {
      id,
      region,
      name,
      amc,
      ticker,
      schemeCode: schemeCode ?? null,
      isin: isin ?? null,
      category: category ?? null,
      latestNav,
      navDate,
    },
  });
}
