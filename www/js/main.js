// Boot: wires input, the game loop and the UI together.
import { Game } from './game.js';
import { UI } from './ui.js';
import { profile, save } from './storage.js';
import { unlock } from './audio.js';
import { initAds, showRewarded, maybeShowInterstitial, isAdFree } from './ads.js';

const canvas = document.getElementById('game');

const game = new Game(canvas, {
  onHud: (score, coins, mult) => ui.hud(score, coins, mult),
  onHint: msg => ui.hint(msg),
  onGameOver: result => onGameOver(result)
});

const ui = new UI({
  play: () => { game.start(); ui.showHud(); },
  pause: () => { game.pause(); ui.show('paused'); },
  resume: () => { game.resume(); ui.showHud(); },
  quit: () => { game.state = 'idle'; ui.show('menu'); },
  revive: async () => {
    const btn = document.getElementById('reviveBtn');
    btn.disabled = true;
    const earned = await showRewarded();
    btn.disabled = false;
    if (earned) { ui.showHud(); game.revive(); }
    else ui.toast('Ad unavailable - try again');
  },
  double: async () => {
    const btn = document.getElementById('doubleBtn');
    btn.disabled = true;
    const earned = await showRewarded();
    btn.disabled = false;
    if (!earned) return ui.toast('Ad unavailable - try again');
    profile.coins += lastRunCoins;      // they already banked one copy
    save();
    ui.coinsDoubled(lastRunCoins * 2);
    ui.toast(`+${lastRunCoins} bonus coins`);
  }
});

let lastRunCoins = 0;

async function onGameOver({ score, coins, canRevive }) {
  lastRunCoins = coins;
  profile.coins += coins;
  profile.runs += 1;
  const isBest = score > profile.best;
  if (isBest) profile.best = score;
  save();
  ui.gameOver({ score, coins, isBest, canRevive, canDouble: coins > 0 && !isAdFree() });
  if (!canRevive && !isAdFree()) maybeShowInterstitial();
}

// ------------------------------------------------------------------- input

function onTap(clientX) {
  unlock();
  if (game.state !== 'playing') return;
  game.move(clientX < window.innerWidth / 2 ? -1 : 1);
}

canvas.addEventListener('pointerdown', e => {
  e.preventDefault();
  onTap(e.clientX);
}, { passive: false });

// Keyboard support makes desktop playtesting (and accessibility) possible.
window.addEventListener('keydown', e => {
  if (['ArrowLeft', 'a', 'A'].includes(e.key)) game.move(-1);
  else if (['ArrowRight', 'd', 'D'].includes(e.key)) game.move(1);
  else if (e.key === 'Escape' && game.state === 'playing') {
    game.pause(); ui.show('paused');
  } else if (e.key === ' ' && game.state === 'idle') {
    e.preventDefault(); game.start(); ui.showHud();
  }
});

window.addEventListener('resize', () => game.resize());
window.addEventListener('orientationchange', () => setTimeout(() => game.resize(), 120));

// Losing focus mid-run must not cost the player their score.
document.addEventListener('visibilitychange', () => {
  if (document.hidden && game.state === 'playing') { game.pause(); ui.show('paused'); }
});

// Android hardware back button (Capacitor).
window.Capacitor?.Plugins?.App?.addListener?.('backButton', () => {
  if (game.state === 'playing') { game.pause(); ui.show('paused'); }
  else ui.show('menu');
});

// Debug handle for local playtesting only - never exposed in the packaged app.
if (['localhost', '127.0.0.1'].includes(location.hostname)) {
  window.__game = game;
}

initAds();
ui.show('menu');
