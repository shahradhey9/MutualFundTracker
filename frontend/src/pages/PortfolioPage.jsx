import { usePortfolio } from '../hooks/usePortfolio.js';
import { useDisplayRates } from '../hooks/useDisplayRates.js';
import { HoldingsTable } from '../components/HoldingsTable.jsx';
import { CurrencySelector } from '../components/CurrencySelector.jsx';
import { SkeletonRow } from '../components/LoadingSpinner.jsx';
import { fmtCurrency, fmtPct } from '../lib/format.js';

function StatCard({ label, value, sub, subPositive, accent, icon }) {
  return (
    <>
      <style>{`
        .stat-card {
          background: var(--bg-card);
          border: 1px solid var(--border-light);
          border-radius: var(--radius-xl);
          padding: 22px 24px 18px;
          position: relative; overflow: hidden;
          box-shadow: var(--shadow-card);
          transition: box-shadow 0.18s, transform 0.18s;
        }
        .stat-card:hover { box-shadow: var(--shadow-md); transform: translateY(-1px); }
        .stat-icon-wrap {
          width: 42px; height: 42px; border-radius: var(--radius-lg);
          display: flex; align-items: center; justify-content: center;
          margin-bottom: 14px; flex-shrink: 0;
        }
        .stat-label { font-size: 11px; font-weight: 600; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.07em; margin-bottom: 6px; }
        .stat-value { font-size: 26px; font-weight: 700; color: var(--text-primary); letter-spacing: -0.03em; line-height: 1.1; }
        .stat-sub { font-size: 12px; margin-top: 8px; font-weight: 500; display: flex; align-items: center; gap: 4px; }
        .stat-divider { position: absolute; left: 0; top: 0; bottom: 0; width: 4px; border-radius: var(--radius-xl) 0 0 var(--radius-xl); }
      `}</style>
      <div className="stat-card">
        <div className="stat-divider" style={{ background: accent || 'var(--accent)' }} />
        <div className="stat-icon-wrap" style={{ background: (accent || 'var(--accent)') + '18' }}>
          {icon || (
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke={accent || 'var(--accent)'} strokeWidth="1.8" strokeLinecap="round">
              <path d="M2 14l4-4 3 3 5-6 4 3"/>
            </svg>
          )}
        </div>
        <div className="stat-label">{label}</div>
        <div className="stat-value">{value ?? '—'}</div>
        {sub && (
          <div className="stat-sub" style={{
            color: subPositive === true ? 'var(--color-gain)'
                 : subPositive === false ? 'var(--color-loss)'
                 : 'var(--text-muted)',
          }}>
            {subPositive === true && <span>▲</span>}
            {subPositive === false && <span>▼</span>}
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
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 22, flexWrap: 'wrap' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 12, color: 'var(--text-muted)' }}>
        <div style={{
          width: 7, height: 7, borderRadius: '50%',
          background: isFetching ? 'var(--color-warn)' : 'var(--color-gain)',
          boxShadow: isFetching ? '0 0 0 3px rgba(217,119,6,0.2)' : '0 0 0 3px rgba(22,163,74,0.2)',
          transition: 'background 0.3s',
        }} />
        {isFetching ? 'Syncing…' : time ? `Synced at ${time}` : 'Loading…'}
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
    </div>
  );
}

const EXCHANGE_COUNTRY = {
  'NYSE Arca': 'USA', 'NYSE': 'USA', 'NASDAQ': 'USA', 'Cboe': 'USA', 'BATS': 'USA',
  'Paris': 'France', 'Euronext Paris': 'France',
  'London': 'UK', 'LSE': 'UK',
  'Frankfurt': 'Germany', 'XETRA': 'Germany',
  'Amsterdam': 'Netherlands',
  'Tokyo': 'Japan', 'TSE': 'Japan',
  'Sydney': 'Australia', 'ASX': 'Australia',
  'Hong Kong': 'Hong Kong', 'HKEX': 'Hong Kong',
  'Toronto': 'Canada', 'TSX': 'Canada',
  'Singapore': 'Singapore', 'SGX': 'Singapore',
  'Zurich': 'Switzerland', 'SIX': 'Switzerland',
  'Milan': 'Italy', 'Madrid': 'Spain', 'Stockholm': 'Sweden',
  'Copenhagen': 'Denmark', 'Oslo': 'Norway', 'Helsinki': 'Finland',
  'Brussels': 'Belgium', 'Vienna': 'Austria', 'Warsaw': 'Poland',
  'Seoul': 'South Korea', 'KRX': 'South Korea',
  'Shanghai': 'China', 'Shenzhen': 'China',
  'Mumbai': 'India', 'NSE': 'India', 'BSE': 'India',
};
const CURRENCY_COUNTRY = {
  USD: 'USA', EUR: 'Europe', GBP: 'UK', JPY: 'Japan',
  CAD: 'Canada', AUD: 'Australia', HKD: 'Hong Kong', SGD: 'Singapore',
  CHF: 'Switzerland', KRW: 'South Korea', CNY: 'China', INR: 'India',
  BRL: 'Brazil', MXN: 'Mexico', ZAR: 'South Africa',
};

function getHoldingCountry(h) {
  if (h.amc) {
    const match = Object.entries(EXCHANGE_COUNTRY).find(([ex]) => h.amc.includes(ex));
    if (match) return match[1];
  }
  return CURRENCY_COUNTRY[h.currency] || h.currency;
}

function GlobalHoldingsCard({ globalHoldings, isLoading }) {
  return (
    <div className="stat-card">
      <div className="stat-divider" style={{ background: '#10b981' }} />
      <div className="stat-icon-wrap" style={{ background: '#10b98118' }}>
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="#10b981" strokeWidth="1.8" strokeLinecap="round">
          <path d="M2 14l4-4 3 3 5-6 4 3"/>
        </svg>
      </div>
      <div className="stat-label">Global Holdings</div>
      {isLoading || globalHoldings.length === 0 ? (
        <div className="stat-value">—</div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 4 }}>
          {globalHoldings.map(h => (
            <div key={h.holdingId} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
              <div>
                <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)' }}>
                  {h.ticker}
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                  {getHoldingCountry(h)}
                </div>
              </div>
              <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', textAlign: 'right' }}>
                {fmtCurrency(h.currentValue, h.currency)}
              </div>
            </div>
          ))}
        </div>
      )}
      {globalHoldings.length > 0 && (
        <div className="stat-sub" style={{ color: 'var(--text-muted)', marginTop: 6 }}>
          {globalHoldings.length} ETF{globalHoldings.length !== 1 ? 's' : ''}
        </div>
      )}
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

  // India: always sum raw INR values — no currency conversion
  const indiaRawTotal = indiaHoldings.reduce((s, h) => s + (h.currentValue ?? 0), 0);

  // Total net worth: convert everything to selected display currency
  const indiaInDisplay  = indiaHoldings.reduce((s, h) => s + (convert(h.currentValue, h.currency) ?? 0), 0);
  const globalInDisplay = globalHoldings.reduce((s, h) => s + (convert(h.currentValue, h.currency) ?? 0), 0);
  const totalValue      = indiaInDisplay + globalInDisplay;

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
          value={isLoading ? '—' : indiaHoldings.length ? fmtCurrency(indiaRawTotal, 'INR') : '—'}
          sub={indiaHoldings.length ? `${indiaHoldings.length} fund${indiaHoldings.length > 1 ? 's' : ''}` : null}
          accent="#f59e0b"
        />
        <GlobalHoldingsCard globalHoldings={globalHoldings} isLoading={isLoading} />
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
