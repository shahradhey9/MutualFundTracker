import { useState, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { api } from './lib/api.js';
import { useUIStore } from './lib/store.js';
import { useAuth } from './hooks/useAuth.js';
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
        .sidebar{width:224px;flex-shrink:0;background:var(--bg-card);border-right:1px solid var(--border-light);display:flex;flex-direction:column;height:100vh;position:sticky;top:0;box-shadow:1px 0 0 var(--border-light)}
        .nav-item{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:var(--radius-md);cursor:pointer;font-size:13px;font-weight:400;color:var(--text-secondary);transition:background .12s,color .12s;border:1px solid transparent;background:none;width:100%;text-align:left;font-family:var(--font-sans)}
        .nav-item:hover{background:var(--bg-hover);color:var(--text-primary)}
        .nav-item.active{background:var(--accent-light);color:var(--accent);font-weight:500;border-color:var(--border-focus)}
        .nav-item.active svg{color:var(--accent)}
        .sidebar-footer{padding:14px 16px;border-top:1px solid var(--border-light);margin-top:auto}
        .user-avatar{width:30px;height:30px;border-radius:50%;background:var(--accent);color:#fff;font-size:12px;font-weight:600;display:flex;align-items:center;justify-content:center;flex-shrink:0}
        .user-name{font-size:13px;font-weight:500;color:var(--text-primary)}
        .user-email{font-size:11px;color:var(--text-muted);margin-top:1px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:120px;font-family:var(--font-mono)}
        .logout-btn{background:none;border:1px solid var(--border-light);cursor:pointer;color:var(--text-muted);font-size:16px;padding:4px 7px;line-height:1;border-radius:var(--radius-sm);transition:all .12s;font-family:inherit}
        .logout-btn:hover{border-color:var(--color-loss);color:var(--color-loss);background:var(--color-loss-bg)}
        .brand-text{font-size:14px;font-weight:600;color:var(--text-primary);letter-spacing:-0.01em}
        .brand-sub{font-size:10px;color:var(--text-muted);margin-top:1px;font-family:var(--font-mono)}
        .nav-section{font-size:10px;font-weight:600;color:var(--text-muted);letter-spacing:.08em;text-transform:uppercase;padding:0 8px;margin-bottom:4px;margin-top:8px}
      `}</style>
      <div className="sidebar">
        <div style={{ padding:'20px 16px 16px', borderBottom:'1px solid var(--border-light)' }}>
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
        <nav style={{ padding:'12px 8px', flex:1, overflowY:'auto' }}>
          <div className="nav-section">Menu</div>
          {NAV_ITEMS.map(item => (
            <button key={item.id}
              className={`nav-item${activeTab === item.id ? ' active' : ''}`}
              onClick={() => setActiveTab(item.id)}>
              {item.icon}{item.label}
            </button>
          ))}
        </nav>
        <div className="sidebar-footer">
          <div style={{ display:'flex', alignItems:'center', gap:10 }}>
            <div className="user-avatar">{(user?.name || user?.email || 'U')[0].toUpperCase()}</div>
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
  const { activeTab } = useUIStore();
  const titles = { portfolio: 'Portfolio', add: 'Add Holding', upload: 'Upload CSV', analytics: 'Analytics' };
  const today = new Date().toLocaleDateString('en-GB', { weekday:'long', day:'numeric', month:'long', year:'numeric' });
  return (
    <>
      <style>{`
        .topbar{height:54px;border-bottom:1px solid var(--border-light);display:flex;align-items:center;justify-content:space-between;padding:0 28px;background:var(--bg-card);flex-shrink:0}
        .topbar-title{font-size:14px;font-weight:600;color:var(--text-primary)}
        .topbar-date{font-size:11px;color:var(--text-muted);font-family:var(--font-mono)}
      `}</style>
      <div className="topbar">
        <span className="topbar-title">{titles[activeTab] || 'Dashboard'}</span>
        <span className="topbar-date">{today}</span>
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
        .page-wrap{flex:1;padding:28px;max-width:980px;width:100%;margin:0 auto;box-sizing:border-box}
      `}</style>
      <div className="app-root">
        <Sidebar user={user} onLogout={logout} />
        <div className="main-col">
          <TopBar />
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
