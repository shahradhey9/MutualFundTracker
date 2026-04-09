import { verifyJwt } from '../routes/auth.routes.js';
import { logger } from '../lib/logger.js';

/**
 * requireAuth middleware
 * Verifies the Bearer JWT on every protected request.
 * Sets req.userId from the token's `sub` claim.
 *
 * Swap verifyJwt() for your auth provider's SDK in production:
 *   Clerk:  const { userId } = getAuth(req);
 *   Auth0:  req.auth.payload.sub
 */
export function requireAuth(req, res, next) {
  try {
    const header = req.headers.authorization;
    if (!header?.startsWith('Bearer ')) {
      return res.status(401).json({ error: 'Missing or invalid Authorization header' });
    }

    const token = header.slice(7);
    const payload = verifyJwt(token);
    req.userId = payload.sub;
    next();
  } catch (err) {
    logger.warn(`Auth rejected: ${err.message}`);
    return res.status(401).json({ error: 'Unauthorized' });
  }
}
