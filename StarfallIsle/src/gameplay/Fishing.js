// Balik tutma: olta at -> bekle -> vurdu! -> makara mini oyunu.
// Makara: dikey cubukta balik kacar, oyuncu tusu basili tutarak yesil
// bolgeyi yukari iter; balik bolgenin icindeyken ilerleme dolar.
import * as THREE from 'three';
import { FISH } from '../world/WorldData.js';
import { t } from '../core/i18n.js';

export class Fishing {
  constructor(game) {
    this.game = game;
    this.state = 'idle';
    this.bobber = new THREE.Mesh(
      new THREE.SphereGeometry(0.09, 10, 8),
      new THREE.MeshStandardMaterial({ color: '#ff4a4a', roughness: 0.4 }),
    );
    const white = new THREE.Mesh(new THREE.SphereGeometry(0.091, 10, 4, 0, Math.PI * 2, 0, Math.PI / 2), new THREE.MeshStandardMaterial({ color: '#ffffff' }));
    this.bobber.add(white);
    this.bobber.visible = false;
    game.renderer.scene.add(this.bobber);
    this.lineGeo = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]);
    this.line = new THREE.Line(this.lineGeo, new THREE.LineBasicMaterial({ color: '#f5f5f5', transparent: true, opacity: 0.8 }));
    this.line.visible = false;
    this.line.frustumCulled = false;
    game.renderer.scene.add(this.line);
    this.rod = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.02, 1.3, 5), new THREE.MeshStandardMaterial({ color: '#8a5a3b' }));
    this.rod.geometry.translate(0, 0.65, 0);
    this.rod.visible = false;
    game.player.model.body.add(this.rod);
    this.rod.position.set(0.15, 0.1, 0.25);
    this.rod.rotation.x = 0.9;
    this.buildUI();
  }

  buildUI() {
    const el = document.createElement('div');
    el.className = 'fishing hidden';
    el.innerHTML = `
      <div class="fish-bar"><div class="fish-zone"></div><div class="fish-icon">🐟</div></div>
      <div class="fish-progress"><div class="fish-fill"></div></div>
      <div class="fish-hint"></div>`;
    document.getElementById('ui').appendChild(el);
    this.el = el;
    this.zoneEl = el.querySelector('.fish-zone');
    this.iconEl = el.querySelector('.fish-icon');
    this.fillEl = el.querySelector('.fish-fill');
    this.hintEl = el.querySelector('.fish-hint');
  }

  // Oyuncunun onunde balik tutulabilir su var mi?
  waterAhead() {
    const g = this.game;
    const p = g.player.pos;
    const T = g.world.terrain;
    const dir = new THREE.Vector3(Math.sin(g.player.yaw), 0, Math.cos(g.player.yaw));
    for (const d of [2.5, 3.5, 4.5]) {
      const x = p.x + dir.x * d;
      const z = p.z + dir.z * d;
      const wl = T.waterLevel(x, z);
      if (T.height(x, z) < wl - 0.5 && p.y >= wl - 0.2) return { pos: new THREE.Vector3(x, wl, z), lake: T.isLake(x, z) };
    }
    return null;
  }

  canFish() {
    const g = this.game;
    return g.save?.flags.rod && g.player.grounded && !g.player.swimming && this.waterAhead();
  }

  start() {
    const spot = this.waterAhead();
    if (!spot) return false;
    const g = this.game;
    this.spot = spot;
    this.state = 'wait';
    this.timer = 2 + Math.random() * 4;
    g.player.frozen = true;
    g.player.vel.set(0, 0, 0);
    this.bobber.position.copy(spot.pos);
    this.bobber.visible = true;
    this.line.visible = true;
    this.rod.visible = true;
    g.audio.sfx('cast');
    this.showHint(t('fish.waiting', { key: g.input.glyph('back') }));
    this.el.classList.remove('hidden');
    this.el.classList.add('waiting');
    return true;
  }

  pickFish() {
    const night = this.game.sky.state.night > 0.5;
    const water = this.spot.lake ? 'lake' : 'sea';
    const valid = FISH.filter((f) => (f.water === 'any' || f.water === water)
      && (f.time === 'any' || (f.time === 'night' && night) || (f.time === 'day' && !night)));
    const total = valid.reduce((a, f) => a + f.weight, 0);
    let r = Math.random() * total;
    for (const f of valid) {
      r -= f.weight;
      if (r <= 0) return f;
    }
    return valid[0];
  }

  showHint(s) {
    this.hintEl.textContent = s;
  }

  stop() {
    const g = this.game;
    this.state = 'idle';
    this.bobber.visible = false;
    this.line.visible = false;
    this.rod.visible = false;
    this.el.classList.add('hidden');
    this.el.classList.remove('waiting', 'reeling');
    g.player.frozen = false;
  }

  update(dt, input) {
    if (this.state === 'idle') return;
    const g = this.game;
    // olta ipi
    const tip = new THREE.Vector3(0, 1.3, 0);
    this.rod.localToWorld(tip);
    const arr = this.lineGeo.attributes.position.array;
    arr[0] = tip.x; arr[1] = tip.y; arr[2] = tip.z;
    arr[3] = this.bobber.position.x; arr[4] = this.bobber.position.y; arr[5] = this.bobber.position.z;
    this.lineGeo.attributes.position.needsUpdate = true;
    const tt = performance.now() / 1000;

    if (input.pressed('back') || input.pressed('jump')) {
      if (this.state !== 'result') this.showHint('');
      this.stop();
      return;
    }

    if (this.state === 'wait') {
      this.bobber.position.y = this.spot.pos.y + Math.sin(tt * 2.5) * 0.03;
      this.timer -= dt;
      if (this.timer <= 0) {
        this.state = 'bite';
        this.timer = 1.0;
        this.fish = this.pickFish();
        g.audio.sfx('bite');
        input.rumble(0.6, 200);
        this.showHint(t('fish.bite', { key: g.input.glyph('interact') }));
        g.particles.normal.emit({ pos: this.spot.pos, count: 10, radial: 1.5, gravity: -6, life: 0.6, size: 0.15, color: '#ffffff', alpha: 0.8 });
      }
    } else if (this.state === 'bite') {
      this.bobber.position.y = this.spot.pos.y - 0.12 + Math.sin(tt * 30) * 0.04;
      this.timer -= dt;
      if (input.pressed('interact')) {
        this.state = 'reel';
        this.progress = 0.3;
        this.zone = 0.35;
        this.zoneVel = 0;
        this.fishY = 0.5;
        this.fishVel = 0;
        this.fishTarget = 0.5;
        this.el.classList.remove('waiting');
        this.el.classList.add('reeling');
        this.showHint(t('fish.reel', { key: g.input.glyph('interact') }));
      } else if (this.timer <= 0) {
        this.showHint(t('fish.escaped'));
        g.audio.sfx('lose');
        this.state = 'wait';
        this.timer = 2.5 + Math.random() * 4;
      }
    } else if (this.state === 'reel') {
      const diff = this.fish.diff;
      const holding = input.held('interact');
      this.zoneVel += (holding ? 2.6 : -2.2) * dt;
      this.zoneVel *= 0.92;
      this.zone = THREE.MathUtils.clamp(this.zone + this.zoneVel * dt * 1.6, 0, 0.72);
      if (this.zone === 0 || this.zone === 0.72) this.zoneVel = 0;
      // balik: rastgele hedeflere kacar, zorluk hizi belirler
      if (Math.random() < dt * (0.8 + diff * 2.2)) this.fishTarget = Math.random();
      this.fishVel += (this.fishTarget - this.fishY) * dt * (3 + diff * 9);
      this.fishVel *= 0.9;
      this.fishY = THREE.MathUtils.clamp(this.fishY + this.fishVel * dt * 3, 0, 1);
      const zoneH = 0.28 - diff * 0.06;
      const inside = this.fishY >= this.zone && this.fishY <= this.zone + zoneH;
      this.progress += (inside ? 0.32 : -0.22 * (0.6 + diff)) * dt;
      this.zoneEl.style.bottom = `${this.zone * 100}%`;
      this.zoneEl.style.height = `${zoneH * 100}%`;
      this.iconEl.style.bottom = `${this.fishY * 92}%`;
      this.fillEl.style.width = `${Math.max(0, Math.min(1, this.progress)) * 100}%`;
      this.zoneEl.classList.toggle('on', inside);
      this.bobber.position.x = this.spot.pos.x + Math.sin(tt * 9) * 0.15;
      if (inside && Math.random() < dt * 8) g.audio.sfx('reel');
      if (this.progress >= 1) this.catch();
      else if (this.progress <= 0) {
        this.showHint(t('fish.lost'));
        g.audio.sfx('lose');
        this.el.classList.remove('reeling');
        this.state = 'wait';
        this.timer = 3 + Math.random() * 3;
      }
    }
  }

  catch() {
    const g = this.game;
    const f = this.fish;
    const size = Math.round(f.size[0] + Math.random() * (f.size[1] - f.size[0]));
    const isNew = !(g.save.fish[f.id] > 0);
    g.save.fish[f.id] = (g.save.fish[f.id] || 0) + 1;
    g.stats.add('fish_caught', 1);
    g.stats.max('species_max', g.quests.species());
    g.audio.sfx('fanfare');
    g.particles.normal.emit({ pos: this.spot.pos, count: 20, radial: 2.5, gravity: -8, life: 0.9, size: 0.18, color: '#ffffff', alpha: 0.85 });
    this.stop();
    g.ui.hud.fishCard(t(`fish.${f.id}`), size, isNew);
    g.events.emit('fishCaught', { id: f.id, size, isNew });
    g.autosave();
  }
}
