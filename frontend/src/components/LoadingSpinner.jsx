export function LoadingSpinner({ label = 'Loading…' }) {
  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      minHeight: '60vh',
      gap: 12,
      color: 'var(--color-text-tertiary)',
      fontSize: 13,
      fontFamily: 'var(--font-mono)',
    }}>
      <div style={{
        width: 24,
        height: 24,
        border: '2px solid var(--color-border-secondary)',
        borderTopColor: 'var(--color-text-primary)',
        borderRadius: '50%',
        animation: 'gwt-spin 0.7s linear infinite',
      }} />
      <style>{`@keyframes gwt-spin { to { transform: rotate(360deg); } }`}</style>
      {label}
    </div>
  );
}

// Inline skeleton for table rows while data loads
export function SkeletonRow({ cols = 6 }) {
  return (
    <tr>
      {Array.from({ length: cols }).map((_, i) => (
        <td key={i} style={{ padding: '11px 10px', borderBottom: '0.5px solid var(--color-border-tertiary)' }}>
          <div style={{
            height: 12,
            borderRadius: 4,
            background: 'var(--color-background-secondary)',
            width: i === 0 ? '80%' : '60%',
            animation: 'gwt-pulse 1.5s ease-in-out infinite',
          }} />
          <style>{`@keyframes gwt-pulse { 0%,100%{opacity:1} 50%{opacity:0.4} }`}</style>
        </td>
      ))}
    </tr>
  );
}
