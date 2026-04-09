import { useState, useEffect, useCallback } from 'react';
import { api } from '../lib/api.js';
import { useQueryClient } from '@tanstack/react-query';

const TOKEN_KEY = 'gwt_token';

export function useAuth() {
  const qc = useQueryClient();
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Restore session on mount
  useEffect(() => {
    const token = localStorage.getItem(TOKEN_KEY);
    if (!token) { setLoading(false); return; }

    api.get('/auth/me')
      .then(res => setUser(res.data))
      .catch(() => {
        localStorage.removeItem(TOKEN_KEY);
        setUser(null);
      })
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (email, password) => {
    setError(null);
    const { data } = await api.post('/auth/login', { email, password });
    localStorage.setItem(TOKEN_KEY, data.token);
    setUser(data.user);
    qc.invalidateQueries(); // refresh all queries for this user
    return data.user;
  }, [qc]);

  const register = useCallback(async (email, name, password) => {
    setError(null);
    const { data } = await api.post('/auth/register', { email, name, password });
    localStorage.setItem(TOKEN_KEY, data.token);
    setUser(data.user);
    return data.user;
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    setUser(null);
    qc.clear(); // clear all cached queries
  }, [qc]);

  return { user, loading, error, login, register, logout };
}
