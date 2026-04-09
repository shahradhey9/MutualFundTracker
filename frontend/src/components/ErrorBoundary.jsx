import { Component } from 'react';

export class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error, info) {
    console.error('ErrorBoundary caught:', error, info);
  }

  render() {
    if (this.state.error) {
      return (
        <div style={{
          padding: '2rem',
          margin: '2rem auto',
          maxWidth: 480,
          border: '0.5px solid var(--color-border-danger)',
          borderRadius: 'var(--border-radius-lg)',
          background: 'var(--color-background-danger)',
        }}>
          <div style={{ fontSize: 16, fontWeight: 500, color: 'var(--color-text-danger)', marginBottom: 8 }}>
            Something went wrong
          </div>
          <div style={{ fontSize: 13, color: 'var(--color-text-secondary)', fontFamily: 'var(--font-mono)', marginBottom: 16, wordBreak: 'break-word' }}>
            {this.state.error.message}
          </div>
          <button
            onClick={() => this.setState({ error: null })}
            style={{
              padding: '8px 16px', fontSize: 13,
              border: '0.5px solid var(--color-border-secondary)',
              borderRadius: 'var(--border-radius-md)',
              background: 'var(--color-background-primary)',
              color: 'var(--color-text-primary)',
              cursor: 'pointer',
            }}
          >
            Try again
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
