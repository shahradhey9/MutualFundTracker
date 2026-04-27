import { usePortfolio, useRefreshNav } from '../hooks/usePortfolio.js';
import { useDisplayRates } from '../hooks/useDisplayRates.js';
import { HoldingsTable } from '../components/HoldingsTable.jsx';
import { CurrencySelector } from '../components/CurrencySelector.jsx';
import { SkeletonRow } from '../components/LoadingSpinner.jsx';
import { fmtCurrency, fmtPct } from '../lib/format.js';

function StatCard({ label, value, sub, subPositive, accent }) {
  return (
    <>
      <style>{`
        .stat-card {
          background: var(--bg-card);
          border: 1px solid var(--border-light);
          border-radius: var(--radius-lg);
          padding: 20px 22px;
          position: relative;
          overflow: hidden;
          box-shadow: var(--shadow-card);
          transition: box-shadow 0.15s;
        }
        .stat-card:hover { box-shadow: var(--shadow-md); }
        .stat-accent-bar { position: absolute; top: 0; left: 0; right: 0; height: 3px; border-radius: var(--radius-lg) var(--radius-lg) 0 0; }
        .stat-label { font-size: 11px; font-weight: 600; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; margin-bottom: 10px; font-family: var(--font-mono); }
        .stat-value { font-size: 24px; font-weight: 600; color: var(--text-primary); letter-spacing: -0.02em; line-height: 1; }
        .stat-sub { font-size: 12px; margin-top: 8px; font-weight: 500; }
      `}</style>
      <div className="stat-card">
        <div className="stat-accent-bar" style={{ background: accent || 'var(--accent)' }} />
        <div className="stat-label">{label}</div>
        <div className="stat-value">{value ?? '—'}</div>
        {sub && (
          <div className="stat-sub" style={{
            color: subPositive === true
              ? 'var(--color-gain)'
              : subPositive === false
              ? 'var(--color-loss)'
              : 'var(--text-muted)',
          }}>
            {sub}
          </div>
        )}
      </div>
    </>
  );
}

function SyncPill({ isFetching, dataUpdatedAt, inrPerUsd, hasHoldings, onRefresh, isRefreshing }) {
  const time = dataUpdatedAt
    ? new Date(dataUpdatedAt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
    : null;
  const busy = isFetching || isRefreshing;
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 22, flexWrap: 'wrap' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 12, color: 'var(--text-muted)' }}>
        <div style={{
          width: 7, height: 7, borderRadius: '50%',
          background: busy ? 'var(--color-warn)' : 'var(--color-gain)',
          boxShadow: busy ? '0 0 0 3px rgba(217,119,6,0.2)' : '0 0 0 3px rgba(22,163,74,0.2)',
          transition: 'background 0.3s',
        }} />
        {isRefreshing ? 'Refreshing NAV rates…' : isFetching ? 'Syncing…' : time ? `Synced at ${time}` : 'Loading…'}
      </div>
      {hasHoldings && (
        <div style={{
          fontSize: 12, color: 'var(--text-muted)',
          padding: '3px 10px',
          background: 'var(--bg-muted)',
          borderRadius: 'var(--radius-pill)',
          border: '1px solid var(--border-light)',
          fontFamily: 'var(--font-mono)',
        }}>
          USD/INR: <strong style={{ color: 'var(--text-primary)' }}>{inrPerUsd.toFixed(2)}</strong>
        </div>
      )}
      <CurrencySelector />
      <button
        className="btn btn-secondary"
        onClick={onRefresh}
        disabled={busy}
        style={{ fontSize: 12, padding: '4px 12px', display: 'flex', alignItems: 'center', gap: 6 }}
        title="Force-refresh NAV rates from AMFI and Yahoo Finance"
      >
        <svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
          style={{ animation: isRefreshing ? 'gwt-spin 0.8s linear infinite' : 'none' }}>
          <path d="M13.5 2.5A7 7 0 1 0 14 8" />
          <polyline points="14 2 14 6 10 6" />
        </svg>
        {isRefreshing ? 'Refreshing…' : 'Refresh NAV'}
      </button>
    </div>
  );
}

function SkeletonTable() {
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
      <tbody>{Array.from({ length: 3 }).map((_, i) => <SkeletonRow key={i} cols={7} />)}</tbody>
    </table>
  );
}

export function PortfolioPage() {
  const { data: holdings = [], isFetching, isLoading, dataUpdatedAt, isError, error } = usePortfolio();
  const { displayCurrency, convert, inrPerUsd } = useDisplayRates();
  const { mutate: refreshNav, isPending: isRefreshing } = useRefreshNav();

  if (isError) {
    return (
      <div style={{
        padding: '14px 18px', borderRadius: 'var(--radius-md)',
        border: '1px solid var(--color-loss)',
        background: 'var(--color-loss-bg)',
        color: 'var(--color-loss)', fontSize: 13,
      }}>
        Failed to load portfolio: {error?.message}
      </div>
    );
  }

  const indiaHoldings  = holdings.filter(h => h.region === 'INDIA');
  const globalHoldings = holdings.filter(h => h.region === 'GLOBAL');

  const indiaValue  = indiaHoldings.reduce((s, h) => s + (convert(h.currentValue, 'INR') ?? 0), 0);
  const globalValue = globalHoldings.reduce((s, h) => s + (convert(h.currentValue, 'USD') ?? 0), 0);
  const totalValue  = indiaValue + globalValue;

  const totalCost    = holdings.reduce((s, h) => s + (convert(h.costBasis, h.currency) ?? 0), 0);
  const totalGain    = holdings.reduce((s, h) => s + (convert(h.gain, h.currency) ?? 0), 0);
  const totalGainPct = totalCost > 0 ? (totalGain / totalCost) * 100 : null;

  return (
    <div>
      <SyncPill
        isFetching={isFetching}
        dataUpdatedAt={dataUpdatedAt}
        inrPerUsd={inrPerUsd}
        hasHoldings={holdings.length > 0}
        onRefresh={refreshNav}
        isRefreshing={isRefreshing}
      />

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0,1fr))', gap: 14, marginBottom: 24 }}>
        <StatCard
          label="Total net worth"
          value={isLoading ? '—' : holdings.length ? fmtCurrency(totalValue, displayCurrency) : '—'}
          sub={totalGainPct != null ? fmtPct(totalGainPct) + ' overall return' : holdings.length ? 'Add avg cost for P&L' : null}
          subPositive={totalGainPct != null ? totalGainPct >= 0 : null}
          accent="var(--accent)"
        />
        <StatCard
          label="India holdings"
          value={isLoading ? '—' : indiaHoldings.length ? fmtCurrency(indiaValue, displayCurrency) : '—'}
          sub={indiaHoldings.length ? `${indiaHoldings.length} fund${indiaHoldings.length > 1 ? 's' : ''}` : null}
          accent="#f59e0b"
        />
        <StatCard
          label="Global holdings"
          value={isLoading ? '—' : globalHoldings.length ? fmtCurrency(globalValue, displayCurrency) : '—'}
          sub={globalHoldings.length ? `${globalHoldings.length} ETF${globalHoldings.length > 1 ? 's' : ''}` : null}
          accent="#10b981"
        />
      </div>

      <div className="gwt-table-wrap">
        <div className="section-header">
          <span className="section-title">Holdings</span>
          {holdings.length > 0 && (
            <span style={{ fontSize: 12, color: 'var(--text-muted)', fontFamily: 'var(--font-mono)' }}>
              {holdings.length} position{holdings.length > 1 ? 's' : ''}
            </span>
          )}
        </div>
        {isLoading
          ? <div style={{ padding: 20 }}><SkeletonTable /></div>
          : <HoldingsTable holdings={holdings} displayCurrency={displayCurrency} convert={convert} />
        }
      </div>
    </div>
  );
}
