// Screen routing, HUD updates and the theme shop. Menus are DOM rather than
// canvas so they stay crisp, tappable and respect device safe areas.
import { profile, save } from './storage.js';
import { SKINS } from './skins.js';
import { sfx, unlock } from './audio.js';

const $ = id => document.getElementById(id);

const SCREENS = ['menu', 'how', 'shop', 'paused', 'over'];

export class UI {
  constructor(hooks) {
    this.hooks = hooks;
    this.el = Object.fromEntries(
      [...SCREENS, 'hud', 'toast'].map(id => [id, $(id)])
    );
    this.bind();
    this.refresh();
    this.renderShop();
  }

  // ------------------------------------------------------------- navigation

  show(name) {
    $('hint').hidden = true;
    for (const id of SCREENS) this.el[id].hidden = id !== name;
    this.el.hud.hidden = name !== null;
    if (name === 'menu' || name === 'shop') this.refresh();
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
    $('soundBtn').textContent = `SOUND: ${profile.sound ? 'ON' : 'OFF'}`;
    $('hapticBtn').textContent = `VIBRATE: ${profile.haptics ? 'ON' : 'OFF'}`;
  }

  hud(score, coins, mult) {
    $('score').textContent = score;
    $('coinCount').textContent = profile.coins + coins;
    const combo = $('combo');
    combo.textContent = `x${mult}`;
    combo.classList.toggle('hidden', mult <= 1);
  }

  gameOver({ score, coins, isBest, canRevive, canDouble }) {
    $('overTitle').textContent = isBest ? 'NEW BEST!' : 'RUN OVER';
    $('finalScore').textContent = score;
    $('finalBest').textContent = profile.best;
    $('finalCoins').textContent = coins;
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
