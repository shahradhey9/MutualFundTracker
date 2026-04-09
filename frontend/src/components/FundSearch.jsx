import { useState, useCallback, useEffect } from 'react';
import { useFundSearch } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';
import { fmtCurrency } from '../lib/format.js';

function useDebounce(value, delay = 350) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

export function FundSearch() {
  const {
    searchQuery, setSearchQuery,
    searchRegion, setSearchRegion,
    selectedFund, setSelectedFund,
    clearSearch,
  } = useUIStore();

  const debouncedQuery = useDebounce(searchQuery);
  const { data: results, isFetching, isError, error } = useFundSearch(debouncedQuery, searchRegion);

  const handleRegionChange = useCallback((region) => {
    setSearchRegion(region);
    setSelectedFund(null);
  }, [setSearchRegion, setSelectedFund]);

  const inputStyle = {
    flex: 1,
    padding: '9px 14px',
    fontSize: 13,
    border: '0.5px solid var(--color-border-secondary)',
    borderRadius: 'var(--border-radius-md)',
    background: 'var(--color-background-primary)',
    color: 'var(--color-text-primary)',
    outline: 'none',
  };

  const segmentBase = {
    padding: '8px 16px',
    fontSize: 13,
    border: '0.5px solid var(--color-border-secondary)',
    cursor: 'pointer',
    transition: 'background 0.12s',
  };

  return (
    <div>
      {/* Search bar */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
        <input
          style={inputStyle}
          type="text"
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          placeholder={searchRegion === 'INDIA'
            ? 'Search by fund name or AMC — e.g. "Parag Parikh" or "HDFC"'
            : 'Search by name or ticker — e.g. "Vanguard" or "VOO"'}
          autoFocus
        />
        {searchQuery && (
          <button
            onClick={clearSearch}
            style={{ ...segmentBase, borderRadius: 'var(--border-radius-md)', background: 'none', color: 'var(--color-text-secondary)' }}
          >
            ✕
          </button>
        )}
      </div>

      {/* Region toggle */}
      <div style={{ display: 'flex', marginBottom: 16, borderRadius: 'var(--border-radius-md)', overflow: 'hidden', border: '0.5px solid var(--color-border-secondary)', width: 'fit-content' }}>
        {['INDIA', 'GLOBAL'].map((r, i) => (
          <button
            key={r}
            onClick={() => handleRegionChange(r)}
            style={{
              ...segmentBase,
              borderRadius: 0,
              borderLeft: i > 0 ? '0.5px solid var(--color-border-secondary)' : 'none',
              background: searchRegion === r
                ? 'var(--color-text-primary)'
                : 'var(--color-background-primary)',
              color: searchRegion === r
                ? 'var(--color-background-primary)'
                : 'var(--color-text-secondary)',
              fontWeight: searchRegion === r ? 500 : 400,
            }}
          >
            {r === 'INDIA' ? 'India (AMFI)' : 'Global (Yahoo Finance)'}
          </button>
        ))}
      </div>

      {/* Results */}
      {debouncedQuery.length >= 2 && (
        <div>
          {isFetching && (
            <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', marginBottom: 8 }}>
              Searching {searchRegion === 'INDIA' ? 'AMFI' : 'Yahoo Finance'}…
            </div>
          )}
          {isError && (
            <div style={{ fontSize: 13, color: '#A32D2D', padding: 12, border: '0.5px solid #F7C1C1', borderRadius: 'var(--border-radius-md)' }}>
              Search failed: {error?.message}
            </div>
          )}
          {!isFetching && results?.length === 0 && (
            <div style={{ fontSize: 13, color: 'var(--color-text-tertiary)', padding: '14px', border: '0.5px solid var(--color-border-tertiary)', borderRadius: 'var(--border-radius-md)' }}>
              No results for "{debouncedQuery}" in {searchRegion === 'INDIA' ? 'AMFI' : 'Yahoo Finance'}.
            </div>
          )}
          {results?.length > 0 && (
            <div>
              <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--color-text-tertiary)', marginBottom: 8 }}>
                {results.length} result{results.length > 1 ? 's' : ''} — click to select
              </div>
              <div style={{ maxHeight: 340, overflowY: 'auto', border: '0.5px solid var(--color-border-tertiary)', borderRadius: 'var(--border-radius-lg)', padding: 8 }}>
                {results.map(fund => (
                  <div
                    key={fund.id}
                    onClick={() => setSelectedFund(fund)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '10px 12px',
                      borderRadius: 'var(--border-radius-md)',
                      cursor: 'pointer',
                      background: selectedFund?.id === fund.id
                        ? 'var(--color-background-secondary)'
                        : 'transparent',
                      outline: selectedFund?.id === fund.id
                        ? '1px solid var(--color-border-secondary)'
                        : 'none',
                      transition: 'background 0.1s',
                    }}
                    onMouseEnter={e => {
                      if (selectedFund?.id !== fund.id)
                        e.currentTarget.style.background = 'var(--color-background-secondary)';
                    }}
                    onMouseLeave={e => {
                      if (selectedFund?.id !== fund.id)
                        e.currentTarget.style.background = 'transparent';
                    }}
                  >
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--color-text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {fund.name}
                      </div>
                      <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>
                        {fund.ticker}
                        {fund.amc && ` · ${fund.amc}`}
                        {fund.latestNav != null && ` · NAV ${fmtCurrency(fund.latestNav, fund.currency || (searchRegion === 'INDIA' ? 'INR' : 'USD'))}`}
                        {fund.category && ` · ${fund.category}`}
                      </div>
                    </div>
                    <span style={{
                      marginLeft: 12,
                      flexShrink: 0,
                      fontSize: 10,
                      padding: '2px 7px',
                      borderRadius: 20,
                      fontFamily: 'var(--font-mono)',
                      fontWeight: 500,
                      background: searchRegion === 'INDIA' ? '#FAEEDA' : '#E6F1FB',
                      color: searchRegion === 'INDIA' ? '#854F0B' : '#185FA5',
                    }}>
                      {searchRegion === 'INDIA' ? 'AMFI' : 'Yahoo'}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {debouncedQuery.length < 2 && (
        <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', lineHeight: 1.8, border: '0.5px solid var(--color-border-tertiary)', borderRadius: 'var(--border-radius-md)', padding: '14px 16px' }}>
          ℹ India — searches live AMFI NAVAll.txt (4,000+ Direct Growth schemes)<br />
          ℹ Global — searches Yahoo Finance (equities, ETFs, mutual funds)<br />
          ℹ Type at least 2 characters to search
        </div>
      )}
    </div>
  );
}
