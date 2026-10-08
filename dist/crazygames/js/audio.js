// All sound is synthesized with WebAudio - no audio files to ship or license.
import { profile } from './storage.js';

let ctx = null;
let master = null;

function ensure() {
  if (ctx) return ctx;
  const AC = window.AudioContext || window.webkitAudioContext;
  if (!AC) return null;
  ctx = new AC();
  master = ctx.createGain();
  master.gain.value = 0.32;
  master.connect(ctx.destination);
  return ctx;
}

// Browsers block audio until a user gesture; call this from the first tap.
export function unlock() {
  const c = ensure();
  if (c && c.state === 'suspended') c.resume().catch(() => {});
}

function blip({ freq = 440, dur = 0.12, type = 'square', gain = 0.3, slide = 0, delay = 0 }) {
  if (!profile.sound) return;
  const c = ensure();
  if (!c) return;
  const t0 = c.currentTime + delay;
  const osc = c.createOscillator();
  const g = c.createGain();
  osc.type = type;
  osc.frequency.setValueAtTime(freq, t0);
  if (slide) osc.frequency.exponentialRampToValueAtTime(Math.max(40, freq + slide), t0 + dur);
  g.gain.setValueAtTime(0.0001, t0);
  g.gain.exponentialRampToValueAtTime(gain, t0 + 0.012);
  g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
  osc.connect(g).connect(master);
  osc.start(t0);
  osc.stop(t0 + dur + 0.02);
}

function noise({ dur = 0.35, gain = 0.4 }) {
  if (!profile.sound) return;
  const c = ensure();
  if (!c) return;
  const frames = Math.floor(c.sampleRate * dur);
  const buf = c.createBuffer(1, frames, c.sampleRate);
  const data = buf.getChannelData(0);
  for (let i = 0; i < frames; i++) {
    data[i] = (Math.random() * 2 - 1) * (1 - i / frames) ** 2;
  }
  const src = c.createBufferSource();
  const filter = c.createBiquadFilter();
  const g = c.createGain();
  filter.type = 'lowpass';
  filter.frequency.setValueAtTime(1800, c.currentTime);
  filter.frequency.exponentialRampToValueAtTime(140, c.currentTime + dur);
  g.gain.value = gain;
  src.buffer = buf;
  src.connect(filter).connect(g).connect(master);
  src.start();
}

// Combo climbs a pentatonic scale so long chains sound like they're building.
const SCALE = [0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24];

export const sfx = {
  move: () => blip({ freq: 320, dur: 0.06, type: 'triangle', gain: 0.16, slide: 90 }),
  gate: (combo = 0) => {
    const step = SCALE[Math.min(combo, SCALE.length - 1)];
    const f = 330 * Math.pow(2, step / 12);
    blip({ freq: f, dur: 0.14, type: 'square', gain: 0.2 });
    blip({ freq: f * 1.5, dur: 0.1, type: 'sine', gain: 0.12, delay: 0.02 });
  },
  coin: () => {
    blip({ freq: 980, dur: 0.07, type: 'sine', gain: 0.22 });
    blip({ freq: 1460, dur: 0.09, type: 'sine', gain: 0.18, delay: 0.05 });
  },
  paint: () => blip({ freq: 520, dur: 0.16, type: 'sawtooth', gain: 0.16, slide: 280 }),
  crash: () => {
    noise({ dur: 0.45, gain: 0.5 });
    blip({ freq: 180, dur: 0.4, type: 'sawtooth', gain: 0.25, slide: -130 });
  },
  ui: () => blip({ freq: 620, dur: 0.07, type: 'triangle', gain: 0.18 }),
  buy: () => {
    [523, 659, 784, 1047].forEach((f, i) =>
      blip({ freq: f, dur: 0.16, type: 'triangle', gain: 0.2, delay: i * 0.07 }));
  },
  revive: () => {
    [392, 523, 659].forEach((f, i) =>
      blip({ freq: f, dur: 0.22, type: 'sine', gain: 0.22, delay: i * 0.09 }));
  }
};

// ---------------------------------------------------------------- music
//
// A generative bassline on a lookahead scheduler. It layers up with the combo
// multiplier, so holding a chain is audible as well as visible. Nothing is
// streamed or loaded - it is the same oscillators as the sound effects.

const ROOTS = [55, 55, 73.42, 55, 82.41, 55, 73.42, 65.41];  // A1 pattern
const ARP = [220, 329.63, 440, 329.63];

let musicTimer = null;
let nextNote = 0;
let stepIndex = 0;
let intensity = 0;           // 0-2, driven by the combo multiplier

const STEP = 0.235;          // eighth notes at ~128bpm
const LOOKAHEAD = 0.12;

function scheduleStep(time) {
  const c = ctx;
  const i = stepIndex % 8;

  // Bass - always present.
  const bass = c.createOscillator();
  const bg = c.createGain();
  bass.type = 'triangle';
  bass.frequency.setValueAtTime(ROOTS[i], time);
  bg.gain.setValueAtTime(0.0001, time);
  bg.gain.exponentialRampToValueAtTime(0.22, time + 0.015);
  bg.gain.exponentialRampToValueAtTime(0.0001, time + STEP * 0.9);
  bass.connect(bg).connect(master);
  bass.start(time);
  bass.stop(time + STEP);

  // Hi-hat from the first combo step up.
  if (intensity >= 1 && i % 2 === 1) {
    const frames = Math.floor(c.sampleRate * 0.04);
    const buf = c.createBuffer(1, frames, c.sampleRate);
    const data = buf.getChannelData(0);
    for (let n = 0; n < frames; n++) data[n] = (Math.random() * 2 - 1) * (1 - n / frames);
    const src = c.createBufferSource();
    const hp = c.createBiquadFilter();
    const hg = c.createGain();
    hp.type = 'highpass';
    hp.frequency.value = 7000;
    hg.gain.value = 0.07;
    src.buffer = buf;
    src.connect(hp).connect(hg).connect(master);
    src.start(time);
  }

  // Arpeggio once the chain is really going.
  if (intensity >= 2) {
    const a = c.createOscillator();
    const ag = c.createGain();
    a.type = 'square';
    a.frequency.setValueAtTime(ARP[i % ARP.length], time);
    ag.gain.setValueAtTime(0.0001, time);
    ag.gain.exponentialRampToValueAtTime(0.05, time + 0.01);
    ag.gain.exponentialRampToValueAtTime(0.0001, time + STEP * 0.6);
    a.connect(ag).connect(master);
    a.start(time);
    a.stop(time + STEP);
  }

  stepIndex += 1;
}

export function startMusic() {
  if (!profile.music || musicTimer) return;
  const c = ensure();
  if (!c) return;
  stepIndex = 0;
  intensity = 0;
  nextNote = c.currentTime + 0.1;
  musicTimer = setInterval(() => {
    if (!profile.music) return stopMusic();
    while (nextNote < ctx.currentTime + LOOKAHEAD) {
      scheduleStep(nextNote);
      nextNote += STEP;
    }
  }, 25);
}

export function stopMusic() {
  if (musicTimer) clearInterval(musicTimer);
  musicTimer = null;
}

/** Combo multiplier 1-5 maps onto the three arrangement layers. */
export function setMusicIntensity(mult) {
  intensity = mult >= 4 ? 2 : mult >= 2 ? 1 : 0;
}

/** Ducks all audio (ads must play without the game underneath them). */
export function setMuted(on) {
  const c = ensure();
  if (!c || !master) return;
  try {
    master.gain.setValueAtTime(on ? 0 : 0.32, c.currentTime);
  } catch {
    master.gain.value = on ? 0 : 0.32;
  }
  if (on) stopMusic();
}

export function buzz(pattern) {
  if (!profile.haptics) return;
  try { navigator.vibrate?.(pattern); } catch { /* unsupported */ }
}
