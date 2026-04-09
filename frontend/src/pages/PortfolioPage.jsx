import { usePortfolio } from '../hooks/usePortfolio.js';
import { useFxRate, toUSD } from '../hooks/useFxRate.js';
import { MetricCard } from '../components/MetricCard.jsx';
import { HoldingsTable } from '../components/HoldingsTable.jsx';
import { FxBanner } from '../components/FxBanner.jsx';
import { LoadingSpinner, SkeletonRow } from '../components/LoadingSpinner.jsx';
import { fmtINR, fmtUSD, fmtPct } from '../lib/format.js';

function SyncStatus({ isFetching, dataUpdatedAt }) {
  const time = dataUpdatedAt
    ? new Date(dataUpdatedAt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
    : null;
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      marginBottom: '1rem', fontSize: 12,
      color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)',
    }}>
      <div style={{
        width: 7, height: 7, borderRadius: '50%',
        background: isFetching ? '#EF9F27' : '#3B6D11',
        transition: 'background 0.3s',
      }} />
      <span>
        {isFetching ? 'Syncing prices…' : time ? `Prices synced · ${time}` : 'Loading…'}
      </span>
    </div>
  );
}

function SkeletonTable() {
  return (
    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
      <tbody>
        {Array.from({ length: 3 }).map((_, i) => <SkeletonRow key={i} cols={7} />)}
      </tbody>
    </table>
  );
}

export function PortfolioPage() {
  const { data: holdings = [], isFetching, isLoading, dataUpdatedAt, isError, error } = usePortfolio();
  const { inrPerUsd } = useFxRate();

  if (isError) {
    return (
      <div style={{
        padding: '1.5rem', borderRadius: 'var(--border-radius-lg)',
        border: '0.5px solid var(--color-border-danger)',
        background: 'var(--color-background-danger)',
        color: 'var(--color-text-danger)', fontSize: 13,
      }}>
        Failed to load portfolio: {error?.message}
      </div>
    );
  }

  const indiaHoldings  = holdings.filter(h => h.region === 'INDIA');
  const globalHoldings = holdings.filter(h => h.region === 'GLOBAL');

  const indiaValue  = indiaHoldings.reduce((s, h) => s + (h.currentValue ?? 0), 0);
  const globalValue = globalHoldings.reduce((s, h) => s + (h.currentValue ?? 0), 0);
  const totalUSD    = toUSD(holdings, inrPerUsd);

  const totalCost    = holdings.reduce((s, h) => s + (h.costBasis ?? 0), 0);
  const totalGain    = holdings.reduce((s, h) => s + (h.gain ?? 0), 0);
  const totalGainPct = totalCost > 0 ? (totalGain / totalCost) * 100 : null;

  return (
    <div>
      <SyncStatus isFetching={isFetching} dataUpdatedAt={dataUpdatedAt} />
      {holdings.length > 0 && <FxBanner />}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0,1fr))', gap: 10, marginBottom: '1.5rem' }}>
        <MetricCard
          label="total net worth"
          value={isLoading ? '—' : holdings.length ? fmtUSD(totalUSD) : '—'}
          sub={
            isLoading ? null :
            totalGainPct != null
              ? fmtPct(totalGainPct) + ' overall return'
              : holdings.length ? 'Add avg cost for P&L' : null
          }
          subPositive={totalGainPct != null ? totalGainPct >= 0 : null}
        />
        <MetricCard
          label="india holdings"
          value={isLoading ? '—' : indiaHoldings.length ? fmtINR(indiaValue) : '—'}
          sub={indiaHoldings.length ? `${indiaHoldings.length} fund${indiaHoldings.length > 1 ? 's' : ''}` : null}
        />
        <MetricCard
          label="global holdings"
          value={isLoading ? '—' : globalHoldings.length ? fmtUSD(globalValue) : '—'}
          sub={globalHoldings.length ? `${globalHoldings.length} ETF${globalHoldings.length > 1 ? 's' : ''}` : null}
        />
      </div>

      <div style={{
        fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--color-text-tertiary)',
        letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 10,
      }}>
        Holdings
      </div>

      {isLoading ? <SkeletonTable /> : <HoldingsTable holdings={holdings} />}
    </div>
  );
}
