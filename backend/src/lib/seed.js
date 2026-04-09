/**
 * Seed script — run with: npm run db:seed
 *
 * Creates:
 *  - A demo user (demo@gwt.dev / demo1234)
 *  - 4 fund_meta entries (2 Indian MFs, 2 global ETFs)
 *  - 4 holdings linked to the demo user
 *
 * Safe to run multiple times — all upserts are idempotent.
 */

import 'dotenv/config';
import { PrismaClient } from '@prisma/client';
import { createHash } from 'crypto';

const prisma = new PrismaClient();

function hashPassword(password) {
  return createHash('sha256').update(password + process.env.JWT_SECRET).digest('hex');
}

async function main() {
  console.log('🌱 Seeding database...');

  // ── Demo user ──────────────────────────────────────────────────────────────
  const user = await prisma.user.upsert({
    where: { email: 'demo@gwt.dev' },
    update: {},
    create: {
      email: 'demo@gwt.dev',
      name: 'Demo Investor',
      passwordHash: hashPassword('demo1234'),
    },
  });
  console.log(`  User: ${user.email} (id: ${user.id})`);

  // ── Fund meta ──────────────────────────────────────────────────────────────
  const funds = [
    {
      id: 'IN-120503',
      region: 'INDIA',
      name: 'Parag Parikh Flexi Cap Fund — Direct Growth',
      amc: 'Parag Parikh AMC',
      ticker: 'AMFI-120503',
      schemeCode: '120503',
      isin: 'INF879O01027',
      category: 'Equity Scheme - Flexi Cap Fund',
      latestNav: 84.32,
      navDate: new Date(),
    },
    {
      id: 'IN-100033',
      region: 'INDIA',
      name: 'HDFC Mid-Cap Opportunities Fund — Direct Growth',
      amc: 'HDFC Asset Management Company Limited',
      ticker: 'AMFI-100033',
      schemeCode: '100033',
      isin: 'INF179K01VR5',
      category: 'Equity Scheme - Mid Cap Fund',
      latestNav: 121.47,
      navDate: new Date(),
    },
    {
      id: 'GL-VOO',
      region: 'GLOBAL',
      name: 'Vanguard S&P 500 ETF',
      amc: 'Vanguard',
      ticker: 'VOO',
      isin: 'US9229083632',
      category: 'ETF - US Large Cap Blend',
      latestNav: 487.22,
      navDate: new Date(),
    },
    {
      id: 'GL-QQQ',
      region: 'GLOBAL',
      name: 'Invesco QQQ Trust Series 1',
      amc: 'Invesco',
      ticker: 'QQQ',
      isin: 'US46090E1038',
      category: 'ETF - US Technology',
      latestNav: 431.88,
      navDate: new Date(),
    },
  ];

  for (const fund of funds) {
    await prisma.fundMeta.upsert({
      where: { ticker: fund.ticker },
      update: { latestNav: fund.latestNav, navDate: fund.navDate },
      create: fund,
    });
    console.log(`  Fund: ${fund.ticker} — ${fund.name.slice(0, 40)}…`);
  }

  // ── Holdings ───────────────────────────────────────────────────────────────
  const holdings = [
    { fundId: 'IN-120503', units: 150.000, avgCost: 68.50,  purchaseAt: new Date('2023-04-01') },
    { fundId: 'IN-100033', units: 80.000,  avgCost: 95.20,  purchaseAt: new Date('2022-11-15') },
    { fundId: 'GL-VOO',    units: 12.000,  avgCost: 380.00, purchaseAt: new Date('2023-01-10') },
    { fundId: 'GL-QQQ',    units: 8.000,   avgCost: 360.00, purchaseAt: new Date('2023-06-20') },
  ];

  for (const h of holdings) {
    await prisma.holding.upsert({
      where: { userId_fundId: { userId: user.id, fundId: h.fundId } },
      update: {},
      create: { userId: user.id, ...h },
    });
    console.log(`  Holding: ${h.fundId} × ${h.units} units`);
  }

  console.log('\n✅ Seed complete');
  console.log(`\nDemo login:\n  Email:    demo@gwt.dev\n  Password: demo1234\n  POST /api/auth/login to get a JWT`);
}

main()
  .catch(console.error)
  .finally(() => prisma.$disconnect());
