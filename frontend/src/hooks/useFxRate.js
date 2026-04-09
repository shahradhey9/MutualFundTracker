import { useQuery } from '@tanstack/react-query';

// Updated April 2026 — real market rate
const FALLBACK_INR_PER_USD = 84.47;

export function useFxRate() {
  const { data } = useQuery({
    queryKey: ['fx', 'USD', 'INR'],
    queryFn: async () => {
      // Try Frankfurter first (free, no key)
      try {
        const res = await fetch('https://api.frankfurter.app/latest?from=USD&to=INR');
        if (res.ok) {
          const json = await res.json();
          const rate = json.rates?.INR;
          if (rate && rate > 0) {
            return { inrPerUsd: rate, usdPerInr: 1 / rate, isLive: true, source: 'Frankfurter' };
          }
        }
      } catch (_) {}

      // Fallback: exchangerate-api (also free, no key for basic)
      try {
        const res = await fetch('https://open.er-api.com/v6/latest/USD');
        if (res.ok) {
          const json = await res.json();
          const rate = json.rates?.INR;
          if (rate && rate > 0) {
            return { inrPerUsd: rate, usdPerInr: 1 / rate, isLive: true, source: 'ExchangeRate-API' };
          }
        }
      } catch (_) {}

      throw new Error('All FX APIs unavailable');
    },
    staleTime: 4 * 60 * 60 * 1000,  // 4 hours
    retry: 1,
    refetchOnWindowFocus: false,
  });

  if (data) return data;

  return {
    inrPerUsd: FALLBACK_INR_PER_USD,
    usdPerInr: 1 / FALLBACK_INR_PER_USD,
    isLive: false,
    source: 'fallback',
  };
}

export function toUSD(holdings, inrPerUsd) {
  return holdings.reduce((sum, h) => {
    const val = h.currentValue ?? 0;
    return sum + (h.currency === 'INR' ? val / inrPerUsd : val);
  }, 0);
}
