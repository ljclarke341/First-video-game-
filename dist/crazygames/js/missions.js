// Missions: three rolling goals that give a run a point beyond the high score.
//
// A player chasing only a personal best has one reason to play and loses it
// the moment a run goes badly. A mission that is 80% done survives a bad run.
import { profile, save } from './storage.js';

const TEMPLATES = [
  // --- single-run goals: progress takes the best run, never accumulates ---
  { type: 'gates', targets: [12, 20, 32], best: true,
    text: n => `Pass ${n} gates in one run`, reward: n => n * 5 },
  { type: 'score', targets: [250, 550, 1100], best: true,
    text: n => `Score ${n} in one run`, reward: n => Math.round(n / 4) },
  { type: 'combo', targets: [3, 4, 5], best: true,
    text: n => `Reach a x${n} combo`, reward: n => n * 45 },
  { type: 'coinsRun', targets: [8, 15, 25], best: true,
    text: n => `Collect ${n} coins in one run`, reward: n => n * 8 },
  { type: 'duration', targets: [20, 35, 55], best: true,
    text: n => `Survive ${n} seconds`, reward: n => n * 4 },
  { type: 'paints', targets: [5, 10, 18], best: true,
    text: n => `Grab ${n} paint blobs in one run`, reward: n => n * 12 },
  { type: 'cleanGates', targets: [10, 18, 28], best: true,
    text: n => `Pass ${n} gates without a continue`, reward: n => n * 7 },
  { type: 'topSpeed', targets: [300, 400, 520], best: true,
    text: n => `Push the course to speed ${n}`, reward: n => Math.round(n / 2) },

  // --- running totals: progress accumulates across runs ---
  { type: 'runs', targets: [3, 6, 12], best: false,
    text: n => `Play ${n} runs`, reward: n => n * 18 },
  { type: 'coins', targets: [25, 60, 110], best: false,
    text: n => `Collect ${n} coins`, reward: n => n },
  { type: 'totalGates', targets: [50, 120, 250], best: false,
    text: n => `Pass ${n} gates in total`, reward: n => n * 2 },
  { type: 'totalScore', targets: [1000, 2500, 6000], best: false,
    text: n => `Earn ${n} points in total`, reward: n => Math.round(n / 12) },
  { type: 'dailies', targets: [1, 2, 4], best: false,
    text: n => n === 1 ? 'Finish a daily challenge' : `Finish ${n} daily challenges`,
    reward: n => n * 90 },

  // --- collection ---
  { type: 'themes', targets: [2, 3, 4], best: true,
    text: n => `Own ${n} themes`, reward: n => n * 60 }
];

const SLOTS = 3;

/**
 * Difficulty tier scales with experience. A brand-new player handed "pass 32
 * gates in one run" has been given a wall, not a goal - their median run is
 * about nine gates.
 */
function tierFor(runs) {
  const ceiling = runs < 10 ? 0 : runs < 35 ? 1 : 2;
  return (Math.random() * (ceiling + 1)) | 0;
}

function roll(exclude) {
  const pool = TEMPLATES.filter(t => !exclude.includes(t.type));
  const list = pool.length ? pool : TEMPLATES;
  const t = list[(Math.random() * list.length) | 0];
  const target = t.targets[Math.min(tierFor(profile.runs || 0), t.targets.length - 1)];
  return { type: t.type, target, progress: 0, reward: t.reward(target) };
}

/** Tops the list back up to three, keeping the types distinct. */
export function ensureMissions() {
  if (!Array.isArray(profile.missions)) profile.missions = [];
  while (profile.missions.length < SLOTS) {
    profile.missions.push(roll(profile.missions.map(m => m.type)));
  }
  save();
  return profile.missions;
}

export function missionText(m) {
  return TEMPLATES.find(t => t.type === m.type).text(m.target);
}

/**
 * Folds one run's results into the active missions.
 * Returns the missions completed by this run, each already paid out.
 */
/**
 * `run` carries one run's results. Everything a mission can key on is
 * computed here so the templates above stay declarative.
 */
export function applyRun(run) {
  ensureMissions();
  const values = {
    gates: run.gates,
    score: run.score,
    combo: run.combo,
    coins: run.coins,
    coinsRun: run.coins,
    duration: Math.floor(run.duration || 0),
    paints: run.paints || 0,
    cleanGates: run.revives ? 0 : run.gates,
    topSpeed: Math.round(run.topSpeed || 0),
    runs: 1,
    totalGates: run.gates,
    totalScore: run.score,
    dailies: run.daily ? 1 : 0,
    themes: (profile.owned || []).length
  };
  const done = [];

  for (let i = 0; i < profile.missions.length; i++) {
    const m = profile.missions[i];
    const tpl = TEMPLATES.find(t => t.type === m.type);
    const v = values[m.type] || 0;
    // "In one run" goals take the best single run; the rest accumulate.
    m.progress = tpl.best ? Math.max(m.progress, v) : m.progress + v;

    if (m.progress >= m.target) {
      done.push({ text: missionText(m), reward: m.reward });
      profile.coins += m.reward;
      profile.missions[i] = null;
    }
  }

  profile.missions = profile.missions.filter(Boolean);
  ensureMissions();
  save();
  return done;
}
