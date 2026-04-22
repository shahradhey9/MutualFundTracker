import { usePortfolio } from '../hooks/usePortfolio.js';
import { useDisplayRates } from '../hooks/useDisplayRates.js';
import { HoldingsTable } from '../components/HoldingsTable.jsx';
import { CurrencySelector } from '../components/CurrencySelector.jsx';
import { SkeletonRow } from '../components/LoadingSpinner.jsx';
import { fmtCurrency, fmtPct } from '../lib/format.js';

function StatCard({ label, value, sub, subPositive, accent }) {
  const accentColor = accent || '#2563eb';
  return (
    <>
      <style>{`
        .stat-card-wrap {
          background: #fff;
          border: 1px solid #e8e8e4;
          border-radius: 12px;
          padding: 20px 22px;
          position: relative;
          overflow: hidden;
        }
        @media (prefers-color-scheme: dark) {
          .stat-card-wrap { background: #1c1c1a !important; border-color: #2a2a28 !important; }
          .stat-label { color: #888780 !important; }
          .stat-value { color: #e8e8e4 !important; }
        }
        .stat-label { font-size: 12px; color: #888780; margin-bottom: 8px; font-weight: 500; text-transform: uppercase; letter-spacing: 0.05em; }
        .stat-value { font-size: 26px; font-weight: 500; color: #1a1a18; letter-spacing: -0.02em; line-height: 1; }
        .stat-accent { position: absolute; top: 0; left: 0; right: 0; height: 3px; border-radius: 12px 12px 0 0; }
      `}</style>
      <div className="stat-card-wrap">
        <div className="stat-accent" style={{ background: accentColor }} />
        <div className="stat-label">{label}</div>
        <div className="stat-value">{value ?? '—'}</div>
        {sub && (
          <div style={{
            fontSize: 12, marginTop: 8, fontWeight: 500,
            color: subPositive === true ? '#16a34a' : subPositive === false ? '#dc2626' : '#888780',
          }}>
            {sub}
          </div>
        )}
      </div>
    </>
  );
}

function SyncPill({ isFetching, dataUpdatedAt, inrPerUsd, hasHoldings }) {
  const time = dataUpdatedAt
    ? new Date(dataUpdatedAt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
    : null;
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 16, marginBottom: 20, flexWrap: 'wrap' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 12, color: '#888780' }}>
        <div style={{
          width: 7, height: 7, borderRadius: '50%',
          background: isFetching ? '#f59e0b' : '#16a34a',
          boxShadow: isFetching ? '0 0 0 2px rgba(245,158,11,0.2)' : '0 0 0 2px rgba(22,163,74,0.2)',
          transition: 'background 0.3s',
        }} />
        {isFetching ? 'Syncing live prices…' : time ? `Synced at ${time}` : 'Loading…'}
      </div>
      {hasHoldings && (
        <div style={{ fontSize: 12, color: '#888780', padding: '3px 10px', background: '#f4f4f2', borderRadius: 20, border: '1px solid #e8e8e4' }}>
          USD/INR: <strong style={{ color: '#1a1a18' }}>{inrPerUsd.toFixed(2)}</strong>
        </div>
      )}
      <CurrencySelector />
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

  if (isError) {
    return (
      <div style={{ padding: '16px 20px', borderRadius: 10, border: '1px solid #fecaca', background: '#fef2f2', color: '#b91c1c', fontSize: 14 }}>
        Failed to load portfolio: {error?.message}
      </div>
    );
  }

  const indiaHoldings  = holdings.filter(h => h.region === 'INDIA');
  const globalHoldings = holdings.filter(h => h.region === 'GLOBAL');

  // All values converted to the user's chosen display currency
  const indiaValue  = indiaHoldings.reduce((s, h) => s + (convert(h.currentValue, 'INR') ?? 0), 0);
  const globalValue = globalHoldings.reduce((s, h) => s + (convert(h.currentValue, 'USD') ?? 0), 0);
  const totalValue  = indiaValue + globalValue;

  const totalCost = holdings.reduce((s, h) => s + (convert(h.costBasis, h.currency) ?? 0), 0);
  const totalGain = holdings.reduce((s, h) => s + (convert(h.gain, h.currency) ?? 0), 0);
  const totalGainPct = totalCost > 0 ? (totalGain / totalCost) * 100 : null;

  return (
    <div>
      <SyncPill
        isFetching={isFetching}
        dataUpdatedAt={dataUpdatedAt}
        inrPerUsd={inrPerUsd}
        hasHoldings={holdings.length > 0}
      />

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0,1fr))', gap: 14, marginBottom: 28 }}>
        <StatCard
          label="Total net worth"
          value={isLoading ? '—' : holdings.length ? fmtCurrency(totalValue, displayCurrency) : '—'}
          sub={totalGainPct != null ? fmtPct(totalGainPct) + ' overall return' : holdings.length ? 'Add avg cost for P&L' : null}
          subPositive={totalGainPct != null ? totalGainPct >= 0 : null}
          accent="#2563eb"
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

      <div style={{ background: '#fff', border: '1px solid #e8e8e4', borderRadius: 12, overflow: 'hidden' }}>
        <div style={{ padding: '16px 20px', borderBottom: '1px solid #e8e8e4', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 14, fontWeight: 500, color: '#1a1a18' }}>Holdings</span>
          {holdings.length > 0 && (
            <span style={{ fontSize: 12, color: '#888780' }}>{holdings.length} position{holdings.length > 1 ? 's' : ''}</span>
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
