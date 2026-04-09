const styles = {
  INDIA: {
    background: '#FAEEDA',
    color: '#854F0B',
  },
  GLOBAL: {
    background: '#E6F1FB',
    color: '#185FA5',
  },
};

export function RegionBadge({ region }) {
  const s = styles[region] || styles.GLOBAL;
  return (
    <span style={{
      display: 'inline-block',
      fontSize: 10,
      padding: '2px 7px',
      borderRadius: 20,
      fontFamily: 'var(--font-mono)',
      fontWeight: 500,
      marginLeft: 6,
      verticalAlign: 'middle',
      background: s.background,
      color: s.color,
    }}>
      {region === 'INDIA' ? 'AMFI' : 'Yahoo'}
    </span>
  );
}

export function GainPill({ pct }) {
  if (pct == null) return <span style={{ color: 'var(--color-text-tertiary)', fontSize: 11 }}>—</span>;
  const pos = pct >= 0;
  return (
    <span style={{
      display: 'inline-block',
      padding: '2px 8px',
      borderRadius: 20,
      fontSize: 11,
      fontFamily: 'var(--font-mono)',
      background: pos ? '#EAF3DE' : '#FCEBEB',
      color: pos ? '#3B6D11' : '#A32D2D',
    }}>
      {pos ? '+' : ''}{Number(pct).toFixed(2)}%
    </span>
  );
}
