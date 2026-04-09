/**
 * fuzzyMatch.js
 *
 * Scores how similar two strings are (0–1).
 * Used to auto-match uploaded fund names against AMFI/Yahoo results.
 *
 * Strategy:
 *  1. Normalise both strings (lowercase, strip punctuation, common suffixes)
 *  2. Score by: exact match > all words present > bigram overlap > word overlap
 */

const STRIP = /[-–—().,&']/g;
const NOISE = /\b(fund|direct|growth|plan|regular|dividend|payout|reinvestment|option|scheme|series|fof|etf|index)\b/g;

function normalise(s) {
  return s
    .toLowerCase()
    .replace(STRIP, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function normaliseFull(s) {
  return normalise(s).replace(NOISE, '').replace(/\s+/g, ' ').trim();
}

function bigrams(s) {
  const set = new Set();
  for (let i = 0; i < s.length - 1; i++) set.add(s[i] + s[i + 1]);
  return set;
}

function bigramScore(a, b) {
  const ba = bigrams(a);
  const bb = bigrams(b);
  if (!ba.size || !bb.size) return 0;
  let common = 0;
  ba.forEach(bg => { if (bb.has(bg)) common++; });
  return (2 * common) / (ba.size + bb.size);
}

function wordOverlap(a, b) {
  const wa = new Set(a.split(' ').filter(w => w.length > 2));
  const wb = new Set(b.split(' ').filter(w => w.length > 2));
  if (!wa.size || !wb.size) return 0;
  let common = 0;
  wa.forEach(w => { if (wb.has(w)) common++; });
  return common / Math.max(wa.size, wb.size);
}

export function score(query, candidate) {
  const qn  = normalise(query);
  const cn  = normalise(candidate);
  const qf  = normaliseFull(query);
  const cf  = normaliseFull(candidate);

  // Exact match after normalisation
  if (qn === cn) return 1.0;
  if (qf === cf && qf.length > 3) return 0.97;

  // One contains the other
  if (cn.includes(qn) || qn.includes(cn)) return 0.9;
  if (cf.includes(qf) || qf.includes(cf)) return 0.85;

  // Weighted combination
  const bg = bigramScore(qf, cf);
  const wo = wordOverlap(qf, cf);
  return bg * 0.6 + wo * 0.4;
}

/**
 * Given a query string and a list of fund objects,
 * returns them sorted by match score, highest first.
 * Only returns results with score > threshold.
 */
export function rankMatches(query, funds, threshold = 0.25) {
  return funds
    .map(f => ({ fund: f, score: score(query, f.name) }))
    .filter(r => r.score >= threshold)
    .sort((a, b) => b.score - a.score);
}

/**
 * Returns confidence level string for display.
 */
export function confidence(s) {
  if (s >= 0.85) return 'high';
  if (s >= 0.55) return 'medium';
  return 'low';
}
