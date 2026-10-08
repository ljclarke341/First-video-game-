// Boot: wires input, the game loop and the UI together.
import { Game } from './game.js';
import { UI } from './ui.js';
import { profile, save } from './storage.js';
import { unlock, stopMusic, startMusic } from './audio.js';
import { applyRun } from './missions.js';
import { dailySeed, dailyState, recordDaily, todayKey } from './daily.js';
import { initLeaderboard, boardAvailable, submitDaily, subscribeDaily } from './leaderboard.js';
import { initAds, showRewarded, maybeShowInterstitial, isAdFree } from './ads.js';

const canvas = document.getElementById('game');

const game = new Game(canvas, {
  onHud: (score, coins, mult) => ui.hud(score, coins, mult),
  onHint: msg => ui.hint(msg),
  onGameOver: result => onGameOver(result)
});

// A daily run is flagged so its result is recorded against today's date and
// the game-over screen can say so.
let dailyRun = false;

const ui = new UI({
  play: () => { dailyRun = false; game.start(); ui.showHud(); },
  daily: () => {
    if (!dailyState().available) return ui.toast('Already played today');
    dailyRun = true;
    game.start(dailySeed());
    ui.showHud();
    ui.toast("Today's course - one attempt");
  },
  musicToggled: () => {
    if (game.state === 'playing') startMusic(); else stopMusic();
  },
  board: () => {
    ui.showBoard(boardAvailable());
    if (!boardAvailable()) return;
    stopBoard();
    // One subscription while the screen is open, torn down on navigation.
    stopBoard = subscribeDaily(todayKey(), (rows, total) => ui.renderBoard(rows, total));
  },
  boardClosed: () => stopBoard(),
  boardUsable: () => boardAvailable(),
  pause: () => { game.pause(); ui.show('paused'); },
  resume: () => { game.resume(); ui.showHud(); },
  quit: () => { game.state = 'idle'; stopMusic(); ui.show('menu'); },
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
let stopBoard = () => {};

async function onGameOver({
  score, coins, gates, bestCombo, duration, paints, topSpeed, revives, canRevive
}) {
  lastRunCoins = coins;
  const prevBest = profile.best;
  profile.coins += coins;
  profile.runs += 1;
  const isBest = score > prevBest;
  if (isBest) profile.best = score;

  const wasDaily = dailyRun;

  // Missions are scored before the screen renders so payouts show immediately.
  const completed = applyRun({
    gates, score, coins, duration, paints, topSpeed, revives,
    combo: bestCombo, daily: wasDaily
  });

  if (wasDaily) {
    recordDaily(score);
    // Posting must never hold up or break the game-over screen.
    submitDaily(todayKey(), score).catch(() => {});
  }
  save();

  // "So close" only means something against a best you nearly matched.
  const gap = prevBest - score;
  const nearMiss = !isBest && prevBest > 0 && gap > 0 && score >= prevBest * 0.85
    ? gap : 0;

  ui.gameOver({
    score, coins, isBest, canRevive, nearMiss, completed, daily: wasDaily,
    canDouble: coins > 0 && !isAdFree()
  });
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
// The live board lights up if this view can run it; the game is complete
// without it either way.
initLeaderboard().catch(() => {});
ui.show('menu');
