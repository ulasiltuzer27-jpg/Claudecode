import dialogJson from '../data/dialog.json';
import type { Player } from '../entities/Player';
import { T } from '../i18n/tr';
import { INVENTORY_SIZE } from '../rpg/Inventory';
import { item, RARITY_COLOR, type EquipSlot, type ItemDef } from '../rpg/Items';
import type { QuestDef, QuestLog } from '../rpg/Quests';
import { el } from './Hud';
import type { Icons } from './Icons';

export const DIALOG = dialogJson as unknown as Record<string, Record<string, unknown> & { name: string; greet: string; bye: string }>;

export interface Settings {
  master: number;
  music: number;
  sfx: number;
  sensitivity: number;
  invertY: boolean;
  quality: 'low' | 'medium' | 'high';
}

const RARITY_NAME = { common: 'Sıradan', uncommon: 'Nadir', rare: 'Efsanevi' };
const TYPE_NAME: Record<string, string> = { weapon: 'Silah', shield: 'Kalkan', ring: 'Yüzük', consumable: 'Tüketilebilir', junk: 'Ganimet', quest: 'Görev eşyası' };

/** Fareyle üstüne gelince eşya bilgisi. */
export class Tooltip {
  private e: HTMLDivElement;
  constructor(parent: HTMLElement) {
    this.e = el('div', 'tooltip hidden', parent);
  }
  show(def: ItemDef, x: number, y: number, extra = ''): void {
    const lines: string[] = [];
    if (def.damage) lines.push(`${T.stats.damage}: ${def.damage}${def.twoHanded ? ' (iki el)' : ''}`);
    if (def.speed && def.speed !== 1) lines.push(`Hız: ${def.speed > 1 ? 'hızlı' : 'yavaş'}`);
    if (def.armor) lines.push(`${T.stats.armor}: ${def.armor}`);
    if (def.block) lines.push(`${T.stats.block}: %${Math.round(def.block * 100)}`);
    if (def.damageBonus) lines.push(`+${def.damageBonus} ${T.stats.damage.toLowerCase()}`);
    if (def.hpBonus) lines.push(`+${def.hpBonus} ${T.stats.hp.toLowerCase()}`);
    if (def.heal) lines.push(`+${def.heal} can`);
    if (def.mana) lines.push(`+${def.mana} mana`);
    this.e.innerHTML = `<div class="n" style="color:${RARITY_COLOR[def.rarity]}">${def.name}</div>
      <div class="t">${RARITY_NAME[def.rarity]} ${TYPE_NAME[def.type]}</div>
      ${lines.map((l) => `<div class="s">${l}</div>`).join('')}
      <div class="d">${def.desc}</div>
      ${def.value ? `<div style="color:#ffd860;margin-top:4px">Değer: ${def.value} ${T.gold.toLowerCase()}</div>` : ''}${extra}`;
    this.e.classList.remove('hidden');
    const r = this.e.getBoundingClientRect();
    this.e.style.left = `${Math.min(window.innerWidth - r.width - 8, x + 16)}px`;
    this.e.style.top = `${Math.min(window.innerHeight - r.height - 8, y + 16)}px`;
  }
  hide(): void {
    this.e.classList.add('hidden');
  }
}

function slotEl(parent: HTMLElement, icons: Icons, id: string | null, qty: number, cls = 'slot'): HTMLDivElement {
  const s = el('div', cls, parent);
  if (id) {
    const img = el('img', '', s);
    img.src = icons.get(id);
    img.draggable = false;
    s.style.borderColor = RARITY_COLOR[item(id).rarity] + '88';
    if (qty > 1) el('span', 'qty', s, String(qty));
  }
  return s;
}

export class Panels {
  readonly root: HTMLDivElement;
  readonly tooltip: Tooltip;
  private screen: HTMLDivElement | null = null;
  private panel: HTMLDivElement | null = null;
  openPanel: '' | 'inventory' | 'quests' | 'dialog' | 'shop' = '';

  constructor(parent: HTMLElement, private icons: Icons) {
    this.root = el('div', 'panels', parent);
    this.tooltip = new Tooltip(parent);
  }

  // --- Tam ekranlar -----------------------------------------------------------

  private setScreen(build: (s: HTMLDivElement) => void, cls = ''): HTMLDivElement {
    this.screen?.remove();
    const s = el('div', `screen ${cls}`, this.root);
    build(s);
    this.screen = s;
    return s;
  }

  hideScreen(): void {
    this.screen?.remove();
    this.screen = null;
  }

  get screenOpen(): boolean {
    return !!this.screen;
  }

  loading(): { set: (f: number, label: string) => void } {
    let bar!: HTMLDivElement;
    let label!: HTMLDivElement;
    this.setScreen((s) => {
      el('h1', 'title', s, T.title);
      el('div', 'subtitle', s, T.subtitle);
      bar = el('div', '', el('div', 'loadbar', s));
      label = el('div', 'loadlabel', s, T.loading);
    });
    return {
      set: (f, l) => {
        bar.style.width = `${Math.round(f * 100)}%`;
        label.textContent = `${T.loading} · ${l}`;
      },
    };
  }

  mainMenu(o: { hasSave: boolean; onNew: () => void; onContinue: () => void; onSettings: () => void; onCredits: () => void }): void {
    this.setScreen((s) => {
      el('h1', 'title', s, T.title);
      el('div', 'subtitle', s, T.subtitle);
      const m = el('div', 'menu', s);
      const cont = el('button', 'btn', m, T.continue);
      cont.disabled = !o.hasSave;
      cont.onclick = o.onContinue;
      el('button', 'btn', m, T.newGame).onclick = o.onNew;
      el('button', 'btn', m, T.settings).onclick = o.onSettings;
      el('button', 'btn', m, T.credits).onclick = o.onCredits;
      const g = el('div', 'controls-grid', s);
      for (const [k, v] of T.controls) { el('b', '', g, k); el('span', '', g, v); }
    });
  }

  credits(onBack: () => void): void {
    this.setScreen((s) => {
      el('h2', 'title', s, T.credits).style.fontSize = '40px';
      el('p', 'credits', s, T.creditsText);
      el('button', 'btn', s, T.back).onclick = onBack;
    });
  }

  pause(o: { onResume: () => void; onSave: () => void; onSettings: () => void; onMenu: () => void }): void {
    this.setScreen((s) => {
      el('h1', 'title', s, T.paused).style.fontSize = '54px';
      const m = el('div', 'menu', s);
      m.style.marginTop = '26px';
      el('button', 'btn', m, T.resume).onclick = o.onResume;
      el('button', 'btn', m, T.save).onclick = o.onSave;
      el('button', 'btn', m, T.settings).onclick = o.onSettings;
      el('button', 'btn', m, T.mainMenu).onclick = o.onMenu;
      const g = el('div', 'controls-grid', s);
      for (const [k, v] of T.controls) { el('b', '', g, k); el('span', '', g, v); }
    }, 'clear');
  }

  settings(cur: Settings, onChange: (s: Settings) => void, onBack: () => void): void {
    this.setScreen((s) => {
      el('h2', 'title', s, T.settings).style.fontSize = '44px';
      const grid = el('div', 'settings', s);
      grid.style.marginTop = '22px';
      const slider = (label: string, key: 'master' | 'music' | 'sfx' | 'sensitivity', min: number, max: number) => {
        el('label', '', grid, label);
        const i = el('input', '', grid);
        i.type = 'range';
        i.min = String(min);
        i.max = String(max);
        i.step = '0.01';
        i.value = String(cur[key]);
        i.oninput = () => { cur[key] = Number(i.value); onChange(cur); };
      };
      slider(T.volumeMaster, 'master', 0, 1);
      slider(T.volumeMusic, 'music', 0, 1);
      slider(T.volumeSfx, 'sfx', 0, 1);
      slider(T.sensitivity, 'sensitivity', 0.2, 3);
      el('label', '', grid, T.invertY);
      const inv = el('button', 'btn small', grid, cur.invertY ? T.on : T.off);
      inv.onclick = () => { cur.invertY = !cur.invertY; inv.textContent = cur.invertY ? T.on : T.off; onChange(cur); };
      el('label', '', grid, T.quality);
      const sel = el('select', '', grid);
      for (const q of ['low', 'medium', 'high'] as const) {
        const op = el('option', '', sel, T.qualityNames[q]);
        op.value = q;
        if (cur.quality === q) op.selected = true;
      }
      sel.onchange = () => { cur.quality = sel.value as Settings['quality']; onChange(cur); };
      el('button', 'btn', s, T.back).onclick = onBack;
    });
  }

  death(onRespawn: () => void): void {
    this.setScreen((s) => {
      const t = el('h1', 'title', s, T.youDied);
      t.style.color = '#d04030';
      el('div', 'subtitle', s, T.diedHint).style.textTransform = 'none';
      el('button', 'btn', s, T.respawn).onclick = onRespawn;
    });
  }

  // --- Oyun içi paneller -------------------------------------------------------

  private setPanel(kind: Panels['openPanel'], cls: string, build: (p: HTMLDivElement) => void): void {
    this.panel?.remove();
    this.tooltip.hide();
    this.openPanel = kind;
    const p = el('div', `panel ${cls}`, this.root);
    build(p);
    this.panel = p;
  }

  closePanel(): void {
    this.panel?.remove();
    this.panel = null;
    this.openPanel = '';
    this.tooltip.hide();
  }

  private hoverable(s: HTMLElement, id: string, extra = ''): void {
    s.onmousemove = (e) => this.tooltip.show(item(id), e.clientX, e.clientY, extra);
    s.onmouseleave = () => this.tooltip.hide();
  }

  inventory(player: Player, o: { onSlot: (i: number) => void; onAssign: (i: number) => void; onUnequip: (s: EquipSlot) => void; onClose: () => void }): void {
    const inv = player.inventory;
    this.setPanel('inventory', '', (p) => {
      el('button', 'btn small close', p, '✕').onclick = o.onClose;
      el('h2', '', p, T.inventory);
      const wrap = el('div', 'inv', p);
      const left = el('div', 'inv-left', wrap);
      el('h3', '', left, T.equipment);
      for (const slot of ['mainhand', 'offhand', 'ring'] as EquipSlot[]) {
        const row = el('div', 'eqrow', left);
        const id = inv.equipment[slot];
        const s = slotEl(row, this.icons, id, 1, 'slot eq');
        el('span', '', row, id ? `<b style="color:${RARITY_COLOR[item(id).rarity]}">${item(id).name}</b><br>${T.slotNames[slot]}` : T.slotNames[slot]);
        if (id) {
          this.hoverable(s, id, '<div class="hint">Tıkla: çıkar</div>');
          s.onclick = () => o.onUnequip(slot);
        }
      }
      el('h3', '', left, 'Nitelikler');
      const st = player.stats;
      const sl = el('div', 'statlist', left);
      const row = (k: string, v: string) => { el('span', '', sl, k); el('b', '', sl, v); };
      row(T.stats.hp, `${Math.ceil(player.hp)} / ${st.maxHp}`);
      row(T.stats.mana, `${Math.ceil(player.mana)} / ${st.maxMana}`);
      row(T.stats.damage, String(st.damage));
      row(T.stats.armor, String(st.armor));
      row(T.stats.block, `%${Math.round(st.block * 100)}`);
      row(T.stats.spell, String(st.spellPower));
      el('div', 'goldline', left, `◉ ${inv.gold} ${T.gold}`);

      const right = el('div', '', wrap);
      el('h3', '', right, `Çanta (${INVENTORY_SIZE - inv.freeSlots()}/${INVENTORY_SIZE})`);
      const grid = el('div', 'grid', right);
      inv.slots.forEach((stack, i) => {
        const s = slotEl(grid, this.icons, stack?.id ?? null, stack?.qty ?? 0);
        if (!stack) return;
        this.hoverable(s, stack.id);
        s.onclick = () => o.onSlot(i);
        s.oncontextmenu = (e) => { e.preventDefault(); o.onAssign(i); };
      });
      el('div', 'hint', right, T.invHint);
    });
  }

  quests(log: QuestLog, onClose: () => void): void {
    this.setPanel('quests', '', (p) => {
      el('button', 'btn small close', p, '✕').onclick = onClose;
      el('h2', '', p, T.questLog);
      const shown = [...log.status].filter(([, s]) => s !== 'locked' && s !== 'available');
      if (!shown.length) el('p', 'hint', p, T.noQuests);
      for (const [id, status] of shown) {
        const q = log.def(id);
        const box = el('div', `quest ${status}`, p);
        el('div', 'qn', box, `${q.name}${status === 'done' ? ' ✔' : ''}`);
        el('div', 'qs', box, q.summary);
        const prog = log.progress.get(id) ?? [];
        q.objectives.forEach((ob, i) => el('div', 'qo', box, `${prog[i] >= ob.count ? '✔' : '•'} ${ob.text}: ${prog[i] ?? 0} / ${ob.count}`));
        if (status === 'complete') el('div', 'qo', box, `<span style="color:var(--good)">${T.questReady}</span>`);
        el('div', 'hint', box, `${T.reward}: ${rewardText(q)}`);
      }
    });
  }

  dialog(who: string, text: string, options: { label: string; action: () => void }[], reward?: string): void {
    this.setPanel('dialog', 'dialog', (p) => {
      el('div', 'who', p, who);
      el('div', 'text', p, text);
      if (reward) el('div', 'reward', p, `${T.reward}: ${reward}`);
      const opts = el('div', 'opts', p);
      options.forEach((op, i) => {
        const b = el('button', 'btn small', opts, `${i + 1}. ${op.label}`);
        b.onclick = op.action;
        b.dataset.key = String(i + 1);
      });
    });
  }

  /** Diyalogda 1/2/3 tuşları. */
  pressOption(n: number): boolean {
    if (this.openPanel !== 'dialog' || !this.panel) return false;
    const b = this.panel.querySelector<HTMLButtonElement>(`button[data-key="${n}"]`);
    b?.click();
    return !!b;
  }

  shop(player: Player, stock: string[], o: { onBuy: (id: string) => void; onSell: (i: number) => void; onClose: () => void }): void {
    const inv = player.inventory;
    this.setPanel('shop', '', (p) => {
      el('button', 'btn small close', p, '✕').onclick = o.onClose;
      el('h2', '', p, `${T.shop} — ${DIALOG.merchant.name}`);
      const wrap = el('div', 'shop', p);
      const left = el('div', '', wrap);
      el('h3', '', left, T.buy);
      const g1 = el('div', 'grid', left);
      g1.style.gridTemplateColumns = 'repeat(4, 56px)';
      g1.style.rowGap = '14px';
      for (const id of stock) {
        const s = slotEl(g1, this.icons, id, 1);
        el('span', 'price', s, `${buyPrice(id)}`);
        this.hoverable(s, id, `<div class="hint">Fiyat: ${buyPrice(id)} altın</div>`);
        s.onclick = () => o.onBuy(id);
      }
      el('div', 'goldline', left, `◉ ${inv.gold} ${T.gold}`);
      const right = el('div', '', wrap);
      el('h3', '', right, T.sell);
      const g2 = el('div', 'grid', right);
      inv.slots.forEach((stack, i) => {
        const s = slotEl(g2, this.icons, stack?.id ?? null, stack?.qty ?? 0);
        if (!stack) return;
        const def = item(stack.id);
        if (def.type === 'quest') { s.style.opacity = '0.4'; return; }
        this.hoverable(s, stack.id, `<div class="hint">Satış: ${sellPrice(stack.id)} altın</div>`);
        s.onclick = () => o.onSell(i);
      });
      el('div', 'hint', p, T.shopHint);
    });
  }
}

export const buyPrice = (id: string): number => Math.max(1, Math.round(item(id).value * 1.6));
export const sellPrice = (id: string): number => Math.max(1, Math.round(item(id).value * 0.5));

export function rewardText(q: QuestDef): string {
  const parts = [`${q.reward.xp} XP`, `${q.reward.gold} altın`];
  for (const [id, n] of q.reward.items) parts.push(n > 1 ? `${item(id).name} ×${n}` : item(id).name);
  return parts.join(', ');
}
