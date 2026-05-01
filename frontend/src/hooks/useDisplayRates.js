import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api.js';
import { useUIStore } from '../lib/store.js';

// Rough fallback rates (units of currency per 1 USD) — used before the live API responds.
const FALLBACK_FROM_USD = {
  USD: 1, INR: 84.47, GBP: 0.79, EUR: 0.93,
  CAD: 1.36, AUD: 1.54, JPY: 155, SGD: 1.35, CHF: 0.90, AED: 3.67,
  HKD: 7.82, HKD_fallback: 7.82,
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
 * Returns FX rates and a `convert(amount, nativeCurrency)` function that converts
 * any holding value to the user's chosen display currency.
 *
 * Pre-fetches USD/INR, USD/GBP and USD/EUR unconditionally because these are the
 * three most common native currencies (India, LSE, Euronext funds).
 * All other currencies fall back to FALLBACK_FROM_USD.
 */
export function useDisplayRates() {
  const displayCurrency = useUIStore(s => s.displayCurrency);

  // Always fetch USD/INR (India holdings + SyncPill), USD/GBP (LSE funds), USD/EUR (Euronext).
  const { data: inrPerUsdRaw } = useFxQuery('USD', 'INR');
  const { data: usdToGbpRaw }  = useFxQuery('USD', 'GBP');
  const { data: usdToEurRaw }  = useFxQuery('USD', 'EUR');

  // Fetch USD→displayCurrency only when it isn't already covered above.
  const coreRates = ['USD', 'INR', 'GBP', 'EUR'];
  const { data: usdToDisplayExtraRaw } = useFxQuery(
    'USD', displayCurrency,
    !coreRates.includes(displayCurrency),
  );

  // Build a map: currency code → units of that currency per 1 USD (live rate or fallback).
  const ratesFromUsd = {
    ...FALLBACK_FROM_USD,
    INR: inrPerUsdRaw ?? FALLBACK_FROM_USD.INR,
    GBP: usdToGbpRaw ?? FALLBACK_FROM_USD.GBP,
    EUR: usdToEurRaw ?? FALLBACK_FROM_USD.EUR,
  };

  // Resolve the effective rate for the selected display currency.
  const usdToDisplay = (() => {
    if (displayCurrency === 'USD') return 1;
    if (coreRates.includes(displayCurrency)) return ratesFromUsd[displayCurrency];
    return usdToDisplayExtraRaw ?? FALLBACK_FROM_USD[displayCurrency] ?? 1;
  })();
  ratesFromUsd[displayCurrency] = usdToDisplay;

  const inrPerUsd = ratesFromUsd.INR;

  /**
   * Convert `amount` (in `nativeCurrency`) to `displayCurrency`.
   * Triangulates via USD: nativeCurrency → USD → displayCurrency.
   */
  const convert = (amount, nativeCurrency) => {
    if (amount == null || isNaN(Number(amount))) return null;
    const n = Number(amount);
    if (nativeCurrency === displayCurrency) return n;

    const nativePerUsd = ratesFromUsd[nativeCurrency] ?? FALLBACK_FROM_USD[nativeCurrency] ?? 1;
    return (n / nativePerUsd) * usdToDisplay;
  };

  return { displayCurrency, convert, inrPerUsd, usdToDisplay };
}
