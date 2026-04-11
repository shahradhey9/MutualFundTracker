import { create } from 'zustand';

// ── Auth store (singleton — prevents split-brain between Shell and LoginPage) ──
export const useAuthStore = create((set) => ({
  user: null,
  authLoading: true,
  authInitialized: false,
  setUser: (user) => set({ user }),
  setAuthLoading: (v) => set({ authLoading: v }),
  setAuthInitialized: (v) => set({ authInitialized: v }),
}));

export const useUIStore = create((set) => ({
  activeTab: 'portfolio',
  setActiveTab: (tab) => set({ activeTab: tab }),

  // Add holding flow
  selectedFund: null,
  setSelectedFund: (fund) => set({ selectedFund: fund }),
  clearSelectedFund: () => set({ selectedFund: null }),

  // Edit holding flow
  editingHolding: null,
  setEditingHolding: (holding) => set({ editingHolding: holding }),
  clearEditingHolding: () => set({ editingHolding: null }),

  // Search state
  searchQuery: '',
  setSearchQuery: (q) => set({ searchQuery: q }),
  searchRegion: 'INDIA',
  setSearchRegion: (r) => set({ searchRegion: r }),
  clearSearch: () => set({ searchQuery: '', selectedFund: null }),
}));
