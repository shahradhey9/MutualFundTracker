import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api.js';
import { useUIStore } from '../lib/store.js';

// Rough fallback rates (USD → currency) used before the live API responds.
const FALLBACK_FROM_USD = {
  USD: 1, INR: 84.47, GBP: 0.79, EUR: 0.93,
  CAD: 1.36, AUD: 1.54, JPY: 155, SGD: 1.35, CHF: 0.90, AED: 3.67,
};

function useFxQuery(from, to, enabled = true) {
  return useQuery({
    queryKey: ['fx', from, to],
    queryFn: async () => {
      const { data } = await api.get('/fx/rate', { params: { from, to } });
      if (data?.rate > 0) return data.rate;
      throw new Error('Invalid rate');
    },
    staleTime: 4 * 60 * 60 * 1000,  // 4 hours
    retry: 1,
    refetchOnWindowFocus: false,
    enabled,
  });
}

/**
 * Returns FX rates and a `convert(amount, nativeCurrency)` function that
 * converts any holding value to the user's chosen display currency.
 *
 * Also exposes `inrPerUsd` for the SyncPill (same query, no extra request).
 */
export function useDisplayRates() {
  const displayCurrency = useUIStore(s => s.displayCurrency);

  // Always keep USD/INR — needed for legacy toUSD logic and the SyncPill.
  const { data: inrPerUsdRaw } = useFxQuery('USD', 'INR');
  const inrPerUsd = inrPerUsdRaw ?? FALLBACK_FROM_USD.INR;

  // When displayCurrency is neither USD nor INR, fetch USD → displayCurrency.
  // We derive INR → displayCurrency via triangulation:
  //   INR → display = (1 / inrPerUsd) × usdToDisplay
  // This avoids a second HTTP request.
  const needsExtraFx = displayCurrency !== 'USD' && displayCurrency !== 'INR';
  const { data: usdToDisplayRaw } = useFxQuery('USD', displayCurrency, needsExtraFx);

  // Determine effective conversion rates
  let usdToDisplay, inrToDisplay;

  if (displayCurrency === 'USD') {
    usdToDisplay = 1;
    inrToDisplay = 1 / inrPerUsd;
  } else if (displayCurrency === 'INR') {
    usdToDisplay = inrPerUsd;
    inrToDisplay = 1;
  } else {
    const fallback = FALLBACK_FROM_USD[displayCurrency] ?? 1;
    usdToDisplay = usdToDisplayRaw ?? fallback;
    inrToDisplay = usdToDisplay / inrPerUsd;
  }

  /** Convert `amount` from `nativeCurrency` (INR or USD) to `displayCurrency`. */
  const convert = (amount, nativeCurrency) => {
    if (amount == null || isNaN(Number(amount))) return null;
    const n = Number(amount);
    if (nativeCurrency === displayCurrency) return n;
    if (nativeCurrency === 'USD') return n * usdToDisplay;
    if (nativeCurrency === 'INR') return n * inrToDisplay;
    return n; // unknown native currency — pass through
  };

  return { displayCurrency, convert, inrPerUsd, usdToDisplay, inrToDisplay };
}
