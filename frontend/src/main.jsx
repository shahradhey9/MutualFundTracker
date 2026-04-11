import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App.jsx';
import { api } from './lib/api.js';

// Fire a health-check ping immediately on page load.
// On Render free tier the backend sleeps after 15 min idle; this ping triggers the
// cold-start wake-up as early as possible so subsequent API calls wait less.
api.get('/health').catch(() => { /* non-fatal — backend may be mid cold-start */ });

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
