import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { useUIStore } from './lib/store.js';
import { useAuth } from './hooks/useAuth.js';
import { ErrorBoundary } from './components/ErrorBoundary.jsx';
import { LoadingSpinner } from './components/LoadingSpinner.jsx';
import { LoginPage } from './pages/LoginPage.jsx';
import { PortfolioPage } from './pages/PortfolioPage.jsx';
import { AddHoldingPage } from './pages/AddHoldingPage.jsx';
import { AnalyticsPage } from './pages/AnalyticsPage.jsx';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 2,
      retryDelay: attempt => Math.min(1000 * 2 ** attempt, 10000),
      // Show stale data immediately while revalidating in the background
      staleTime: 60 * 1000,
    },
  },
});

const TABS = [
  { id: 'portfolio', label: 'Portfolio' },
  { id: 'add',       label: 'Add Holding' },
  { id: 'analytics', label: 'Analytics' },
];

function Header({ user, onLogout }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '1.25rem 0 1rem',
      borderBottom: '0.5px solid var(--color-border-tertiary)',
      marginBottom: '1.5rem',
    }}>
      <div style={{ fontSize: 22, fontWeight: 300, letterSpacing: '-0.02em', color: 'var(--color-text-primary)' }}>
        <em style={{ fontStyle: 'italic' }}>Global</em> Wealth Tracker
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
        <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)' }}>
          {new Date().toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })}
        </div>
        <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)', fontFamily: 'var(--font-mono)' }}>
          {user?.name || user?.email}
        </div>
        <button
          onClick={onLogout}
          style={{
            padding: '5px 12px', fontSize: 12,
            border: '0.5px solid var(--color-border-secondary)',
            borderRadius: 'var(--border-radius-md)',
            background: 'none', color: 'var(--color-text-secondary)',
            cursor: 'pointer',
          }}
        >
          Sign out
        </button>
      </div>
    </div>
  );
}

function TabBar() {
  const { activeTab, setActiveTab } = useUIStore();
  return (
    <div style={{ display: 'flex', borderBottom: '0.5px solid var(--color-border-tertiary)', marginBottom: '1.5rem' }}>
      {TABS.map(tab => (
        <button
          key={tab.id}
          onClick={() => setActiveTab(tab.id)}
          style={{
            padding: '8px 16px', fontSize: 13,
            color: activeTab === tab.id ? 'var(--color-text-primary)' : 'var(--color-text-secondary)',
            cursor: 'pointer', border: 'none', background: 'none',
            borderBottom: activeTab === tab.id
              ? '2px solid var(--color-text-primary)'
              : '2px solid transparent',
            marginBottom: '-0.5px',
            fontWeight: activeTab === tab.id ? 500 : 400,
            transition: 'color 0.15s',
          }}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}

function Shell() {
  const { user, loading, logout } = useAuth();
  const { activeTab } = useUIStore();

  // Restore session from localStorage — show spinner
  if (loading) return <LoadingSpinner label="Restoring session…" />;

  // Not logged in — show auth page
  if (!user) return <LoginPage />;

  return (
    <div style={{ maxWidth: 960, margin: '0 auto', padding: '0 16px', fontFamily: 'var(--font-sans, system-ui)' }}>
      <Header user={user} onLogout={logout} />
      <TabBar />
      <ErrorBoundary>
        <div style={{ paddingBottom: '3rem' }}>
          {activeTab === 'portfolio'  && <PortfolioPage />}
          {activeTab === 'add'        && <AddHoldingPage />}
          {activeTab === 'analytics'  && <AnalyticsPage />}
        </div>
      </ErrorBoundary>
    </div>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Shell />
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
}
