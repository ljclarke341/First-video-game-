// Procedural course generation.
//
// The generator walks a *solution path* forward: it always knows which lane and
// which shape the player can be in when the next gate arrives, and builds the
// gate around that. Paint blobs are placed only where the path actually reaches
// them. So an impossible gate can never be generated - every death is a player
// mistake, which is what makes a score-chase game feel worth replaying.

export const LANES = 3;
export const WALL = -1;

const pick = arr => arr[(Math.random() * arr.length) | 0];
const chance = p => Math.random() < p;
const lerp = (a, b, t) => a + (b - a) * t;
/** 0 before `from` gates, 1 after `to`, linear between. */
const ramp = (n, from, to) => Math.max(0, Math.min(1, (n - from) / (to - from)));

export class Generator {
  constructor() {
    this.reset();
  }

  reset() {
    this.gates = 0;    // gates SPAWNED - runs ~6 ahead of the player
    this.passed = 0;   // gates the player has actually cleared
    this.pathLane = 1;    // lane the solution path is in
    this.pathColor = 0;   // shape the player holds when the next gate arrives
    this.lastGateY = 0;   // y of the most recently spawned gate (negative = above screen)
  }

  /** 0 at the start of a run, 1 once the course is at full intensity. */
  get difficulty() {
    return Math.min(1, this.passed / 80);
  }

  get speed() {
    return lerp(215, 700, this.difficulty);
  }

  /**
   * Seconds between gates. This is the real difficulty dial: it is how long a
   * player gets to read three cells, find their own shape and commit to a lane.
   * The opening gives over 1.5s because a new player is doing all three of
   * those things consciously; by the end it is muscle memory at 0.72s.
   */
  get gap() {
    return this.speed * lerp(1.55, 0.72, this.difficulty);
  }

  /**
   * Mechanics are introduced one at a time rather than all being live from
   * gate one. Each returns 0 until its mechanic should first appear, so the
   * opening teaches steering, then shape-matching, then walls.
   */
  get changeChance() {
    if (this.passed < 4) return 0;                       // steering only
    return lerp(0.18, 0.60, ramp(this.passed, 4, 60));
  }

  get wallChance() {
    if (this.passed < 12) return 0;                      // no instant deaths yet
    return lerp(0.05, 0.42, ramp(this.passed, 12, 65));
  }

  /** A second safe cell - guaranteed while the player is still learning. */
  get mercyChance() {
    if (this.passed < 6) return 1;
    return 0.40 * (1 - this.difficulty);
  }

  /**
   * Spawns gates until the course is filled to `lookahead` units above the
   * screen. Pushes gates, paint blobs and coins into `out`.
   */
  fill(out, lookahead) {
    while (this.lastGateY > -lookahead) {
      const gap = this.gap;
      const gateY = this.lastGateY - gap;
      this.spawnSection(out, gateY, gap);
      this.lastGateY = gateY;
    }
  }

  spawnSection(out, gateY, gap) {
    const d = this.difficulty;
    const fromLane = this.pathLane;
    const fromColor = this.pathColor;

    // Where the path goes through this gate.
    const solLane = chance(0.72)
      ? pick([0, 1, 2].filter(l => l !== fromLane))
      : fromLane;

    // Does the player have to repaint before this gate?
    const needsChange = chance(this.changeChance);
    const solColor = needsChange
      ? pick([0, 1, 2].filter(c => c !== fromColor))
      : fromColor;

    if (needsChange) {
      // The blob sits mid-corridor. Putting it on a third lane forces a real
      // detour; keeping it on the path lanes is the gentler version.
      // A blob off the direct line forces a real detour - only once the
      // player is comfortable collecting one at all.
      const detour = d > 0.3 && chance(lerp(0.15, 0.5, d));
      const paintLane = detour
        ? pick([0, 1, 2])
        : pick([fromLane, solLane]);
      out.push({
        type: 'paint', y: gateY + gap * 0.52, lane: paintLane,
        color: solColor, taken: false, spin: Math.random() * 6.28
      });
      this.pathLane = paintLane;
    }

    // Build the gate cells around the solution cell.
    const wallChance = this.wallChance;
    let mercyLeft = chance(this.mercyChance) ? 1 : 0;   // at most one extra safe cell
    const cells = [];
    for (let i = 0; i < LANES; i++) {
      if (i === solLane) {
        cells.push(solColor);
      } else if (mercyLeft > 0) {
        mercyLeft -= 1;
        cells.push(solColor);
      } else if (chance(wallChance)) {
        cells.push(WALL);
      } else {
        cells.push(pick([0, 1, 2].filter(c => c !== solColor)));
      }
    }

    out.push({ type: 'gate', y: gateY, cells, solLane, resolved: false });

    // Coins reward committing to the line early rather than drifting late.
    const coinCount = chance(0.62) ? 1 + ((Math.random() * 3) | 0) : 0;
    for (let i = 0; i < coinCount; i++) {
      const t = 0.18 + (i + 1) * (0.52 / (coinCount + 1));
      out.push({
        type: 'coin', y: gateY + gap * t,
        lane: chance(0.75) ? solLane : pick([0, 1, 2]),
        taken: false, spin: Math.random() * 6.28
      });
    }

    this.pathLane = solLane;
    this.pathColor = solColor;
    this.gates += 1;
  }
}
