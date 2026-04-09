import { Router } from 'express';
import { z } from 'zod';
import {
  getPortfolio,
  upsertHolding,
  updateHolding,
  deleteHolding,
} from '../services/portfolio.service.js';
import { requireAuth } from '../middleware/auth.js';
import { asyncHandler, AppError } from '../lib/asyncHandler.js';

const router = Router();
router.use(requireAuth);

// GET /api/portfolio
router.get('/', asyncHandler(async (req, res) => {
  const holdings = await getPortfolio(req.userId);
  res.json({ holdings });
}));

// POST /api/portfolio/holdings
router.post('/holdings', asyncHandler(async (req, res) => {
  const schema = z.object({
    fundId: z.string().min(1),
    units: z.number().positive('Units must be a positive number'),
    avgCost: z.number().positive('Average cost must be positive').optional(),
    purchaseAt: z.string().min(1, 'Purchase date is required'),
  });

  const parsed = schema.safeParse(req.body);
  if (!parsed.success) {
    throw new AppError(400, parsed.error.errors.map(e => e.message).join(', '));
  }

  const holding = await upsertHolding(req.userId, parsed.data);
  res.status(201).json(holding);
}));

// PATCH /api/portfolio/holdings/:id
router.patch('/holdings/:id', asyncHandler(async (req, res) => {
  const schema = z.object({
    units: z.number().positive().optional(),
    avgCost: z.number().positive().optional(),
    purchaseAt: z.string().optional(),
  }).refine(data => Object.keys(data).length > 0, {
    message: 'At least one field must be provided for update',
  });

  const parsed = schema.safeParse(req.body);
  if (!parsed.success) {
    throw new AppError(400, parsed.error.errors.map(e => e.message).join(', '));
  }

  const updated = await updateHolding(req.userId, req.params.id, parsed.data);
  res.json(updated);
}));

// DELETE /api/portfolio/holdings/:id
router.delete('/holdings/:id', asyncHandler(async (req, res) => {
  await deleteHolding(req.userId, req.params.id);
  res.status(204).end();
}));

export default router;
