// Ad bridge.
//
// In a browser this shows a harmless placeholder so the reward flow stays
// testable. On a device, if the AdMob Capacitor plugin is installed and
// configured (see docs/MONETIZATION.md), the real calls are used instead.
//
// Every method resolves rather than rejects: a broken ad network must never
// be able to break the game.
import { profile } from './storage.js';

const AdMob = () => window.Capacitor?.Plugins?.AdMob || null;
const isNative = () => !!window.Capacitor?.isNativePlatform?.();

// Replace with your own unit IDs from the AdMob console before release.
// These are Google's official test IDs - safe during development, and using
// real IDs on your own device can get your AdMob account suspended.
export const AD_UNITS = {
  rewarded: 'ca-app-pub-3940256099942544/5224354917',
  interstitial: 'ca-app-pub-3940256099942544/1033173712'
};

let initialized = false;
let gameOverCount = 0;

export async function initAds() {
  if (initialized || !isNative()) return;
  const plugin = AdMob();
  if (!plugin) return;
  try {
    await plugin.initialize({ initializeForTesting: true });
    initialized = true;
  } catch {
    /* ads stay disabled; the game is unaffected */
  }
}

export function isAdFree() {
  return !!profile.adFree;
}

const isDevHost = () =>
  ['localhost', '127.0.0.1', ''].includes(location.hostname);

function placeholder(seconds = 3) {
  // Only ever a local testing aid. On a real host with no ad SDK the honest
  // answer is "no ad", not a fake one.
  if (!isDevHost()) return Promise.resolve(false);
  return new Promise(resolve => {
    const el = document.getElementById('adSim');
    const timer = document.getElementById('adTimer');
    if (!el || !timer) return resolve(true);
    let left = seconds;
    timer.textContent = String(left);
    el.hidden = false;
    const tick = setInterval(() => {
      left -= 1;
      timer.textContent = String(Math.max(0, left));
      if (left <= 0) {
        clearInterval(tick);
        el.hidden = true;
        resolve(true);
      }
    }, 1000);
  });
}

/** Resolves true when the reward was earned, false otherwise. */
export async function showRewarded() {
  const plugin = AdMob();
  if (!isNative() || !plugin) return placeholder(3);
  try {
    await plugin.prepareRewardVideoAd({ adId: AD_UNITS.rewarded });
    const reward = await plugin.showRewardVideoAd();
    return !!reward;
  } catch {
    return false;
  }
}

/** Interstitials run between runs only, and never on the first two. */
export async function maybeShowInterstitial() {
  gameOverCount += 1;
  if (isAdFree() || gameOverCount < 3 || gameOverCount % 3 !== 0) return;
  const plugin = AdMob();
  if (!isNative() || !plugin) return;
  try {
    await plugin.prepareInterstitial({ adId: AD_UNITS.interstitial });
    await plugin.showInterstitial();
  } catch {
    /* ignore */
  }
}
