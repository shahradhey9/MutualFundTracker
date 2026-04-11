import { useEffect, useCallback } from 'react';
import { api } from '../lib/api.js';
import { useAuthStore } from '../lib/store.js';
import { useQueryClient } from '@tanstack/react-query';

const TOKEN_KEY = 'gwt_token';

export function useAuth() {
  const qc = useQueryClient();
  const { user, authLoading, authInitialized, setUser, setAuthLoading, setAuthInitialized } = useAuthStore();

  // Restore session from localStorage — guarded by authInitialized so this
  // only runs once even though multiple components call useAuth().
  useEffect(() => {
    if (authInitialized) return;
    setAuthInitialized(true);

    const token = localStorage.getItem(TOKEN_KEY);
    if (!token) { setAuthLoading(false); return; }

    api.get('/auth/me')
      .then(res => setUser(res.data))
      .catch(() => {
        localStorage.removeItem(TOKEN_KEY);
        setUser(null);
      })
      .finally(() => setAuthLoading(false));
  }, [authInitialized, setUser, setAuthLoading, setAuthInitialized]);

  const login = useCallback(async (email, password) => {
    const { data } = await api.post('/auth/login', { email, password });
    localStorage.setItem(TOKEN_KEY, data.token);
    setUser(data.user);
    qc.invalidateQueries();
    return data.user;
  }, [qc, setUser]);

  const register = useCallback(async (email, name, password) => {
    const { data } = await api.post('/auth/register', { email, name, password });
    localStorage.setItem(TOKEN_KEY, data.token);
    setUser(data.user);
    return data.user;
  }, [setUser]);

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    setUser(null);
    qc.clear();
  }, [qc, setUser]);

  return { user, loading: authLoading, login, register, logout };
}
