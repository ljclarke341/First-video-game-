// Persistent player profile. localStorage can throw (private mode, blocked
// site data), so every access is guarded and the game runs fine without it.
const KEY = 'chroma-rush/v1';

const DEFAULTS = {
  best: 0,
  coins: 0,
  runs: 0,
  owned: ['neon'],
  skin: 'neon',
  sound: true,
  music: true,
  haptics: true,
  adFree: false,
  missions: [],
  daily: null,          // { date, score } for the most recent attempt
  dailyBest: 0,
  dailyStreak: 0,
  dailyLast: ''
};

// Storage backend. Defaults to localStorage; the CrazyGames build swaps in
// the portal's Data Module after its SDK initializes, which syncs a
// signed-in player's progress across their devices.
let backend = {
  getItem: k => localStorage.getItem(k),
  setItem: (k, v) => localStorage.setItem(k, v)
};

function hydrate(saved) {
  const merged = { ...DEFAULTS, ...saved };
  if (!Array.isArray(merged.owned) || !merged.owned.includes('neon')) {
    merged.owned = ['neon', ...(Array.isArray(merged.owned) ? merged.owned : [])];
  }
  return merged;
}

function read() {
  try {
    const raw = backend.getItem(KEY);
    if (!raw) return { ...DEFAULTS };
    return hydrate(JSON.parse(raw));
  } catch {
    return { ...DEFAULTS };
  }
}

export const profile = read();

export function save() {
  try {
    backend.setItem(KEY, JSON.stringify(profile));
  } catch {
    /* storage unavailable - session-only progress */
  }
}

/**
 * Switches to a different store once it becomes available, loading whatever
 * it already holds. `profile` is mutated in place so every module that
 * imported it keeps a valid reference. Calls `onLoaded` if the UI needs a
 * repaint with the restored values.
 */
export async function adoptBackend(next, onLoaded) {
  backend = next;
  let raw = null;
  try {
    raw = await next.getItem(KEY);      // may be sync or async
  } catch {
    return;
  }
  if (raw) {
    try {
      const restored = hydrate(JSON.parse(raw));
      for (const k of Object.keys(profile)) delete profile[k];
      Object.assign(profile, restored);
      onLoaded?.();
      return;
    } catch { /* corrupt remote value - fall through and overwrite it */ }
  }
  save();                                // seed the store with local progress
}
