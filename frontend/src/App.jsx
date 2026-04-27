import { useState, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { api } from './lib/api.js';
import { useUIStore } from './lib/store.js';
import { useAuth } from './hooks/useAuth.js';
import { useRefreshNav } from './hooks/usePortfolio.js';
import { ErrorBoundary } from './components/ErrorBoundary.jsx';
import { LoadingSpinner } from './components/LoadingSpinner.jsx';
import { LoadingOverlay } from './components/LoadingOverlay.jsx';
import { LoginPage } from './pages/LoginPage.jsx';
import { PortfolioPage } from './pages/PortfolioPage.jsx';
import { AddHoldingPage } from './pages/AddHoldingPage.jsx';
import { AnalyticsPage } from './pages/AnalyticsPage.jsx';
import { UploadPage } from './pages/UploadPage.jsx';

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
    id: 'upload', label: 'Upload CSV',
    icon: <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor"><path d="M8 1a.75.75 0 0 1 .75.75v5.19l1.72-1.72a.75.75 0 1 1 1.06 1.06L8 9.81 4.47 6.28a.75.75 0 0 1 1.06-1.06L7.25 6.94V1.75A.75.75 0 0 1 8 1zM1.5 10.5a.75.75 0 0 1 .75.75v1.5c0 .138.112.25.25.25h11a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 13.5 14.5h-11A1.75 1.75 0 0 1 .75 12.75v-1.5a.75.75 0 0 1 .75-.75z"/></svg>,
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
          width: 240px; flex-shrink: 0;
          background: var(--sidebar-bg);
          display: flex; flex-direction: column;
          height: 100vh; position: sticky; top: 0;
          box-shadow: 4px 0 20px rgba(37,37,71,0.18);
        }
        .sidebar-brand {
          display: flex; align-items: center; gap: 12px;
          padding: 22px 20px 18px;
          border-bottom: 1px solid var(--sidebar-border);
        }
        .sidebar-logo {
          width: 38px; height: 38px; border-radius: 10px;
          background: var(--sidebar-brand-bg);
          display: flex; align-items: center; justify-content: center;
          flex-shrink: 0; box-shadow: 0 4px 12px rgba(0,0,0,0.25);
        }
        .sidebar-brand-text { font-size: 15px; font-weight: 700; color: #fff; letter-spacing: -0.01em; }
        .sidebar-brand-sub  { font-size: 10px; color: var(--sidebar-text); margin-top: 1px; }
        .sidebar-section {
          font-size: 10px; font-weight: 700; letter-spacing: .1em;
          text-transform: uppercase; color: var(--sidebar-section);
          padding: 20px 20px 6px;
        }
        .nav-item {
          display: flex; align-items: center; gap: 12px;
          padding: 10px 20px; cursor: pointer;
          font-size: 13px; font-weight: 500;
          color: var(--sidebar-text);
          transition: background .15s, color .15s;
          border: none; background: none;
          width: 100%; text-align: left;
          font-family: var(--font-sans);
          position: relative;
        }
        .nav-item:hover { background: var(--sidebar-bg-hover); color: var(--sidebar-text-active); }
        .nav-item.active { background: var(--sidebar-active-bg); color: var(--sidebar-text-active); }
        .nav-item.active::before {
          content: '';
          position: absolute; left: 0; top: 50%; transform: translateY(-50%);
          width: 4px; height: 22px; border-radius: 0 4px 4px 0;
          background: var(--sidebar-active-dot);
        }
        .nav-item.active .nav-dot {
          background: var(--sidebar-active-dot);
          box-shadow: 0 0 8px rgba(245,166,35,0.6);
        }
        .nav-dot {
          width: 6px; height: 6px; border-radius: 50%;
          background: transparent; flex-shrink: 0;
          transition: background .15s;
          margin-left: auto;
        }
        .nav-item:hover .nav-dot { background: var(--sidebar-text); }
        .sidebar-footer {
          padding: 16px 20px; border-top: 1px solid var(--sidebar-border);
          margin-top: auto;
        }
        .user-avatar {
          width: 34px; height: 34px; border-radius: 50%;
          background: linear-gradient(135deg,#7c6fcd,#f5a623);
          color: #fff; font-size: 13px; font-weight: 700;
          display: flex; align-items: center; justify-content: center;
          flex-shrink: 0;
        }
        .user-name  { font-size: 13px; font-weight: 600; color: #fff; }
        .user-email { font-size: 10px; color: var(--sidebar-text); margin-top: 1px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 116px; }
        .logout-btn {
          background: none; border: 1px solid rgba(255,255,255,0.15);
          cursor: pointer; color: var(--sidebar-text);
          font-size: 14px; padding: 4px 8px; line-height: 1;
          border-radius: var(--radius-sm); transition: all .15s; font-family: inherit;
          margin-left: auto; flex-shrink: 0;
        }
        .logout-btn:hover { border-color: #f12b2c; color: #f12b2c; }
      `}</style>
      <div className="sidebar">
        {/* Brand */}
        <div className="sidebar-brand">
          <div className="sidebar-logo">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
              <path d="M4 16V8l6-5 6 5v8H12v-5H8v5H4z" fill="white" opacity=".9"/>
              <circle cx="10" cy="7" r="1.5" fill="#f5a623"/>
            </svg>
          </div>
          <div>
            <div className="sidebar-brand-text">GWT</div>
            <div className="sidebar-brand-sub">Global Wealth Tracker</div>
          </div>
        </div>

        {/* Nav */}
        <nav style={{ flex:1, overflowY:'auto', paddingBottom:8 }}>
          <div className="sidebar-section">Main Menu</div>
          {NAV_ITEMS.map(item => (
            <button key={item.id}
              className={`nav-item${activeTab === item.id ? ' active' : ''}`}
              onClick={() => setActiveTab(item.id)}>
              {item.icon}
              {item.label}
              {activeTab === item.id && <span className="nav-dot" />}
            </button>
          ))}
        </nav>

        {/* User */}
        <div className="sidebar-footer">
          <div style={{ display:'flex', alignItems:'center', gap:10 }}>
            <div className="user-avatar">{(user?.name || user?.email || 'U')[0].toUpperCase()}</div>
            <div style={{ flex:1, minWidth:0 }}>
              <div className="user-name">{user?.name || 'Investor'}</div>
              <div className="user-email">{user?.email}</div>
            </div>
            <button className="logout-btn" onClick={onLogout} title="Sign out">✕</button>
          </div>
        </div>
      </div>
    </>
  );
}

function TopBar({ user }) {
  const { activeTab } = useUIStore();
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';
  const name = user?.name || user?.email?.split('@')[0] || 'Investor';
  const today = new Date().toLocaleDateString('en-GB', { weekday:'long', day:'numeric', month:'long', year:'numeric' });
  const { mutate: refreshNav, isPending: isRefreshing } = useRefreshNav();
  const titles = { portfolio: 'Portfolio Overview', add: 'Add Holding', upload: 'Upload CSV', analytics: 'Analytics' };

  return (
    <>
      <style>{`
        .topbar {
          height: 64px; flex-shrink: 0;
          background: var(--bg-card);
          border-bottom: 1px solid var(--border-light);
          display: flex; align-items: center;
          justify-content: space-between;
          padding: 0 28px;
          box-shadow: 0 1px 0 var(--border-light);
        }
        .topbar-left { display:flex; flex-direction:column; gap:1px; }
        .topbar-greeting { font-size: 18px; font-weight: 700; color: var(--text-primary); letter-spacing: -0.02em; line-height: 1.2; }
        .topbar-subtitle { font-size: 11px; color: var(--text-muted); font-family: var(--font-mono); }
        .topbar-right { display:flex; align-items:center; gap:12px; }
        .topbar-avatar {
          width: 36px; height: 36px; border-radius: 50%;
          background: linear-gradient(135deg,#7c6fcd,#f5a623);
          color: #fff; font-size: 13px; font-weight: 700;
          display: flex; align-items: center; justify-content: center;
          box-shadow: 0 2px 8px rgba(124,111,205,0.4);
        }
        .refresh-nav-btn {
          display: flex; align-items: center; gap: 6px;
          font-size: 12px; font-weight: 500;
          padding: 7px 14px; border-radius: var(--radius-pill);
          border: 1.5px solid var(--border);
          background: var(--bg-card); color: var(--text-secondary);
          cursor: pointer; font-family: var(--font-sans);
          transition: all .15s;
        }
        .refresh-nav-btn:hover:not(:disabled) {
          border-color: var(--accent); color: var(--accent);
          background: var(--accent-light);
          box-shadow: 0 2px 8px var(--accent-ring);
        }
        .refresh-nav-btn:disabled { opacity:.55; cursor:not-allowed; }
        .topbar-page-chip {
          font-size: 11px; font-weight: 600; letter-spacing: .04em;
          text-transform: uppercase; color: var(--text-muted);
          background: var(--bg-hover); padding: 4px 10px;
          border-radius: var(--radius-pill);
          border: 1px solid var(--border-light);
        }
      `}</style>
      <div className="topbar">
        <div className="topbar-left">
          <div className="topbar-greeting">{greeting}, {name}</div>
          <div className="topbar-subtitle">{today} · {titles[activeTab] || 'Dashboard'}</div>
        </div>
        <div className="topbar-right">
          <button
            className="refresh-nav-btn"
            onClick={() => refreshNav()}
            disabled={isRefreshing}
            title="Force-refresh NAV rates from AMFI and Yahoo Finance"
          >
            <svg width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"
              style={{ animation: isRefreshing ? 'gwt-spin 0.8s linear infinite' : 'none', flexShrink:0 }}>
              <path d="M13.5 2.5A7 7 0 1 0 14 8" />
              <polyline points="14 2 14 6 10 6" />
            </svg>
            {isRefreshing ? 'Refreshing…' : 'Refresh NAV'}
          </button>
          <div className="topbar-avatar">{name[0].toUpperCase()}</div>
        </div>
      </div>
    </>
  );
}

function WakingUpBanner() {
  return (
    <div style={{
      position: 'fixed', top: 0, left: 0, right: 0, zIndex: 9999,
      background: '#1d4ed8', color: '#fff',
      fontSize: 12, fontFamily: 'var(--font-mono, monospace)',
      padding: '8px 16px', textAlign: 'center',
    }}>
      Server is waking up from sleep — this takes ~15 s on the free tier. Hang tight…
    </div>
  );
}

function Shell() {
  const { user, loading, logout } = useAuth();
  const { activeTab, setActiveTab } = useUIStore();
  const [showWakingUp, setShowWakingUp] = useState(false);

  // Show a banner if the backend is taking more than 3 s to respond.
  // Clears automatically once loading finishes.
  useEffect(() => {
    if (!loading) { setShowWakingUp(false); return; }
    const t = setTimeout(() => setShowWakingUp(true), 3000);
    return () => clearTimeout(t);
  }, [loading]);

  if (loading) return (
    <>
      {showWakingUp && <WakingUpBanner />}
      <LoadingSpinner label="Connecting to server…" />
    </>
  );
  if (!user)   return <LoginPage />;
  return (
    <>
      <LoadingOverlay />
      <style>{`
        .app-root{display:flex;min-height:100vh;background:var(--bg-app);font-family:var(--font-sans)}
        .main-col{flex:1;display:flex;flex-direction:column;min-width:0;background:var(--bg-app)}
        .page-wrap{flex:1;padding:30px 32px;max-width:1060px;width:100%;margin:0 auto;box-sizing:border-box}
      `}</style>
      <div className="app-root">
        <Sidebar user={user} onLogout={logout} />
        <div className="main-col">
          <TopBar user={user} />
          <div className="page-wrap">
            <ErrorBoundary>
              {activeTab === 'portfolio'  && <PortfolioPage />}
              {activeTab === 'add'        && <AddHoldingPage />}
              {activeTab === 'upload'     && <UploadPage onDone={() => setActiveTab('portfolio')} />}
              {activeTab === 'analytics'  && <AnalyticsPage />}
            </ErrorBoundary>
          </div>
        </div>
      </div>
    </>
  );
}

// Keep the Render free-tier backend alive while any user has the app open.
// Render sleeps services after 15 min of inactivity; pinging every 10 min prevents that.
function useKeepAlive() {
  useEffect(() => {
    const ping = () => api.get('/health').catch(() => {});
    const id = setInterval(ping, 10 * 60 * 1000); // every 10 minutes
    return () => clearInterval(id);
  }, []);
}

export default function App() {
  useKeepAlive();
  return (
    <QueryClientProvider client={queryClient}>
      <Shell />
    </QueryClientProvider>
  );
}
