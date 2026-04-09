import { useState } from 'react';
import { useAuth } from '../hooks/useAuth.js';

const inp = {
  width: '100%',
  padding: '10px 14px',
  fontSize: 14,
  border: '0.5px solid var(--color-border-secondary)',
  borderRadius: 'var(--border-radius-md)',
  background: 'var(--color-background-primary)',
  color: 'var(--color-text-primary)',
  outline: 'none',
  boxSizing: 'border-box',
  fontFamily: 'var(--font-sans)',
};

const btn = {
  width: '100%',
  padding: '11px',
  fontSize: 14,
  fontWeight: 500,
  border: 'none',
  borderRadius: 'var(--border-radius-md)',
  background: 'var(--color-text-primary)',
  color: 'var(--color-background-primary)',
  cursor: 'pointer',
};

export function LoginPage() {
  const { login, register } = useAuth();
  const [mode, setMode] = useState('login'); // 'login' | 'register'
  const [email, setEmail]       = useState('');
  const [name, setName]         = useState('');
  const [password, setPassword] = useState('');
  const [error, setError]       = useState('');
  const [loading, setLoading]   = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (mode === 'login') {
        await login(email, password);
      } else {
        await register(email, name, password);
      }
    } catch (err) {
      setError(err.message || 'Something went wrong');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'var(--color-background-tertiary)',
      padding: '1rem',
    }}>
      <div style={{
        width: '100%',
        maxWidth: 380,
        background: 'var(--color-background-primary)',
        border: '0.5px solid var(--color-border-tertiary)',
        borderRadius: 'var(--border-radius-lg)',
        padding: '2rem',
      }}>
        {/* Logo */}
        <div style={{ marginBottom: '2rem', textAlign: 'center' }}>
          <div style={{ fontSize: 22, fontWeight: 300, letterSpacing: '-0.02em', color: 'var(--color-text-primary)' }}>
            <em style={{ fontStyle: 'italic' }}>Global</em> Wealth Tracker
          </div>
          <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', marginTop: 4 }}>
            {mode === 'login' ? 'Sign in to your portfolio' : 'Create your account'}
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {mode === 'register' && (
              <div>
                <label style={{ display: 'block', fontSize: 12, color: 'var(--color-text-tertiary)', marginBottom: 5 }}>Name</label>
                <input style={inp} type="text" value={name} onChange={e => setName(e.target.value)} placeholder="Your name" autoFocus />
              </div>
            )}
            <div>
              <label style={{ display: 'block', fontSize: 12, color: 'var(--color-text-tertiary)', marginBottom: 5 }}>Email</label>
              <input style={inp} type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="you@example.com" autoFocus={mode === 'login'} required />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: 12, color: 'var(--color-text-tertiary)', marginBottom: 5 }}>Password</label>
              <input style={inp} type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder={mode === 'register' ? 'At least 6 characters' : '••••••••'} required />
            </div>
          </div>

          {error && (
            <div style={{
              marginTop: 12, padding: '8px 12px', fontSize: 13, borderRadius: 'var(--border-radius-md)',
              background: 'var(--color-background-danger)', color: 'var(--color-text-danger)',
            }}>
              {error}
            </div>
          )}

          <button type="submit" style={{ ...btn, marginTop: 20, opacity: loading ? 0.7 : 1 }} disabled={loading}>
            {loading ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        {/* Demo shortcut */}
        {mode === 'login' && (
          <button
            onClick={() => { setEmail('demo@gwt.dev'); setPassword('demo1234'); }}
            style={{ width: '100%', marginTop: 8, padding: '10px', fontSize: 13, border: '0.5px solid var(--color-border-tertiary)', borderRadius: 'var(--border-radius-md)', background: 'none', color: 'var(--color-text-secondary)', cursor: 'pointer' }}
          >
            Use demo account
          </button>
        )}

        <div style={{ textAlign: 'center', marginTop: 20, fontSize: 13, color: 'var(--color-text-tertiary)' }}>
          {mode === 'login' ? (
            <>No account? <button onClick={() => { setMode('register'); setError(''); }} style={{ background: 'none', border: 'none', color: 'var(--color-text-primary)', cursor: 'pointer', fontWeight: 500 }}>Register</button></>
          ) : (
            <>Already have one? <button onClick={() => { setMode('login'); setError(''); }} style={{ background: 'none', border: 'none', color: 'var(--color-text-primary)', cursor: 'pointer', fontWeight: 500 }}>Sign in</button></>
          )}
        </div>
      </div>
    </div>
  );
}
