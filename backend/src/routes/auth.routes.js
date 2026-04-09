/**
 * Auth routes — /api/auth
 *
 * Development: issues self-signed JWTs so you can test the API without
 * an external auth provider. Set NODE_ENV=development and JWT_SECRET in .env.
 *
 * Production: remove the /register and /login routes entirely and rely on
 * your auth provider (Clerk, Auth0) to issue tokens. The requireAuth
 * middleware is the only thing that needs to change.
 */

import { Router } from 'express';
import { z } from 'zod';
import { createHash } from 'crypto';
import { prisma } from '../lib/prisma.js';
import { asyncHandler, AppError } from '../lib/asyncHandler.js';
import { logger } from '../lib/logger.js';

const router = Router();

// ── Minimal JWT implementation (no external deps) ────────────────────────────
function base64url(str) {
  return Buffer.from(str).toString('base64')
    .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}

function signJwt(payload) {
  const secret = process.env.JWT_SECRET;
  if (!secret) throw new Error('JWT_SECRET not set');

  const header = base64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = base64url(JSON.stringify({
    ...payload,
    iat: Math.floor(Date.now() / 1000),
    exp: Math.floor(Date.now() / 1000) + 60 * 60 * 24 * 7, // 7 days
  }));

  const sig = createHash('sha256')
    .update(`${header}.${body}${secret}`)
    .digest('base64url');

  return `${header}.${body}.${sig}`;
}

export function verifyJwt(token) {
  const secret = process.env.JWT_SECRET;
  if (!secret) throw new Error('JWT_SECRET not set');

  const parts = token.split('.');
  if (parts.length !== 3) throw new Error('Invalid token format');

  const [header, body, sig] = parts;
  const expected = createHash('sha256')
    .update(`${header}.${body}${secret}`)
    .digest('base64url');

  if (sig !== expected) throw new Error('Invalid token signature');

  const payload = JSON.parse(Buffer.from(body, 'base64url').toString());
  if (payload.exp < Math.floor(Date.now() / 1000)) throw new Error('Token expired');

  return payload;
}

// Simple password hash — replace with bcrypt in production
function hashPassword(password) {
  return createHash('sha256').update(password + process.env.JWT_SECRET).digest('hex');
}

// ── POST /api/auth/register ──────────────────────────────────────────────────
router.post('/register', asyncHandler(async (req, res) => {
  if (process.env.NODE_ENV === 'production') {
    throw new AppError(404, 'Not found');
  }

  const schema = z.object({
    email: z.string().email(),
    name: z.string().min(1).optional(),
    password: z.string().min(6),
  });

  const parsed = schema.safeParse(req.body);
  if (!parsed.success) {
    throw new AppError(400, parsed.error.errors.map(e => e.message).join(', '));
  }

  const { email, name, password } = parsed.data;
  const existing = await prisma.user.findUnique({ where: { email } });
  if (existing) throw new AppError(409, 'Email already registered');

  const user = await prisma.user.create({
    data: { email, name: name || email.split('@')[0], passwordHash: hashPassword(password) },
    select: { id: true, email: true, name: true },
  });

  logger.info(`New user registered: ${email}`);
  const token = signJwt({ sub: user.id, email: user.email });
  res.status(201).json({ token, user });
}));

// ── POST /api/auth/login ─────────────────────────────────────────────────────
router.post('/login', asyncHandler(async (req, res) => {
  if (process.env.NODE_ENV === 'production') {
    throw new AppError(404, 'Not found');
  }

  const schema = z.object({
    email: z.string().email(),
    password: z.string().min(1),
  });

  const parsed = schema.safeParse(req.body);
  if (!parsed.success) {
    throw new AppError(400, parsed.error.errors.map(e => e.message).join(', '));
  }

  const { email, password } = parsed.data;
  const user = await prisma.user.findUnique({ where: { email } });

  if (!user || user.passwordHash !== hashPassword(password)) {
    throw new AppError(401, 'Invalid email or password');
  }

  const token = signJwt({ sub: user.id, email: user.email });
  res.json({ token, user: { id: user.id, email: user.email, name: user.name } });
}));

// ── GET /api/auth/me ─────────────────────────────────────────────────────────
router.get('/me', asyncHandler(async (req, res) => {
  const header = req.headers.authorization;
  if (!header?.startsWith('Bearer ')) throw new AppError(401, 'No token');

  const payload = verifyJwt(header.slice(7));
  const user = await prisma.user.findUnique({
    where: { id: payload.sub },
    select: { id: true, email: true, name: true, createdAt: true },
  });
  if (!user) throw new AppError(404, 'User not found');
  res.json(user);
}));

export default router;
