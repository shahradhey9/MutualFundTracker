import { useState, useEffect, useCallback, useRef } from 'react';
import { useFundSearch } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';
import { fmtCurrency } from '../lib/format.js';

const REGIONS = [
  { value: 'INDIA',  label: 'India',  sub: 'AMFI — 4,000+ Direct Growth funds' },
  { value: 'GLOBAL', label: 'Global', sub: 'Global exchanges — NYSE, LSE, Euronext & more' },
];

// Derive the exchange label from a Yahoo Finance ticker suffix.
function getExchangeLabel(ticker) {
  if (!ticker) return 'Global';
  const t = ticker.toUpperCase();
  // Europe
  if (t.endsWith('.L') || t.endsWith('.IL')) return 'LSE';
  if (t.endsWith('.PA'))  return 'Euronext Paris';
  if (t.endsWith('.AS'))  return 'Euronext Amsterdam';
  if (t.endsWith('.DE'))  return 'XETRA';
  if (t.endsWith('.MI'))  return 'Borsa Italiana';
  if (t.endsWith('.BR'))  return 'Euronext Brussels';
  if (t.endsWith('.MC'))  return 'BME Madrid';
  if (t.endsWith('.SW'))  return 'SIX Swiss';
  if (t.endsWith('.LS'))  return 'Euronext Lisbon';
  if (t.endsWith('.OL'))  return 'Oslo Børs';
  if (t.endsWith('.ST'))  return 'Nasdaq Stockholm';
  if (t.endsWith('.HE'))  return 'Nasdaq Helsinki';
  if (t.endsWith('.CO'))  return 'Nasdaq Copenhagen';
  if (t.endsWith('.IS'))  return 'Borsa Istanbul';
  if (t.endsWith('.WA'))  return 'WSE Warsaw';
  // North America
  if (t.endsWith('.TO') || t.endsWith('.V') || t.endsWith('.CN')) return 'TSX';
  // Latin America
  if (t.endsWith('.SN'))  return 'Santiago (Chile)';
  if (t.endsWith('.MX'))  return 'BMV Mexico';
  if (t.endsWith('.SA'))  return 'B3 Brazil';
  if (t.endsWith('.BA'))  return 'BCBA Argentina';
  // Asia-Pacific
  if (t.endsWith('.AX'))  return 'ASX';
  if (t.endsWith('.NZ'))  return 'NZX';
  if (t.endsWith('.T'))   return 'TSE Tokyo';
  if (t.endsWith('.SI'))  return 'SGX';
  if (t.endsWith('.HK'))  return 'HKEX';
  if (t.endsWith('.SS'))  return 'Shanghai';
  if (t.endsWith('.SZ'))  return 'Shenzhen';
  if (t.endsWith('.TW') || t.endsWith('.TWO')) return 'TWSE';
  if (t.endsWith('.KS') || t.endsWith('.KQ'))  return 'KRX Korea';
  if (t.endsWith('.BK'))  return 'SET Thailand';
  if (t.endsWith('.KL'))  return 'Bursa Malaysia';
  if (t.endsWith('.JK'))  return 'IDX Indonesia';
  if (t.endsWith('.PS'))  return 'PSE Philippines';
  // India
  if (t.endsWith('.BO'))  return 'BSE';
  if (t.endsWith('.NS'))  return 'NSE';
  // Middle East / Africa
  if (t.endsWith('.TA'))  return 'TASE Israel';
  if (t.endsWith('.JO'))  return 'JSE';
  return 'NYSE/NASDAQ';
}

export function FundSearch() {
  const {
    searchQuery, setSearchQuery,
    searchRegion, setSearchRegion,
    selectedFund, setSelectedFund,
    clearSearch,
  } = useUIStore();

  const inputRef = useRef(null);

  const [debouncedQuery, setDebouncedQuery] = useState('');
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(searchQuery.trim()), 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  const { data: results, isFetching, isError, error } = useFundSearch(debouncedQuery, searchRegion);

  const handleRegionChange = useCallback((region) => {
    setSearchRegion(region);
    setSelectedFund(null);
    clearSearch();
    setDebouncedQuery('');
  }, [setSearchRegion, setSelectedFund, clearSearch]);

  const handleClear = useCallback(() => {
    clearSearch();
    setDebouncedQuery('');
    inputRef.current?.focus();
  }, [clearSearch]);

  const showResults = debouncedQuery.length >= 2;

  return (
    <div>
      <style>{`
        .region-card {
          display: flex;
          align-items: flex-start;
          gap: 10px;
          cursor: pointer;
          padding: 12px 16px;
          border-radius: var(--radius-md);
          border: 1.5px solid var(--border);
          background: var(--bg-card);
          transition: border-color 0.15s, background 0.15s, box-shadow 0.15s;
          flex: 1;
        }
        .region-card:hover { border-color: var(--accent); background: var(--bg-hover); }
        .region-card.active {
          border-color: var(--accent);
          background: var(--accent-light);
          box-shadow: 0 0 0 3px var(--accent-ring);
        }
        .region-card input[type="radio"] { accent-color: var(--accent); margin-top: 2px; cursor: pointer; }
        .region-label { font-size: 13px; font-weight: 500; color: var(--text-primary); }
        .region-sub { font-size: 11px; color: var(--text-muted); font-family: var(--font-mono); margin-top: 2px; }

        .search-wrap { position: relative; flex: 1; }
        .search-input {
          width: 100%;
          padding: 10px 40px 10px 14px;
          font-size: 13px;
          font-family: var(--font-sans);
          background: var(--bg-input);
          color: var(--text-primary);
          border: 1.5px solid var(--border);
          border-radius: var(--radius-md);
          outline: none;
          box-sizing: border-box;
          transition: border-color 0.15s, box-shadow 0.15s;
        }
        .search-input:focus {
          border-color: var(--border-focus);
          box-shadow: 0 0 0 3px var(--accent-ring);
        }
        .search-input::placeholder { color: var(--text-muted); }

        .search-spinner {
          position: absolute; right: 12px; top: 50%; transform: translateY(-50%);
          width: 15px; height: 15px;
          border: 2px solid var(--border);
          border-top-color: var(--accent);
          border-radius: 50%;
          animation: gwt-spin 0.7s linear infinite;
        }

        .results-list {
          border: 1px solid var(--border-light);
          border-radius: var(--radius-lg);
          background: var(--bg-card);
          overflow: hidden;
          box-shadow: var(--shadow-md);
          animation: gwt-fade-in 0.15s ease;
        }
        .results-header {
          padding: 8px 14px;
          font-size: 11px;
          font-family: var(--font-mono);
          color: var(--text-muted);
          border-bottom: 1px solid var(--border-light);
          background: var(--bg-muted);
        }
        .result-item {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 11px 14px;
          cursor: pointer;
          border-bottom: 1px solid var(--border-light);
          transition: background 0.1s;
        }
        .result-item:last-child { border-bottom: none; }
        .result-item:hover { background: var(--bg-hover); }
        .result-item.selected { background: var(--accent-light); }
        .result-item.selected .result-name { color: var(--accent); }
        .result-name { font-size: 13px; font-weight: 500; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .result-meta { font-size: 11px; color: var(--text-muted); font-family: var(--font-mono); margin-top: 2px; }

        .no-results {
          padding: 20px 16px;
          text-align: center;
          font-size: 13px;
          color: var(--text-muted);
          border: 1.5px dashed var(--border);
          border-radius: var(--radius-md);
        }
        .hint-box {
          padding: 14px 16px;
          font-size: 12px;
          color: var(--text-muted);
          font-family: var(--font-mono);
          line-height: 1.8;
          border: 1px solid var(--border-light);
          border-radius: var(--radius-md);
          background: var(--bg-muted);
        }
      `}</style>

      {/* Region selector */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 14 }}>
        {REGIONS.map(({ value, label, sub }) => (
          <label key={value} className={`region-card${searchRegion === value ? ' active' : ''}`}>
            <input
              type="radio"
              name="fund-region"
              value={value}
              checked={searchRegion === value}
              onChange={() => handleRegionChange(value)}
            />
            <div>
              <div className="region-label">{label}</div>
              <div className="region-sub">{sub}</div>
            </div>
          </label>
        ))}
      </div>

      {/* Search input */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
        <div className="search-wrap">
          <input
            ref={inputRef}
            className="search-input"
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder={
              searchRegion === 'INDIA'
                ? 'Search by fund name or AMC — e.g. "Parag Parikh" or "HDFC Mid"'
                : 'Search by name or ticker — e.g. "Vanguard" or "VOO"'
            }
            autoFocus
          />
          {isFetching && <div className="search-spinner" />}
        </div>
        {searchQuery && (
          <button className="btn btn-secondary" onClick={handleClear} style={{ flexShrink: 0 }}>
            ✕ Clear
          </button>
        )}
      </div>

      {/* Results */}
      {showResults ? (
        <div>
          {isError && (
            <div style={{ fontSize: 13, color: 'var(--color-loss)', padding: '10px 14px',
              background: 'var(--color-loss-bg)', border: '1px solid var(--color-loss)',
              borderRadius: 'var(--radius-md)', opacity: 0.9 }}>
              Search failed: {error?.message}
            </div>
          )}
          {!isFetching && results?.length === 0 && (
            <div className="no-results">No results for "{debouncedQuery}" — try a different name or ticker.</div>
          )}
          {results?.length > 0 && (
            <div>
              <div className="results-list" style={{ maxHeight: 360, overflowY: 'auto' }}>
                <div className="results-header">
                  {results.length} result{results.length !== 1 ? 's' : ''} — click to select
                </div>
                {results.map(fund => (
                  <div
                    key={fund.id}
                    className={`result-item${selectedFund?.id === fund.id ? ' selected' : ''}`}
                    onClick={() => setSelectedFund(fund)}
                  >
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="result-name">{fund.name}</div>
                      <div className="result-meta">
                        {fund.ticker}
                        {fund.amc && ` · ${fund.amc}`}
                        {fund.latestNav != null && ` · ${fmtCurrency(fund.latestNav, fund.currency || (searchRegion === 'INDIA' ? 'INR' : 'USD'))}`}
                        {fund.category && ` · ${fund.category}`}
                      </div>
                    </div>
                    <span className={`badge ${searchRegion === 'INDIA' ? 'badge-india' : 'badge-global'}`} style={{ marginLeft: 12 }}>
                      {searchRegion === 'INDIA' ? 'AMFI' : getExchangeLabel(fund.ticker)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      ) : (
        <div className="hint-box">
          ℹ Results appear as you type — try "parag", "mid cap", "hdfc flexi", "VOO", "VWRA"
        </div>
      )}
    </div>
  );
}
