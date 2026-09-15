// Core gameplay: fixed virtual coordinate space, scrolling course, one-touch
// lane control, and the render pass.
import { Generator, LANES, WALL } from './level.js';
import { getSkin } from './skins.js';
import { profile } from './storage.js';
import { sfx, buzz } from './audio.js';

const VW = 480;                // virtual width; everything is tuned in these units
const PLAYER_R = 24;
const GATE_H = 44;
const LOOKAHEAD = 1400;
const PICKUP_BAND = 32;   // vertical grab window around the player
const EDGE_FORGIVE = 13;  // how close to a cell edge still counts as the neighbour
const MAX_DT = 1 / 30;

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const laneX = i => (VW * (i * 2 + 1)) / 6;

/** Paths one of the three shape glyphs. The caller fills or strokes it. */
function shapePath(ctx, shape, x, y, r) {
  ctx.beginPath();
  if (shape === 0) {
    ctx.arc(x, y, r, 0, Math.PI * 2);
  } else if (shape === 1) {
    const h = r * 1.15;
    ctx.moveTo(x, y - h);
    ctx.lineTo(x + h * 0.92, y + h * 0.72);
    ctx.lineTo(x - h * 0.92, y + h * 0.72);
    ctx.closePath();
  } else {
    const s = r * 0.92;
    ctx.rect(x - s, y - s, s * 2, s * 2);
  }
}

export class Game {
  constructor(canvas, hooks = {}) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.hooks = hooks;
    this.state = 'idle';     // idle | playing | paused | dying | over
    this.gen = new Generator();
    this.items = [];
    this.particles = [];
    this.trail = [];
    this.stars = [];
    this.scroll = 0;
    this.shake = 0;
    this.timeScale = 1;
    this.flash = 0;
    this.grace = 0;
    this.VH = 800;
    this.scale = 1;
    this.last = 0;
    this.player = { x: laneX(1), lane: 1, color: 0, squash: 0 };
    this.resize();
    this.loop = this.loop.bind(this);
    requestAnimationFrame(this.loop);
  }

  get skin() { return getSkin(profile.skin); }
  get playerY() { return this.VH * 0.74; }

  resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2.5);
    const cw = this.canvas.clientWidth || window.innerWidth;
    const ch = this.canvas.clientHeight || window.innerHeight;
    this.canvas.width = Math.round(cw * dpr);
    this.canvas.height = Math.round(ch * dpr);
    this.scale = cw / VW;
    this.VH = ch / this.scale;
    this.ctx.setTransform(dpr * this.scale, 0, 0, dpr * this.scale, 0, 0);
    if (!this.stars.length) this.seedStars();
  }

  seedStars() {
    this.stars = Array.from({ length: 46 }, () => ({
      x: Math.random() * VW,
      y: Math.random() * 1600,
      z: 0.25 + Math.random() * 0.85,
      r: 0.6 + Math.random() * 1.7
    }));
  }

  // ---------------------------------------------------------------- lifecycle

  start() {
    this.gen.reset();
    this.items.length = 0;
    this.particles.length = 0;
    this.trail.length = 0;
    this.scroll = 0;
    this.score = 0;
    this.coinsEarned = 0;
    this.combo = 0;
    this.bestCombo = 0;
    this.revivesUsed = 0;
    this.shake = 0;
    this.flash = 0;
    this.grace = 0.6;
    this.timeScale = 1;
    this.player.lane = 1;
    this.player.x = laneX(1);
    this.player.color = 0;
    this.player.squash = 0;
    this.gen.pathLane = 1;
    this.gen.pathColor = 0;
    // Contextual hints for a player's first few runs. A rules screen gets
    // skipped; a prompt at the moment the thing appears gets read.
    this.hints = profile.runs < 3 ? { paint: false, wall: false } : null;
    if (this.hints) this.hooks.onHint?.('TAP LEFT OR RIGHT TO MOVE');
    // Place the first gate just above the screen so the run opens with a short
    // "get ready" beat instead of several dead seconds.
    this.gen.lastGateY = this.gen.gap - 60;
    this.gen.fill(this.items, LOOKAHEAD);
    this.state = 'playing';
  }

  pause() { if (this.state === 'playing') this.state = 'paused'; }
  resume() { if (this.state === 'paused') { this.state = 'playing'; this.last = 0; } }

  /** Continue after a crash: clear the danger and resync the course. */
  revive() {
    this.revivesUsed += 1;
    this.items.length = 0;
    this.particles.length = 0;
    this.combo = 0;
    this.timeScale = 1;
    this.grace = 1.4;
    this.flash = 0.6;
    // Rebuild from the player's *current* state so the next gate is reachable.
    this.gen.lastGateY = 0;
    this.gen.pathLane = this.player.lane;
    this.gen.pathColor = this.player.color;
    this.gen.fill(this.items, LOOKAHEAD);
    this.state = 'playing';
    this.last = 0;
    sfx.revive();
  }

  get mult() { return Math.min(5, 1 + Math.floor(this.combo / 4)); }

  // ------------------------------------------------------------------- input

  move(dir) {
    if (this.state !== 'playing') return;
    const next = clamp(this.player.lane + dir, 0, LANES - 1);
    if (next === this.player.lane) return;
    this.player.lane = next;
    sfx.move();
  }

  // ------------------------------------------------------------------ update

  update(dt) {
    // The menu keeps a live, drifting background so the game never looks frozen.
    if (this.state === 'idle') {
      this.scroll += 90 * dt;
      for (const s of this.stars) {
        s.y += 90 * dt * s.z * 0.55;
        if (s.y > this.VH + 20) { s.y = -20; s.x = Math.random() * VW; }
      }
      return;
    }
    if (this.state === 'paused' || this.state === 'over') return;

    const speed = this.gen.speed * this.timeScale;
    const move = speed * dt;
    this.scroll += move;
    if (this.grace > 0) this.grace -= dt;
    if (this.flash > 0) this.flash = Math.max(0, this.flash - dt * 2.2);
    if (this.shake > 0) this.shake = Math.max(0, this.shake - dt * 26);

    // Lane easing - snappy but not instant, so mid-transition reads on screen.
    const target = laneX(this.player.lane);
    const k = 1 - Math.exp(-28 * dt);
    this.player.x += (target - this.player.x) * k;
    this.player.squash *= Math.exp(-9 * dt);

    for (const s of this.stars) {
      s.y += move * s.z * 0.55;
      if (s.y > this.VH + 20) { s.y = -20; s.x = Math.random() * VW; }
    }

    if (this.state === 'dying') {
      this.timeScale = Math.max(0.12, this.timeScale - dt * 1.4);
      this.updateParticles(dt);
      this.dyingFor = (this.dyingFor || 0) + dt;
      if (this.dyingFor > 0.85) this.finish();
      return;
    }

    this.trail.push({ x: this.player.x, y: this.playerY });
    if (this.trail.length > 13) this.trail.shift();

    const py = this.playerY;
    for (const it of this.items) {
      it.py = it.y;
      it.y += move;
      if (it.spin !== undefined) it.spin += dt * 3.2;
    }

    for (const it of this.items) {
      if (it.type === 'gate') {
        if (!it.resolved && it.y >= py) { it.resolved = true; this.resolveGate(it); }
      } else if (!it.taken && it.y >= py - PICKUP_BAND && it.py <= py + PICKUP_BAND) {
        // A band, not a single-frame line crossing: the player is usually still
        // easing toward the next lane as a blob passes, and punishing that
        // makes pickups feel arbitrary. The `py` term also catches a blob that
        // skipped the whole band in one long frame.
        if (Math.abs(this.player.x - laneX(it.lane)) < 46) this.collect(it);
      }
    }

    // Reap what has scrolled past, then extend the course.
    if (this.items.length > 48) {
      this.items = this.items.filter(it => it.y < this.VH + 120);
    }
    this.gen.lastGateY += move;
    this.gen.fill(this.items, LOOKAHEAD);

    this.updateParticles(dt);
    if (this.hints) this.checkHints();
    this.hooks.onHud?.(this.score, this.coinsEarned, this.mult);
  }

  /** Fires each hint once, as its mechanic first comes into view. */
  checkHints() {
    for (const it of this.items) {
      if (it.y < -60 || it.y > this.playerY) continue;
      if (!this.hints.paint && it.type === 'paint') {
        this.hints.paint = true;
        this.hooks.onHint?.('GRAB THE BLOB TO CHANGE YOUR SHAPE');
        return;
      }
      if (!this.hints.wall && it.type === 'gate' && it.cells.includes(WALL)) {
        this.hints.wall = true;
        this.hooks.onHint?.('STRIPED WALLS ARE DEADLY');
        return;
      }
    }
  }

  resolveGate(gate) {
    const cellW = VW / LANES;
    const idx = clamp(Math.floor(this.player.x / cellW), 0, LANES - 1);
    let cell = gate.cells[idx];

    // If the player is within a hair of a cell edge, let the neighbour count.
    // Losing a run to a few pixels reads as the game cheating, not as a
    // mistake the player can learn from.
    if (cell !== this.player.color) {
      const edge = this.player.x - idx * cellW;
      const near = edge < EDGE_FORGIVE ? idx - 1 : edge > cellW - EDGE_FORGIVE ? idx + 1 : -1;
      if (near >= 0 && near < LANES && gate.cells[near] === this.player.color) {
        cell = gate.cells[near];
      }
    }

    if (cell === this.player.color) {
      this.gen.passed += 1;      // drives the difficulty curve
      this.combo += 1;
      this.bestCombo = Math.max(this.bestCombo, this.combo);
      this.score += 10 * this.mult;
      this.player.squash = 1;
      this.shake = Math.min(6, 1.6 + this.mult * 0.5);
      this.burst(this.player.x, this.playerY, this.colorOf(this.player.color), 9, 150);
      sfx.gate(this.combo);
      buzz(8);
      return;
    }
    if (this.grace > 0) return;   // post-revive shield
    this.crash(gate, idx);
  }

  collect(it) {
    it.taken = true;
    if (it.type === 'coin') {
      this.coinsEarned += 1;
      this.score += 5 * this.mult;
      this.burst(laneX(it.lane), this.playerY, '#f5b93a', 8, 130);
      sfx.coin();
    } else {
      this.player.color = it.color;
      this.player.squash = 0.8;
      this.burst(laneX(it.lane), this.playerY, this.colorOf(it.color), 14, 190);
      sfx.paint();
      buzz(6);
    }
  }

  crash(gate, idx) {
    this.state = 'dying';
    this.dyingFor = 0;
    this.shake = 16;
    this.flash = 1;
    const cell = gate.cells[idx];
    const col = cell === WALL ? '#ff3b3b' : this.colorOf(cell);
    this.burst(this.player.x, this.playerY, col, 34, 330);
    this.burst(this.player.x, this.playerY, this.colorOf(this.player.color), 20, 220);
    sfx.crash();
    buzz([30, 40, 70]);
  }

  finish() {
    this.state = 'over';
    this.timeScale = 1;
    this.hooks.onGameOver?.({
      score: this.score,
      coins: this.coinsEarned,
      bestCombo: this.bestCombo,
      canRevive: this.revivesUsed < 1
    });
  }

  colorOf(i) { return this.skin.colors[i] || '#ffffff'; }

  burst(x, y, color, count, spread) {
    for (let i = 0; i < count; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = spread * (0.35 + Math.random() * 0.8);
      this.particles.push({
        x, y, color,
        vx: Math.cos(a) * s,
        vy: Math.sin(a) * s - 40,
        life: 0.45 + Math.random() * 0.45,
        max: 0.9,
        r: 2 + Math.random() * 4
      });
    }
    if (this.particles.length > 320) this.particles.splice(0, this.particles.length - 320);
  }

  updateParticles(dt) {
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.life -= dt;
      if (p.life <= 0) { this.particles.splice(i, 1); continue; }
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vy += 620 * dt;
      p.vx *= 0.97;
    }
  }

  // ------------------------------------------------------------------ render

  render() {
    const ctx = this.ctx;
    const skin = this.skin;
    const h = this.VH;

    ctx.save();
    if (this.shake > 0.2) {
      ctx.translate((Math.random() - 0.5) * this.shake, (Math.random() - 0.5) * this.shake);
    }

    // Background
    const g = ctx.createLinearGradient(0, 0, 0, h);
    g.addColorStop(0, skin.bg);
    g.addColorStop(0.55, this.mix(skin.bg, skin.colors[2], 0.06));
    g.addColorStop(1, skin.bg);
    ctx.fillStyle = g;
    ctx.fillRect(-20, -20, VW + 40, h + 40);

    this.drawStars(ctx);
    this.drawGrid(ctx, h, skin);
    this.drawLaneGuides(ctx, h, skin);
    if (this.state !== 'idle') this.drawBeam(ctx);

    for (const it of this.items) {
      if (it.y < -120 || it.y > h + 120) continue;
      if (it.type === 'gate') this.drawGate(ctx, it);
      else if (!it.taken) (it.type === 'coin' ? this.drawCoin : this.drawPaint).call(this, ctx, it);
    }

    this.drawParticles(ctx);
    if (this.state !== 'dying' && this.state !== 'idle') this.drawPlayer(ctx);

    // Vignette keeps the eye on the centre lane cluster.
    const v = ctx.createRadialGradient(VW / 2, h * 0.55, h * 0.26, VW / 2, h * 0.55, h * 0.78);
    v.addColorStop(0, 'rgba(0,0,0,0)');
    v.addColorStop(1, 'rgba(0,0,0,.55)');
    ctx.fillStyle = v;
    ctx.fillRect(0, 0, VW, h);

    if (this.flash > 0) {
      ctx.fillStyle = `rgba(255,255,255,${this.flash * 0.36})`;
      ctx.fillRect(0, 0, VW, h);
    }
    ctx.restore();
  }

  mix(a, b, t) {
    const pc = c => [1, 3, 5].map(i => parseInt(c.slice(i, i + 2), 16));
    try {
      const [r1, g1, b1] = pc(a), [r2, g2, b2] = pc(b);
      return `rgb(${Math.round(r1 + (r2 - r1) * t)},${Math.round(g1 + (g2 - g1) * t)},${Math.round(b1 + (b2 - b1) * t)})`;
    } catch { return a; }
  }

  drawStars(ctx) {
    ctx.fillStyle = 'rgba(255,255,255,.5)';
    for (const s of this.stars) {
      ctx.globalAlpha = 0.16 + s.z * 0.4;
      ctx.beginPath();
      ctx.arc(s.x, s.y % (this.VH + 40), s.r, 0, 6.283);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  drawGrid(ctx, h, skin) {
    ctx.strokeStyle = skin.grid;
    ctx.lineWidth = 1;
    const step = 110;
    const off = this.scroll % step;
    ctx.beginPath();
    for (let y = off - step; y < h + step; y += step) {
      ctx.moveTo(0, y); ctx.lineTo(VW, y);
    }
    ctx.stroke();
  }

  /**
   * A soft column in the player's own colour, running from the player up the
   * lane they are aimed at. New players lose track of their own shape while
   * reading the gate; this puts both in one glance without solving the gate
   * for them.
   */
  drawBeam(ctx) {
    const col = this.colorOf(this.player.color);
    const top = Math.max(0, this.playerY - 520);
    const g = ctx.createLinearGradient(0, top, 0, this.playerY);
    g.addColorStop(0, 'rgba(0,0,0,0)');
    g.addColorStop(1, col);
    ctx.save();
    ctx.globalAlpha = 0.16;
    ctx.fillStyle = g;
    ctx.fillRect(this.player.x - VW / 6, top, VW / 3, this.playerY - top);
    ctx.restore();
  }

  drawLaneGuides(ctx, h, skin) {
    ctx.strokeStyle = skin.grid;
    ctx.lineWidth = 1;
    ctx.setLineDash([5, 13]);
    ctx.beginPath();
    for (let i = 1; i < LANES; i++) {
      const x = (VW / LANES) * i;
      ctx.moveTo(x, 0); ctx.lineTo(x, h);
    }
    ctx.stroke();
    ctx.setLineDash([]);
  }

  drawGate(ctx, gate) {
    const cellW = VW / LANES;
    const top = gate.y - GATE_H / 2;
    for (let i = 0; i < LANES; i++) {
      const x = i * cellW;
      const cx = x + cellW / 2;
      const cell = gate.cells[i];

      if (cell === WALL) {
        ctx.fillStyle = 'rgba(12,12,20,.95)';
        ctx.fillRect(x, top, cellW, GATE_H);
        ctx.save();
        ctx.beginPath();
        ctx.rect(x, top, cellW, GATE_H);
        ctx.clip();
        ctx.strokeStyle = 'rgba(255,59,59,.55)';
        ctx.lineWidth = 7;
        ctx.beginPath();
        for (let s = -GATE_H; s < cellW + GATE_H; s += 16) {
          ctx.moveTo(x + s, top + GATE_H);
          ctx.lineTo(x + s + GATE_H, top);
        }
        ctx.stroke();
        ctx.restore();
        ctx.strokeStyle = 'rgba(255,59,59,.9)';
        ctx.lineWidth = 2;
        ctx.strokeRect(x + 1, top + 1, cellW - 2, GATE_H - 2);
      } else {
        const col = this.colorOf(cell);
        ctx.fillStyle = col;
        ctx.globalAlpha = 0.14;
        ctx.fillRect(x, top, cellW, GATE_H);
        ctx.globalAlpha = 1;
        ctx.strokeStyle = col;
        ctx.lineWidth = 2;
        ctx.strokeRect(x + 1.5, top + 1.5, cellW - 3, GATE_H - 3);
        ctx.save();
        ctx.shadowColor = col;
        ctx.shadowBlur = 14;
        ctx.fillStyle = col;
        shapePath(ctx, cell, cx, gate.y, 12);
        ctx.fill();
        ctx.restore();
      }
    }
  }

  drawCoin(ctx, c) {
    const x = laneX(c.lane);
    const w = Math.abs(Math.cos(c.spin)) * 9 + 2.5;
    ctx.save();
    ctx.shadowColor = '#f5b93a';
    ctx.shadowBlur = 16;
    ctx.fillStyle = '#f5b93a';
    ctx.beginPath();
    ctx.ellipse(x, c.y, w, 10, 0, 0, 6.283);
    ctx.fill();
    ctx.restore();
  }

  drawPaint(ctx, p) {
    const x = laneX(p.lane);
    const col = this.colorOf(p.color);
    const pulse = 1 + Math.sin(p.spin * 1.7) * 0.09;
    ctx.save();
    ctx.globalAlpha = 0.2;
    ctx.fillStyle = col;
    ctx.beginPath();
    ctx.arc(x, p.y, 24 * pulse, 0, 6.283);
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.strokeStyle = col;
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(x, p.y, 20 * pulse, 0, 6.283);
    ctx.stroke();
    ctx.shadowColor = col;
    ctx.shadowBlur = 16;
    ctx.fillStyle = col;
    shapePath(ctx, p.color, x, p.y, 10);
    ctx.fill();
    ctx.restore();
  }

  drawParticles(ctx) {
    for (const p of this.particles) {
      ctx.globalAlpha = Math.max(0, p.life / p.max);
      ctx.fillStyle = p.color;
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.r, 0, 6.283);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  drawPlayer(ctx) {
    const col = this.colorOf(this.player.color);
    const y = this.playerY;

    for (let i = 0; i < this.trail.length; i++) {
      const t = this.trail[i];
      ctx.globalAlpha = (i / this.trail.length) * 0.3;
      ctx.fillStyle = col;
      ctx.beginPath();
      ctx.arc(t.x, t.y, PLAYER_R * (0.25 + (i / this.trail.length) * 0.6), 0, 6.283);
      ctx.fill();
    }
    ctx.globalAlpha = 1;

    const s = 1 + this.player.squash * 0.28;
    ctx.save();
    if (this.grace > 0 && Math.floor(this.grace * 12) % 2 === 0) ctx.globalAlpha = 0.45;
    ctx.translate(this.player.x, y);
    ctx.scale(s, 2 - s);
    ctx.shadowColor = col;
    ctx.shadowBlur = 26;
    ctx.fillStyle = col;
    shapePath(ctx, this.player.color, 0, 0, PLAYER_R);
    ctx.fill();
    ctx.fillStyle = 'rgba(255,255,255,.85)';
    shapePath(ctx, this.player.color, 0, 0, PLAYER_R * 0.42);
    ctx.fill();
    ctx.restore();
  }

  // -------------------------------------------------------------------- loop

  loop(now) {
    requestAnimationFrame(this.loop);
    if (!this.last) this.last = now;
    const dt = Math.min((now - this.last) / 1000, MAX_DT);
    this.last = now;
    this.update(dt);
    this.render();
  }
}
