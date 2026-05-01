export function fmtINR(n) {
  return '₹' + Number(n).toLocaleString('en-IN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

export function fmtUSD(n) {
  return '$' + Number(n).toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

// All currencies the UI supports for display.
export const DISPLAY_CURRENCIES = [
  { code: 'USD', label: 'US Dollar',          flag: '🇺🇸' },
  { code: 'INR', label: 'Indian Rupee',        flag: '🇮🇳' },
  { code: 'GBP', label: 'British Pound',       flag: '🇬🇧' },
  { code: 'EUR', label: 'Euro',                flag: '🇪🇺' },
  { code: 'CAD', label: 'Canadian Dollar',     flag: '🇨🇦' },
  { code: 'AUD', label: 'Australian Dollar',   flag: '🇦🇺' },
  { code: 'JPY', label: 'Japanese Yen',        flag: '🇯🇵' },
  { code: 'SGD', label: 'Singapore Dollar',    flag: '🇸🇬' },
  { code: 'CHF', label: 'Swiss Franc',         flag: '🇨🇭' },
  { code: 'AED', label: 'UAE Dirham',          flag: '🇦🇪' },
];

// Currencies that share the '$' symbol with USD — must use explicit prefixes
// to avoid ambiguity regardless of browser locale behaviour.
const DOLLAR_PREFIX = { CAD: 'CA$', AUD: 'A$', SGD: 'S$', HKD: 'HK$', NZD: 'NZ$' };

export function fmtCurrency(n, currency = 'USD') {
  if (n == null || isNaN(Number(n))) return '—';
  // INR keeps its traditional Indian grouping (e.g. ₹12,34,567.89)
  if (currency === 'INR') return fmtINR(n);
  // Dollar-sign currencies: prepend explicit prefix so CA$, A$, S$, HK$ are
  // never confused with US$.
  const dollarPrefix = DOLLAR_PREFIX[currency];
  if (dollarPrefix) {
    const formatted = new Intl.NumberFormat('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(Number(n));
    return dollarPrefix + formatted;
  }
  // All other currencies use Intl so the correct symbol is auto-applied
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: currency === 'JPY' ? 0 : 2,
    }).format(Number(n));
  } catch {
    return fmtUSD(n);
  }
}

export function fmtUnits(n) {
  return Number(n).toLocaleString('en-IN', {
    minimumFractionDigits: 3,
    maximumFractionDigits: 3,
  });
}

export function fmtPct(n, withSign = true) {
  const s = Number(n).toFixed(2) + '%';
  return withSign && n > 0 ? '+' + s : s;
}

export function fmtDate(d) {
  return new Date(d).toLocaleDateString('en-GB', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

// Approximate combined net worth in USD (India holdings converted at spot rate)
// In V2 this will use a live FX API
export function approxUSD(holdings, inrToUsd = 0.012) {
  return holdings.reduce((sum, h) => {
    const val = h.currentValue;
    return sum + (h.currency === 'INR' ? val * inrToUsd : val);
  }, 0);
}
