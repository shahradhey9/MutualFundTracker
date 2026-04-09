import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:4000/api',
  headers: { 'Content-Type': 'application/json' },
});

// Inject auth token on every request
api.interceptors.request.use((config) => {
  // Replace with your auth provider's token retrieval
  // e.g. const token = await getToken();  (Clerk)
  //      or localStorage.getItem('gwt_token')
  const token = localStorage.getItem('gwt_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Global error normalisation
api.interceptors.response.use(
  (res) => res,
  (err) => {
    const message = err.response?.data?.error || err.message || 'Unknown error';
    return Promise.reject(new Error(message));
  }
);
