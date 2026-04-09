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

export function fmtCurrency(n, currency) {
  if (currency === 'INR') return fmtINR(n);
  return fmtUSD(n);
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
