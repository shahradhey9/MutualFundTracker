import { useState, useEffect, useCallback, useRef } from 'react';
import { useFundSearch } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';
import { fmtCurrency } from '../lib/format.js';

const REGIONS = [
  { value: 'INDIA',  label: 'India',  sub: 'AMFI — 4,000+ Direct Growth funds' },
  { value: 'GLOBAL', label: 'Global', sub: 'NYSE / NASDAQ — equities & ETFs' },
];

export function FundSearch() {
  const {
    searchQuery, setSearchQuery,
    searchRegion, setSearchRegion,
    selectedFund, setSelectedFund,
    clearSearch,
  } = useUIStore();

  const inputRef = useRef(null);

  // Debounce the query — fire search 300ms after the user stops typing
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
      {/* Region radio buttons */}
      <div style={{ display: 'flex', gap: 14, marginBottom: 14 }}>
        {REGIONS.map(({ value, label, sub }) => {
          const active = searchRegion === value;
          return (
            <label
              key={value}
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: 8,
                cursor: 'pointer',
                padding: '10px 14px',
                borderRadius: 'var(--border-radius-md)',
                border: `0.5px solid ${active ? 'var(--color-text-primary)' : 'var(--color-border-secondary)'}`,
                background: active ? 'var(--color-background-secondary)' : 'transparent',
                transition: 'border-color 0.15s, background 0.15s',
                flex: 1,
              }}
            >
              <input
                type="radio"
                name="fund-region"
                value={value}
                checked={active}
                onChange={() => handleRegionChange(value)}
                style={{ marginTop: 2, accentColor: 'var(--color-text-primary)', cursor: 'pointer' }}
              />
              <div>
                <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--color-text-primary)' }}>
                  {label}
                </div>
                <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', marginTop: 2 }}>
                  {sub}
                </div>
              </div>
            </label>
          );
        })}
      </div>

      {/* Search input */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
        <div style={{ flex: 1, position: 'relative' }}>
          <input
            ref={inputRef}
            style={{
              width: '100%',
              padding: '9px 36px 9px 14px',
              fontSize: 13,
              border: '0.5px solid var(--color-border-secondary)',
              borderRadius: 'var(--border-radius-md)',
              background: 'var(--color-background-primary)',
              color: 'var(--color-text-primary)',
              outline: 'none',
              boxSizing: 'border-box',
            }}
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder={
              searchRegion === 'INDIA'
                ? 'Type fund name or AMC — e.g. "Parag Parikh" or "HDFC Mid"'
                : 'Type name or ticker — e.g. "Vanguard" or "VOO"'
            }
            autoFocus
          />
          {/* Inline spinner while fetching */}
          {isFetching && (
            <div style={{
              position: 'absolute', right: 10, top: '50%', transform: 'translateY(-50%)',
              width: 14, height: 14,
              border: '2px solid var(--color-border-secondary)',
              borderTopColor: 'var(--color-text-primary)',
              borderRadius: '50%',
              animation: 'gwt-spin 0.7s linear infinite',
            }} />
          )}
        </div>
        {searchQuery && (
          <button
            onClick={handleClear}
            style={{
              padding: '8px 14px',
              fontSize: 13,
              border: '0.5px solid #185FA5',
              borderRadius: 'var(--border-radius-md)',
              background: 'none',
              color: '#185FA5',
              cursor: 'pointer',
              flexShrink: 0,
            }}
          >
            ✕
          </button>
        )}
      </div>

      {/* Results */}
      {showResults ? (
        <div>
          {isError && (
            <div style={{ fontSize: 13, color: '#A32D2D', padding: 12, border: '0.5px solid #F7C1C1', borderRadius: 'var(--border-radius-md)' }}>
              Search failed: {error?.message}
            </div>
          )}
          {!isFetching && results?.length === 0 && (
            <div style={{ fontSize: 13, color: 'var(--color-text-tertiary)', padding: '14px', border: '0.5px solid var(--color-border-tertiary)', borderRadius: 'var(--border-radius-md)' }}>
              No results for "{debouncedQuery}".
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
                      background: selectedFund?.id === fund.id ? 'var(--color-background-secondary)' : 'transparent',
                      outline: selectedFund?.id === fund.id ? '1px solid var(--color-border-secondary)' : 'none',
                      transition: 'background 0.1s',
                    }}
                    onMouseEnter={e => { if (selectedFund?.id !== fund.id) e.currentTarget.style.background = 'var(--color-background-secondary)'; }}
                    onMouseLeave={e => { if (selectedFund?.id !== fund.id) e.currentTarget.style.background = 'transparent'; }}
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
                      {searchRegion === 'INDIA' ? 'AMFI' : 'Global'}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      ) : (
        <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', lineHeight: 1.8, border: '0.5px solid var(--color-border-tertiary)', borderRadius: 'var(--border-radius-md)', padding: '14px 16px' }}>
          ℹ Results appear as you type — try "parag", "mid cap", "hdfc flexi", "VOO"
        </div>
      )}
    </div>
  );
}
