import * as THREE from 'three';
import type { Assets } from '../core/Assets';
import type { GameCtx } from '../core/Context';
import { rng } from '../core/Random';
import { COMBO, computeDamage, fromFront, inArc } from '../rpg/Combat';
import { Inventory } from '../rpg/Inventory';
import { item } from '../rpg/Items';
import { addXp, derive, type Derived } from '../rpg/Stats';
import { zoneAt } from '../world/Layout';
import { Character, turnTowards, wrapAngle } from './Character';
import { Projectile } from './Projectile';

export type PlayerState = 'move' | 'attack' | 'block' | 'dodge' | 'cast' | 'hit' | 'dead' | 'interact';

const WALK = 4.4;
const RUN = 8.2;
const GRAVITY = 26;
const JUMP_V = 9.2;
const RADIUS = 0.55;

export class Player {
  readonly char: Character;
  readonly inventory = new Inventory();
  level = 1;
  xp = 0;
  hp = 100;
  mana = 50;
  stamina = 100;
  stats: Derived;
  state: PlayerState = 'move';
  stateTime = 0;
  facing = 0;
  private vel = new THREE.Vector3();
  private speed = 0;
  vy = 0;
  grounded = true;
  private airTime = 0;
  private combo = 0;
  private comboQueued = false;
  private swingHit = false;
  private castFired = false;
  private dodgeDir = new THREE.Vector2();
  private staminaDelay = 0;
  private lastDamage = -99;
  private stepDist = 0;
  private potionCooldown = 0;
  private castCooldown = 0;
  private deathTimer = 0;
  private hitLock = 0;
  /** Hotbar: 4 yuva, eşya kimliği (iksirler). */
  hotbar: (string | null)[] = ['health_potion', 'mana_potion', 'big_health_potion', null];
  /** Test için: son oynatılan klip ve sayılan vuruşlar. */
  readonly debug = { swings: 0, hits: 0, casts: 0, dodges: 0, jumps: 0, steps: 0 };

  constructor(assets: Assets) {
    this.char = new Character(assets, 'knight');
    this.stats = derive(this.level, this.inventory.equipment);
  }

  get alive(): boolean {
    return this.state !== 'dead';
  }

  /** Ateş topu bekleme süresi (HUD). */
  get castCd(): number {
    return Math.max(0, this.castCooldown);
  }

  get position(): THREE.Vector3 {
    return this.char.position;
  }

  get twoHanded(): boolean {
    const w = this.inventory.equipment.mainhand;
    return !!w && !!item(w).twoHanded;
  }

  /** Ekipman değişti: statları ve elindeki modelleri güncelle. */
  refreshEquipment(): void {
    const eq = this.inventory.equipment;
    const before = this.stats;
    this.stats = derive(this.level, eq);
    // Maksimum can artınca mevcut can oranı korunur.
    this.hp = Math.min(this.stats.maxHp, this.hp * (this.stats.maxHp / before.maxHp));
    this.char.equip('right', eq.mainhand ? item(eq.mainhand).model ?? null : null);
    this.char.equip('left', eq.offhand ? item(eq.offhand).model ?? null : null);
  }

  gainXp(amount: number, ctx: GameCtx): void {
    const levels = addXp(this, amount);
    for (const L of levels) {
      this.stats = derive(this.level, this.inventory.equipment);
      this.hp = this.stats.maxHp;
      this.mana = this.stats.maxMana;
      this.stamina = this.stats.maxStamina;
      ctx.bus.emit('levelUp', { level: L });
      ctx.world.particles.levelUp(this.position.clone().setY(this.position.y + 0.3));
      ctx.audio.play2D('level_up', { jitter: 0 });
      this.char.play('Cheer', { loop: false, fade: 0.2 });
    }
  }

  private setState(s: PlayerState): void {
    this.state = s;
    this.stateTime = 0;
  }

  /** Kameraya göre hareket girdisi (dünya XZ, normalize). */
  private moveInput(ctx: GameCtx): THREE.Vector2 {
    const i = ctx.input;
    const f = (i.down('forward') ? 1 : 0) - (i.down('back') ? 1 : 0);
    const r = (i.down('right') ? 1 : 0) - (i.down('left') ? 1 : 0);
    const yaw = ctx.aimYaw();
    // Kamera ileri: (-sin, -cos); sağ: (cos, -sin)
    const v = new THREE.Vector2(-Math.sin(yaw) * f + Math.cos(yaw) * r, -Math.cos(yaw) * f - Math.sin(yaw) * r);
    if (v.lengthSq() > 1) v.normalize();
    return v;
  }

  private useStamina(n: number): boolean {
    if (this.stamina < n * 0.5) return false;
    this.stamina = Math.max(0, this.stamina - n);
    this.staminaDelay = 0.8;
    return true;
  }

  update(dt: number, ctx: GameCtx): void {
    this.stateTime += dt;
    this.potionCooldown -= dt;
    this.castCooldown -= dt;
    this.hitLock -= dt;
    const input = ctx.input;
    const pos = this.position;
    const move = this.moveInput(ctx);
    const moving = move.lengthSq() > 0.01;

    if (this.state === 'dead') {
      this.deathTimer += dt;
      this.char.update(dt);
      if (this.deathTimer > 2.6 && this.deathTimer - dt <= 2.6) ctx.bus.emit('playerDied', {});
      return;
    }

    // --- eylem başlatma ----------------------------------------------------
    const free = this.state === 'move' || this.state === 'block';
    if (input.hit('attack') && (free || this.state === 'attack')) this.tryAttack(ctx);
    if (input.hit('dodge') && free && this.grounded && this.useStamina(22)) this.startDodge(move);
    if (input.hit('cast') && free && this.grounded) this.tryCast(ctx);
    if (input.hit('jump') && this.state === 'move' && this.grounded && this.useStamina(8)) {
      this.vy = JUMP_V;
      this.grounded = false;
      this.debug.jumps++;
      this.char.play('Jump_Start', { loop: false, fade: 0.08 });
      ctx.audio.play2D('jump', { volume: 0.7 });
    }
    for (let k = 0; k < 4; k++) if (input.hit(`slot${k + 1}` as 'slot1')) this.useHotbar(k, ctx);

    if (this.state === 'move' && input.down('block') && this.grounded) {
      this.setState('block');
      this.char.play('Blocking', { fade: 0.12 });
    } else if (this.state === 'block' && !input.down('block')) {
      this.setState('move');
    }

    // --- durum davranışı ---------------------------------------------------
    let targetSpeed = 0;
    let desiredFacing = this.facing;
    switch (this.state) {
      case 'move': {
        const run = input.down('run') && moving && this.stamina > 1;
        targetSpeed = moving ? (run ? RUN : WALK) : 0;
        if (run) { this.stamina = Math.max(0, this.stamina - 14 * dt); this.staminaDelay = 0.5; }
        if (moving) desiredFacing = Math.atan2(move.x, move.y);
        break;
      }
      case 'block':
        targetSpeed = moving ? WALK * 0.35 : 0;
        desiredFacing = wrapAngle(ctx.aimYaw() + Math.PI);
        break;
      case 'attack':
        this.updateAttack(ctx);
        targetSpeed = moving ? 1.2 : 0;
        break;
      case 'cast':
        this.updateCast(ctx);
        desiredFacing = wrapAngle(ctx.aimYaw() + Math.PI);
        break;
      case 'dodge': {
        const t = this.stateTime / 0.55;
        const sp = 10 * (1 - t * 0.7);
        this.vel.set(this.dodgeDir.x * sp, 0, this.dodgeDir.y * sp);
        if (t >= 1) this.setState('move');
        break;
      }
      case 'hit':
        if (this.stateTime > 0.38) this.setState('move');
        break;
      case 'interact':
        if (this.stateTime > 0.7) this.setState('move');
        break;
    }

    if (this.state !== 'dodge') {
      // Hızlanma/yavaşlama: anında durmak "buz pateni" kadar kötü durmaz ama
      // biraz yumuşatmak koşuyu ağırlıklı hissettiriyor.
      const accel = targetSpeed > this.speed ? 18 : 24;
      this.speed += Math.sign(targetSpeed - this.speed) * Math.min(Math.abs(targetSpeed - this.speed), accel * dt);
      const dir = moving ? move : new THREE.Vector2(Math.sin(this.facing), Math.cos(this.facing));
      this.vel.set(dir.x * this.speed, 0, dir.y * this.speed);
    }
    this.facing = turnTowards(this.facing, desiredFacing, (this.state === 'move' ? 12 : 16) * dt);

    // --- fizik --------------------------------------------------------------
    let nx = pos.x + this.vel.x * dt;
    let nz = pos.z + this.vel.z * dt;
    if (!ctx.world.walkable(nx, nz)) { nx = pos.x; nz = pos.z; this.speed *= 0.5; }
    const ground = ctx.world.groundY(nx, nz);
    const res = ctx.world.colliders.resolve(nx, nz, RADIUS, pos.y - ground);
    pos.x = res.x;
    pos.z = res.z;
    const g2 = ctx.world.groundY(pos.x, pos.z);
    if (!this.grounded) {
      this.vy -= GRAVITY * dt;
      pos.y += this.vy * dt;
      this.airTime += dt;
      if (pos.y <= g2) {
        pos.y = g2;
        this.grounded = true;
        this.vy = 0;
        if (this.airTime > 0.25) {
          ctx.audio.play2D('land', { volume: 0.8 });
          ctx.audio.play2D(`step_${ctx.world.terrain.surfaceAt(pos.x, pos.z)}`, { volume: 0.9 });
        }
        this.airTime = 0;
      } else if (this.airTime > 0.25 && this.char.currentName === 'Jump_Start' && this.char.progress() > 0.9) {
        this.char.play('Jump_Idle', { fade: 0.1 });
      }
    } else {
      // Yokuş aşağı yürürken zeminden kopma; büyük düşüşte havaya geç.
      if (g2 < pos.y - 0.9) {
        this.grounded = false;
        this.vy = 0;
      } else pos.y = g2;
    }

    this.char.root.rotation.y = this.facing;
    this.updateLocomotionAnim(moving);
    this.updateFootsteps(dt, ctx);
    this.regen(dt);
    this.char.update(dt);
  }

  private updateLocomotionAnim(moving: boolean): void {
    if (this.state !== 'move' || !this.grounded) return;
    // Seviye atlama sevinci / iniş klibi bitene kadar bölme (hareket ederse böl).
    const oneShot = this.char.currentName === 'Cheer' || this.char.currentName === 'PickUp' || this.char.currentName === 'Interact';
    if (oneShot && !moving && this.char.progress() < 0.95) return;
    if (this.speed > 6.2) this.char.play('Running_A', { timeScale: this.speed / 7.2 });
    else if (this.speed > 0.4) this.char.play('Walking_A', { timeScale: Math.max(0.6, this.speed / 3.6) });
    else this.char.play(this.twoHanded ? '2H_Melee_Idle' : 'Idle', { fade: 0.25 });
  }

  private updateFootsteps(dt: number, ctx: GameCtx): void {
    if (!this.grounded || this.state === 'dodge') return;
    const horiz = Math.hypot(this.vel.x, this.vel.z);
    if (horiz < 0.5) return;
    this.stepDist += horiz * dt;
    const stride = horiz > 6 ? 2.75 : 1.85;
    if (this.stepDist >= stride) {
      this.stepDist = 0;
      this.debug.steps++;
      const surf = ctx.world.terrain.surfaceAt(this.position.x, this.position.z);
      ctx.audio.play2D(`step_${surf}`, { volume: horiz > 6 ? 0.75 : 0.55 });
    }
  }

  private regen(dt: number): void {
    this.staminaDelay -= dt;
    if (this.staminaDelay <= 0 && this.state !== 'block') this.stamina = Math.min(this.stats.maxStamina, this.stamina + 30 * dt);
    this.mana = Math.min(this.stats.maxMana, this.mana + 2.2 * dt);
    const calm = performance.now() / 1000 - this.lastDamage > 7;
    if (calm) {
      const rate = zoneAt(this.position.x, this.position.z) === 'village' ? 6 : 1.2;
      this.hp = Math.min(this.stats.maxHp, this.hp + rate * dt);
    }
  }

  // --- saldırı ---------------------------------------------------------------

  private comboClips(): string[] {
    return this.twoHanded ? COMBO.twoHanded : COMBO.oneHanded;
  }

  private tryAttack(ctx: GameCtx): void {
    if (this.state === 'attack') {
      if (this.char.progress() >= COMBO.queueFrom && this.combo < 2) this.comboQueued = true;
      return;
    }
    if (!this.useStamina(this.twoHanded ? 16 : 11)) {
      ctx.audio.play2D('ui_error', { ui: true, volume: 0.5 });
      return;
    }
    this.combo = 0;
    this.startSwing(ctx);
  }

  private startSwing(ctx: GameCtx): void {
    this.setState('attack');
    this.swingHit = false;
    this.comboQueued = false;
    // Hedef yumuşak kilit: yakında bir düşman varsa ona dön, yoksa kameranın baktığı yöne.
    const target = this.nearestEnemy(ctx, 5, Math.PI * 0.6);
    this.facing = target
      ? Math.atan2(target.char.position.x - this.position.x, target.char.position.z - this.position.z)
      : wrapAngle(ctx.aimYaw() + Math.PI);
    this.char.root.rotation.y = this.facing;
    const clip = this.comboClips()[this.combo];
    this.char.play(clip, { loop: false, fade: 0.08, restart: true, timeScale: 1.35 * this.stats.attackSpeed });
    this.debug.swings++;
  }

  private nearestEnemy(ctx: GameCtx, range: number, halfAngle: number) {
    let best = null;
    let bestD = Infinity;
    const aim = wrapAngle(ctx.aimYaw() + Math.PI);
    for (const e of ctx.enemies) {
      if (!e.alive) continue;
      const d = e.char.position.distanceTo(this.position);
      if (d < bestD && inArc(this.position.x, this.position.z, aim, e.char.position.x, e.char.position.z, range, halfAngle, e.radius)) {
        best = e;
        bestD = d;
      }
    }
    return best;
  }

  private updateAttack(ctx: GameCtx): void {
    const p = this.char.progress();
    if (!this.swingHit && p >= COMBO.hitAt) {
      this.swingHit = true;
      ctx.audio.play3D(this.twoHanded ? 'swing_heavy' : 'swing', this.position, { volume: 0.9 });
      const reach = this.twoHanded ? 3.3 : 2.8;
      const spin = this.twoHanded && this.combo === 2;
      const mult = COMBO.multipliers[this.combo];
      for (const e of ctx.enemies) {
        if (!e.alive) continue;
        const ep = e.char.position;
        if (Math.abs(ep.y - this.position.y) > 2.5 * e.scale) continue;
        if (inArc(this.position.x, this.position.z, this.facing, ep.x, ep.z, reach, spin ? Math.PI : Math.PI * 0.42, e.radius)) {
          e.takeDamage(this.stats.damage * mult, this.position, ctx, { knockback: this.combo === 2 ? 6 : 3, source: 'melee' });
          this.debug.hits++;
        }
      }
    }
    if (this.comboQueued && p >= COMBO.endAt * 0.85 && this.combo < 2) {
      if (this.useStamina(this.twoHanded ? 16 : 11)) {
        this.combo++;
        this.startSwing(ctx);
        return;
      }
      this.comboQueued = false;
    }
    if (p >= COMBO.endAt + (this.combo === 2 ? 0.12 : 0)) this.setState('move');
  }

  // --- büyü ------------------------------------------------------------------

  private tryCast(ctx: GameCtx): void {
    if (this.castCooldown > 0) return;
    if (this.mana < 18) {
      ctx.bus.emit('notify', { text: 'Yeterli mana yok', kind: 'warn' });
      ctx.audio.play2D('ui_error', { ui: true });
      return;
    }
    this.mana -= 18;
    this.castCooldown = 0.9;
    this.castFired = false;
    this.setState('cast');
    this.facing = wrapAngle(ctx.aimYaw() + Math.PI);
    this.char.play('Spellcast_Shoot', { loop: false, fade: 0.08, restart: true, timeScale: 1.6 });
  }

  private updateCast(ctx: GameCtx): void {
    const p = this.char.progress();
    if (!this.castFired && p > 0.35) {
      this.castFired = true;
      this.debug.casts++;
      const from = this.char.slotWorldPosition('right').add(new THREE.Vector3(0, 0.2, 0));
      // Çıkış noktası el; ama el gövdenin içine girmişse göğüsten çıksın.
      if (from.distanceTo(this.position) > 2.5) from.copy(this.position).setY(this.position.y + 1.6);
      const aim = ctx.aimPoint(new THREE.Vector3());
      const dir = aim.sub(from).normalize();
      ctx.projectiles.push(new Projectile(ctx, 'fireball', 'player', from, dir.multiplyScalar(26), this.stats.spellPower));
      ctx.audio.play3D('fireball_cast', from);
    }
    if (p > 0.72) this.setState('move');
  }

  private startDodge(move: THREE.Vector2): void {
    this.setState('dodge');
    this.debug.dodges++;
    if (move.lengthSq() > 0.01) this.dodgeDir.copy(move).normalize();
    else this.dodgeDir.set(Math.sin(this.facing), Math.cos(this.facing));
    this.facing = Math.atan2(this.dodgeDir.x, this.dodgeDir.y);
    this.char.root.rotation.y = this.facing;
    this.char.play('Dodge_Forward', { loop: false, fade: 0.06, restart: true, timeScale: 1.25 });
  }

  // --- eşya --------------------------------------------------------------------

  useHotbar(k: number, ctx: GameCtx): void {
    const id = this.hotbar[k];
    if (!id) return;
    if (this.inventory.count(id) === 0) {
      ctx.bus.emit('notify', { text: `${item(id).name} kalmadı`, kind: 'warn' });
      ctx.audio.play2D('ui_error', { ui: true });
      return;
    }
    this.consume(id, ctx);
  }

  /** İksir iç. Döner: kullanıldı mı. */
  consume(id: string, ctx: GameCtx): boolean {
    const def = item(id);
    if (def.type !== 'consumable' || this.potionCooldown > 0 || !this.alive) return false;
    if (def.heal && this.hp >= this.stats.maxHp && !def.mana) {
      ctx.bus.emit('notify', { text: 'Canın zaten dolu', kind: 'warn' });
      return false;
    }
    this.inventory.remove(id, 1);
    this.potionCooldown = 0.8;
    if (def.heal) {
      this.hp = Math.min(this.stats.maxHp, this.hp + def.heal);
      ctx.floatText(this.position.clone().setY(this.position.y + 2.6), `+${def.heal}`, 'heal');
      ctx.world.particles.heal(this.position.clone().setY(this.position.y + 0.5));
    }
    if (def.mana) {
      this.mana = Math.min(this.stats.maxMana, this.mana + def.mana);
      ctx.floatText(this.position.clone().setY(this.position.y + 2.6), `+${def.mana}`, 'mana');
    }
    ctx.audio.play2D('heal');
    return true;
  }

  playInteract(): void {
    if (this.state !== 'move') return;
    this.setState('interact');
    this.char.play('Interact', { loop: false, fade: 0.1, restart: true, timeScale: 1.4 });
  }

  // --- hasar ---------------------------------------------------------------------

  /** Düşmandan gelen vuruş. from: saldıranın konumu. */
  takeHit(raw: number, from: THREE.Vector3, ctx: GameCtx, o: { ranged?: boolean } = {}): void {
    if (!this.alive) return;
    // Yuvarlanma: kısa bir yenilmezlik penceresi
    if (this.state === 'dodge' && this.stateTime > 0.04 && this.stateTime < 0.42) {
      ctx.floatText(this.position.clone().setY(this.position.y + 2.4), 'Kaçtı', 'block');
      return;
    }
    const dx = from.x - this.position.x;
    const dz = from.z - this.position.z;
    const blocking = this.state === 'block' && fromFront(this.facing, dx, dz) && this.stamina > 0;
    const res = computeDamage({
      base: raw, armor: this.stats.armor, rollVariance: rng.next(), rollCrit: rng.next(), critChance: 0.05,
      blocked: blocking ? this.stats.block : 0,
    });
    this.lastDamage = performance.now() / 1000;
    if (blocking) {
      this.stamina = Math.max(0, this.stamina - raw * 0.9);
      this.staminaDelay = 0.9;
      this.char.play('Block_Hit', { loop: false, fade: 0.05, restart: true });
      ctx.audio.play3D('block', this.position, { volume: 1 });
      ctx.world.particles.hitSpark(this.position.clone().setY(this.position.y + 1.4).add(new THREE.Vector3(Math.sin(this.facing) * 0.6, 0, Math.cos(this.facing) * 0.6)));
      // Dikenli kalkan saldırana geri hasar verir.
      if (this.inventory.equipment.offhand === 'spike_shield' && !o.ranged) {
        const attacker = ctx.enemies.find((e) => e.alive && e.char.position.distanceTo(from) < 0.5);
        attacker?.takeDamage(6, this.position, ctx, { knockback: 2, source: 'thorns' });
      }
      setTimeout(() => { if (this.state === 'block') this.char.play('Blocking', { fade: 0.15 }); }, 350);
    } else {
      ctx.audio.play2D('player_hurt', { volume: 0.9 });
      ctx.audio.play3D('hit', this.position, { volume: 0.7 });
      ctx.shake(Math.min(0.5, res.amount / 40));
      if (res.amount > this.stats.maxHp * 0.08 && this.hitLock <= 0 && (this.state === 'move' || this.state === 'block')) {
        this.setState('hit');
        this.hitLock = 1.1;
        this.char.play(rng.chance(0.5) ? 'Hit_A' : 'Hit_B', { loop: false, fade: 0.06, restart: true, timeScale: 1.3 });
      }
    }
    this.hp -= res.amount;
    ctx.floatText(this.position.clone().setY(this.position.y + 2.5), blocking ? `${res.amount}` : `-${res.amount}`, blocking ? 'block' : 'player');
    ctx.bus.emit('playerDamaged', { amount: res.amount });
    if (this.hp <= 0) this.die(ctx);
  }

  private die(ctx: GameCtx): void {
    this.hp = 0;
    this.setState('dead');
    this.deathTimer = 0;
    this.char.play('Death_A', { loop: false, fade: 0.1 });
    ctx.audio.play2D('game_over', { jitter: 0 });
  }

  respawn(x: number, z: number, y: number): void {
    this.position.set(x, y, z);
    this.hp = this.stats.maxHp;
    this.mana = this.stats.maxMana;
    this.stamina = this.stats.maxStamina;
    this.vy = 0;
    this.grounded = true;
    this.speed = 0;
    this.setState('move');
    this.char.play('Idle', { fade: 0.01, restart: true });
  }
}
