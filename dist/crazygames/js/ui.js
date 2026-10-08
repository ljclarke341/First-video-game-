// Screen routing, HUD updates and the theme shop. Menus are DOM rather than
// canvas so they stay crisp, tappable and respect device safe areas.
import { profile, save } from './storage.js';
import { SKINS } from './skins.js';
import { sfx, unlock } from './audio.js';
import { ensureMissions, missionText } from './missions.js';
import { dailyState, todayKey } from './daily.js';

const $ = id => document.getElementById(id);

const SCREENS = ['menu', 'how', 'shop', 'missions', 'board', 'paused', 'over'];

export class UI {
  constructor(hooks) {
    this.hooks = hooks;
    this.el = Object.fromEntries(
      [...SCREENS, 'hud', 'toast'].map(id => [id, $(id)])
    );
    this.bind();
    $('boardBtn').hidden = true;      // shown once a live board is confirmed
    this.refresh();
    this.renderShop();
    this.renderMissions();
  }

  // ------------------------------------------------------------- navigation

  show(name) {
    $('hint').hidden = true;
    for (const id of SCREENS) this.el[id].hidden = id !== name;
    this.el.hud.hidden = name !== null;
    if (name === 'menu' || name === 'shop') this.refresh();
    if (name === 'missions') this.renderMissions();
    // The board holds a live subscription; drop it whenever we navigate away.
    if (name !== 'board') this.hooks.boardClosed?.();
  }

  showHud() {
    for (const id of SCREENS) this.el[id].hidden = true;
    this.el.hud.hidden = false;
  }

  hint(msg) {
    const el = $('hint');
    el.textContent = msg;
    el.hidden = false;
    clearTimeout(this._hintTimer);
    this._hintTimer = setTimeout(() => { el.hidden = true; }, 2600);
  }

  toast(msg) {
    const t = this.el.toast;
    t.textContent = msg;
    t.hidden = false;
    clearTimeout(this._toastTimer);
    this._toastTimer = setTimeout(() => { t.hidden = true; }, 1700);
  }

  // ------------------------------------------------------------------ wiring

  bind() {
    const tap = (id, fn) => $(id).addEventListener('click', () => { unlock(); sfx.ui(); fn(); });

    tap('playBtn', () => this.hooks.play());
    tap('dailyBtn', () => this.hooks.daily());
    tap('missionBtn', () => this.show('missions'));
    tap('missionBack', () => this.show('menu'));
    tap('boardBtn', () => this.hooks.board());
    tap('boardFromOver', () => this.hooks.board());
    tap('boardBack', () => this.show('menu'));
    tap('shopBtn', () => this.show('shop'));
    tap('howBtn', () => this.show('how'));
    tap('howBack', () => this.show('menu'));
    tap('shopBack', () => this.show('menu'));

    tap('pauseBtn', () => this.hooks.pause());
    tap('resumeBtn', () => this.hooks.resume());
    tap('restartBtn', () => this.hooks.play());
    tap('quitBtn', () => this.hooks.quit());

    tap('againBtn', () => this.hooks.play());
    tap('overMenuBtn', () => this.hooks.quit());
    tap('reviveBtn', () => this.hooks.revive());
    tap('doubleBtn', () => this.hooks.double());

    tap('soundBtn', () => {
      profile.sound = !profile.sound;
      save();
      this.refresh();
    });
    tap('musicBtn', () => {
      profile.music = !profile.music;
      save();
      this.hooks.musicToggled?.();
      this.refresh();
    });
    tap('hapticBtn', () => {
      profile.haptics = !profile.haptics;
      save();
      this.refresh();
    });
  }

  // ------------------------------------------------------------------- state

  refresh() {
    $('bestMenu').textContent = profile.best;
    $('coinsMenu').textContent = profile.coins;
    $('coinsShop').textContent = profile.coins;
    $('soundBtn').textContent = `SFX: ${profile.sound ? 'ON' : 'OFF'}`;
    $('musicBtn').textContent = `MUSIC: ${profile.music ? 'ON' : 'OFF'}`;
    $('hapticBtn').textContent = `BUZZ: ${profile.haptics ? 'ON' : 'OFF'}`;

    const daily = dailyState();
    const btn = $('dailyBtn');
    btn.disabled = !daily.available;
    const streak = profile.dailyStreak > 1 ? `  ${profile.dailyStreak} DAY STREAK` : '';
    btn.textContent = daily.available
      ? 'DAILY CHALLENGE'
      : `DAILY DONE - ${daily.score}${streak}`;
  }

  // ------------------------------------------------------------- leaderboard

  showBoard(available) {
    $('boardDate').textContent = todayKey();
    const note = $('boardNote');
    if (available) {
      note.hidden = true;
      this.renderBoard(null, 0);              // "loading" until the first snapshot
    } else {
      $('boardList').innerHTML = '';
      note.hidden = false;
      note.textContent =
        'The live board runs on the hosted version of the game. Your daily '
        + 'scores are still saved on this device.';
    }
    this.show('board');
  }

  /** `rows` null means waiting for the first snapshot. */
  renderBoard(rows, total) {
    const list = $('boardList');
    list.innerHTML = '';

    if (rows === null) {
      const p = document.createElement('div');
      p.className = 'lb-empty';
      p.textContent = 'Loading today\u2019s board...';
      list.append(p);
      return;
    }
    if (!rows.length) {
      const p = document.createElement('div');
      p.className = 'lb-empty';
      p.textContent = 'Nobody has played today\u2019s course yet. Go set the pace.';
      list.append(p);
      return;
    }

    for (const r of rows) {
      const row = document.createElement('div');
      row.className = `lb-row${r.isMe ? ' me' : ''}`;

      const rank = document.createElement('span');
      rank.className = 'lb-rank';
      rank.textContent = `#${r.rank}`;

      const name = document.createElement('span');
      name.className = 'lb-name';
      name.textContent = r.isMe ? `${r.name} (you)` : r.name;

      const score = document.createElement('span');
      score.className = 'lb-score';
      score.textContent = r.score;

      row.append(rank, name, score);
      list.append(row);
    }

    $('boardDate').textContent =
      `${todayKey()}  -  ${total} player${total === 1 ? '' : 's'}`;
  }

  /** Hides the board entry point where no live board exists (packaged app). */
  setBoardAvailable(ok) {
    $('boardBtn').hidden = !ok;
  }

  renderMissions() {
    const list = $('missionList');
    list.innerHTML = '';
    for (const m of ensureMissions()) {
      const pct = Math.min(100, Math.round((m.progress / m.target) * 100));

      const card = document.createElement('div');
      card.className = 'mission';

      const top = document.createElement('div');
      top.className = 'mission-top';
      const text = document.createElement('span');
      text.className = 'mission-text';
      text.textContent = missionText(m);
      const reward = document.createElement('span');
      reward.className = 'mission-reward';
      const dot = document.createElement('span');
      dot.className = 'coin-dot';
      reward.append(dot, document.createTextNode(String(m.reward)));
      top.append(text, reward);

      const bar = document.createElement('div');
      bar.className = 'bar';
      const fill = document.createElement('i');
      fill.style.width = pct + '%';
      bar.append(fill);

      const count = document.createElement('div');
      count.className = 'mission-count';
      count.textContent = `${Math.min(m.progress, m.target)} / ${m.target}`;

      card.append(top, bar, count);
      list.append(card);
    }
  }

  hud(score, coins, mult) {
    $('score').textContent = score;
    $('coinCount').textContent = profile.coins + coins;
    const combo = $('combo');
    combo.textContent = `x${mult}`;
    combo.classList.toggle('hidden', mult <= 1);
  }

  gameOver({ score, coins, isBest, canRevive, canDouble, nearMiss, completed, daily }) {
    $('overTitle').textContent = isBest ? 'NEW BEST!'
      : nearMiss ? 'SO CLOSE'
      : daily ? 'DAILY DONE' : 'RUN OVER';

    // A near miss is the strongest reason to tap Play Again, so name the gap.
    const sub = $('overSub');
    if (nearMiss) {
      sub.textContent = `${nearMiss} point${nearMiss === 1 ? '' : 's'} off your best`;
      sub.hidden = false;
    } else if (daily) {
      sub.textContent = 'Come back tomorrow for a new course';
      sub.hidden = false;
    } else {
      sub.hidden = true;
    }

    const box = $('overMissions');
    box.innerHTML = '';
    box.hidden = !completed || !completed.length;
    for (const c of completed || []) {
      const row = document.createElement('div');
      row.className = 'payout';
      const label = document.createElement('span');
      label.textContent = c.text;
      const amt = document.createElement('b');
      amt.textContent = `+${c.reward}`;
      row.append(label, amt);
      box.append(row);
    }

    $('finalScore').textContent = score;
    $('finalBest').textContent = profile.best;
    $('finalCoins').textContent = coins;
    $('boardFromOver').hidden = !(daily && this.hooks.boardUsable?.());
    $('reviveBtn').hidden = !canRevive;
    // Only offered when there is something worth doubling, and never alongside
    // a revive - two ad prompts on one screen reads as a shakedown.
    $('doubleBtn').hidden = canRevive || !canDouble;
    this.show('over');
  }

  coinsDoubled(total) {
    $('finalCoins').textContent = total;
    $('doubleBtn').hidden = true;
    this.refresh();
  }

  // -------------------------------------------------------------------- shop

  renderShop() {
    const grid = $('skinGrid');
    grid.innerHTML = '';
    for (const skin of SKINS) {
      const owned = profile.owned.includes(skin.id);
      const equipped = profile.skin === skin.id;

      const card = document.createElement('button');
      card.type = 'button';
      card.className = `skin${equipped ? ' equipped' : ''}${owned ? '' : ' locked'}`;

      const sw = document.createElement('div');
      sw.className = 'swatches';
      for (const c of skin.colors) {
        const d = document.createElement('span');
        d.className = 'sw';
        d.style.background = c;
        d.style.boxShadow = `0 0 10px ${c}`;
        sw.append(d);
      }

      const name = document.createElement('div');
      name.className = 'skin-name';
      name.textContent = skin.name;

      const meta = document.createElement('div');
      meta.className = 'skin-meta';
      if (equipped) {
        meta.textContent = 'EQUIPPED';
      } else if (owned) {
        meta.textContent = 'TAP TO EQUIP';
      } else {
        const dot = document.createElement('span');
        dot.className = 'coin-dot';
        meta.append(dot, document.createTextNode(String(skin.cost)));
      }

      card.append(sw, name, meta);
      card.addEventListener('click', () => this.onSkinTap(skin));
      grid.append(card);
    }
  }

  onSkinTap(skin) {
    unlock();
    const owned = profile.owned.includes(skin.id);
    if (owned) {
      profile.skin = skin.id;
      save();
      sfx.ui();
      this.toast(`${skin.name} equipped`);
    } else if (profile.coins >= skin.cost) {
      profile.coins -= skin.cost;
      profile.owned.push(skin.id);
      profile.skin = skin.id;
      save();
      sfx.buy();
      this.toast(`${skin.name} unlocked!`);
    } else {
      sfx.ui();
      this.toast(`Need ${skin.cost - profile.coins} more coins`);
      return;
    }
    this.refresh();
    this.renderShop();
  }
}
