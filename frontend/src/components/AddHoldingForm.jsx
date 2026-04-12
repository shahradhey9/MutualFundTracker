import { useState, useEffect } from 'react';
import { useAddHolding, useUpdateHolding } from '../hooks/usePortfolio.js';
import { useUIStore } from '../lib/store.js';
import { fmtCurrency } from '../lib/format.js';

const label = {
  display: 'block',
  fontSize: 11,
  color: 'var(--color-text-tertiary)',
  fontFamily: 'var(--font-mono)',
  marginBottom: 5,
};

const input = {
  width: '100%',
  padding: '8px 12px',
  fontSize: 13,
  border: '0.5px solid var(--color-border-secondary)',
  borderRadius: 'var(--border-radius-md)',
  background: 'var(--color-background-primary)',
  color: 'var(--color-text-primary)',
  fontFamily: 'var(--font-mono)',
  outline: 'none',
  boxSizing: 'border-box',
};

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

  const [units, setUnits] = useState('');
  const [avgCost, setAvgCost] = useState('');
  const [purchaseAt, setPurchaseAt] = useState(new Date().toISOString().split('T')[0]);
  const [validationError, setValidationError] = useState('');

  // Pre-fill form when editing
  useEffect(() => {
    if (editingHolding) {
      setUnits(String(editingHolding.units));
      setAvgCost(editingHolding.avgCost ? String(editingHolding.avgCost) : '');
      setPurchaseAt(editingHolding.purchaseAt?.split('T')[0] || new Date().toISOString().split('T')[0]);
    }
  }, [editingHolding]);

  // Show/hide global loading overlay
  useEffect(() => {
    if (adding) setOverlayMessage('Adding holding…');
    else if (updating) setOverlayMessage('Updating holding…');
    else clearOverlayMessage();
  }, [adding, updating]);

  if (!fund && !isEditing) return null;

  const displayName = isEditing ? editingHolding.name : fund.name;
  const displayNav = isEditing
    ? fmtCurrency(editingHolding.liveNav, editingHolding.currency)
    : fund.latestNav != null
    ? fmtCurrency(fund.latestNav, fund.currency || (fund.region === 'INDIA' ? 'INR' : 'USD'))
    : 'Fetching…';

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
        {
          onSuccess: () => {
            clearEditingHolding();
            setActiveTab('portfolio');
          },
        }
      );
    } else {
      addHolding(
        { fund, units: Number(units), avgCost: avgCost ? Number(avgCost) : undefined, purchaseAt },
        {
          onSuccess: () => {
            clearSearch();
            clearSelectedFund();
            setActiveTab('portfolio');
          },
        }
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
    <div style={{
      background: 'var(--color-background-secondary)',
      borderRadius: 'var(--border-radius-lg)',
      padding: '1.25rem',
      marginTop: 16,
    }}>
      <div style={{ fontWeight: 500, fontSize: 14, color: 'var(--color-text-primary)', marginBottom: 3 }}>
        {displayName}
      </div>
      <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)', marginBottom: 16 }}>
        {isEditing ? 'Editing holding' : `Current NAV: ${displayNav}`}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12, marginBottom: 12 }}>
        <div>
          <label style={label}>Units held</label>
          <input style={input} type="number" step="0.001" min="0" value={units} onChange={e => setUnits(e.target.value)} placeholder="0.000" autoFocus />
        </div>
        <div>
          <label style={label}>Purchase date</label>
          <input style={input} type="date" value={purchaseAt} onChange={e => setPurchaseAt(e.target.value)} />
        </div>
        <div>
          <label style={label}>Avg cost / unit (optional)</label>
          <input style={input} type="number" step="0.01" min="0" value={avgCost} onChange={e => setAvgCost(e.target.value)} placeholder="For P&L tracking" />
        </div>
      </div>

      {(validationError || addError) && (
        <div style={{ fontSize: 12, color: '#A32D2D', marginBottom: 10 }}>
          {validationError || addErr?.message}
        </div>
      )}

      <div style={{ display: 'flex', gap: 8 }}>
        <button
          onClick={handleSubmit}
          disabled={isPending}
          style={{
            padding: '9px 20px',
            fontSize: 13,
            fontWeight: 500,
            border: '0.5px solid var(--color-border-secondary)',
            borderRadius: 'var(--border-radius-md)',
            background: 'var(--color-text-primary)',
            color: 'var(--color-background-primary)',
            cursor: isPending ? 'not-allowed' : 'pointer',
            opacity: isPending ? 0.6 : 1,
          }}
        >
          {isPending ? 'Saving…' : isEditing ? 'Update holding' : 'Add to portfolio'}
        </button>
        <button
          onClick={handleCancel}
          style={{
            padding: '9px 16px',
            fontSize: 13,
            border: '0.5px solid var(--color-border-secondary)',
            borderRadius: 'var(--border-radius-md)',
            background: 'none',
            color: 'var(--color-text-secondary)',
            cursor: 'pointer',
          }}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
