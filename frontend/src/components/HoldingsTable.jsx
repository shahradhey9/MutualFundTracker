import { useState, useEffect } from 'react';
import { RegionBadge, GainPill } from './Badges.jsx';
import { fmtCurrency, fmtUnits, fmtDate } from '../lib/format.js';
import { useDeleteHolding } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';

const th = {
  padding: '6px 10px',
  fontSize: 11,
  fontWeight: 500,
  color: 'var(--color-text-tertiary)',
  fontFamily: 'var(--font-mono)',
  letterSpacing: '0.04em',
  borderBottom: '0.5px solid var(--color-border-tertiary)',
  textAlign: 'left',
  whiteSpace: 'nowrap',
};
const thR = { ...th, textAlign: 'right' };
const td = {
  padding: '11px 10px',
  borderBottom: '0.5px solid var(--color-border-tertiary)',
  verticalAlign: 'middle',
  fontSize: 13,
};
const tdR = { ...td, textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12 };

export function HoldingsTable({ holdings }) {
  const [hoveredRow, setHoveredRow] = useState(null);
  const { mutate: deleteHolding, isPending: deleting } = useDeleteHolding();
  const setEditingHolding = useUIStore(s => s.setEditingHolding);
  const setActiveTab = useUIStore(s => s.setActiveTab);
  const setOverlayMessage = useUIStore(s => s.setOverlayMessage);
  const clearOverlayMessage = useUIStore(s => s.clearOverlayMessage);

  useEffect(() => {
    if (deleting) setOverlayMessage('Removing holding…');
    else clearOverlayMessage();
  }, [deleting]);

  if (!holdings?.length) {
    return (
      <div style={{
        textAlign: 'center',
        padding: '3rem 1rem',
        border: '0.5px dashed var(--color-border-tertiary)',
        borderRadius: 'var(--border-radius-lg)',
        color: 'var(--color-text-tertiary)',
        fontSize: 13,
      }}>
        <div style={{ fontSize: 28, marginBottom: 10 }}>◎</div>
        <div>No holdings yet.</div>
        <div style={{ marginTop: 6 }}>
          <button
            onClick={() => setActiveTab('add')}
            style={{
              marginTop: 12,
              padding: '8px 18px',
              fontSize: 13,
              border: '0.5px solid var(--color-border-secondary)',
              borderRadius: 'var(--border-radius-md)',
              background: 'var(--color-text-primary)',
              color: 'var(--color-background-primary)',
              cursor: 'pointer',
            }}
          >
            Add your first holding
          </button>
        </div>
      </div>
    );
  }

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            <th style={th}>Fund</th>
            <th style={thR}>NAV / Price</th>
            <th style={thR}>Units</th>
            <th style={thR}>Current value</th>
            <th style={thR}>Cost basis</th>
            <th style={thR}>Gain / loss</th>
            <th style={thR}>Since</th>
            <th style={th}></th>
          </tr>
        </thead>
        <tbody>
          {holdings.map((h, i) => (
            <tr
              key={h.holdingId}
              onMouseEnter={() => setHoveredRow(i)}
              onMouseLeave={() => setHoveredRow(null)}
              style={{
                background: hoveredRow === i
                  ? 'var(--color-background-secondary)'
                  : 'transparent',
                transition: 'background 0.1s',
              }}
            >
              <td style={{ ...td, maxWidth: 320 }}>
                <div style={{ fontWeight: 500, color: 'var(--color-text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {h.name}
                  <RegionBadge region={h.region} />
                </div>
                <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>
                  {h.ticker} · {h.amc}
                </div>
              </td>
              <td style={tdR}>{fmtCurrency(h.liveNav, h.currency)}</td>
              <td style={tdR}>{fmtUnits(h.units)}</td>
              <td style={tdR}>{fmtCurrency(h.currentValue, h.currency)}</td>
              <td style={tdR}>{h.costBasis ? fmtCurrency(h.costBasis, h.currency) : '—'}</td>
              <td style={tdR}><GainPill pct={h.gainPct} /></td>
              <td style={tdR}>{fmtDate(h.purchaseAt)}</td>
              <td style={{ ...td, whiteSpace: 'nowrap' }}>
                <button
                  onClick={() => { setEditingHolding(h); setActiveTab('add'); }}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', fontSize: 13, padding: '2px 6px' }}
                  title="Edit"
                >
                  ✎
                </button>
                <button
                  onClick={() => {
                    if (confirm(`Remove ${h.name}?`)) deleteHolding(h.holdingId);
                  }}
                  disabled={deleting}
                  style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--color-text-tertiary)', fontSize: 13, padding: '2px 6px' }}
                  title="Remove"
                >
                  ✕
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
