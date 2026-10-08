// CrazyGames backend.
//
// Implements the same surface as ads.js so platform.js can swap between them.
// On CrazyGames the game runs framed on their domain and the SDK is present;
// anywhere else `detect()` is false and none of this runs.
//
// SDK shape verified against CrazyGames' published v3 integration notes.
// Everything here is defensive: a missing method, a thrown call or a callback
// that never fires must not be able to wedge the game.
import { setMuted } from './audio.js';

const sdk = () => window.CrazyGames?.SDK;

// Their midgame ads are throttled to one per three minutes server-side, so
// requesting at every natural break is fine - early requests are ignored.
const OVERS_BETWEEN_ADS = 3;
const AD_TIMEOUT_MS = 45000;

let ready = false;
let gameOvers = 0;

export function detect() {
  return !!sdk();
}

export async function init() {
  const s = sdk();
  if (!s) return false;
  try {
    await s.init();
    ready = true;
  } catch {
    return false;
  }
  // Tells the portal the game is playable; it ends the measured load time.
  try { s.game?.loadingStop?.(); } catch { /* non-fatal */ }
  return true;
}

/** No in-app purchases on the portal, so ads are always on. */
export function isAdFree() { return false; }

/**
 * Runs one ad. Resolves true only when it played to completion.
 * The caller blocks its UI until this settles, so it always settles.
 */
function runAd(type) {
  return new Promise(resolve => {
    const s = sdk();
    if (!ready || !s?.ad?.requestAd) return resolve(false);

    let settled = false;
    const finish = value => {
      if (settled) return;
      settled = true;
      setMuted(false);
      resolve(value);
    };

    try {
      s.ad.requestAd(type, {
        adStarted: () => setMuted(true),
        adFinished: () => finish(true),
        // Unfilled, ad-blocked or dismissed all arrive here. Normal, not a bug.
        adError: () => finish(false)
      });
    } catch {
      return finish(false);
    }

    // A callback that never fires would otherwise leave the player stuck.
    setTimeout(() => finish(false), AD_TIMEOUT_MS);
  });
}

export function showRewarded() {
  return runAd('rewarded');
}

export async function maybeShowInterstitial() {
  gameOvers += 1;
  if (gameOvers < OVERS_BETWEEN_ADS || gameOvers % OVERS_BETWEEN_ADS !== 0) return;
  await runAd('midgame');
}

// ---- portal lifecycle -------------------------------------------------
// The portal uses these to decide when it may interrupt the player.

export function gameplayStart() {
  try { sdk()?.game?.gameplayStart?.(); } catch { /* non-fatal */ }
}

export function gameplayStop() {
  try { sdk()?.game?.gameplayStop?.(); } catch { /* non-fatal */ }
}

/** Signals a moment of delight - the portal may prompt for a rating. */
export function happytime() {
  try { sdk()?.game?.happytime?.(); } catch { /* non-fatal */ }
}
