import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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
      staleTime: 60 * 1000,
    },
  },
});

const NAV_ITEMS = [
  {
    id: 'portfolio', label: 'Portfolio',
    icon: <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor"><path d="M2 3a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v1a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V3zm0 5a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v1a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V8zm1 4a1 1 0 0 0 0 2h6a1 1 0 0 0 0-2H3z"/></svg>,
  },
  {
    id: 'add', label: 'Add Holding',
    icon: <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor"><path d="M8 2a.75.75 0 0 1 .75.75v4.5h4.5a.75.75 0 0 1 0 1.5h-4.5v4.5a.75.75 0 0 1-1.5 0v-4.5h-4.5a.75.75 0 0 1 0-1.5h4.5v-4.5A.75.75 0 0 1 8 2z"/></svg>,
  },
  {
    id: 'analytics', label: 'Analytics',
    icon: <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor"><path d="M1 11a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1v-3zm5-4a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H7a1 1 0 0 1-1-1V7zm5-5a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1h-2a1 1 0 0 1-1-1V2z"/></svg>,
  },
];

function Sidebar({ user, onLogout }) {
  const { activeTab, setActiveTab } = useUIStore();

  return (
    <>
      <style>{`
        .sidebar {
          width: 220px;
          flex-shrink: 0;
          background: #fff;
          border-right: 1px solid #e8e8e4;
          display: flex;
          flex-direction: column;
          height: 100vh;
          position: sticky;
          top: 0;
        }
        @media (prefers-color-scheme: dark) {
          .sidebar { background: #1c1c1a; border-color: #2a2a28; }
          .nav-item:hover { background: #252523 !important; }
          .nav-item.active { background: #252523 !important; color: #5a9cf5 !important; }
          .nav-item.active svg { color: #5a9cf5 !important; }
          .sidebar-footer { border-color: #2a2a28 !important; }
          .user-name { color: #e8e8e4 !important; }
          .user-email { color: #888780 !important; }
          .logout-btn { color: #888780 !important; }
          .logout-btn:hover { color: #e8e8e4 !important; }
          .brand-text { color: #e8e8e4 !important; }
          .brand-sub { color: #888780 !important; }
        }
        .nav-item {
          display: flex;
          align-items: center;
          gap: 10px;
          padding: 9px 14px;
          border-radius: 8px;
          cursor: pointer;
          font-size: 14px;
          color: #5f5e5a;
          font-weight: 400;
          transition: background 0.12s, color 0.12s;
          border: none;
          background: none;
          width: 100%;
          text-align: left;
          font-family: inherit;
        }
        .nav-item:hover { background: #f4f4f2; color: #1a1a18; }
        .nav-item.active { background: #eff6ff; color: #2563eb; font-weight: 500; }
        .nav-item.active svg { color: #2563eb; }
        .sidebar-footer {
          padding: 16px;
          border-top: 1px solid #e8e8e4;
          margin-top: auto;
        }
        .user-avatar {
          width: 32px; height: 32px; border-radius: 50%;
          background: #2563eb;
          color: white; font-size: 13px; font-weight: 500;
          display: flex; align-items: center; justify-content: center;
          flex-shrink: 0;
        }
        .user-name { font-size: 13px; font-weight: 500; color: #1a1a18; }
        .user-email { font-size: 11px; color: #888780; margin-top: 1px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 120px; }
        .logout-btn {
          background: none; border: none; cursor: pointer;
          color: #b4b2a9; font-size: 18px; padding: 4px; line-height: 1;
          border-radius: 4px; transition: color 0.12s;
          font-family: inherit;
        }
        .logout-btn:hover { color: #ef4444; }
        .brand-text { font-size: 15px; font-weight: 500; color: #1a1a18; letter-spacing: -0.01em; }
        .brand-sub { font-size: 11px; color: #b4b2a9; margin-top: 1px; }
      `}</style>

      <div className="sidebar">
        {/* Brand */}
        <div style={{ padding:'20px 16px 16px', borderBottom:'1px solid #e8e8e4' }}>
          <div style={{ display:'flex', alignItems:'center', gap:10 }}>
            <div style={{ width:32, height:32, borderRadius:8, background:'#2563eb', display:'flex', alignItems:'center', justifyContent:'center', flexShrink:0 }}>
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path d="M8 1L13 6H9.5V15H6.5V6H3L8 1Z" fill="white"/>
              </svg>
            </div>
            <div>
              <div className="brand-text">GWT</div>
              <div className="brand-sub">Global Wealth Tracker</div>
            </div>
          </div>
        </div>

        {/* Nav */}
        <nav style={{ padding:'12px 8px', flex:1 }}>
          <div style={{ fontSize:10, fontWeight:600, color:'#b4b2a9', letterSpacing:'0.08em', textTransform:'uppercase', padding:'0 8px', marginBottom:6 }}>
            Menu
          </div>
          {NAV_ITEMS.map(item => (
            <button
              key={item.id}
              className={`nav-item${activeTab === item.id ? ' active' : ''}`}
              onClick={() => setActiveTab(item.id)}
            >
              {item.icon}
              {item.label}
            </button>
          ))}
        </nav>

        {/* User footer */}
        <div className="sidebar-footer">
          <div style={{ display:'flex', alignItems:'center', gap:10 }}>
            <div className="user-avatar">
              {(user?.name || user?.email || 'U')[0].toUpperCase()}
            </div>
            <div style={{ flex:1, minWidth:0 }}>
              <div className="user-name">{user?.name || 'Investor'}</div>
              <div className="user-email">{user?.email}</div>
            </div>
            <button className="logout-btn" onClick={onLogout} title="Sign out">×</button>
          </div>
        </div>
      </div>
    </>
  );
}

function TopBar() {
  const today = new Date().toLocaleDateString('en-GB', {
    weekday: 'long', day: 'numeric', month: 'long', year: 'numeric',
  });
  const { activeTab } = useUIStore();
  const titles = { portfolio: 'Portfolio', add: 'Add Holding', analytics: 'Analytics' };

  return (
    <>
      <style>{`
        .topbar {
          height: 56px;
          border-bottom: 1px solid #e8e8e4;
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 0 28px;
          background: #fff;
          flex-shrink: 0;
        }
        @media (prefers-color-scheme: dark) {
          .topbar { background: #1c1c1a; border-color: #2a2a28; }
          .topbar-title { color: #e8e8e4 !important; }
          .topbar-date { color: #888780 !important; }
        }
        .topbar-title { font-size: 15px; font-weight: 500; color: #1a1a18; }
        .topbar-date { font-size: 12px; color: #b4b2a9; font-family: var(--font-mono, monospace); }
      `}</style>
      <div className="topbar">
        <span className="topbar-title">{titles[activeTab]}</span>
        <span className="topbar-date">{today}</span>
      </div>
    </>
  );
}

function Shell() {
  const { user, loading, logout } = useAuth();
  const { activeTab } = useUIStore();

  if (loading) return <LoadingSpinner label="Loading your portfolio…" />;
  if (!user) return <LoginPage />;

  return (
    <>
      <style>{`
        .app-root {
          display: flex;
          min-height: 100vh;
          background: #f7f7f5;
          font-family: var(--font-sans, 'DM Sans', system-ui);
        }
        @media (prefers-color-scheme: dark) {
          .app-root { background: #141413; }
          .main-content { background: #141413 !important; }
          .page-inner { background: #141413 !important; }
        }
        .main-content {
          flex: 1;
          display: flex;
          flex-direction: column;
          min-width: 0;
          background: #f7f7f5;
        }
        .page-inner {
          flex: 1;
          padding: 28px;
          max-width: 960px;
          width: 100%;
          margin: 0 auto;
          box-sizing: border-box;
        }
      `}</style>

      <div className="app-root">
        <Sidebar user={user} onLogout={logout} />
        <div className="main-content">
          <TopBar />
          <div className="page-inner">
            <ErrorBoundary>
              {activeTab === 'portfolio'  && <PortfolioPage />}
              {activeTab === 'add'        && <AddHoldingPage />}
              {activeTab === 'analytics'  && <AnalyticsPage />}
            </ErrorBoundary>
          </div>
        </div>
      </div>
    </>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Shell />
    </QueryClientProvider>
  );
}
