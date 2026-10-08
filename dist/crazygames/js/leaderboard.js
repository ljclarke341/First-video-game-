// Live daily leaderboard.
//
// Backed by the artifact runtime's shared document store, so every player
// who opens the published page writes to the same database and sees other
// people's scores arrive live.
//
// That runtime only exists on claude.ai. In the packaged Android app
// `window.claude` is absent, every call here no-ops, and the UI shows the
// board as unavailable rather than breaking. See docs/LEADERBOARD.md for
// wiring a real backend for the Play Store build - the three functions
// below are the entire surface a replacement has to implement.

const DOC_ROOT = 'daily';   // daily/<viewerId> -> { scores: { 'YYYY-MM-DD': n } }
const KEEP_DAYS = 14;
const TOP_N = 25;

let db = null;
let user = null;
let myId = null;
let available = false;

/** Resolves true when a live board is usable in this view. */
export async function initLeaderboard() {
  if (!window.claude?.use) return false;      // not running on claude.ai
  try {
    db = await window.claude.use('db');
    user = await window.claude.use('user');
  } catch {
    return false;
  }
  if (!db) return false;
  try {
    myId = user ? await user.id() : null;
  } catch {
    myId = null;
  }
  // Without an id there is no own-row to write to, so treat it as read-only.
  available = true;
  return true;
}

export function boardAvailable() { return available; }
export function canPost() { return available && !!myId; }

/** Records today's score on the viewer's own row, keeping their best. */
export async function submitDaily(dateKey, score) {
  if (!canPost()) return false;
  const ref = db.doc(`${DOC_ROOT}/${myId}`);
  try {
    const snap = await ref.get();
    const existing = (snap.exists && snap.data()?.scores) || {};
    if (typeof existing[dateKey] === 'number' && existing[dateKey] >= score) {
      return true;                            // already posted something better
    }
    const merged = { ...existing, [dateKey]: score };
    // Keep the row small - only the most recent days are ever displayed.
    const kept = {};
    for (const k of Object.keys(merged).sort().slice(-KEEP_DAYS)) kept[k] = merged[k];
    await ref.set({ scores: kept, updated: new Date().toISOString() });
    return true;
  } catch {
    return false;                             // a dead board must not break a run
  }
}

/**
 * Subscribes to today's board. `onRows(rows, total)` fires on every change,
 * including other players' scores landing live. Returns an unsubscribe.
 */
export function subscribeDaily(dateKey, onRows) {
  if (!available) return () => {};
  let stopped = false;

  const unsub = db.collection(DOC_ROOT).onSnapshot(
    async snap => {
      const rows = [];
      for (const doc of snap.docs) {
        const score = doc.data()?.scores?.[dateKey];
        if (typeof score !== 'number') continue;
        rows.push({ id: doc.id, score, isMe: doc.id === myId });
      }
      rows.sort((a, b) => b.score - a.score);
      rows.forEach((r, i) => { r.rank = i + 1; });

      const top = rows.slice(0, TOP_N);
      // Always include the viewer's own row, even outside the top slice.
      const mine = rows.find(r => r.isMe);
      if (mine && !top.includes(mine)) top.push(mine);

      // Names are resolved per render and never stored - they differ by
      // viewer and go stale. profiles() is cached, so this is cheap.
      let names = {};
      try {
        if (user) names = await user.profiles(top.map(r => r.id));
      } catch { /* fall back to a generic label */ }
      for (const r of top) r.name = names[r.id]?.name || 'Player';

      if (!stopped) onRows(top, rows.length);
    },
    () => { if (!stopped) onRows([], 0); }
  );

  return () => { stopped = true; unsub(); };
}
