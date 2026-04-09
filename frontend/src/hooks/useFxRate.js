import { useQuery } from '@tanstack/react-query';

const FALLBACK_INR_PER_USD = 84; // used if API is down

/**
 * Fetches live INR/USD rate from Frankfurter (https://www.frankfurter.app)
 * Free, no API key, ~1 req/day is all we need.
 *
 * Returns: { inrPerUsd: number, usdPerInr: number, isLive: boolean }
 */
export function useFxRate() {
  const { data, isError } = useQuery({
    queryKey: ['fx', 'USD', 'INR'],
    queryFn: async () => {
      const res = await fetch('https://api.frankfurter.app/latest?from=USD&to=INR');
      if (!res.ok) throw new Error('FX API unavailable');
      const json = await res.json();
      const rate = json.rates?.INR;
      if (!rate) throw new Error('INR rate missing');
      return { inrPerUsd: rate, usdPerInr: 1 / rate, isLive: true };
    },
    staleTime: 6 * 60 * 60 * 1000,   // treat as fresh for 6 hours
    retry: 1,
    refetchOnWindowFocus: false,
  });

  if (data) return data;

  // Graceful fallback when API is unreachable
  return {
    inrPerUsd: FALLBACK_INR_PER_USD,
    usdPerInr: 1 / FALLBACK_INR_PER_USD,
    isLive: false,
  };
}

/**
 * Convert a portfolio value to USD using a live rate.
 * @param {Array} holdings
 * @param {number} inrPerUsd
 */
export function toUSD(holdings, inrPerUsd) {
  return holdings.reduce((sum, h) => {
    const val = h.currentValue ?? 0;
    return sum + (h.currency === 'INR' ? val / inrPerUsd : val);
  }, 0);
}
