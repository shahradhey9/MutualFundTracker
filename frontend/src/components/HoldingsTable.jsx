import { useDeleteHolding } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';
import { RegionBadge, GainPill } from './Badges.jsx';
import { fmtCurrency, fmtUnits, fmtDate } from '../lib/format.js';
import { useEffect } from 'react';

export function HoldingsTable({ holdings, displayCurrency = 'USD', convert = (v) => v }) {
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
        padding: '3.5rem 1.5rem',
        color: 'var(--text-muted)',
      }}>
        <div style={{ fontSize: 32, marginBottom: 12, opacity: 0.4 }}>◎</div>
        <div style={{ fontSize: 14, fontWeight: 500, color: 'var(--text-secondary)', marginBottom: 6 }}>
          No holdings yet
        </div>
        <div style={{ fontSize: 13, marginBottom: 20 }}>
          Add your first fund to start tracking
        </div>
        <button className="btn btn-primary" onClick={() => setActiveTab('add')}>
          Add your first holding
        </button>
      </div>
    );
  }

  return (
    <div style={{ overflowX: 'auto' }}>
      <table className="gwt-table">
        <thead>
          <tr>
            <th>Fund</th>
            <th className="right">NAV / Price</th>
            <th className="right">Units</th>
            <th className="right">Current value</th>
            <th className="right">Cost basis</th>
            <th className="right">Gain / Loss</th>
            <th className="right">Since</th>
            <th style={{ width: 72 }}></th>
          </tr>
        </thead>
        <tbody>
          {holdings.map(h => (
            <tr key={h.holdingId}>
              <td style={{ maxWidth: 300 }}>
                <div style={{ fontWeight: 500, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {h.name}
                  <RegionBadge region={h.region} />
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>
                  {h.ticker}{h.amc && ` · ${h.amc}`}
                </div>
              </td>
              <td className="td-mono">{fmtCurrency(h.liveNav, h.currency)}</td>
              <td className="td-mono">{fmtUnits(h.units)}</td>
              <td className="td-mono">{fmtCurrency(convert(h.currentValue, h.currency), displayCurrency)}</td>
              <td className="td-mono">{h.costBasis ? fmtCurrency(convert(h.costBasis, h.currency), displayCurrency) : <span style={{ color: 'var(--text-muted)' }}>—</span>}</td>
              <td className="td-mono"><GainPill pct={h.gainPct} /></td>
              <td className="td-mono">{fmtDate(h.purchaseAt)}</td>
              <td>
                <div style={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                  <button
                    className="btn btn-ghost"
                    onClick={() => { setEditingHolding(h); setActiveTab('add'); }}
                    title="Edit holding"
                  >
                    ✎
                  </button>
                  <button
                    className="btn btn-danger-ghost"
                    onClick={() => { if (confirm(`Remove ${h.name}?`)) deleteHolding(h.holdingId); }}
                    disabled={deleting}
                    title="Remove holding"
                  >
                    ✕
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
