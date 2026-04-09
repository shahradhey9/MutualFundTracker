import {
  PieChart, Pie, Cell, Tooltip, BarChart, Bar,
  XAxis, YAxis, CartesianGrid, ResponsiveContainer, Legend,
} from 'recharts';
import { usePortfolio } from '../hooks/usePortfolio.js';
import { useFxRate, toUSD } from '../hooks/useFxRate.js';
import { GainPill } from '../components/Badges.jsx';
import { fmtCurrency, fmtUnits, fmtDate, fmtPct, fmtUSD } from '../lib/format.js';

const COLORS = { INDIA: '#3B6D11', GLOBAL: '#185FA5' };

function AllocationDonut({ holdings }) {
  const indiaVal  = holdings.filter(h => h.region === 'INDIA').reduce((s,h) => s + h.currentValue, 0);
  const globalVal = holdings.filter(h => h.region === 'GLOBAL').reduce((s,h) => s + h.currentValue, 0);
  const total = indiaVal + globalVal;

  const data = [
    { name: 'India funds', value: indiaVal,  color: COLORS.INDIA  },
    { name: 'Global ETFs', value: globalVal, color: COLORS.GLOBAL },
  ].filter(d => d.value > 0);

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
      <PieChart width={110} height={110}>
        <Pie data={data} cx={50} cy={50} innerRadius={32} outerRadius={50}
          dataKey="value" strokeWidth={0}>
          {data.map(d => <Cell key={d.name} fill={d.color} />)}
        </Pie>
        <Tooltip formatter={v => fmtUSD(v)} />
      </PieChart>
      <div>
        {data.map(d => (
          <div key={d.name} style={{ display:'flex', alignItems:'center', gap:8, fontSize:12, color:'var(--color-text-secondary)', marginBottom:6 }}>
            <div style={{ width:10, height:10, borderRadius:2, background:d.color, flexShrink:0 }} />
            {d.name} — {total ? fmtPct(d.value / total * 100, false) : '0%'}
          </div>
        ))}
      </div>
    </div>
  );
}

function ValueBar({ holdings }) {
  const data = holdings.map(h => ({
    name: h.ticker,
    value: +h.currentValue.toFixed(2),
    fill: COLORS[h.region],
  }));
  return (
    <ResponsiveContainer width="100%" height={160}>
      <BarChart data={data} margin={{ top: 4, right: 4, bottom: 4, left: 4 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(128,128,128,0.12)" vertical={false} />
        <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#888' }} axisLine={false} tickLine={false} />
        <YAxis tick={{ fontSize: 10, fill: '#888' }} axisLine={false} tickLine={false}
          tickFormatter={v => v >= 1000 ? (v / 1000).toFixed(0) + 'k' : v} />
        <Tooltip formatter={(v, _, p) => [fmtCurrency(v, p.payload.fill === COLORS.INDIA ? 'INR' : 'USD'), 'Value']} />
        <Bar dataKey="value" radius={[4,4,0,0]}>
          {data.map((d,i) => <Cell key={i} fill={d.fill} />)}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

// Gain/loss waterfall — shows each holding's absolute gain in a unified currency
function GainWaterfall({ holdings, inrPerUsd }) {
  const data = holdings
    .filter(h => h.gain != null)
    .map(h => ({
      name: h.ticker,
      gain: h.currency === 'INR'
        ? +(h.gain / inrPerUsd).toFixed(2)
        : +h.gain.toFixed(2),
      fill: h.gain >= 0 ? COLORS.INDIA : '#A32D2D',
    }));

  if (!data.length) return (
    <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', padding: '1rem 0' }}>
      Add average cost to holdings to see gain/loss breakdown.
    </div>
  );

  return (
    <ResponsiveContainer width="100%" height={160}>
      <BarChart data={data} margin={{ top: 4, right: 4, bottom: 4, left: 4 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="rgba(128,128,128,0.12)" vertical={false} />
        <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#888' }} axisLine={false} tickLine={false} />
        <YAxis tick={{ fontSize: 10, fill: '#888' }} axisLine={false} tickLine={false}
          tickFormatter={v => (v >= 0 ? '+' : '') + (v >= 1000 || v <= -1000 ? (v/1000).toFixed(1)+'k' : v)} />
        <Tooltip formatter={v => [`$${v >= 0 ? '+' : ''}${v.toLocaleString('en-US', {minimumFractionDigits:2})}`, 'Gain (USD)']} />
        <Bar dataKey="gain" radius={[4,4,0,0]}>
          {data.map((d,i) => <Cell key={i} fill={d.fill} />)}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

const thStyle  = { padding:'6px 10px', fontSize:11, fontWeight:500, color:'var(--color-text-tertiary)', fontFamily:'var(--font-mono)', borderBottom:'0.5px solid var(--color-border-tertiary)', textAlign:'left' };
const thRStyle = { ...thStyle, textAlign:'right' };
const tdStyle  = { padding:'10px 10px', borderBottom:'0.5px solid var(--color-border-tertiary)', fontSize:13, verticalAlign:'middle' };
const tdRStyle = { ...tdStyle, textAlign:'right', fontFamily:'var(--font-mono)', fontSize:12 };

const cardStyle = {
  background: 'var(--color-background-primary)',
  border: '0.5px solid var(--color-border-tertiary)',
  borderRadius: 'var(--border-radius-lg)',
  padding: '1rem 1.25rem',
};

const sectionLabel = {
  fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--color-text-tertiary)',
  letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 10,
};

export function AnalyticsPage() {
  const { data: holdings = [], isLoading } = usePortfolio();
  const { inrPerUsd } = useFxRate();

  if (isLoading) {
    return <div style={{ color: 'var(--color-text-tertiary)', fontSize: 13, fontFamily: 'var(--font-mono)' }}>Loading analytics…</div>;
  }

  if (!holdings.length) {
    return (
      <div style={{ textAlign:'center', padding:'3rem 1rem', border:'0.5px dashed var(--color-border-tertiary)', borderRadius:'var(--border-radius-lg)', color:'var(--color-text-tertiary)', fontSize:13 }}>
        <div style={{ fontSize:28, marginBottom:10 }}>◎</div>
        Add holdings to see analytics.
      </div>
    );
  }

  // Portfolio-level summary stats
  const totalUSD     = toUSD(holdings, inrPerUsd);
  const totalCost    = toUSD(holdings.filter(h => h.costBasis), inrPerUsd);
  const totalGainUSD = toUSD(holdings.filter(h => h.gain != null).map(h => ({ ...h, currentValue: h.gain })), inrPerUsd);
  const totalGainPct = totalCost > 0 ? (totalGainUSD / totalCost) * 100 : null;
  const bestHolder   = [...holdings].sort((a,b) => (b.gainPct ?? -Infinity) - (a.gainPct ?? -Infinity))[0];
  const worstHolder  = [...holdings].filter(h => h.gainPct != null).sort((a,b) => (a.gainPct ?? 0) - (b.gainPct ?? 0))[0];

  return (
    <div>
      {/* Summary stat cards */}
      <div style={{ display:'grid', gridTemplateColumns:'repeat(3, minmax(0,1fr))', gap:10, marginBottom:'1.5rem' }}>
        {[
          {
            label: 'total gain / loss',
            value: totalGainPct != null ? fmtPct(totalGainPct) : '—',
            sub: totalGainUSD !== 0 ? `${totalGainUSD >= 0 ? '+' : ''}${fmtUSD(totalGainUSD)} absolute` : 'Add avg cost',
            subPositive: totalGainPct != null ? totalGainPct >= 0 : null,
          },
          {
            label: 'best performer',
            value: bestHolder?.gainPct != null ? fmtPct(bestHolder.gainPct) : '—',
            sub: bestHolder?.ticker,
            subPositive: bestHolder?.gainPct != null ? bestHolder.gainPct >= 0 : null,
          },
          {
            label: 'needs attention',
            value: worstHolder?.gainPct != null ? fmtPct(worstHolder.gainPct) : '—',
            sub: worstHolder?.ticker,
            subPositive: worstHolder?.gainPct != null ? worstHolder.gainPct >= 0 : null,
          },
        ].map(card => (
          <div key={card.label} style={{ background:'var(--color-background-secondary)', borderRadius:'var(--border-radius-md)', padding:'14px 16px' }}>
            <div style={{ fontSize:12, color:'var(--color-text-tertiary)', fontFamily:'var(--font-mono)', marginBottom:6 }}>{card.label}</div>
            <div style={{ fontSize:22, fontWeight:500, letterSpacing:'-0.02em', color: card.subPositive === false ? '#A32D2D' : card.subPositive === true ? '#3B6D11' : 'var(--color-text-primary)' }}>
              {card.value}
            </div>
            {card.sub && <div style={{ fontSize:12, marginTop:4, fontFamily:'var(--font-mono)', color:'var(--color-text-tertiary)' }}>{card.sub}</div>}
          </div>
        ))}
      </div>

      {/* Charts row */}
      <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap:12, marginBottom:'1.5rem' }}>
        <div style={cardStyle}>
          <div style={{ fontSize:12, color:'var(--color-text-secondary)', marginBottom:14, fontFamily:'var(--font-mono)' }}>Allocation by region</div>
          <AllocationDonut holdings={holdings} />
        </div>
        <div style={cardStyle}>
          <div style={{ fontSize:12, color:'var(--color-text-secondary)', marginBottom:14, fontFamily:'var(--font-mono)' }}>Holding value breakdown</div>
          <ValueBar holdings={holdings} />
        </div>
      </div>

      {/* Gain/loss waterfall */}
      <div style={{ ...cardStyle, marginBottom:'1.5rem' }}>
        <div style={{ fontSize:12, color:'var(--color-text-secondary)', marginBottom:14, fontFamily:'var(--font-mono)' }}>
          Gain / loss per holding (converted to USD)
        </div>
        <GainWaterfall holdings={holdings} inrPerUsd={inrPerUsd} />
      </div>

      {/* Performance table */}
      <div style={sectionLabel}>Performance summary</div>
      <div style={{ overflowX:'auto' }}>
        <table style={{ width:'100%', borderCollapse:'collapse' }}>
          <thead>
            <tr>
              <th style={thStyle}>Fund</th>
              <th style={thRStyle}>Units</th>
              <th style={thRStyle}>Live NAV</th>
              <th style={thRStyle}>Value</th>
              <th style={thRStyle}>Cost basis</th>
              <th style={thRStyle}>Return</th>
              <th style={thRStyle}>Held since</th>
              <th style={thRStyle}>Days held</th>
            </tr>
          </thead>
          <tbody>
            {holdings.map(h => {
              const days = h.purchaseAt
                ? Math.floor((Date.now() - new Date(h.purchaseAt)) / 86400000)
                : null;
              return (
                <tr key={h.holdingId}>
                  <td style={tdStyle}>
                    <div style={{ fontWeight:500 }}>{h.ticker}</div>
                    <div style={{ fontSize:11, color:'var(--color-text-tertiary)', fontFamily:'var(--font-mono)' }}>{h.amc}</div>
                  </td>
                  <td style={tdRStyle}>{fmtUnits(h.units)}</td>
                  <td style={tdRStyle}>{fmtCurrency(h.liveNav, h.currency)}</td>
                  <td style={tdRStyle}>{fmtCurrency(h.currentValue, h.currency)}</td>
                  <td style={tdRStyle}>{h.costBasis ? fmtCurrency(h.costBasis, h.currency) : '—'}</td>
                  <td style={tdRStyle}><GainPill pct={h.gainPct} /></td>
                  <td style={tdRStyle}>{fmtDate(h.purchaseAt)}</td>
                  <td style={{ ...tdRStyle, color:'var(--color-text-tertiary)' }}>{days != null ? `${days}d` : '—'}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
