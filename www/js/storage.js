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
  haptics: true,
  adFree: false
};

function read() {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return { ...DEFAULTS };
    const saved = JSON.parse(raw);
    const merged = { ...DEFAULTS, ...saved };
    if (!Array.isArray(merged.owned) || !merged.owned.includes('neon')) {
      merged.owned = ['neon', ...(Array.isArray(merged.owned) ? merged.owned : [])];
    }
    return merged;
  } catch {
    return { ...DEFAULTS };
  }
}

export const profile = read();

export function save() {
  try {
    localStorage.setItem(KEY, JSON.stringify(profile));
  } catch {
    /* storage unavailable - session-only progress */
  }
}
