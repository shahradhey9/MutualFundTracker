import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api.js';

export const goalKeys = {
  all: ['goals'],
};

export function useGoals() {
  return useQuery({
    queryKey: goalKeys.all,
    queryFn: async () => {
      const { data } = await api.get('/goals');
      return data.goals;
    },
    staleTime: 2 * 60 * 1000,
  });
}

export function useCreateGoal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload) => {
      const { data } = await api.post('/goals', payload);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: goalKeys.all }),
  });
}

export function useUpdateGoal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ goalId, ...updates }) => {
      const { data } = await api.patch(`/goals/${goalId}`, updates);
      return data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: goalKeys.all }),
  });
}

export function useDeleteGoal() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (goalId) => {
      await api.delete(`/goals/${goalId}`);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: goalKeys.all }),
  });
}
