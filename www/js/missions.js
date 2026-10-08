// Missions: three rolling goals that give a run a point beyond the high score.
//
// A player chasing only a personal best has one reason to play and loses it
// the moment a run goes badly. A mission that is 80% done survives a bad run.
import { profile, save } from './storage.js';

const TEMPLATES = [
  { type: 'gates', targets: [12, 20, 32], best: true,
    text: n => `Pass ${n} gates in one run`, reward: n => n * 5 },
  { type: 'score', targets: [250, 550, 1100], best: true,
    text: n => `Score ${n} in one run`, reward: n => Math.round(n / 4) },
  { type: 'combo', targets: [3, 4, 5], best: true,
    text: n => `Reach a x${n} combo`, reward: n => n * 45 },
  { type: 'coins', targets: [25, 60, 110], best: false,
    text: n => `Collect ${n} coins`, reward: n => n },
  { type: 'runs', targets: [3, 6, 12], best: false,
    text: n => `Play ${n} runs`, reward: n => n * 18 }
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
export function applyRun({ gates, score, combo, coins }) {
  ensureMissions();
  const values = { gates, score, combo, coins, runs: 1 };
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
