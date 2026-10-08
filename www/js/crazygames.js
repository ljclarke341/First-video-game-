// CrazyGames backend.
//
// Implements the same surface as ads.js so platform.js can swap between them.
// On CrazyGames the game runs framed on their domain and the SDK is present;
// anywhere else `detect()` is false and none of this runs.
//
// SDK shape verified against CrazyGames' published v3 integration notes.
// Everything here is defensive: a missing method, a thrown call or a callback
// that never fires must not be able to wedge the game.
import { setMuted, setPortalMute } from './audio.js';

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
  // The portal expects a matched pair around asset loading. This game has
  // nothing to stream - everything ships in the initial download - so the
  // window is immediate, but both calls still have to be reported.
  try { s.game?.loadingStart?.(); } catch { /* non-fatal */ }
  try { s.game?.loadingStop?.(); } catch { /* non-fatal */ }

  // Ad problems are otherwise invisible from the outside: the player just
  // sees "unavailable" whether the SDK is missing, the call threw, or the
  // request was simply unfilled. These say which.
  console.info('[CG] ready =', ready,
               '| ad module =', typeof s.ad?.requestAd,
               '| keys =', Object.keys(s).join(','));

  applyPortalSettings();
  // The listener name is not something this build could verify against the
  // live docs, so it is optional - gameplayStart re-checks as a backstop.
  try { s.game?.addSettingsChangeListener?.(applyPortalSettings); } catch { /* ok */ }
  return true;
}

/** Mirrors the portal's own volume control into the game's master gain. */
function applyPortalSettings() {
  try {
    setPortalMute(!!sdk()?.game?.settings?.muteAudio);
  } catch { /* leave audio as-is */ }
}

/**
 * The portal's cloud save. Same interface as localStorage, and for a
 * signed-in player it follows them across devices. Only valid after init(),
 * which is why storage swaps to it rather than starting on it.
 */
export function dataModule() {
  const d = sdk()?.data;
  if (!ready || !d) return null;
  return {
    getItem: k => d.getItem(k),
    setItem: (k, v) => d.setItem(k, v)
  };
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
    if (!ready) {
      console.warn('[CG] ad skipped: SDK never finished init()');
      return resolve(false);
    }
    if (typeof s?.ad?.requestAd !== 'function') {
      console.warn('[CG] ad skipped: SDK.ad.requestAd missing. SDK.ad =', s?.ad);
      return resolve(false);
    }

    let settled = false;
    const finish = value => {
      if (settled) return;
      settled = true;
      setMuted(false);
      resolve(value);
    };

    console.info('[CG] requestAd ->', type);
    try {
      s.ad.requestAd(type, {
        adStarted: () => { console.info('[CG] adStarted', type); setMuted(true); },
        adFinished: () => { console.info('[CG] adFinished', type); finish(true); },
        // Unfilled, ad-blocked or dismissed all arrive here. Normal, not a bug.
        adError: (err, data) => {
          console.warn('[CG] adError', type, err, data);
          finish(false);
        }
      });
    } catch (e) {
      console.warn('[CG] requestAd threw', e);
      return finish(false);
    }

    // A callback that never fires would otherwise leave the player stuck.
    setTimeout(() => {
      if (!settled) console.warn('[CG] ad timed out with no callback', type);
      finish(false);
    }, AD_TIMEOUT_MS);
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
  applyPortalSettings();            // backstop if no settings listener exists
  try { sdk()?.game?.gameplayStart?.(); } catch { /* non-fatal */ }
}

export function gameplayStop() {
  try { sdk()?.game?.gameplayStop?.(); } catch { /* non-fatal */ }
}

/** Signals a moment of delight - the portal may prompt for a rating. */
export function happytime() {
  try { sdk()?.game?.happytime?.(); } catch { /* non-fatal */ }
}
