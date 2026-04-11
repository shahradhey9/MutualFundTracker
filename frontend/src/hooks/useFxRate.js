import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api.js';

// Used when the backend FX endpoint is unavailable
const FALLBACK_INR_PER_USD = 84.47;

export function useFxRate() {
  const { data } = useQuery({
    queryKey: ['fx', 'USD', 'INR'],
    queryFn: async () => {
      // Proxy through our backend to avoid browser CORS restrictions on third-party FX APIs
      const { data: json } = await api.get('/fx/rate', { params: { from: 'USD', to: 'INR' } });
      const rate = json?.rate;
      if (rate && rate > 0) {
        return { inrPerUsd: rate, usdPerInr: 1 / rate, isLive: json.isLive ?? true, source: 'Backend' };
      }
      throw new Error('FX rate unavailable');
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
