// Picks the host this build is running on and routes ads and lifecycle
// events to it. One codebase, two distributions: the Capacitor/AdMob build
// for Google Play and the CrazyGames build for the web portal.
import * as admob from './ads.js';
import * as crazygames from './crazygames.js';

const noop = () => {};

const ADMOB = {
  name: 'admob',
  init: admob.initAds,
  showRewarded: admob.showRewarded,
  maybeShowInterstitial: admob.maybeShowInterstitial,
  isAdFree: admob.isAdFree,
  gameplayStart: noop,
  gameplayStop: noop,
  happytime: noop
};

let backend = ADMOB;

export async function initPlatform() {
  if (crazygames.detect()) backend = { name: 'crazygames', ...crazygames };
  try {
    await backend.init();
  } catch { /* the game runs fine without a host */ }
  return backend.name;
}

export const platformName = () => backend.name;
export const showRewarded = () => backend.showRewarded();
export const maybeShowInterstitial = () => backend.maybeShowInterstitial();
export const isAdFree = () => backend.isAdFree();
export const gameplayStart = () => backend.gameplayStart();
export const gameplayStop = () => backend.gameplayStop();
export const happytime = () => backend.happytime();
