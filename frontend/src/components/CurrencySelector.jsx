import { useUIStore } from '../lib/store.js';
import { DISPLAY_CURRENCIES } from '../lib/format.js';

export function CurrencySelector() {
  const displayCurrency = useUIStore(s => s.displayCurrency);
  const setDisplayCurrency = useUIStore(s => s.setDisplayCurrency);

  return (
    <div style={{ position: 'relative', display: 'inline-flex', alignItems: 'center' }}>
      <select
        value={displayCurrency}
        onChange={e => setDisplayCurrency(e.target.value)}
        title="Display currency for portfolio values"
        style={{
          appearance: 'none',
          WebkitAppearance: 'none',
          padding: '5px 28px 5px 11px',
          fontSize: 12,
          fontWeight: 600,
          fontFamily: 'var(--font-mono)',
          border: '1.5px solid var(--border)',
          borderRadius: 'var(--radius-pill)',
          background: 'var(--bg-card)',
          color: 'var(--text-primary)',
          cursor: 'pointer',
          outline: 'none',
          lineHeight: '18px',
          minWidth: 88,
          transition: 'border-color 0.15s, box-shadow 0.15s',
        }}
        onFocus={e => { e.target.style.borderColor = 'var(--border-focus)'; e.target.style.boxShadow = '0 0 0 3px var(--accent-ring)'; }}
        onBlur={e => { e.target.style.borderColor = 'var(--border)'; e.target.style.boxShadow = 'none'; }}
      >
        {DISPLAY_CURRENCIES.map(({ code, flag }) => (
          <option key={code} value={code}>{flag} {code}</option>
        ))}
      </select>
      <span style={{
        position: 'absolute', right: 9,
        pointerEvents: 'none',
        fontSize: 10,
        color: 'var(--text-muted)',
      }}>
        ▾
      </span>
    </div>
  );
}
