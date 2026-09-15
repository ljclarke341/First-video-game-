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

export class Generator {
  constructor() {
    this.reset();
  }

  reset() {
    this.gates = 0;
    this.pathLane = 1;    // lane the solution path is in
    this.pathColor = 0;   // shape the player holds when the next gate arrives
    this.lastGateY = 0;   // y of the most recently spawned gate (negative = above screen)
  }

  /** 0 at the start of a run, 1 once the course is at full intensity. */
  get difficulty() {
    return Math.min(1, this.gates / 55);
  }

  get speed() {
    return lerp(300, 770, this.difficulty);
  }

  /** Seconds between gates - shrinks with difficulty so reactions get tighter. */
  get gap() {
    return this.speed * lerp(0.95, 0.70, this.difficulty);
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
    const needsChange = chance(lerp(0.30, 0.65, d));
    const solColor = needsChange
      ? pick([0, 1, 2].filter(c => c !== fromColor))
      : fromColor;

    if (needsChange) {
      // The blob sits mid-corridor. Putting it on a third lane forces a real
      // detour; keeping it on the path lanes is the gentler version.
      const detour = chance(lerp(0.15, 0.5, d));
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
    const wallChance = lerp(0.06, 0.46, d);
    let mercyLeft = chance(0.45 * (1 - d)) ? 1 : 0;   // at most one extra safe cell
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
