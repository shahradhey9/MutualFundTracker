import { useState, useRef, useCallback, useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api.js';
import { useUIStore } from '../lib/store.js';
import { parseCSV, SAMPLE_CSV } from '../lib/csvParser.js';
import { rankMatches, confidence } from '../lib/fuzzyMatch.js';

// ── Styles ────────────────────────────────────────────────────────────────────
const S = {
  card: {
    background: 'var(--color-background-primary)',
    border: '1px solid #e8e8e4',
    borderRadius: 12,
    overflow: 'hidden',
  },
  cardHeader: {
    padding: '14px 20px',
    borderBottom: '1px solid #e8e8e4',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  cardTitle: { fontSize: 14, fontWeight: 500, color: 'var(--color-text-primary)' },
  cardBody: { padding: '20px' },
  badge: (color) => ({
    display: 'inline-block', fontSize: 11, fontWeight: 500,
    padding: '2px 8px', borderRadius: 20,
    background: color === 'green' ? '#dcfce7' : color === 'amber' ? '#fef9c3' : color === 'red' ? '#fee2e2' : '#f1f5f9',
    color:      color === 'green' ? '#15803d' : color === 'amber' ? '#92400e' : color === 'red' ? '#b91c1c' : '#475569',
  }),
  btn: (variant = 'primary') => ({
    padding: variant === 'sm' ? '6px 14px' : '10px 20px',
    fontSize: variant === 'sm' ? 12 : 14,
    fontWeight: 500,
    borderRadius: 8,
    border: variant === 'secondary' ? '1px solid #e0dfd9' : 'none',
    background: variant === 'primary' ? '#2563eb' : variant === 'danger' ? '#dc2626' : 'transparent',
    color: variant === 'primary' || variant === 'danger' ? '#fff' : 'var(--color-text-secondary)',
    cursor: 'pointer',
    fontFamily: 'inherit',
    transition: 'opacity 0.15s',
  }),
};

// ── Step 1: Upload ────────────────────────────────────────────────────────────
function UploadStep({ onParsed }) {
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState('');
  const inputRef = useRef();

  function handleFile(file) {
    if (!file) return;
    if (!file.name.endsWith('.csv')) { setError('Please upload a .csv file'); return; }
    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const result = parseCSV(e.target.result);
        onParsed(result);
      } catch (err) {
        setError(err.message);
      }
    };
    reader.readAsText(file);
  }

  function downloadSample() {
    const blob = new Blob([SAMPLE_CSV], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href = url; a.download = 'gwt-sample-portfolio.csv'; a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <div>
      {/* Drop zone */}
      <div
        onClick={() => inputRef.current.click()}
        onDragOver={e => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={e => { e.preventDefault(); setDragging(false); handleFile(e.dataTransfer.files[0]); }}
        style={{
          border: `2px dashed ${dragging ? '#2563eb' : '#e0dfd9'}`,
          borderRadius: 12,
          padding: '48px 24px',
          textAlign: 'center',
          cursor: 'pointer',
          background: dragging ? '#eff6ff' : 'var(--color-background-secondary)',
          transition: 'all 0.15s',
          marginBottom: 20,
        }}
      >
        <div style={{ fontSize: 32, marginBottom: 12 }}>
          <svg width="40" height="40" viewBox="0 0 40 40" fill="none" style={{ margin: '0 auto', display: 'block' }}>
            <rect width="40" height="40" rx="10" fill={dragging ? '#dbeafe' : '#f1f5f9'}/>
            <path d="M20 12v12m0-12l-4 4m4-4l4 4" stroke={dragging ? '#2563eb' : '#94a3b8'} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <path d="M13 28h14" stroke={dragging ? '#2563eb' : '#94a3b8'} strokeWidth="2" strokeLinecap="round"/>
          </svg>
        </div>
        <div style={{ fontSize: 15, fontWeight: 500, color: 'var(--color-text-primary)', marginBottom: 6 }}>
          {dragging ? 'Drop your CSV here' : 'Upload portfolio CSV'}
        </div>
        <div style={{ fontSize: 13, color: 'var(--color-text-tertiary)' }}>
          Drag and drop or click to browse · .csv files only
        </div>
        <input ref={inputRef} type="file" accept=".csv" style={{ display: 'none' }}
          onChange={e => handleFile(e.target.files[0])} />
      </div>

      {error && (
        <div style={{ padding: '10px 14px', borderRadius: 8, background: '#fef2f2', color: '#b91c1c', fontSize: 13, marginBottom: 16, border: '1px solid #fecaca' }}>
          {error}
        </div>
      )}

      {/* CSV format guide */}
      <div style={S.card}>
        <div style={S.cardHeader}>
          <span style={S.cardTitle}>CSV format</span>
          <button style={S.btn('secondary')} onClick={downloadSample}>Download sample</button>
        </div>
        <div style={S.cardBody}>
          <div style={{ fontSize: 13, color: 'var(--color-text-secondary)', marginBottom: 14, lineHeight: 1.6 }}>
            Your CSV needs at minimum a <strong>Fund Name</strong> and <strong>Units</strong> column.
            Column names are flexible — the app understands common broker export formats.
          </div>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
              <thead>
                <tr style={{ background: 'var(--color-background-secondary)' }}>
                  {['Fund Name *', 'Units *', 'Buy Price', 'Purchase Date', 'Region'].map(h => (
                    <th key={h} style={{ padding: '8px 12px', textAlign: 'left', fontWeight: 500, color: 'var(--color-text-secondary)', borderBottom: '1px solid #e8e8e4', whiteSpace: 'nowrap' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {[
                  ['Parag Parikh Flexi Cap Fund', '150', '68.50', '15/01/2023', 'INDIA'],
                  ['HDFC Mid-Cap Opportunities Fund', '80', '95.20', '10/03/2022', 'INDIA'],
                  ['Vanguard S&P 500 ETF', '12', '380.00', '05/02/2023', 'GLOBAL'],
                ].map((row, i) => (
                  <tr key={i}>
                    {row.map((cell, j) => (
                      <td key={j} style={{ padding: '7px 12px', fontSize: 12, color: 'var(--color-text-secondary)', borderBottom: i < 2 ? '1px solid #f1f5f9' : 'none', fontFamily: j > 0 ? 'var(--font-mono, monospace)' : 'inherit' }}>
                        {cell}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)', marginTop: 10 }}>
            * Required · Also accepted: "Scheme Name", "Quantity", "Qty", "Avg Cost", "Avg NAV", "Cost Basis"
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Step 2: Review & Match ────────────────────────────────────────────────────
function MatchRow({ row, index, onUpdate }) {
  const [searching, setSearching] = useState(false);
  const [results, setResults] = useState(null);
  const [showPicker, setShowPicker] = useState(false);

  async function search() {
    setSearching(true);
    try {
      const { data } = await api.get('/funds/search', {
        params: { q: row.csvName.slice(0, 60), region: row.region },
      });
      const ranked = rankMatches(row.csvName, data.results);
      setResults(ranked);
      // Auto-select top match if high confidence
      if (ranked.length > 0 && ranked[0].score >= 0.75) {
        onUpdate(index, { matched: ranked[0].fund, status: ranked[0].score >= 0.85 ? 'auto' : 'review' });
      } else {
        onUpdate(index, { status: 'unmatched' });
      }
    } catch {
      onUpdate(index, { status: 'error' });
    } finally {
      setSearching(false);
    }
  }

  const conf = row.matched ? confidence(row.matchScore ?? 0) : null;
  const confColor = conf === 'high' ? 'green' : conf === 'medium' ? 'amber' : 'red';

  return (
    <div style={{ padding: '14px 20px', borderBottom: '1px solid #f1f5f9' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        {/* Status icon */}
        <div style={{ width: 24, height: 24, borderRadius: '50%', flexShrink: 0, marginTop: 2, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12,
          background: row.status === 'auto' ? '#dcfce7' : row.status === 'review' ? '#fef9c3' : row.status === 'unmatched' ? '#fee2e2' : row.status === 'skip' ? '#f1f5f9' : '#f1f5f9',
          color:      row.status === 'auto' ? '#15803d' : row.status === 'review' ? '#92400e' : row.status === 'unmatched' ? '#b91c1c' : '#64748b',
        }}>
          {row.status === 'auto' ? '✓' : row.status === 'review' ? '?' : row.status === 'skip' ? '—' : '!'}
        </div>

        <div style={{ flex: 1, minWidth: 0 }}>
          {/* CSV name */}
          <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--color-text-primary)', marginBottom: 4 }}>
            {row.csvName}
          </div>
          <div style={{ display: 'flex', gap: 12, fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono, monospace)', flexWrap: 'wrap', marginBottom: 8 }}>
            <span>{row.units} units</span>
            {row.avgCost && <span>@ ₹{row.avgCost}</span>}
            <span>{row.date}</span>
            <span style={S.badge(row.region === 'INDIA' ? 'amber' : 'blue')}>{row.region}</span>
          </div>

          {/* Matched fund */}
          {row.matched && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: 'var(--color-background-secondary)', borderRadius: 8, marginBottom: 8 }}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 13, color: 'var(--color-text-primary)', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {row.matched.name}
                </div>
                <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono, monospace)', marginTop: 2 }}>
                  {row.matched.ticker} · NAV {row.matched.latestNav != null ? (row.region === 'INDIA' ? '₹' : '$') + Number(row.matched.latestNav).toFixed(2) : '—'}
                </div>
              </div>
              <span style={S.badge(confColor)}>
                {conf === 'high' ? 'Good match' : conf === 'medium' ? 'Check match' : 'Weak match'}
              </span>
            </div>
          )}

          {/* Action buttons */}
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {!results && row.status !== 'skip' && (
              <button style={S.btn('sm')} onClick={search} disabled={searching}>
                {searching ? 'Searching…' : row.matched ? 'Re-search' : 'Find match'}
              </button>
            )}
            {results && (
              <button style={{ ...S.btn('sm'), background: '#f1f5f9', color: '#475569' }}
                onClick={() => setShowPicker(!showPicker)}>
                {showPicker ? 'Hide options' : `Pick from ${results.length} results`}
              </button>
            )}
            {row.matched && (
              <button style={{ ...S.btn('sm'), background: '#f1f5f9', color: '#475569' }}
                onClick={() => onUpdate(index, { matched: null, status: 'unmatched', matchScore: 0 })}>
                Clear
              </button>
            )}
            <button style={{ ...S.btn('sm'), background: '#f1f5f9', color: row.status === 'skip' ? '#2563eb' : '#94a3b8' }}
              onClick={() => onUpdate(index, { status: row.status === 'skip' ? 'unmatched' : 'skip', matched: null })}>
              {row.status === 'skip' ? 'Unskip' : 'Skip'}
            </button>
          </div>

          {/* Search result picker */}
          {showPicker && results && (
            <div style={{ marginTop: 10, border: '1px solid #e8e8e4', borderRadius: 8, overflow: 'hidden', maxHeight: 220, overflowY: 'auto' }}>
              {results.length === 0 ? (
                <div style={{ padding: '12px 14px', fontSize: 13, color: 'var(--color-text-tertiary)' }}>
                  No matches found. Try adjusting the fund name.
                </div>
              ) : results.map(({ fund, score: s }, i) => (
                <div key={fund.id || i}
                  onClick={() => { onUpdate(index, { matched: fund, status: 'review', matchScore: s }); setShowPicker(false); }}
                  style={{ padding: '10px 14px', cursor: 'pointer', borderBottom: i < results.length - 1 ? '1px solid #f1f5f9' : 'none',
                    background: row.matched?.id === fund.id ? '#eff6ff' : 'transparent',
                    transition: 'background 0.1s' }}
                  onMouseEnter={e => e.currentTarget.style.background = '#f8fafc'}
                  onMouseLeave={e => e.currentTarget.style.background = row.matched?.id === fund.id ? '#eff6ff' : 'transparent'}
                >
                  <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--color-text-primary)' }}>{fund.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono, monospace)', marginTop: 2, display: 'flex', gap: 10 }}>
                    <span>{fund.ticker}</span>
                    {fund.latestNav && <span>NAV {Number(fund.latestNav).toFixed(2)}</span>}
                    <span style={S.badge(s >= 0.85 ? 'green' : s >= 0.55 ? 'amber' : 'red')}>
                      {Math.round(s * 100)}% match
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function ReviewStep({ parsed, onImport, onBack }) {
  const [rows, setRows] = useState(() =>
    parsed.rows.map(r => ({ ...r, csvName: r.name, matched: null, status: 'pending', matchScore: 0 }))
  );
  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState('');
  const qc = useQueryClient();
  const setOverlayMessage = useUIStore(s => s.setOverlayMessage);
  const clearOverlayMessage = useUIStore(s => s.clearOverlayMessage);

  useEffect(() => {
    if (importing) setOverlayMessage('Importing holdings…');
    else clearOverlayMessage();
  }, [importing]);

  const updateRow = useCallback((index, updates) => {
    setRows(prev => prev.map((r, i) => i === index ? { ...r, ...updates } : r));
  }, []);

  async function searchAll() {
    for (let i = 0; i < rows.length; i++) {
      if (rows[i].status !== 'pending') continue;
      try {
        const { data } = await api.get('/funds/search', {
          params: { q: rows[i].csvName.slice(0, 60), region: rows[i].region },
        });
        const ranked = rankMatches(rows[i].csvName, data.results);
        if (ranked.length > 0 && ranked[0].score >= 0.75) {
          updateRow(i, { matched: ranked[0].fund, status: ranked[0].score >= 0.85 ? 'auto' : 'review', matchScore: ranked[0].score });
        } else {
          updateRow(i, { status: 'unmatched' });
        }
      } catch {
        updateRow(i, { status: 'error' });
      }
      // Small delay between searches to be polite to the API
      await new Promise(r => setTimeout(r, 300));
    }
  }

  async function handleImport() {
    const toImport = rows.filter(r => r.matched && r.status !== 'skip');
    if (!toImport.length) { setImportError('No matched funds to import.'); return; }

    setImporting(true);
    setImportError('');
    let successCount = 0;
    const errors = [];

    for (const row of toImport) {
      try {
        // Ensure fund exists in DB
        await api.post('/funds/ensure', {
          id: row.matched.id,
          region: row.matched.region,
          name: row.matched.name,
          amc: row.matched.amc || '',
          ticker: row.matched.ticker,
          schemeCode: row.matched.schemeCode,
          category: row.matched.category,
        });
        // Add holding
        await api.post('/portfolio/holdings', {
          fundId: row.matched.id,
          units: Number(row.units),
          avgCost: row.avgCost ? Number(row.avgCost) : undefined,
          purchaseAt: row.date,
        });
        successCount++;
      } catch (err) {
        errors.push(`${row.csvName}: ${err.message}`);
      }
    }

    setImporting(false);
    if (successCount > 0) {
      qc.invalidateQueries({ queryKey: ['portfolio'] });
      onImport(successCount, errors);
    } else {
      setImportError('Import failed: ' + errors.join('; '));
    }
  }

  const matched   = rows.filter(r => r.matched && r.status !== 'skip').length;
  const pending   = rows.filter(r => r.status === 'pending').length;
  const unmatched = rows.filter(r => r.status === 'unmatched' || r.status === 'error').length;
  const skipped   = rows.filter(r => r.status === 'skip').length;

  return (
    <div>
      {/* Summary bar */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 16, flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ display: 'flex', gap: 10, flex: 1, flexWrap: 'wrap' }}>
          {[
            { label: `${matched} ready`, color: 'green' },
            { label: `${unmatched} need match`, color: unmatched > 0 ? 'red' : 'green' },
            pending > 0 && { label: `${pending} not searched`, color: 'amber' },
            skipped > 0 && { label: `${skipped} skipped`, color: 'slate' },
          ].filter(Boolean).map(b => (
            <span key={b.label} style={S.badge(b.color)}>{b.label}</span>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          {pending > 0 && (
            <button style={S.btn('secondary')} onClick={searchAll}>
              Auto-match all
            </button>
          )}
          <button style={S.btn('secondary')} onClick={onBack}>Back</button>
          <button style={{ ...S.btn('primary'), opacity: matched === 0 || importing ? 0.65 : 1 }}
            onClick={handleImport} disabled={matched === 0 || importing}>
            {importing ? 'Importing…' : `Import ${matched} fund${matched !== 1 ? 's' : ''}`}
          </button>
        </div>
      </div>

      {parsed.errors?.length > 0 && (
        <div style={{ padding: '10px 14px', borderRadius: 8, background: '#fffbeb', border: '1px solid #fcd34d', fontSize: 12, color: '#92400e', marginBottom: 14 }}>
          {parsed.errors.length} row{parsed.errors.length > 1 ? 's' : ''} skipped during parsing: {parsed.errors.join(' · ')}
        </div>
      )}

      {importError && (
        <div style={{ padding: '10px 14px', borderRadius: 8, background: '#fef2f2', border: '1px solid #fecaca', fontSize: 13, color: '#b91c1c', marginBottom: 14 }}>
          {importError}
        </div>
      )}

      <div style={S.card}>
        <div style={S.cardHeader}>
          <span style={S.cardTitle}>Review matches — {rows.length} funds from CSV</span>
        </div>
        {rows.map((row, i) => (
          <MatchRow key={i} row={row} index={i} onUpdate={updateRow} />
        ))}
      </div>
    </div>
  );
}

// ── Step 3: Done ──────────────────────────────────────────────────────────────
function DoneStep({ count, errors, onViewPortfolio, onUploadMore }) {
  return (
    <div style={{ textAlign: 'center', padding: '48px 24px' }}>
      <div style={{ width: 64, height: 64, borderRadius: '50%', background: '#dcfce7', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 20px', fontSize: 28 }}>
        <svg width="28" height="28" viewBox="0 0 28 28" fill="none">
          <path d="M6 14l6 6L22 8" stroke="#16a34a" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/>
        </svg>
      </div>
      <h2 style={{ fontSize: 20, fontWeight: 500, color: 'var(--color-text-primary)', margin: '0 0 8px' }}>
        {count} fund{count !== 1 ? 's' : ''} imported
      </h2>
      <p style={{ fontSize: 14, color: 'var(--color-text-secondary)', margin: '0 0 24px' }}>
        Your portfolio has been updated with the imported holdings.
      </p>
      {errors?.length > 0 && (
        <div style={{ padding: '10px 14px', borderRadius: 8, background: '#fffbeb', border: '1px solid #fcd34d', fontSize: 12, color: '#92400e', marginBottom: 20, textAlign: 'left' }}>
          {errors.length} fund{errors.length > 1 ? 's' : ''} failed: {errors.join(' · ')}
        </div>
      )}
      <div style={{ display: 'flex', gap: 10, justifyContent: 'center' }}>
        <button style={S.btn('primary')} onClick={onViewPortfolio}>View portfolio</button>
        <button style={S.btn('secondary')} onClick={onUploadMore}>Upload more</button>
      </div>
    </div>
  );
}

// ── Main UploadPage ───────────────────────────────────────────────────────────
export function UploadPage({ onDone }) {
  const [step, setStep] = useState('upload'); // upload | review | done
  const [parsed, setParsed] = useState(null);
  const [importResult, setImportResult] = useState(null);

  return (
    <div>
      {/* Step indicator */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 0, marginBottom: 24 }}>
        {[
          { id: 'upload', label: 'Upload CSV' },
          { id: 'review', label: 'Review & match' },
          { id: 'done',   label: 'Import' },
        ].map((s, i, arr) => (
          <div key={s.id} style={{ display: 'flex', alignItems: 'center' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <div style={{
                width: 24, height: 24, borderRadius: '50%', fontSize: 12, fontWeight: 500,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                background: step === s.id ? '#2563eb' : ['upload','review','done'].indexOf(step) > i ? '#dcfce7' : '#f1f5f9',
                color:      step === s.id ? '#fff' : ['upload','review','done'].indexOf(step) > i ? '#15803d' : '#94a3b8',
              }}>
                {['upload','review','done'].indexOf(step) > i ? '✓' : i + 1}
              </div>
              <span style={{ fontSize: 13, color: step === s.id ? 'var(--color-text-primary)' : 'var(--color-text-tertiary)', fontWeight: step === s.id ? 500 : 400 }}>
                {s.label}
              </span>
            </div>
            {i < arr.length - 1 && (
              <div style={{ width: 32, height: 1, background: '#e8e8e4', margin: '0 12px' }} />
            )}
          </div>
        ))}
      </div>

      {step === 'upload' && (
        <UploadStep onParsed={(result) => { setParsed(result); setStep('review'); }} />
      )}
      {step === 'review' && parsed && (
        <ReviewStep
          parsed={parsed}
          onBack={() => setStep('upload')}
          onImport={(count, errors) => { setImportResult({ count, errors }); setStep('done'); }}
        />
      )}
      {step === 'done' && importResult && (
        <DoneStep
          count={importResult.count}
          errors={importResult.errors}
          onViewPortfolio={onDone}
          onUploadMore={() => { setParsed(null); setImportResult(null); setStep('upload'); }}
        />
      )}
    </div>
  );
}
