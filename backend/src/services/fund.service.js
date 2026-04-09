import { prisma } from '../lib/prisma.js';
import { searchAmfi, getAmfiNav } from './amfi.service.js';
import { searchYahoo, getYahooQuote } from './yahoo.service.js';
import { logger } from '../lib/logger.js';

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
    const results = await searchYahoo(query);
    // Fetch prices for top 10 results in parallel (don't slow down search)
    const top10 = results.slice(0, 10);
    const pricePromises = top10.map(f =>
      getYahooQuote(f.ticker).catch(() => null)
    );
    const prices = await Promise.all(pricePromises);
    return top10.map((f, i) => ({
      ...f,
      latestNav: prices[i]?.price ?? null,
      currency: prices[i]?.currency ?? 'USD',
      navDate: prices[i]?.navDate ?? null,
    }));
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
