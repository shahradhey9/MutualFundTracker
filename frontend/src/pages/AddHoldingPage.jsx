import { FundSearch } from '../components/FundSearch.jsx';
import { AddHoldingForm } from '../components/AddHoldingForm.jsx';
import { useUIStore } from '../lib/store.js';

export function AddHoldingPage() {
  const { selectedFund, editingHolding } = useUIStore();
  const showForm = !!selectedFund || !!editingHolding;

  return (
    <div>
      <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--color-text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 10 }}>
        {editingHolding ? 'Edit holding' : 'Search for a fund or ETF'}
      </div>

      {!editingHolding && <FundSearch />}
      {showForm && <AddHoldingForm />}
    </div>
  );
}
