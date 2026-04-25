import { useState, useEffect } from 'react';
import { useAddHolding, useUpdateHolding, useFundNav } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';
import { useDisplayRates } from '../hooks/useDisplayRates.js';
import { fmtCurrency } from '../lib/format.js';

export function AddHoldingForm() {
  const {
    selectedFund, clearSelectedFund,
    editingHolding, clearEditingHolding,
    clearSearch, setActiveTab,
    setOverlayMessage, clearOverlayMessage,
  } = useUIStore();

  const { mutate: addHolding, isPending: adding, isError: addError, error: addErr } = useAddHolding();
  const { mutate: updateHolding, isPending: updating } = useUpdateHolding();

  const isEditing = !!editingHolding;
  const fund = isEditing ? null : selectedFund;

  const { displayCurrency, convert } = useDisplayRates();

  const [units, setUnits] = useState('');
  const [avgCost, setAvgCost] = useState('');
  const [purchaseAt, setPurchaseAt] = useState(new Date().toISOString().split('T')[0]);
  const [validationError, setValidationError] = useState('');

  useEffect(() => {
    if (editingHolding) {
      setUnits(String(editingHolding.units));
      setAvgCost(editingHolding.avgCost ? String(editingHolding.avgCost) : '');
      setPurchaseAt(editingHolding.purchaseAt?.split('T')[0] || new Date().toISOString().split('T')[0]);
    }
  }, [editingHolding]);

  useEffect(() => {
    if (adding) setOverlayMessage('Adding holding…');
    else if (updating) setOverlayMessage('Updating holding…');
    else clearOverlayMessage();
  }, [adding, updating]);

  const needsLiveNav = !isEditing && fund?.region === 'GLOBAL' && fund?.latestNav == null;
  const { data: liveNavData, isFetching: navFetching } = useFundNav(
    needsLiveNav ? fund?.ticker : null,
    'GLOBAL'
  );

  if (!fund && !isEditing) return null;

  const displayName = isEditing ? editingHolding.name : fund.name;
  const navCurrency = fund?.currency || (fund?.region === 'INDIA' ? 'INR' : 'USD');
  const editCurrency = editingHolding?.currency || 'USD';
  const displayNav = isEditing
    ? fmtCurrency(convert(editingHolding.liveNav, editCurrency), displayCurrency)
    : fund.latestNav != null
    ? fmtCurrency(convert(fund.latestNav, navCurrency), displayCurrency)
    : liveNavData?.nav != null
    ? fmtCurrency(convert(liveNavData.nav, liveNavData.currency || navCurrency), displayCurrency)
    : navFetching ? 'Fetching…' : '—';

  function validate() {
    if (!units || Number(units) <= 0) { setValidationError('Enter a valid number of units.'); return false; }
    if (!purchaseAt) { setValidationError('Select a purchase date.'); return false; }
    setValidationError('');
    return true;
  }

  function handleSubmit() {
    if (!validate()) return;
    if (isEditing) {
      updateHolding(
        { holdingId: editingHolding.holdingId, units: Number(units), avgCost: avgCost ? Number(avgCost) : undefined, purchaseAt },
        { onSuccess: () => { clearEditingHolding(); setActiveTab('portfolio'); } }
      );
    } else {
      addHolding(
        { fund, units: Number(units), avgCost: avgCost ? Number(avgCost) : undefined, purchaseAt },
        { onSuccess: () => { clearSearch(); clearSelectedFund(); setActiveTab('portfolio'); } }
      );
    }
  }

  function handleCancel() {
    clearSelectedFund();
    clearEditingHolding();
    clearSearch();
  }

  const isPending = adding || updating;

  return (
    <>
      <style>{`
        .holding-form {
          background: var(--bg-card);
          border: 1.5px solid var(--border);
          border-radius: var(--radius-lg);
          padding: 20px;
          margin-top: 16px;
          box-shadow: var(--shadow-card);
          animation: gwt-fade-in 0.2s ease;
        }
        .form-fund-name {
          font-size: 14px;
          font-weight: 600;
          color: var(--text-primary);
          margin-bottom: 3px;
        }
        .form-nav-line {
          font-size: 12px;
          color: var(--text-muted);
          font-family: var(--font-mono);
          margin-bottom: 20px;
        }
        .form-grid {
          display: grid;
          grid-template-columns: 1fr 1fr 1fr;
          gap: 14px;
          margin-bottom: 16px;
        }
        @media (max-width: 600px) { .form-grid { grid-template-columns: 1fr; } }
        .form-label {
          display: block;
          font-size: 11px;
          font-weight: 600;
          color: var(--text-muted);
          font-family: var(--font-mono);
          letter-spacing: 0.05em;
          text-transform: uppercase;
          margin-bottom: 6px;
        }
        .form-input {
          width: 100%;
          padding: 9px 12px;
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
        .form-input:focus {
          border-color: var(--border-focus);
          box-shadow: 0 0 0 3px var(--accent-ring);
        }
        .form-input::placeholder { color: var(--text-muted); }
        .form-actions { display: flex; gap: 10px; align-items: center; }
        .form-error {
          font-size: 12px;
          color: var(--color-loss);
          background: var(--color-loss-bg);
          border: 1px solid var(--color-loss);
          border-radius: var(--radius-sm);
          padding: 8px 12px;
          margin-bottom: 14px;
          opacity: 0.9;
        }
      `}</style>

      <div className="holding-form">
        <div className="form-fund-name">{displayName}</div>
        <div className="form-nav-line">
          {isEditing ? 'Editing holding' : `Current NAV: ${displayNav}`}
        </div>

        <div className="form-grid">
          <div>
            <label className="form-label">Units held</label>
            <input className="form-input" type="number" step="0.001" min="0"
              value={units} onChange={e => setUnits(e.target.value)}
              placeholder="0.000" autoFocus />
          </div>
          <div>
            <label className="form-label">Purchase date</label>
            <input className="form-input" type="date"
              value={purchaseAt} onChange={e => setPurchaseAt(e.target.value)} />
          </div>
          <div>
            <label className="form-label">Avg cost / unit <span style={{ textTransform: 'none', fontWeight: 400 }}>(optional)</span></label>
            <input className="form-input" type="number" step="0.01" min="0"
              value={avgCost} onChange={e => setAvgCost(e.target.value)}
              placeholder="For P&L tracking" />
          </div>
        </div>

        {(validationError || addError) && (
          <div className="form-error">{validationError || addErr?.message}</div>
        )}

        <div className="form-actions">
          <button className="btn btn-primary" onClick={handleSubmit} disabled={isPending}>
            {isPending ? 'Saving…' : isEditing ? 'Update holding' : 'Add to portfolio'}
          </button>
          <button className="btn btn-secondary" onClick={handleCancel}>
            Cancel
          </button>
        </div>
      </div>
    </>
  );
}
