export function MetricCard({ label, value, sub, subPositive }) {
  return (
    <div style={{
      background: 'var(--color-background-secondary)',
      borderRadius: 'var(--border-radius-md)',
      padding: '14px 16px',
    }}>
      <div style={{
        fontSize: 12,
        color: 'var(--color-text-tertiary)',
        fontFamily: 'var(--font-mono)',
        marginBottom: 6,
      }}>
        {label}
      </div>
      <div style={{
        fontSize: 22,
        fontWeight: 500,
        color: 'var(--color-text-primary)',
        letterSpacing: '-0.02em',
      }}>
        {value ?? '—'}
      </div>
      {sub && (
        <div style={{
          fontSize: 12,
          marginTop: 4,
          fontFamily: 'var(--font-mono)',
          color: subPositive === true
            ? '#3B6D11'
            : subPositive === false
            ? '#A32D2D'
            : 'var(--color-text-tertiary)',
        }}>
          {sub}
        </div>
      )}
    </div>
  );
}
