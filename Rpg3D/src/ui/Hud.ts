import * as THREE from 'three';
import type { FloatKind } from '../core/Context';
import type { Enemy } from '../entities/Enemy';
import type { Npc } from '../entities/Npc';
import type { Player } from '../entities/Player';
import { T } from '../i18n/tr';
import { item } from '../rpg/Items';
import type { QuestLog } from '../rpg/Quests';
import { xpToNext } from '../rpg/Stats';
import { CAMPS, RUINS, WORLD_HALF, zoneAt } from '../world/Layout';
import type { Terrain } from '../world/Terrain';
import type { Icons } from './Icons';

export function el<K extends keyof HTMLElementTagNameMap>(tag: K, cls = '', parent?: HTMLElement, html?: string): HTMLElementTagNameMap[K] {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (html !== undefined) e.innerHTML = html;
  parent?.appendChild(e);
  return e;
}

interface Floater { el: HTMLDivElement; pos: THREE.Vector3; t: number; life: number; dx: number }

class Bar {
  readonly root: HTMLDivElement;
  private fill: HTMLDivElement;
  private lag: HTMLDivElement;
  private label: HTMLSpanElement;
  private last = -1;
  constructor(parent: HTMLElement, cls: string) {
    this.root = el('div', `bar ${cls}`, parent);
    this.lag = el('div', 'lag', this.root);
    this.fill = el('div', 'fill', this.root);
    this.label = el('span', '', this.root);
  }
  set(v: number, max: number, showText = true): void {
    const f = Math.max(0, Math.min(1, v / max));
    if (Math.abs(f - this.last) < 0.001) return;
    this.last = f;
    this.fill.style.transform = `scaleX(${f})`;
    this.lag.style.transform = `scaleX(${f})`;
    this.label.textContent = showText ? `${Math.ceil(v)} / ${Math.round(max)}` : '';
  }
}

export class Hud {
  readonly root: HTMLDivElement;
  private hp: Bar;
  private mp: Bar;
  private st: Bar;
  private lvl: HTMLDivElement;
  private xp: HTMLDivElement;
  private hotbar: HTMLDivElement[] = [];
  private spellSlot: HTMLDivElement;
  private spellCd: HTMLDivElement;
  private tracker: HTMLDivElement;
  private mapCanvas: HTMLCanvasElement;
  private mapBase: HTMLCanvasElement;
  private zoneEl: HTMLDivElement;
  private promptEl: HTMLDivElement;
  private toasts: HTMLDivElement;
  private bossBar: HTMLDivElement;
  private boss: Bar;
  private bossName: HTMLDivElement;
  private floaters: HTMLDivElement;
  private floats: Floater[] = [];
  private ebars = new Map<number, { bar: HTMLDivElement; fill: HTMLDivElement; name: HTMLDivElement }>();
  private vignette: HTMLDivElement;
  private hurtFlash = 0;
  private trackerKey = '';
  private hotbarKey = '';
  private zone = '';
  private tmp = new THREE.Vector3();

  constructor(parent: HTMLElement, private icons: Icons) {
    this.root = el('div', 'hud hidden', parent);
    this.vignette = el('div', 'vignette', this.root);
    this.floaters = el('div', 'floaters', this.root);
    const bars = el('div', 'bars', this.root);
    this.hp = new Bar(bars, 'hp');
    this.mp = new Bar(bars, 'mp thin');
    this.st = new Bar(bars, 'st thin');
    this.lvl = el('div', 'lvl', bars);
    this.xp = el('div', '', el('div', 'xpbar', this.root));
    const hb = el('div', 'hotbar', this.root);
    for (let i = 0; i < 4; i++) {
      const s = el('div', 'hslot', hb);
      el('span', 'key', s, String(i + 1));
      el('img', '', s);
      el('span', 'qty', s);
      this.hotbar.push(s);
    }
    this.spellSlot = el('div', 'hslot spell', hb);
    el('span', 'key', this.spellSlot, 'Q');
    const fire = el('div', '', this.spellSlot);
    fire.style.cssText = 'width:30px;height:30px;border-radius:50%;background:radial-gradient(circle at 40% 40%,#fff3a0,#ff9a30 45%,#c03010 75%,transparent 80%);';
    this.spellCd = el('div', 'cd', this.spellSlot);
    el('div', 'crosshair', this.root);
    const mm = el('div', 'minimap', this.root);
    this.mapCanvas = el('canvas', '', mm);
    this.mapCanvas.width = this.mapCanvas.height = 180;
    this.mapBase = document.createElement('canvas');
    this.zoneEl = el('div', 'zone-name', this.root);
    this.tracker = el('div', 'tracker', this.root);
    this.promptEl = el('div', 'prompt hidden', this.root);
    this.toasts = el('div', 'toasts', this.root);
    this.bossBar = el('div', 'bossbar hidden', this.root);
    this.bossName = el('div', 'name', this.bossBar);
    this.boss = new Bar(this.bossBar, 'hp');
  }

  show(v: boolean): void {
    this.root.classList.toggle('hidden', !v);
  }

  /** Minimap altlığı: arazi renkleri bir kez çizilir. */
  bakeMap(terrain: Terrain): void {
    const N = 256;
    this.mapBase.width = this.mapBase.height = N;
    const g = this.mapBase.getContext('2d')!;
    const img = g.createImageData(N, N);
    const c = new THREE.Color();
    for (let j = 0; j < N; j++) {
      for (let i = 0; i < N; i++) {
        const x = -WORLD_HALF + (i / N) * WORLD_HALF * 2;
        const z = -WORLD_HALF + (j / N) * WORLD_HALF * 2;
        const h = terrain.heightAt(x, z);
        if (h < 0) c.setRGB(0.18, 0.42, 0.62);
        else terrain.srgbColorAt(x, z, c);
        const shade = 0.75 + Math.min(0.35, h / 60);
        const k = (j * N + i) * 4;
        img.data[k] = Math.min(255, c.r * 255 * shade);
        img.data[k + 1] = Math.min(255, c.g * 255 * shade);
        img.data[k + 2] = Math.min(255, c.b * 255 * shade);
        img.data[k + 3] = 255;
      }
    }
    g.putImageData(img, 0, 0);
  }

  update(dt: number, player: Player, quests: QuestLog, enemies: Enemy[], npcs: Npc[], camera: THREE.Camera, camYaw: number, castCd: number): void {
    const s = player.stats;
    this.hp.set(player.hp, s.maxHp);
    this.mp.set(player.mana, s.maxMana, false);
    this.st.set(player.stamina, s.maxStamina, false);
    this.lvl.innerHTML = `${T.level} <b>${player.level}</b> · ${Math.floor(player.xp)} / ${xpToNext(player.level)} ${T.xp}`;
    this.xp.style.transform = `scaleX(${player.xp / xpToNext(player.level)})`;
    this.spellCd.style.transform = `scaleY(${Math.max(0, castCd / 0.9)})`;
    this.spellSlot.style.opacity = player.mana >= 18 ? '1' : '0.45';

    // Hotbar (yalnızca değişince yeniden çiz)
    const hk = player.hotbar.map((id) => (id ? `${id}:${player.inventory.count(id)}` : '-')).join('|');
    if (hk !== this.hotbarKey) {
      this.hotbarKey = hk;
      player.hotbar.forEach((id, i) => {
        const slot = this.hotbar[i];
        const img = slot.querySelector('img')!;
        const qty = slot.querySelector('.qty')!;
        if (id) {
          const n = player.inventory.count(id);
          img.src = this.icons.get(id);
          img.title = item(id).name;
          qty.textContent = String(n);
          slot.classList.toggle('empty', n === 0);
        } else {
          img.removeAttribute('src');
          qty.textContent = '';
          slot.classList.add('empty');
        }
      });
    }

    // Hasar alınca kenarlarda kırmızı
    this.hurtFlash = Math.max(0, this.hurtFlash - dt * 1.5);
    const low = 1 - player.hp / s.maxHp;
    const red = Math.max(this.hurtFlash, low > 0.65 ? (low - 0.65) * 1.6 : 0);
    this.vignette.style.boxShadow = `inset 0 0 160px rgba(170, 0, 0, ${red.toFixed(3)})`;

    this.updateTracker(quests);
    this.updateMinimap(player, enemies, npcs, quests, camYaw);
    const z = zoneAt(player.position.x, player.position.z);
    if (z !== this.zone) { this.zone = z; this.zoneEl.textContent = T.zone[z]; }
    this.updateFloaters(dt, camera);
    this.updateEnemyBars(enemies, camera, player);
  }

  hurt(): void {
    this.hurtFlash = 0.55;
  }

  private updateTracker(quests: QuestLog): void {
    const active = quests.active();
    const key = active.map((q) => `${q.id}:${quests.get(q.id)}:${quests.progress.get(q.id)?.join(',')}`).join('|');
    if (key === this.trackerKey) return;
    this.trackerKey = key;
    this.tracker.innerHTML = '';
    for (const q of active) {
      el('h4', '', this.tracker, q.name);
      if (quests.get(q.id) === 'complete') {
        el('p', 'done', this.tracker, `✔ ${T.questReady} — Bilge Ozan`);
        continue;
      }
      const p = quests.progress.get(q.id) ?? [];
      q.objectives.forEach((o, i) => el('p', p[i] >= o.count ? 'done' : '', this.tracker, `${o.text}: ${p[i] ?? 0} / ${o.count}`));
    }
  }

  private updateMinimap(player: Player, enemies: Enemy[], npcs: Npc[], quests: QuestLog, camYaw: number): void {
    const g = this.mapCanvas.getContext('2d')!;
    const W = 180;
    const viewRadius = 70; // birim
    const scale = W / (viewRadius * 2);
    const px = player.position.x;
    const pz = player.position.z;
    g.save();
    g.clearRect(0, 0, W, W);
    g.translate(W / 2, W / 2);
    // Kameraya göre döndür: yukarı = kameranın baktığı yön
    g.rotate(camYaw);
    const N = this.mapBase.width;
    const texPerUnit = N / (WORLD_HALF * 2);
    const sx = (px + WORLD_HALF) * texPerUnit - viewRadius * 1.5 * texPerUnit;
    const sz = (pz + WORLD_HALF) * texPerUnit - viewRadius * 1.5 * texPerUnit;
    const sw = viewRadius * 3 * texPerUnit;
    g.imageSmoothingEnabled = true;
    g.drawImage(this.mapBase, sx, sz, sw, sw, -viewRadius * 1.5 * scale, -viewRadius * 1.5 * scale, viewRadius * 3 * scale, viewRadius * 3 * scale);
    const dot = (x: number, z: number, r: number, color: string) => {
      const dx = (x - px) * scale;
      const dz = (z - pz) * scale;
      if (dx * dx + dz * dz > (W / 2 + 4) ** 2) return;
      g.fillStyle = color;
      g.beginPath();
      g.arc(dx, dz, r, 0, Math.PI * 2);
      g.fill();
    };
    for (const e of enemies) if (e.alive && e.state !== 'dormant') dot(e.char.position.x, e.char.position.z, e.isBoss ? 5 : 3, e.isBoss ? '#ff3030' : '#e04a3a');
    for (const n of npcs) dot(n.position.x, n.position.z, 3.5, '#ffd23a');
    // Görev hedefi işareti
    const target = this.questTarget(quests);
    if (target) {
      const dx = (target.x - px) * scale;
      const dz = (target.z - pz) * scale;
      const d = Math.hypot(dx, dz);
      const lim = W / 2 - 8;
      const k = d > lim ? lim / d : 1;
      g.fillStyle = '#ffd23a';
      g.strokeStyle = '#3a2400';
      g.lineWidth = 2;
      g.beginPath();
      g.moveTo(dx * k, dz * k - 7);
      g.lineTo(dx * k + 5, dz * k);
      g.lineTo(dx * k, dz * k + 7);
      g.lineTo(dx * k - 5, dz * k);
      g.closePath();
      g.stroke();
      g.fill();
    }
    g.restore();
    // Oyuncu oku (ortada, oyuncunun baktığı yöne)
    g.save();
    g.translate(W / 2, W / 2);
    g.rotate(camYaw - player.facing + Math.PI);
    g.fillStyle = '#fff';
    g.strokeStyle = '#000';
    g.lineWidth = 1.5;
    g.beginPath();
    g.moveTo(0, -8);
    g.lineTo(5.5, 6);
    g.lineTo(0, 3);
    g.lineTo(-5.5, 6);
    g.closePath();
    g.fill();
    g.stroke();
    g.restore();
  }

  questTarget(quests: QuestLog): { x: number; z: number } | null {
    for (const q of quests.active()) {
      if (quests.get(q.id) === 'complete') return { x: -4.5, z: 5.5 };
      if (q.id === 'q1_bones') return CAMPS.find((c) => c.id === 'east_field')!;
      if (q.id === 'q2_amulet') return CAMPS.find((c) => c.id === 'north_wood')!;
      if (q.id === 'q3_lord') return RUINS;
    }
    if (quests.get('q1_bones') === 'available') return { x: -4.5, z: 5.5 };
    return null;
  }

  setPrompt(text: string | null): void {
    if (!text) { this.promptEl.classList.add('hidden'); return; }
    this.promptEl.innerHTML = `<kbd>E</kbd>${text}`;
    this.promptEl.classList.remove('hidden');
  }

  toast(text: string, kind: string = 'info'): void {
    const t = el('div', `toast ${kind}`, this.toasts, text);
    setTimeout(() => t.remove(), 4000);
    while (this.toasts.children.length > 6) this.toasts.firstChild!.remove();
  }

  banner(text: string, small = ''): void {
    const b = el('div', 'banner', this.root, `${text}${small ? `<small>${small}</small>` : ''}`);
    setTimeout(() => b.remove(), 3300);
  }

  setBoss(e: Enemy | null): void {
    this.bossBar.classList.toggle('hidden', !e);
    if (e) {
      this.bossName.textContent = e.def.name;
      this.boss.set(e.hp, e.maxHp);
    }
  }

  float(pos: THREE.Vector3, text: string, kind: FloatKind): void {
    const e = el('div', `float ${kind}`, this.floaters, text);
    this.floats.push({ el: e, pos: pos.clone(), t: 0, life: kind === 'xp' || kind === 'gold' ? 1.6 : 1.0, dx: (Math.random() - 0.5) * 40 });
    if (this.floats.length > 40) this.floats.shift()!.el.remove();
  }

  private project(pos: THREE.Vector3, camera: THREE.Camera): { x: number; y: number; visible: boolean } {
    this.tmp.copy(pos).project(camera);
    return {
      x: (this.tmp.x * 0.5 + 0.5) * window.innerWidth,
      y: (-this.tmp.y * 0.5 + 0.5) * window.innerHeight,
      visible: this.tmp.z < 1 && this.tmp.z > -1,
    };
  }

  private updateFloaters(dt: number, camera: THREE.Camera): void {
    this.floats = this.floats.filter((f) => {
      f.t += dt;
      if (f.t >= f.life) { f.el.remove(); return false; }
      const p = this.project(f.pos, camera);
      const k = f.t / f.life;
      f.el.style.display = p.visible ? '' : 'none';
      f.el.style.left = `${p.x + f.dx * k}px`;
      f.el.style.top = `${p.y - k * 60}px`;
      f.el.style.opacity = String(k < 0.7 ? 1 : 1 - (k - 0.7) / 0.3);
      return true;
    });
  }

  private updateEnemyBars(enemies: Enemy[], camera: THREE.Camera, player: Player): void {
    const seen = new Set<number>();
    for (const e of enemies) {
      if (!e.alive || e.isBoss || e.state === 'dormant') continue;
      const d = e.char.position.distanceTo(player.position);
      const recent = e.lastHitAt > 0 && performance.now() / 1000 - e.lastHitAt < 8;
      if (d > 28 || (!recent && !e.engaged)) continue;
      const head = e.char.position.clone().setY(e.char.position.y + e.char.headHeight + 0.35);
      const p = this.project(head, camera);
      if (!p.visible) continue;
      seen.add(e.id);
      let b = this.ebars.get(e.id);
      if (!b) {
        const bar = el('div', 'ebar', this.floaters);
        const fill = el('div', '', bar);
        const name = el('div', 'ebar-name', this.floaters, e.def.name);
        b = { bar, fill, name };
        this.ebars.set(e.id, b);
      }
      b.bar.style.left = `${p.x}px`;
      b.bar.style.top = `${p.y}px`;
      b.name.style.left = `${p.x}px`;
      b.name.style.top = `${p.y}px`;
      b.fill.style.transform = `scaleX(${e.hp / e.maxHp})`;
    }
    for (const [id, b] of this.ebars) {
      if (seen.has(id)) continue;
      b.bar.remove();
      b.name.remove();
      this.ebars.delete(id);
    }
  }
}
