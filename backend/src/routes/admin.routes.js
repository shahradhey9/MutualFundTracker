import { Router } from 'express';
import { runNavSync } from '../jobs/navSync.job.js';
import { logger } from '../lib/logger.js';

const router = Router();

// Simple API key guard for admin routes
function adminOnly(req, res, next) {
  const key = req.headers['x-admin-key'];
  if (!key || key !== process.env.ADMIN_KEY) {
    return res.status(403).json({ error: 'Forbidden' });
  }
  next();
}

// POST /api/admin/sync — trigger NAV sync manually
router.post('/sync', adminOnly, async (req, res) => {
  logger.info('Manual NAV sync triggered via admin API');
  // Run async — don't block the HTTP response
  runNavSync().catch(err => logger.error('Manual sync error:', err));
  res.json({ message: 'NAV sync started' });
});

export default router;
