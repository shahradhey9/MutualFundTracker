/**
 * AMFI Service
 * Source: https://www.amfiindia.com/spages/NAVAll.txt
 *
 * Format of NAVAll.txt (semicolon-delimited):
 * Scheme Code;ISIN Div Payout/ISIN Growth;ISIN Div Reinvestment;Scheme Name;Net Asset Value;Date
 *
 * The file has section headers like:
 * Open Ended Schemes(Equity Scheme - Flexi Cap Fund)
 * which we use to extract the category.
 */

import axios from 'axios';
import { logger } from '../lib/logger.js';
import { cacheGet, cacheSet, TTL } from '../lib/redis.js';

const AMFI_URL = process.env.AMFI_NAV_URL || 'https://www.amfiindia.com/spages/NAVAll.txt';

// Parse the raw AMFI flat file into structured objects
function parseAmfiText(raw) {
  const lines = raw.split('\n').map(l => l.trim()).filter(Boolean);
  const funds = [];
  let currentCategory = 'Unknown';
  let currentAmc = 'Unknown';

  for (const line of lines) {
    // Section headers look like: "Open Ended Schemes(Equity Scheme - Flexi Cap Fund)"
    if (line.startsWith('Open Ended') || line.startsWith('Close Ended') || line.startsWith('Interval')) {
      const match = line.match(/\((.+)\)/);
      currentCategory = match ? match[1] : line;
      continue;
    }

    // AMC name lines have no semicolons and are not headers
    if (!line.includes(';')) {
      if (line.length > 2 && !line.startsWith('-')) currentAmc = line;
      continue;
    }

    const parts = line.split(';');
    if (parts.length < 6) continue;

    const [schemeCode, isinGrowth, isinDiv, schemeName, navRaw, navDate] = parts;
    const nav = parseFloat(navRaw);
    if (!schemeCode || isNaN(nav) || nav <= 0) continue;

    // Only index Direct Growth plans to avoid duplicates
    const nameUpper = schemeName.toUpperCase();
    if (!nameUpper.includes('DIRECT') && !nameUpper.includes('DIR')) continue;
    if (!nameUpper.includes('GROWTH') && !nameUpper.includes('GR')) continue;

    funds.push({
      id: `IN-${schemeCode.trim()}`,
      region: 'INDIA',
      schemeCode: schemeCode.trim(),
      name: schemeName.trim(),
      amc: currentAmc.trim(),
      ticker: `AMFI-${schemeCode.trim()}`,
      isin: isinGrowth?.trim() || null,
      category: currentCategory,
      latestNav: nav,
      navDate: navDate?.trim() || null,
    });
  }

  return funds;
}

// In-memory store for the full parsed AMFI list (avoids re-parsing on every search)
let amfiCache = null;
let amfiCacheTime = 0;
const AMFI_MEM_TTL = 4 * 60 * 60 * 1000; // 4h in ms

export async function fetchAllAmfiNavs() {
  const now = Date.now();
  if (amfiCache && now - amfiCacheTime < AMFI_MEM_TTL) {
    return amfiCache;
  }

  const cacheKey = 'amfi:nav:all';
  const cached = await cacheGet(cacheKey);
  if (cached) {
    amfiCache = cached;
    amfiCacheTime = now;
    return cached;
  }

  logger.info('Fetching fresh NAVAll.txt from AMFI...');
  const response = await axios.get(AMFI_URL, {
    timeout: 30000,
    responseType: 'text',
    headers: { 'User-Agent': 'GlobalWealthTracker/1.0' },
  });

  const funds = parseAmfiText(response.data);
  logger.info(`AMFI: parsed ${funds.length} Direct Growth schemes`);

  await cacheSet(cacheKey, funds, TTL.NAV);
  amfiCache = funds;
  amfiCacheTime = now;
  return funds;
}

// Full-text search across name + AMC
export async function searchAmfi(query) {
  const all = await fetchAllAmfiNavs();
  const q = query.toLowerCase();
  return all
    .filter(f =>
      f.name.toLowerCase().includes(q) ||
      f.amc.toLowerCase().includes(q) ||
      f.schemeCode.includes(q)
    )
    .slice(0, 50); // cap results
}

// Get current NAV for a single scheme code
export async function getAmfiNav(schemeCode) {
  const cacheKey = `amfi:nav:${schemeCode}`;
  const cached = await cacheGet(cacheKey);
  if (cached) return cached;

  const all = await fetchAllAmfiNavs();
  const fund = all.find(f => f.schemeCode === schemeCode);
  if (fund) {
    await cacheSet(cacheKey, fund, TTL.NAV);
  }
  return fund || null;
}
