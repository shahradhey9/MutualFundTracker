export function RegionBadge({ region }) {
  return (
    <span className={`badge ${region === 'INDIA' ? 'badge-india' : 'badge-global'}`}
      style={{ marginLeft: 6, verticalAlign: 'middle' }}>
      {region === 'INDIA' ? 'AMFI' : 'Yahoo'}
    </span>
  );
}

export function GainPill({ pct }) {
  if (pct == null) return <span style={{ color: 'var(--text-muted)', fontSize: 11, fontFamily: 'var(--font-mono)' }}>—</span>;
  const pos = pct >= 0;
  return (
    <span className={`badge ${pos ? 'badge-gain' : 'badge-loss'}`}>
      {pos ? '+' : ''}{Number(pct).toFixed(2)}%
    </span>
  );
}
