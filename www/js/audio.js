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

export function buzz(pattern) {
  if (!profile.haptics) return;
  try { navigator.vibrate?.(pattern); } catch { /* unsupported */ }
}
