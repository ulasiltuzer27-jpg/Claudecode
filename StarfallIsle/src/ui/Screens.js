// Menu ekranlari: ana menu, kayit yuvalari, duraklat, ayarlar, basarimlar,
// gunluk (harita/gorevler/koleksiyon), onay penceresi.
import { Screen, el, esc } from './UI.js';
import { t, LANGS, setLang } from '../core/i18n.js';
import { ACHIEVEMENTS } from '../achievements/AchievementTracker.js';
import { drawAchievementIcon } from '../achievements/iconArt.js';
import { ICONS } from './Hud.js';
import { Platform } from '../core/Platform.js';
import * as WD from '../world/WorldData.js';
import { WORLD_SIZE } from '../world/Terrain.js';

function fmtTime(sec) {
  const m = Math.floor(sec / 60);
  const h = Math.floor(m / 60);
  return h > 0 ? t('time.hm', { h, m: m % 60 }) : t('time.m', { m });
}

function footer(game, pairs) {
  const k = (a) => `<span class="key ${game.input.device === 'pad' ? 'pad' : ''}">${esc(game.input.glyph(a))}</span>`;
  return `<div class="panel-foot">${pairs.map(([a, label]) => `<span>${k(a)}${esc(label)}</span>`).join('')}</div>`;
}

export class MainMenu extends Screen {
  constructor(ui) {
    super(ui, 'main-menu');
  }

  async render() {
    const g = this.game;
    this.el.innerHTML = `
      <div class="logo"><span class="l-star">★</span><div class="l-title">${esc(t('game.title'))}</div><div class="l-sub">${esc(t('game.subtitle'))}</div></div>
      <div class="menu-list"></div>
      <div class="menu-foot"><span>v${esc(g.version)}</span><span class="steam-badge"></span></div>`;
    const list = this.el.querySelector('.menu-list');
    const item = (label, select, disabled = false) => {
      const b = el('button', `menu-item${disabled ? ' disabled' : ''}`, esc(label));
      list.appendChild(b);
      this.add(b, { select, disabled });
    };
    const last = g.profile.lastSlot;
    const lastData = last ? await g.saves.loadSlot(last) : null;
    list.innerHTML = '';
    this.items = [];
    if (lastData) item(t('menu.continue'), () => g.loadSlot(last));
    item(t('menu.play'), () => this.ui.push(new SlotsScreen(this.ui)));
    item(t('menu.achievements'), () => this.ui.push(new AchievementsScreen(this.ui)));
    item(t('menu.settings'), () => this.ui.push(new SettingsScreen(this.ui)));
    item(t('menu.credits'), () => g.ui.credits.show(() => {}));
    if (Platform.isElectron) item(t('menu.quit'), () => Platform.quit());
    this.focus(0, true);
    const st = await Platform.steam.status();
    const badge = this.el.querySelector('.steam-badge');
    if (badge) badge.textContent = st.available ? `Steam ✓ ${st.name || ''}` : t('menu.offline');
  }

  onBack() {}
}

export class SlotsScreen extends Screen {
  constructor(ui) {
    super(ui, 'dim');
  }

  async render() {
    const g = this.game;
    const slots = await g.saves.listSlots();
    this.el.innerHTML = `<div class="panel small"><div class="panel-head"><h2>${esc(t('slots.title'))}</h2></div><div class="panel-body"><div class="btn-list"></div></div>${footer(g, [['confirm', t('ui.select')], ['interact', t('slots.delete')], ['back', t('ui.back')]])}</div>`;
    const list = this.el.querySelector('.btn-list');
    this.items = [];
    for (const { slot, data } of slots) {
      const b = el('button', 'btn slot');
      if (data) {
        const shards = data.collected.filter((id) => id.startsWith('s')).length + data.rewards.filter((r) => WD.QUEST_SHARDS.includes(r)).length;
        b.innerHTML = `<span class="s-title">${esc(t('slots.slot', { n: slot }))}</span><span class="s-meta">★ ${shards}/${WD.SHARD_TOTAL} · ${esc(fmtTime(data.playTime))}<br>${esc(new Date(data.updated).toLocaleDateString())}</span>`;
      } else {
        b.innerHTML = `<span class="s-title">${esc(t('slots.slot', { n: slot }))}</span><span class="s-meta">${esc(t('slots.empty'))}</span>`;
      }
      list.appendChild(b);
      this.add(b, { select: () => (data ? g.loadSlot(slot) : g.newGame(slot)), slot, data });
    }
    this.focus(0, true);
  }

  onInput(input) {
    if (input.pressed('interact') && !input.pressed('confirm')) {
      const it = this.items[this.index];
      if (it?.data) {
        this.ui.push(new ConfirmScreen(this.ui, t('slots.confirmDelete', { n: it.slot }), async () => {
          await this.game.saves.deleteSlot(it.slot);
          if (this.game.profile.lastSlot === it.slot) this.game.profile.lastSlot = null;
          this.refresh();
        }));
      }
    }
  }
}

export class ConfirmScreen extends Screen {
  constructor(ui, text, onYes) {
    super(ui, 'dim');
    this.text = text;
    this.onYes = onYes;
  }

  render() {
    this.el.innerHTML = `<div class="panel small"><div class="panel-body" style="padding-top:26px"><p class="confirm-text">${esc(this.text)}</p><div class="btn-list"></div></div></div>`;
    const list = this.el.querySelector('.btn-list');
    const no = el('button', 'btn', esc(t('ui.no')));
    const yes = el('button', 'btn danger', esc(t('ui.yes')));
    list.append(no, yes);
    this.add(no, { select: () => this.ui.pop() });
    this.add(yes, { select: () => { this.ui.pop(); this.onYes(); } });
  }
}

export class PauseMenu extends Screen {
  constructor(ui) {
    super(ui, 'dim');
  }

  render() {
    const g = this.game;
    const p = g.progress;
    this.el.innerHTML = `<div class="panel small"><div class="panel-head"><h2>${esc(t('pause.title'))}</h2>
      <div class="ach-summary">${ICONS.star} ${p.shardCount()}/${WD.SHARD_TOTAL}</div></div>
      <div class="panel-body"><div class="btn-list"></div></div>${footer(g, [['confirm', t('ui.select')], ['back', t('pause.resume')]])}</div>`;
    const list = this.el.querySelector('.btn-list');
    const b = (label, select) => {
      const e = el('button', 'btn', esc(label));
      list.appendChild(e);
      this.add(e, { select });
    };
    b(t('pause.resume'), () => g.resume());
    b(t('pause.journal'), () => this.ui.push(new JournalScreen(this.ui)));
    b(t('menu.achievements'), () => this.ui.push(new AchievementsScreen(this.ui)));
    b(t('menu.settings'), () => this.ui.push(new SettingsScreen(this.ui)));
    b(t('pause.photo'), () => { g.resume(); g.photo.enter(); });
    b(t('pause.toMenu'), () => g.toMainMenu());
  }

  onBack() {
    this.game.audio.sfx('uiBack');
    this.game.resume();
  }
}

export class SettingsScreen extends Screen {
  constructor(ui) {
    super(ui, 'dim');
  }

  render() {
    const g = this.game;
    const S = g.settings;
    this.el.innerHTML = `<div class="panel"><div class="panel-head"><h2>${esc(t('settings.title'))}</h2></div><div class="panel-body"></div>${footer(g, [['left', '◀ ▶'], ['back', t('ui.back')]])}</div>`;
    const body = this.el.querySelector('.panel-body');
    this.items = [];
    const section = (k) => body.appendChild(el('div', 'section-title', esc(t(k))));
    const row = (label, ctlHtml, handlers) => {
      const r = el('div', 'setting', `<span>${esc(label)}</span><span class="ctl">${ctlHtml}</span>`);
      body.appendChild(r);
      this.add(r, handlers);
      return r;
    };
    const slider = (key, label, min = 0, max = 1, step = 0.1) => {
      const v = S.get(key);
      const pct = ((v - min) / (max - min)) * 100;
      row(label, `<span class="slider"><div style="width:${pct}%"></div></span><b>${Math.round(key === 'sensitivity' ? v * 100 : v * 100)}%</b>`, {
        left: () => { S.set(key, Math.max(min, +(v - step).toFixed(2))); this.refresh(); },
        right: () => { S.set(key, Math.min(max, +(v + step).toFixed(2))); this.refresh(); },
      });
    };
    const toggle = (key, label) => {
      row(label, `<span class="toggle ${S.get(key) ? 'on' : ''}"></span>`, {
        select: () => { S.set(key, !S.get(key)); this.refresh(); },
        left: () => { S.set(key, !S.get(key)); this.refresh(); },
        right: () => { S.set(key, !S.get(key)); this.refresh(); },
      });
    };
    const chooser = (key, label, options, names) => {
      const cur = options.indexOf(S.get(key));
      const set = (d) => { S.set(key, options[(cur + d + options.length) % options.length]); this.refresh(); };
      row(label, `<span class="chooser"><span class="arrow">◀</span><b>${esc(names[Math.max(0, cur)])}</b><span class="arrow">▶</span></span>`, { left: () => set(-1), right: () => set(1), select: () => set(1) });
    };

    section('settings.general');
    chooser('lang', t('settings.language'), LANGS, LANGS.map((l) => t(`lang.${l}`)));
    chooser('textScale', t('settings.textScale'), [1, 1.15, 1.3], ['100%', '115%', '130%']);
    section('settings.audio');
    slider('master', t('settings.master'));
    slider('music', t('settings.music'));
    slider('sfx', t('settings.sfx'));
    slider('ambience', t('settings.ambience'));
    section('settings.video');
    chooser('quality', t('settings.quality'), ['low', 'medium', 'high'], [t('settings.q.low'), t('settings.q.medium'), t('settings.q.high')]);
    toggle('fullscreen', t('settings.fullscreen'));
    toggle('showFps', t('settings.showFps'));
    section('settings.controls');
    slider('sensitivity', t('settings.sensitivity'), 0.3, 2.0, 0.1);
    toggle('invertY', t('settings.invertY'));
    toggle('autoCamera', t('settings.autoCamera'));
    toggle('cameraShake', t('settings.cameraShake'));
    const help = el('div', 'section-title', esc(t('settings.controlsHelp')));
    body.appendChild(help);
    body.appendChild(el('div', '', `<p style="margin:4px 16px;font-weight:700;color:var(--ink-soft);line-height:1.6">${esc(t('settings.controlsText'))}</p>`));
  }
}

export class AchievementsScreen extends Screen {
  constructor(ui) {
    super(ui, 'dim');
  }

  render() {
    const g = this.game;
    const tr = g.achievements;
    const n = tr.count();
    this.el.innerHTML = `<div class="panel"><div class="panel-head"><h2>${esc(t('menu.achievements'))}</h2>
      <div class="ach-summary">${n} / ${ACHIEVEMENTS.length}<div class="bar"><div style="width:${(n / ACHIEVEMENTS.length) * 100}%"></div></div></div></div>
      <div class="panel-body"><div class="ach-grid"></div></div>${footer(g, [['back', t('ui.back')]])}</div>`;
    const grid = this.el.querySelector('.ach-grid');
    this.items = [];
    for (const a of ACHIEVEMENTS) {
      const un = tr.isUnlocked(a.id);
      const hidden = a.hidden && !un;
      const card = el('div', `ach ${un ? '' : 'locked'}`);
      const cv = document.createElement('canvas');
      cv.width = cv.height = 128;
      drawAchievementIcon(cv, hidden ? { glyph: 'star', color: '#6d7480' } : a.icon, !un);
      card.appendChild(cv);
      const prog = !un && a.threshold > 1 && !hidden ? tr.progress(a) : null;
      const info = el('div', '', `<div class="a-name">${esc(hidden ? t('ach.hidden') : t(`ach.${a.id}.name`))}</div>
        <div class="a-desc">${esc(hidden ? t('ach.hiddenDesc') : t(`ach.${a.id}.desc`))}</div>
        ${prog !== null ? `<div class="a-prog"><div style="width:${prog * 100}%"></div></div>` : ''}`);
      card.appendChild(info);
      grid.appendChild(card);
      this.add(card, {});
    }
    requestAnimationFrame(() => {
      const w = grid.clientWidth;
      this.columns = Math.max(1, Math.floor((w + 12) / 262));
    });
  }
}

export class JournalScreen extends Screen {
  constructor(ui) {
    super(ui, 'dim');
    this.tab = 0;
  }

  render() {
    const g = this.game;
    const tabs = ['journal.map', 'journal.quests', 'journal.collection'];
    this.el.innerHTML = `<div class="panel"><div class="panel-head"><h2>${esc(t('pause.journal'))}</h2>
      <div class="tabs">${tabs.map((k, i) => `<span class="tab ${i === this.tab ? 'on' : ''}">${esc(t(k))}</span>`).join('')}</div></div>
      <div class="panel-body"></div>${footer(g, [['tabPrev', '/'], ['tabNext', t('journal.switch')], ['back', t('ui.back')]])}</div>`;
    this.el.querySelectorAll('.tab').forEach((e, i) => e.addEventListener('click', () => { this.tab = i; this.refresh(); }));
    const body = this.el.querySelector('.panel-body');
    if (this.tab === 0) this.renderMap(body);
    else if (this.tab === 1) this.renderQuests(body);
    else this.renderCollection(body);
  }

  onInput(input) {
    if (input.pressed('tabPrev') || input.pressed('left')) { this.tab = (this.tab + 2) % 3; this.refresh(); this.game.audio.sfx('uiMove'); }
    if (input.pressed('tabNext') || input.pressed('right')) { this.tab = (this.tab + 1) % 3; this.refresh(); this.game.audio.sfx('uiMove'); }
    if (input.pressed('journal')) this.onBack();
  }

  renderMap(body) {
    const g = this.game;
    const wrap = el('div', 'map-wrap');
    wrap.appendChild(g.mapCanvas());
    const toPct = (v) => ((v + WORLD_SIZE / 2) / WORLD_SIZE) * 100;
    for (const r of WD.REGIONS) {
      if (r.secret && !g.save.regions.includes(r.id)) continue;
      const known = g.save.regions.includes(r.id);
      const lab = el('div', `map-label ${known ? '' : 'unknown'}`, esc(known ? t(`region.${r.id}`) : '???'));
      lab.style.left = `${toPct(r.x)}%`;
      lab.style.top = `${toPct(r.z)}%`;
      wrap.appendChild(lab);
    }
    const p = el('div', 'map-player');
    p.style.left = `${toPct(g.player.pos.x)}%`;
    p.style.top = `${toPct(g.player.pos.z)}%`;
    wrap.appendChild(p);
    body.appendChild(wrap);
  }

  renderQuests(body) {
    const g = this.game;
    const s = g.save;
    const Q = g.quests;
    const list = [];
    const shards = g.progress.shardCount();
    list.push({ title: t('quest.main.title'), desc: s.flags.finale ? t('quest.main.done') : !s.flags.owlIntro ? t('quest.main.start') : t('quest.main.desc', { n: shards, goal: WD.SHARDS_FOR_FINALE }), state: s.flags.finale ? 'done' : 'active' });
    const q = (id, params) => {
      const st = s.quests[id];
      if (st === 'none') return list.push({ title: t(`quest.${id}.title`), desc: t('quest.unknown'), state: 'none' });
      list.push({ title: t(`quest.${id}.title`), desc: t(st === 'done' ? `quest.${id}.done` : `quest.${id}.desc`, params), state: st });
    };
    q('rabbit', { n: Q.carrots() });
    q('beaver', { n: Q.tools() });
    q('frog');
    q('bear', { n: Q.fishTotal(), species: Q.species(), total: WD.FISH.length });
    for (const it of list) {
      body.appendChild(el('div', `quest ${it.state}`, `<div class="q-state">${it.state === 'done' ? '✓' : it.state === 'active' ? '!' : '?'}</div><div><div class="q-title">${esc(it.title)}</div><div class="q-desc">${esc(it.desc)}</div></div>`));
    }
  }

  renderCollection(body) {
    const g = this.game;
    const s = g.save;
    const p = g.progress;
    const grid = el('div', 'coll-grid');
    const c = (num, label) => grid.appendChild(el('div', 'coll', `<div class="c-num">${esc(num)}</div><div class="c-label">${esc(label)}</div>`));
    c(`${p.shardCount()} / ${WD.SHARD_TOTAL}`, t('coll.shards'));
    c(`${p.featherCount()} / ${WD.FEATHER_TOTAL}`, t('coll.feathers'));
    c(`${s.shellsTotal} / 100`, t('coll.shells'));
    c(`${g.quests.species()} / ${WD.FISH.length}`, t('coll.fish'));
    c(`${s.regions.filter((r) => WD.MAIN_REGIONS.includes(r)).length} / ${WD.MAIN_REGIONS.length}`, t('coll.regions'));
    c(`${p.completion()}%`, t('coll.completion'));
    body.appendChild(grid);
    const fishList = el('div', '', `<div class="section-title">${esc(t('coll.fishList'))}</div>`);
    const fg = el('div', 'coll-grid');
    for (const f of WD.FISH) {
      const n = s.fish[f.id] || 0;
      fg.appendChild(el('div', 'coll', `<div class="c-label">${n ? '🐟' : '❔'} ${esc(n ? t(`fish.${f.id}`) : '???')}</div><div class="c-label">${n ? `× ${n}` : esc(t(`fish.hint.${f.id}`))}</div>`));
    }
    fishList.appendChild(fg);
    body.appendChild(fishList);
  }
}
