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
        style={{
          appearance: 'none',
          WebkitAppearance: 'none',
          padding: '3px 24px 3px 10px',
          fontSize: 12,
          fontWeight: 500,
          fontFamily: 'inherit',
          border: '1px solid var(--color-border-secondary)',
          borderRadius: 20,
          background: 'var(--color-background-secondary)',
          color: 'var(--color-text-primary)',
          cursor: 'pointer',
          outline: 'none',
          lineHeight: '18px',
          minWidth: 80,
        }}
        title="Display currency for portfolio values"
      >
        {DISPLAY_CURRENCIES.map(({ code, flag }) => (
          <option key={code} value={code}>{flag} {code}</option>
        ))}
      </select>
      <span style={{
        position: 'absolute',
        right: 8,
        pointerEvents: 'none',
        fontSize: 9,
        color: 'var(--color-text-tertiary)',
      }}>
        ▾
      </span>
    </div>
  );
}
