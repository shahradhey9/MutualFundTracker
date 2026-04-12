import { useUIStore } from '../lib/store.js';

export function LoadingOverlay() {
  const message = useUIStore(s => s.overlayMessage);
  if (!message) return null;

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      zIndex: 10000,
      background: 'rgba(0, 0, 0, 0.45)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      backdropFilter: 'blur(3px)',
      WebkitBackdropFilter: 'blur(3px)',
    }}>
      <style>{`
        @keyframes gwt-spin {
          to { transform: rotate(360deg); }
        }
      `}</style>
      <div style={{
        background: 'var(--color-background-primary, #fff)',
        borderRadius: 14,
        padding: '32px 44px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 18,
        boxShadow: '0 12px 40px rgba(0, 0, 0, 0.22)',
        minWidth: 200,
      }}>
        <div style={{
          width: 36,
          height: 36,
          border: '3px solid #e8e8e4',
          borderTopColor: '#2563eb',
          borderRadius: '50%',
          animation: 'gwt-spin 0.75s linear infinite',
        }} />
        <div style={{
          fontSize: 14,
          fontWeight: 500,
          color: 'var(--color-text-primary, #1a1a18)',
          textAlign: 'center',
          lineHeight: 1.4,
        }}>
          {message}
        </div>
      </div>
    </div>
  );
}
