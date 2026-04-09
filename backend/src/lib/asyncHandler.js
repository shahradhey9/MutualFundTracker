/**
 * Wraps an async Express route handler so that any rejected promise is
 * automatically forwarded to next(err) — the global error handler in index.js.
 *
 * Usage:
 *   router.get('/path', asyncHandler(async (req, res) => { ... }))
 */
export function asyncHandler(fn) {
  return (req, res, next) => {
    Promise.resolve(fn(req, res, next)).catch(next);
  };
}

/**
 * AppError — a typed error with an HTTP status code.
 * Throw this from services to produce a meaningful API response.
 *
 * Example:
 *   throw new AppError(404, 'Fund not found');
 *   throw new AppError(409, 'Holding already exists');
 */
export class AppError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
    this.name = 'AppError';
  }
}
