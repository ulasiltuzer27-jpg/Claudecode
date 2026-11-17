import * as THREE from 'three';
import enemiesJson from '../data/enemies.json';
import type { Assets } from '../core/Assets';
import type { GameCtx } from '../core/Context';
import { rng } from '../core/Random';
import { computeDamage, inArc } from '../rpg/Combat';
import { Character, turnTowards } from './Character';
import { nextState, type AIState } from './EnemyAI';
import { Projectile } from './Projectile';

export interface EnemyDef {
  name: string;
  model: string;
  scale: number;
  hp: number;
  damage: number;
  armor: number;
  walk: number;
  run: number;
  xp: number;
  aggro: number;
  attackRange: number;
  cooldown: number;
  weapon: string;
  shield?: string;
  ranged?: boolean;
  attacks: string[];
  loot: string;
  boss?: boolean;
}

export const ENEMIES = enemiesJson as Record<string, EnemyDef>;

let nextId = 1;

export class Enemy {
  readonly id = nextId++;
  readonly def: EnemyDef;
  readonly char: Character;
  readonly spawn: THREE.Vector2;
  readonly radius: number;
  readonly scale: number;
  hp: number;
  maxHp: number;
  state: AIState;
  stateTime = 0;
  facing = rng.range(0, Math.PI * 2);
  campId: string | undefined;
  /** Ölümden sonra kaybolma/yeniden doğma sayacı. */
  deadTime = 0;
  removed = false;
  respawnable: boolean;
  /** Son vurulduğu an (can barı görünürlüğü). */
  lastHitAt = -99;
  engaged = false;
  private dormantStart: boolean;
  private cooldown = 0;
  private attackHitDone = false;
  private attackIndex = 0;
  private provoked = 0;
  private justHit = false;
  private patrol = new THREE.Vector2();
  private knock = new THREE.Vector2();
  private summoned = false;
  private lootDropped = false;
  private animDoneFlag = false;

  constructor(assets: Assets, readonly type: string, x: number, z: number, y: number, opts: { dormant?: boolean; campId?: string } = {}) {
    const def = ENEMIES[type];
    if (!def) throw new Error(`Bilinmeyen düşman: ${type}`);
    this.def = def;
    this.scale = def.scale;
    this.radius = 0.6 * def.scale;
    this.char = new Character(assets, def.model, def.scale);
    this.char.position.set(x, y, z);
    this.char.equip('right', def.weapon);
    if (def.shield) this.char.equip('left', def.shield);
    this.spawn = new THREE.Vector2(x, z);
    this.hp = this.maxHp = def.hp;
    this.campId = opts.campId;
    this.dormantStart = !!opts.dormant;
    this.respawnable = !def.boss;
    this.state = opts.dormant ? 'dormant' : 'idle';
    this.enter(this.state);
  }

  get alive(): boolean {
    return this.state !== 'dead';
  }

  get isBoss(): boolean {
    return !!this.def.boss;
  }

  private enter(s: AIState): void {
    const prev = this.state;
    this.state = s;
    this.stateTime = 0;
    this.animDoneFlag = false;
    const c = this.char;
    switch (s) {
      case 'dormant':
        c.play('Skeletons_Inactive_Floor_Pose', { fade: 0.01 });
        break;
      case 'awaken':
        c.play('Skeletons_Awaken_Floor', { loop: false, fade: 0.05, timeScale: 1.5 });
        break;
      case 'idle':
        c.play(this.isBoss ? 'Idle_Combat' : 'Idle', { fade: 0.3 });
        break;
      case 'patrol': {
        const a = rng.range(0, Math.PI * 2);
        const d = rng.range(2, 7);
        this.patrol.set(this.spawn.x + Math.cos(a) * d, this.spawn.y + Math.sin(a) * d);
        c.play('Walking_D_Skeletons', { fade: 0.3, timeScale: 0.9 });
        break;
      }
      case 'alert':
        c.play('Idle_Combat', { fade: 0.15 });
        break;
      case 'chase':
        c.play('Running_C', { fade: 0.2, timeScale: (this.def.run / 5.2) * 0.95 });
        break;
      case 'attack': {
        const clips = this.def.attacks;
        // Boss: her üçüncü saldırı dönme.
        let clip = clips[this.attackIndex % clips.length];
        if (this.isBoss) clip = this.attackIndex % 3 === 2 ? '2H_Melee_Attack_Spin' : clips[this.attackIndex % 2];
        this.attackIndex++;
        this.attackHitDone = false;
        c.play(clip, { loop: false, fade: 0.1, restart: true, timeScale: this.def.ranged ? 1.1 : this.isBoss ? 0.95 : 1.15 });
        break;
      }
      case 'hit':
        c.play(rng.chance(0.5) ? 'Hit_A' : 'Hit_B', { loop: false, fade: 0.05, restart: true, timeScale: 1.4 });
        break;
      case 'return':
        c.play('Walking_D_Skeletons', { fade: 0.3, timeScale: 1.1 });
        break;
      case 'dead':
        c.play(this.def.model.startsWith('skeleton') ? 'Death_C_Skeletons' : 'Death_A', { loop: false, fade: 0.1 });
        break;
    }
    void prev;
  }

  update(dt: number, ctx: GameCtx): void {
    this.stateTime += dt;
    this.cooldown -= dt;
    this.provoked -= dt;
    const pos = this.char.position;
    const pl = ctx.player;
    const toPlayer = new THREE.Vector2(pl.position.x - pos.x, pl.position.z - pos.z);
    const distToPlayer = toPlayer.length();
    const distToSpawn = Math.hypot(pos.x - this.spawn.x, pos.z - this.spawn.y);
    const animDone = this.char.progress() >= 0.98;

    if (this.state === 'dead') {
      this.deadTime += dt;
      this.char.update(dt);
      if (this.deadTime > 1.0 && !this.lootDropped) {
        this.lootDropped = true;
        ctx.world.particles.boneDust(pos.clone().setY(pos.y + 0.4));
      }
      if (this.deadTime > 6) {
        // Batarak kaybol
        pos.y -= dt * 0.6;
        if (this.deadTime > 9 && !this.removed) {
          this.removed = true;
          this.char.root.visible = false;
        }
      }
      return;
    }

    const prevState = this.state;
    const next = nextState({
      state: this.state,
      stateTime: this.stateTime,
      hp: this.hp,
      distToPlayer,
      distToSpawn,
      playerAlive: pl.alive,
      aggroRange: this.def.aggro,
      attackRange: this.def.attackRange,
      cooldownReady: this.cooldown <= 0,
      ranged: !!this.def.ranged,
      animDone,
      provoked: this.provoked > 0,
      justHit: this.justHit,
      atPatrolTarget: Math.hypot(pos.x - this.patrol.x, pos.z - this.patrol.y) < 0.8,
      isBoss: this.isBoss,
    });
    this.justHit = false;
    if (next !== this.state || (next === 'hit' && prevState === 'hit' && this.stateTime > 0.15)) {
      if (next === 'alert') ctx.audio.play3D('enemy_alert', pos, { volume: 0.8 });
      if (next === 'attack') this.cooldown = this.def.cooldown * rng.range(0.85, 1.2);
      if ((next === 'chase' || next === 'alert') && !this.engaged) {
        this.engaged = true;
        if (this.isBoss) ctx.bus.emit('bossEngaged', { id: this.type });
      }
      if (next === 'return' || next === 'idle') this.engaged = false;
      this.enter(next);
    }

    // --- durum davranışı -----------------------------------------------------
    let speed = 0;
    let goal: THREE.Vector2 | null = null;
    let face: number | null = null;
    const playerAngle = Math.atan2(toPlayer.x, toPlayer.y);
    switch (this.state) {
      case 'patrol':
        speed = this.def.walk * 0.8;
        goal = this.patrol;
        break;
      case 'return':
        speed = this.def.walk * 1.4;
        goal = this.spawn;
        this.hp = Math.min(this.maxHp, this.hp + this.maxHp * 0.2 * dt);
        break;
      case 'alert':
      case 'awaken':
        face = playerAngle;
        break;
      case 'chase': {
        face = playerAngle;
        if (this.def.ranged) {
          // Büyücü mesafe korur: çok yakınsa geri çekil, uzaksa yaklaş.
          if (distToPlayer < 6.5) { speed = -this.def.walk; this.char.play('Walking_Backwards', { fade: 0.2 }); }
          else if (distToPlayer > this.def.attackRange * 0.85) { speed = this.def.run; this.char.play('Running_C', { fade: 0.2 }); }
          else { speed = 0; this.char.play('Idle_Combat', { fade: 0.25 }); }
        } else if (distToPlayer > this.def.attackRange * 0.8) {
          speed = this.def.run;
          this.char.play('Running_C', { fade: 0.2, timeScale: (this.def.run / 5.2) * 0.95 });
        } else {
          this.char.play('Idle_Combat', { fade: 0.2 });
        }
        break;
      }
      case 'attack':
        if (this.char.progress() < 0.35) face = playerAngle;
        this.updateAttack(ctx, distToPlayer);
        break;
    }

    if (face !== null) this.facing = turnTowards(this.facing, face, (this.isBoss ? 4 : 7) * dt);
    if (goal) {
      const dx = goal.x - pos.x;
      const dz = goal.y - pos.z;
      if (Math.hypot(dx, dz) > 0.3) this.facing = turnTowards(this.facing, Math.atan2(dx, dz), 6 * dt);
    }

    // Boss yarı canda iki er çağırır
    if (this.isBoss && !this.summoned && this.hp < this.maxHp * 0.5) {
      this.summoned = true;
      ctx.audio.play3D('enemy_cast', pos, { ref: 10 });
      for (const s of [-1, 1]) {
        const e = ctx.spawnEnemy('skeleton_minion', pos.x + s * 4, pos.z + 3, { campId: this.campId });
        e.respawnable = false;
        e.provoke();
      }
      ctx.bus.emit('notify', { text: 'Kemik Lordu ölüleri çağırıyor!', kind: 'warn' });
    }

    // --- hareket ---------------------------------------------------------------
    let vx = Math.sin(this.facing) * speed + this.knock.x;
    let vz = Math.cos(this.facing) * speed + this.knock.y;
    this.knock.multiplyScalar(Math.max(0, 1 - 8 * dt));
    // Sürü ayrışması: birbirinin içine girmesinler.
    for (const o of ctx.enemies) {
      if (o === this || !o.alive) continue;
      const dx = pos.x - o.char.position.x;
      const dz = pos.z - o.char.position.z;
      const d = Math.hypot(dx, dz);
      const min = this.radius + o.radius + 0.2;
      if (d > 0.001 && d < min) { vx += (dx / d) * (min - d) * 6; vz += (dz / d) * (min - d) * 6; }
    }
    // Oyuncunun içine de girmesin
    if (distToPlayer < this.radius + 0.6 && distToPlayer > 0.001) {
      vx -= (toPlayer.x / distToPlayer) * 3;
      vz -= (toPlayer.y / distToPlayer) * 3;
    }
    if (this.state !== 'dormant' && this.state !== 'awaken') {
      let nx = pos.x + vx * dt;
      let nz = pos.z + vz * dt;
      if (!ctx.world.walkable(nx, nz)) { nx = pos.x; nz = pos.z; }
      const res = ctx.world.colliders.resolve(nx, nz, this.radius, 0);
      pos.x = res.x;
      pos.z = res.z;
    }
    pos.y = ctx.world.groundY(pos.x, pos.z);
    this.char.root.rotation.y = this.facing;
    this.char.update(dt);
  }

  private updateAttack(ctx: GameCtx, distToPlayer: number): void {
    const p = this.char.progress();
    const pl = ctx.player;
    const pos = this.char.position;
    if (!this.attackHitDone && p >= (this.def.ranged ? 0.42 : 0.45)) {
      this.attackHitDone = true;
      if (this.def.ranged) {
        const from = this.char.slotWorldPosition('right');
        if (from.distanceTo(pos) > 3) from.copy(pos).setY(pos.y + 1.8);
        const target = pl.position.clone().setY(pl.position.y + 1.3);
        // Biraz önden nişan al: oyuncu koşarken de isabet şansı olsun.
        const dir = target.sub(from).normalize();
        ctx.projectiles.push(new Projectile(ctx, 'bolt', 'enemy', from, dir.multiplyScalar(15), this.def.damage));
        ctx.audio.play3D('enemy_cast', from);
        return;
      }
      ctx.audio.play3D(this.isBoss ? 'swing_heavy' : 'swing', pos, { volume: 0.8, rate: this.isBoss ? 0.8 : 1 });
      const spin = this.char.currentName === '2H_Melee_Attack_Spin';
      const reach = this.def.attackRange + 0.6;
      if (pl.alive && inArc(pos.x, pos.z, this.facing, pl.position.x, pl.position.z, reach, spin ? Math.PI : Math.PI * 0.38, 0.5)) {
        if (Math.abs(pl.position.y - pos.y) < 2.2) pl.takeHit(this.def.damage, pos.clone(), ctx);
      }
      if (this.isBoss) ctx.shake(0.25);
    }
    void distToPlayer;
  }

  /** Uzaktan kışkırtma (oyuncu vurdu ya da boss çağırdı). */
  provoke(): void {
    this.provoked = 6;
  }

  takeDamage(raw: number, from: THREE.Vector3, ctx: GameCtx, o: { knockback?: number; source?: string } = {}): void {
    if (!this.alive) return;
    const res = computeDamage({ base: raw, armor: this.def.armor, rollVariance: rng.next(), rollCrit: rng.next() });
    this.hp -= res.amount;
    this.lastHitAt = performance.now() / 1000;
    this.provoke();
    this.justHit = true;
    const pos = this.char.position;
    const chest = pos.clone().setY(pos.y + this.char.chestHeight);
    ctx.floatText(chest.clone().setY(chest.y + 1.0 * this.scale), `${res.amount}`, res.crit ? 'crit' : 'damage');
    ctx.world.particles.hitSpark(chest);
    ctx.audio.play3D(o.source === 'spell' ? 'bone_hit' : 'hit', chest, { volume: 0.9 });
    ctx.audio.play3D('bone_hit', chest, { volume: 0.6 });
    if (res.crit) ctx.shake(0.12);
    const kb = (o.knockback ?? 3) / (this.isBoss ? 6 : 1);
    const dx = pos.x - from.x;
    const dz = pos.z - from.z;
    const d = Math.hypot(dx, dz) || 1;
    this.knock.set((dx / d) * kb, (dz / d) * kb);
    if (this.state === 'dormant') this.enter('awaken');
    if (this.hp <= 0) {
      this.hp = 0;
      this.enter('dead');
      this.deadTime = 0;
      ctx.audio.play3D('bone_break', chest, { volume: 1, ref: 6 });
      ctx.bus.emit('enemyKilled', { type: this.type, x: pos.x, z: pos.z, xp: this.def.xp });
      if (this.isBoss) ctx.bus.emit('bossDefeated', { id: this.type });
    }
  }

  /** Yeniden doğ (kamp yeniden dolar). */
  reset(y: number): void {
    this.hp = this.maxHp;
    this.char.position.set(this.spawn.x, y, this.spawn.y);
    this.char.root.visible = true;
    this.removed = false;
    this.lootDropped = false;
    this.deadTime = 0;
    this.engaged = false;
    this.summoned = false;
    this.enter(this.dormantStart ? 'dormant' : 'idle');
  }
}
