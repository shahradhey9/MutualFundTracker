import { useFxRate } from '../hooks/useFxRate.js';

export function FxBanner() {
  const { inrPerUsd, isLive } = useFxRate();

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      gap: 8,
      padding: '6px 12px',
      marginBottom: '1rem',
      borderRadius: 'var(--border-radius-md)',
      background: 'var(--color-background-secondary)',
      fontSize: 11,
      fontFamily: 'var(--font-mono)',
      color: 'var(--color-text-tertiary)',
    }}>
      <span style={{
        display: 'inline-block',
        width: 6,
        height: 6,
        borderRadius: '50%',
        background: isLive ? '#3B6D11' : '#854F0B',
        flexShrink: 0,
      }} />
      <span>
        USD/INR: <strong style={{ color: 'var(--color-text-secondary)', fontWeight: 500 }}>
          {inrPerUsd.toFixed(2)}
        </strong>
        {' '}·{' '}
        {isLive
          ? 'live rate · Frankfurter API'
          : 'fallback rate — live API unavailable'}
        {' '}· Net worth shown in USD
      </span>
    </div>
  );
}
