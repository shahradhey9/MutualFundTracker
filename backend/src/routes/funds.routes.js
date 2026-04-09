import { Router } from 'express';
import { z } from 'zod';
import { searchFunds, ensureFundExists } from '../services/fund.service.js';
import { getAmfiNav } from '../services/amfi.service.js';
import { getYahooQuote } from '../services/yahoo.service.js';
import { requireAuth } from '../middleware/auth.js';
import { asyncHandler, AppError } from '../lib/asyncHandler.js';

const router = Router();

const SearchSchema = z.object({
  q: z.string().min(2, 'Query must be at least 2 characters').max(100),
  region: z.enum(['INDIA', 'GLOBAL']),
});

// GET /api/funds/search?q=parag+parikh&region=INDIA
router.get('/search', asyncHandler(async (req, res) => {
  const parsed = SearchSchema.safeParse(req.query);
  if (!parsed.success) {
    throw new AppError(400, parsed.error.errors.map(e => e.message).join(', '));
  }
  const { q, region } = parsed.data;
  const results = await searchFunds(q, region);
  res.json({ results, count: results.length });
}));

// GET /api/funds/nav/:ticker?region=INDIA|GLOBAL
router.get('/nav/:ticker', asyncHandler(async (req, res) => {
  const { ticker } = req.params;
  const { region } = req.query;
  if (!region) throw new AppError(400, 'region query param required (INDIA or GLOBAL)');

  if (region === 'INDIA') {
    const schemeCode = ticker.replace('AMFI-', '');
    const data = await getAmfiNav(schemeCode);
    if (!data) throw new AppError(404, `No AMFI data found for scheme code: ${schemeCode}`);
    return res.json(data);
  }

  if (region === 'GLOBAL') {
    const quote = await getYahooQuote(ticker);
    return res.json(quote);
  }

  throw new AppError(400, 'region must be INDIA or GLOBAL');
}));

// POST /api/funds/ensure
router.post('/ensure', requireAuth, asyncHandler(async (req, res) => {
  const schema = z.object({
    id: z.string().min(1),
    region: z.enum(['INDIA', 'GLOBAL']),
    name: z.string().min(1),
    amc: z.string().min(1),
    ticker: z.string().min(1),
    schemeCode: z.string().optional(),
    category: z.string().optional(),
    isin: z.string().optional(),
  });

  const parsed = schema.safeParse(req.body);
  if (!parsed.success) {
    throw new AppError(400, parsed.error.errors.map(e => e.message).join(', '));
  }

  const fund = await ensureFundExists(parsed.data);
  res.json(fund);
}));

export default router;
