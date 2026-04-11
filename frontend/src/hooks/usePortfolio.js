import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api.js';

// ── Query keys ───────────────────────────────────────────────────────────────
export const keys = {
  portfolio: ['portfolio'],
  fundSearch: (q, region) => ['funds', 'search', q, region],
  fundNav: (ticker) => ['funds', 'nav', ticker],
};

// ── Portfolio ────────────────────────────────────────────────────────────────
export function usePortfolio() {
  return useQuery({
    queryKey: keys.portfolio,
    queryFn: async () => {
      const { data } = await api.get('/portfolio');
      return data.holdings;
    },
    staleTime: 5 * 60 * 1000,    // fresh for 5 min — matches backend cache TTL
    refetchInterval: 10 * 60 * 1000, // background refresh every 10 min
    refetchOnWindowFocus: false, // NAVs are end-of-day, no need to re-fetch on tab focus
  });
}

// ── Add / consolidate holding ────────────────────────────────────────────────
export function useAddHolding() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ fund, units, avgCost, purchaseAt }) => {
      // Step 1: ensure fund exists in fund_meta
      await api.post('/funds/ensure', fund);
      // Step 2: create/merge holding
      const { data } = await api.post('/portfolio/holdings', {
        fundId: fund.id,
        units: Number(units),
        avgCost: avgCost ? Number(avgCost) : undefined,
        purchaseAt,
      });
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.portfolio }),
  });
}

// ── Update holding ───────────────────────────────────────────────────────────
export function useUpdateHolding() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ holdingId, ...updates }) => {
      const { data } = await api.patch(`/portfolio/holdings/${holdingId}`, updates);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.portfolio }),
  });
}

// ── Delete holding ───────────────────────────────────────────────────────────
export function useDeleteHolding() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (holdingId) => {
      await api.delete(`/portfolio/holdings/${holdingId}`);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: keys.portfolio }),
  });
}

// ── Fund search ──────────────────────────────────────────────────────────────
export function useFundSearch(query, region) {
  return useQuery({
    queryKey: keys.fundSearch(query, region),
    queryFn: async () => {
      const { data } = await api.get('/funds/search', {
        params: { q: query, region },
      });
      return data.results;
    },
    enabled: query.trim().length >= 2,
    staleTime: 10 * 60 * 1000, // search results stable for 10 min
    placeholderData: (prev) => prev, // keep old results while new ones load
  });
}
