// Oyun ici gostergeler: sayaclar, etkilesim ipucu, bolge afisi, bildirimler.
import { el, esc } from './UI.js';
import { t } from '../core/i18n.js';
import { drawAchievementIcon } from '../achievements/iconArt.js';
import { SHARD_TOTAL } from '../world/WorldData.js';

export const ICONS = {
  star: '<svg viewBox="0 0 24 24" width="26" height="26"><path d="M12 1.8l3 6.6 7.2.8-5.4 4.9 1.5 7.1L12 17.6l-6.3 3.6 1.5-7.1L1.8 9.2 9 8.4z" fill="#ffc93d" stroke="#e8920c" stroke-width="1.2" stroke-linejoin="round"/></svg>',
  shell: '<svg viewBox="0 0 24 24" width="26" height="26"><path d="M12 21L4 11.5C6 5 18 5 20 11.5z" fill="#ffc4b2" stroke="#e98a74" stroke-width="1.2" stroke-linejoin="round"/><path d="M12 20.5V7.5M12 20.5L8 8.6M12 20.5l4-11.9M12 20.5L5.5 10.5M12 20.5l6.5-10" stroke="#e98a74" stroke-width="0.9"/></svg>',
  feather: '<svg viewBox="0 0 24 24" width="22" height="22"><path d="M17 3c-6 1-10 6-10 12l-2 6 2-1c5 0 10-4 11-10z" fill="#ffc93d" stroke="#e8920c" stroke-width="1.1" stroke-linejoin="round"/></svg>',
};

export class Hud {
  constructor(ui) {
    this.ui = ui;
    const root = el('div', 'hud off');
    root.innerHTML = `
      <div class="counters">
        <div class="pill p-shards"><span class="ic">${ICONS.star}</span><span class="v">0</span><span class="sub">/ ${SHARD_TOTAL}</span></div>
        <div class="pill feathers"></div>
        <div class="pill p-shells"><span class="ic">${ICONS.shell}</span><span class="v">0</span></div>
      </div>
      <div class="region-banner"><div class="r-name"></div><div class="r-sub"></div><div class="r-line"></div></div>
      <div class="toasts"></div>
      <div class="fps hidden"></div>`;
    ui.root.appendChild(root);
    this.root = root;
    this.shardsEl = root.querySelector('.p-shards');
    this.shellsEl = root.querySelector('.p-shells');
    this.feathersEl = root.querySelector('.feathers');
    this.bannerEl = root.querySelector('.region-banner');
    this.toastsEl = root.querySelector('.toasts');
    this.fpsEl = root.querySelector('.fps');
    this.promptEl = null;
    this.hintEl = null;
    this.visible = false;
    this.lastFeathers = '';
  }

  setVisible(v) {
    this.visible = v;
    this.root.classList.toggle('off', !v);
  }

  setCounts({ shards, shells, feathers, flapsUsed }) {
    const sv = this.shardsEl.querySelector('.v');
    if (sv.textContent !== String(shards)) {
      sv.textContent = shards;
      this.bump(this.shardsEl);
    }
    const hv = this.shellsEl.querySelector('.v');
    if (hv.textContent !== String(shells)) {
      hv.textContent = shells;
      this.bump(this.shellsEl);
    }
    const key = `${feathers}:${flapsUsed}`;
    if (key !== this.lastFeathers) {
      this.lastFeathers = key;
      this.feathersEl.classList.toggle('hidden', feathers === 0);
      let html = '';
      for (let i = 0; i < feathers; i++) html += `<div class="feather-dot ${i < feathers - flapsUsed ? 'on' : 'used'}"></div>`;
      this.feathersEl.innerHTML = html;
    }
  }

  bump(e) {
    e.classList.remove('bump');
    void e.offsetWidth;
    e.classList.add('bump');
  }

  prompt(action, text) {
    if (!text) {
      if (this.promptEl) { this.promptEl.remove(); this.promptEl = null; this.promptKey = null; }
      return;
    }
    const g = this.ui.game.input;
    const glyph = g.glyph(action);
    const key = `${glyph}|${text}`;
    if (key === this.promptKey) return;
    this.promptKey = key;
    if (this.promptEl) this.promptEl.remove();
    this.promptEl = el('div', 'prompt', `<span class="key ${g.device === 'pad' ? 'pad' : ''}">${esc(glyph)}</span>${esc(text)}`);
    this.root.appendChild(this.promptEl);
  }

  hint(text, ms = 5000) {
    if (this.hintEl) this.hintEl.remove();
    clearTimeout(this.hintT);
    if (!text) { this.hintEl = null; return; }
    this.hintEl = el('div', 'hint', esc(text));
    this.root.appendChild(this.hintEl);
    this.hintT = setTimeout(() => { this.hintEl?.remove(); this.hintEl = null; }, ms);
  }

  regionBanner(name, sub) {
    this.bannerEl.querySelector('.r-name').textContent = name;
    this.bannerEl.querySelector('.r-sub').textContent = sub || '';
    this.bannerEl.classList.add('show');
    clearTimeout(this.bannerT);
    this.bannerT = setTimeout(() => this.bannerEl.classList.remove('show'), 3800);
  }

  toast(text) {
    const e = el('div', 'toast simple', esc(text));
    this.pushToast(e, 3200);
  }

  achievement(a) {
    const e = el('div', 'toast');
    const cv = document.createElement('canvas');
    cv.width = cv.height = 112;
    drawAchievementIcon(cv, a.icon, false);
    e.appendChild(cv);
    const txt = el('div', '', `<div class="t-kicker">${esc(t('ach.unlocked'))}</div><div class="t-title">${esc(t(`ach.${a.id}.name`))}</div><div class="t-desc">${esc(t(`ach.${a.id}.desc`))}</div>`);
    e.appendChild(txt);
    this.pushToast(e, 5200);
  }

  pushToast(e, ms) {
    this.toastsEl.appendChild(e);
    while (this.toastsEl.children.length > 4) this.toastsEl.firstChild.remove();
    setTimeout(() => {
      e.classList.add('out');
      setTimeout(() => e.remove(), 500);
    }, ms);
  }

  raceBanner(text) {
    this.raceBannerEl?.remove();
    this.raceBannerEl = null;
    if (!text) return;
    this.raceBannerEl = el('div', 'race-banner', esc(text));
    this.ui.root.appendChild(this.raceBannerEl);
  }

  raceTimer(sec) {
    if (sec === null) {
      this.raceTimerEl?.remove();
      this.raceTimerEl = null;
      return;
    }
    if (!this.raceTimerEl) {
      this.raceTimerEl = el('div', 'race-timer');
      this.root.appendChild(this.raceTimerEl);
    }
    this.raceTimerEl.textContent = `${sec.toFixed(1)} s`;
  }

  titleCard(main, sub) {
    this.titleEl?.remove();
    this.titleEl = null;
    if (!main) return;
    this.titleEl = el('div', 'title-card', `<div class="tc-main">${esc(main)}</div><div class="tc-sub">${esc(sub || '')}</div>`);
    this.ui.root.appendChild(this.titleEl);
  }

  fishCard(name, size, isNew) {
    this.fishEl?.remove();
    this.fishEl = el('div', 'fish-card', `${isNew ? `<div class="fc-new">${esc(t('fish.newSpecies'))}</div>` : ''}<div class="fc-name">${esc(name)}</div><div class="fc-size">${size} cm</div>`);
    this.root.appendChild(this.fishEl);
    const e = this.fishEl;
    setTimeout(() => e.remove(), 3200);
  }

  fps(v, show) {
    this.fpsEl.classList.toggle('hidden', !show);
    if (show) this.fpsEl.textContent = `${v.toFixed(0)} FPS`;
  }
}
