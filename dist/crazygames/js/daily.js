// Daily challenge: one fixed course per calendar day, one attempt.
//
// The seed is derived from the local date, so every player gets a byte-
// identical course and the score is comparable. One attempt is the point -
// it makes the day's run matter, and it's the reason to come back tomorrow.
import { profile, save } from './storage.js';
import { hashSeed } from './level.js';

export function todayKey(d = new Date()) {
  const p = n => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function dailySeed(key = todayKey()) {
  return hashSeed('chroma-daily-' + key);
}

/** { available, played, score, key } for today. */
export function dailyState() {
  const key = todayKey();
  const d = profile.daily || {};
  const played = d.date === key;
  return { key, played, available: !played, score: played ? d.score : 0 };
}

export function recordDaily(score) {
  const key = todayKey();
  profile.daily = { date: key, score };
  if (score > (profile.dailyBest || 0)) profile.dailyBest = score;
  profile.dailyStreak = profile.dailyLast === yesterdayKey()
    ? (profile.dailyStreak || 0) + 1
    : 1;
  profile.dailyLast = key;
  save();
}

function yesterdayKey() {
  const d = new Date();
  d.setDate(d.getDate() - 1);
  return todayKey(d);
}
