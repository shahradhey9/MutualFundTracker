import { useState } from 'react';
import { useGoals, useCreateGoal, useUpdateGoal, useDeleteGoal } from '../hooks/useGoals.js';
import { usePortfolio } from '../hooks/usePortfolio.js';
import { fmtINR, fmtPct } from '../lib/format.js';

const GOAL_TYPES = [
  "Child's Education",
  "Retirement",
  "Home Purchase",
  "Emergency Fund",
  "Wealth Creation",
  "Travel",
  "Marriage",
  "Vehicle",
  "Other",
];

const PRIORITIES = ['HIGH', 'MEDIUM', 'LOW'];

const PRIORITY_COLORS = {
  HIGH: '#ef4444',
  MEDIUM: '#f59e0b',
  LOW: '#22c55e',
};

function fmtYearsToGo(years) {
  if (years <= 0) return 'Matured';
  const y = Math.floor(years);
  const m = Math.round((years - y) * 12);
  const parts = [];
  if (y > 0) parts.push(`${y} year${y !== 1 ? 's' : ''}`);
  if (m > 0) parts.push(`${m} month${m !== 1 ? 's' : ''}`);
  return parts.join(', ') + ' to go';
}

function DonutChart({ equityPct, size = 80 }) {
  const r = 28;
  const circ = 2 * Math.PI * r;
  const equityDash = (equityPct / 100) * circ;
  const debtDash = circ - equityDash;
  return (
    <svg width={size} height={size} viewBox="0 0 70 70">
      <circle cx="35" cy="35" r={r} fill="none" stroke="var(--border-light)" strokeWidth="10" />
      <circle cx="35" cy="35" r={r} fill="none" stroke="#3b82f6" strokeWidth="10"
        strokeDasharray={`${equityDash} ${debtDash}`}
        strokeDashoffset={circ * 0.25}
        strokeLinecap="round"
        transform="rotate(-90 35 35)" style={{ transition: 'stroke-dasharray 0.4s' }} />
      <circle cx="35" cy="35" r={r} fill="none" stroke="#22c55e" strokeWidth="10"
        strokeDasharray={`${debtDash} ${equityDash}`}
        strokeDashoffset={-(equityDash - circ * 0.25)}
        strokeLinecap="round"
        transform="rotate(-90 35 35)" style={{ transition: 'stroke-dasharray 0.4s' }} />
    </svg>
  );
}

function ProgressBar({ pct }) {
  const clamped = Math.min(100, Math.max(0, pct));
  return (
    <div style={{ height: 6, background: 'var(--border-light)', borderRadius: 4, overflow: 'hidden', marginTop: 6 }}>
      <div style={{
        height: '100%', width: `${clamped}%`,
        background: clamped >= 100 ? '#22c55e' : 'var(--accent)',
        borderRadius: 4, transition: 'width 0.4s',
      }} />
    </div>
  );
}

function GoalCard({ goal, onEdit, onDelete }) {
  const [deleting, setDeleting] = useState(false);

  async function handleDelete() {
    if (!window.confirm(`Delete goal "${goal.title}"?`)) return;
    setDeleting(true);
    try { await onDelete(goal.id); } finally { setDeleting(false); }
  }

  return (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border-light)',
      borderRadius: 'var(--radius-xl)', padding: '22px 24px',
      boxShadow: 'var(--shadow-card)', marginBottom: 20,
    }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 16 }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{
              fontSize: 11, fontWeight: 700, letterSpacing: '.06em',
              textTransform: 'uppercase', color: '#fff',
              background: PRIORITY_COLORS[goal.priority] || '#6b7280',
              borderRadius: 'var(--radius-pill)', padding: '2px 8px',
            }}>{goal.priority}</span>
            <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{goal.goalType}</span>
          </div>
          <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4 }}>
            GOAL: {goal.title.toUpperCase()}
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{fmtYearsToGo(goal.yearsToGo)}</span>
          <button onClick={() => onEdit(goal)} style={{
            background: 'none', border: '1px solid var(--border)',
            borderRadius: 'var(--radius-sm)', padding: '4px 10px',
            fontSize: 12, cursor: 'pointer', color: 'var(--text-secondary)',
            fontFamily: 'inherit',
          }}>Edit</button>
          <button onClick={handleDelete} disabled={deleting} style={{
            background: 'none', border: '1px solid rgba(239,68,68,0.3)',
            borderRadius: 'var(--radius-sm)', padding: '4px 10px',
            fontSize: 12, cursor: 'pointer', color: '#ef4444',
            fontFamily: 'inherit',
          }}>Delete</button>
        </div>
      </div>

      {/* Metrics */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16, marginBottom: 20 }}>
        {[
          { label: 'Current Amount', value: fmtINR(goal.currentAmount) },
          { label: 'Goal Amount', value: fmtINR(goal.inflationAdjustedTarget) },
          { label: 'Growth', value: goal.growth != null ? fmtPct(goal.growth) : '—' },
          { label: 'Progress', value: fmtPct(goal.progress, false) },
        ].map(({ label, value }) => (
          <div key={label}>
            <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 4 }}>{label}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>{value}</div>
          </div>
        ))}
      </div>
      <ProgressBar pct={goal.progress} />

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20, marginTop: 20, borderTop: '1px solid var(--border-light)', paddingTop: 16 }}>
        {/* Asset Allocation */}
        <div>
          <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '.07em', textTransform: 'uppercase', color: 'var(--text-muted)', marginBottom: 12 }}>Asset Allocation</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <DonutChart equityPct={goal.actualEquityPct} />
            <div>
              <div style={{ display: 'flex', gap: 20 }}>
                {[
                  { label: 'Equity', actual: goal.actualEquityPct, target: goal.targetEquityPct, color: '#3b82f6' },
                  { label: 'Debt', actual: goal.actualDebtPct, target: goal.targetDebtPct, color: '#22c55e' },
                ].map(({ label, actual, target, color }) => (
                  <div key={label}>
                    <div style={{ fontSize: 10, color: 'var(--text-muted)', marginBottom: 2 }}>{label}</div>
                    <div style={{ fontSize: 20, fontWeight: 700, color }}>{actual}%</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{target}%</div>
                  </div>
                ))}
              </div>
              <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 6 }}>Target: {goal.targetEquityPct}% Equity / {goal.targetDebtPct}% Debt</div>
            </div>
          </div>
        </div>

        {/* Tagged Funds */}
        <div>
          <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '.07em', textTransform: 'uppercase', color: 'var(--text-muted)', marginBottom: 12 }}>Tagged Funds</div>
          {goal.taggedFunds.length === 0
            ? <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>No funds tagged</div>
            : <ol style={{ margin: 0, paddingLeft: 18 }}>
                {goal.taggedFunds.map((f, i) => (
                  <li key={f.holdingId} style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 4, lineHeight: 1.4 }}>
                    {f.name}
                    {f.currentValue != null && (
                      <span style={{ color: 'var(--text-muted)', marginLeft: 6 }}>— {fmtINR(f.currentValue)}</span>
                    )}
                  </li>
                ))}
              </ol>
          }
        </div>
      </div>
    </div>
  );
}

const EMPTY_FORM = {
  title: '',
  goalType: "Child's Education",
  targetAmount: '',
  endDate: '',
  priority: 'HIGH',
  inflationRate: '6',
  targetDebtPct: '20',
  holdingIds: [],
};

function GoalModal({ open, onClose, initialData, holdings, onSubmit, isSaving }) {
  const [form, setForm] = useState(initialData || EMPTY_FORM);

  if (!open) return null;

  function set(field, value) {
    setForm(prev => ({ ...prev, [field]: value }));
  }

  function toggleHolding(holdingId) {
    set('holdingIds', form.holdingIds.includes(holdingId)
      ? form.holdingIds.filter(id => id !== holdingId)
      : [...form.holdingIds, holdingId]);
  }

  function handleSubmit(e) {
    e.preventDefault();
    onSubmit({
      title: form.title.trim(),
      goalType: form.goalType,
      targetAmount: Number(form.targetAmount),
      endDate: new Date(form.endDate).toISOString(),
      priority: form.priority,
      inflationRate: Number(form.inflationRate),
      targetDebtPct: Number(form.targetDebtPct),
      holdingIds: form.holdingIds,
    });
  }

  const debtPct = Math.min(100, Math.max(0, Number(form.targetDebtPct) || 0));
  const equityPct = 100 - debtPct;

  return (
    <div style={{
      position: 'fixed', inset: 0, zIndex: 1000,
      background: 'rgba(0,0,0,0.45)',
      display: 'flex', alignItems: 'flex-start', justifyContent: 'center',
      padding: '40px 16px', overflowY: 'auto',
    }} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border-light)',
        borderRadius: 'var(--radius-xl)', width: '100%', maxWidth: 480,
        boxShadow: 'var(--shadow-md)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '20px 24px 0' }}>
          <h2 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>
            {initialData ? 'Edit Goal' : 'Add Goal'}
          </h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 18, color: 'var(--text-muted)', lineHeight: 1, padding: 4 }}>✕</button>
        </div>

        <form onSubmit={handleSubmit} style={{ padding: '16px 24px 24px', display: 'flex', flexDirection: 'column', gap: 12 }}>
          <Field label="Goal Type">
            <select value={form.goalType} onChange={e => set('goalType', e.target.value)} style={inputStyle}>
              {GOAL_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </Field>
          <Field label="Goal Title">
            <input required value={form.title} onChange={e => set('title', e.target.value)}
              placeholder="e.g. A Edu" style={inputStyle} />
          </Field>
          <Field label="Target Amount (₹)">
            <input required type="number" min="1" value={form.targetAmount} onChange={e => set('targetAmount', e.target.value)}
              placeholder="10000000" style={inputStyle} />
          </Field>
          <Field label="End Date">
            <input required type="date" value={form.endDate} onChange={e => set('endDate', e.target.value)} style={inputStyle} />
          </Field>
          <Field label="Priority">
            <select value={form.priority} onChange={e => set('priority', e.target.value)} style={inputStyle}>
              {PRIORITIES.map(p => <option key={p} value={p}>{p}</option>)}
            </select>
          </Field>
          <Field label="Inflation Rate (%)">
            <input type="number" min="0" max="30" step="0.5" value={form.inflationRate}
              onChange={e => set('inflationRate', e.target.value)} style={inputStyle} />
          </Field>
          <Field label={`Target Asset Allocation (Debt, 0–100)`}>
            <input type="number" min="0" max="100" value={form.targetDebtPct}
              onChange={e => set('targetDebtPct', e.target.value)} style={inputStyle} />
            <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4 }}>
              Debt – {debtPct}, Equity – {equityPct}
            </div>
          </Field>

          {/* Fund selection */}
          <div>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: '.07em', textTransform: 'uppercase', color: 'var(--text-muted)', marginBottom: 8 }}>Select Funds</div>
            <div style={{
              maxHeight: 220, overflowY: 'auto',
              border: '1px solid var(--border-light)', borderRadius: 'var(--radius-lg)',
            }}>
              {!holdings || holdings.length === 0
                ? <div style={{ padding: '12px 14px', fontSize: 13, color: 'var(--text-muted)' }}>No holdings in portfolio</div>
                : holdings.map(h => {
                    const checked = form.holdingIds.includes(h.holdingId);
                    return (
                      <label key={h.holdingId} style={{
                        display: 'flex', alignItems: 'flex-start', gap: 10,
                        padding: '10px 14px',
                        borderBottom: '1px solid var(--border-light)',
                        cursor: 'pointer',
                        background: checked ? 'var(--accent-light)' : 'transparent',
                        transition: 'background 0.12s',
                      }}>
                        <input type="checkbox" checked={checked}
                          onChange={() => toggleHolding(h.holdingId)}
                          style={{ marginTop: 2, accentColor: 'var(--accent)', flexShrink: 0 }} />
                        <div>
                          <div style={{ fontSize: 12, fontWeight: 500, color: 'var(--text-primary)', lineHeight: 1.4 }}>
                            {h.name}
                          </div>
                          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                            {h.ticker} · {h.currentValue != null ? fmtINR(h.currentValue) : '—'}
                          </div>
                        </div>
                      </label>
                    );
                  })
              }
            </div>
          </div>

          <div style={{ display: 'flex', gap: 10, marginTop: 4 }}>
            <button type="button" onClick={onClose} style={{
              flex: 1, padding: '10px 0', borderRadius: 'var(--radius-lg)',
              border: '1px solid var(--border)', background: 'var(--bg-app)',
              color: 'var(--text-secondary)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
            }}>Cancel</button>
            <button type="submit" disabled={isSaving} style={{
              flex: 2, padding: '10px 0', borderRadius: 'var(--radius-lg)',
              border: 'none', background: 'var(--accent)',
              color: '#fff', fontSize: 13, fontWeight: 600,
              cursor: 'pointer', fontFamily: 'inherit',
              opacity: isSaving ? 0.7 : 1,
            }}>{isSaving ? 'Saving…' : initialData ? 'Update Goal' : 'Add Goal'}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

function Field({ label, children }) {
  return (
    <div>
      <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-muted)', marginBottom: 5, letterSpacing: '.04em' }}>{label}</div>
      {children}
    </div>
  );
}

const inputStyle = {
  width: '100%', boxSizing: 'border-box',
  padding: '9px 12px', fontSize: 13,
  border: '1px solid var(--border)', borderRadius: 'var(--radius-lg)',
  background: 'var(--bg-app)', color: 'var(--text-primary)',
  fontFamily: 'var(--font-sans)',
  outline: 'none',
};

export function GoalsPage() {
  const { data: goals, isLoading, error } = useGoals();
  const { data: portfolio } = usePortfolio();
  const { mutateAsync: createGoal, isPending: isCreating } = useCreateGoal();
  const { mutateAsync: updateGoal, isPending: isUpdating } = useUpdateGoal();
  const { mutateAsync: deleteGoal } = useDeleteGoal();

  const [modalOpen, setModalOpen] = useState(false);
  const [editingGoal, setEditingGoal] = useState(null);

  const holdings = portfolio || [];

  function openAdd() {
    setEditingGoal(null);
    setModalOpen(true);
  }

  function openEdit(goal) {
    setEditingGoal(goal);
    setModalOpen(true);
  }

  function closeModal() {
    setModalOpen(false);
    setEditingGoal(null);
  }

  async function handleSubmit(payload) {
    if (editingGoal) {
      await updateGoal({ goalId: editingGoal.id, ...payload });
    } else {
      await createGoal(payload);
    }
    closeModal();
  }

  const editInitialData = editingGoal ? {
    title: editingGoal.title,
    goalType: editingGoal.goalType,
    targetAmount: String(editingGoal.targetAmount),
    endDate: editingGoal.endDate.slice(0, 10),
    priority: editingGoal.priority,
    inflationRate: String(editingGoal.inflationRate),
    targetDebtPct: String(editingGoal.targetDebtPct),
    holdingIds: editingGoal.taggedFunds.map(f => f.holdingId),
  } : null;

  return (
    <>
      <style>{`
        .goals-empty {
          text-align: center; padding: 60px 20px;
          color: var(--text-muted); font-size: 14px;
        }
      `}</style>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>Financial Goals</h1>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 3 }}>Track your portfolio against long-term goals</div>
        </div>
        <button onClick={openAdd} style={{
          display: 'flex', alignItems: 'center', gap: 6,
          padding: '9px 18px', borderRadius: 'var(--radius-pill)',
          border: 'none', background: 'var(--accent)',
          color: '#fff', fontSize: 13, fontWeight: 600,
          cursor: 'pointer', fontFamily: 'inherit',
          boxShadow: '0 2px 8px var(--accent-ring)',
        }}>
          <svg width="13" height="13" viewBox="0 0 16 16" fill="currentColor">
            <path d="M8 2a.75.75 0 0 1 .75.75v4.5h4.5a.75.75 0 0 1 0 1.5h-4.5v4.5a.75.75 0 0 1-1.5 0v-4.5h-4.5a.75.75 0 0 1 0-1.5h4.5v-4.5A.75.75 0 0 1 8 2z"/>
          </svg>
          Add Goal
        </button>
      </div>

      {isLoading && (
        <div style={{ color: 'var(--text-muted)', fontSize: 14 }}>Loading goals…</div>
      )}
      {error && (
        <div style={{ color: '#ef4444', fontSize: 14 }}>Failed to load goals. Please try again.</div>
      )}
      {!isLoading && !error && goals?.length === 0 && (
        <div className="goals-empty">
          <div style={{ fontSize: 40, marginBottom: 12 }}>🎯</div>
          <div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-secondary)', marginBottom: 8 }}>No goals yet</div>
          <div>Create your first financial goal and tag funds from your portfolio.</div>
        </div>
      )}

      {goals?.map(goal => (
        <GoalCard key={goal.id} goal={goal} onEdit={openEdit} onDelete={deleteGoal} />
      ))}

      <GoalModal
        open={modalOpen}
        onClose={closeModal}
        initialData={editInitialData}
        holdings={holdings}
        onSubmit={handleSubmit}
        isSaving={isCreating || isUpdating}
      />
    </>
  );
}
