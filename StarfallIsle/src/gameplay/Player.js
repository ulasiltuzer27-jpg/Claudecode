// Mina'nin hareketi. Rapier'in kinematik karakter kontrolcusu carpismayi
// cozer; hiz, yercekimi, ziplama, kanat cirpma, suzulme ve yuzme burada.
//
// Sayilar tasarim kararidir: ziplama tepe yuksekligi v^2/2g = 10^2/56 ~ 1.79 m,
// her kanat cirpma ~1.45 m ekler. Fener zirvesinin son yari 6.8 m oldugu
// icin 4 tuy gerekir (1.79 + 4x1.45 = 7.6 m). tools/verify.mjs bunu denetler.
import * as THREE from 'three';
import { FoxModel } from '../models/Fox.js';
import { BOUNDARY_RADIUS } from '../world/Terrain.js';
import { UPDRAFTS } from '../world/WorldData.js';

export const MOVE = {
  gravity: -28,
  jumpVel: 10,
  flapVel: 9,
  walk: 6.5,
  sprint: 10.5,
  swim: 3.6,
  swimSprint: 5.4,
  glideSpeed: 9.5,
  glideSink: -2.2,
  updraftVel: 7,
  bounceVel: 17,
  groundAccel: 45,
  airAccel: 16,
  coyote: 0.13,
  jumpBuffer: 0.14,
  radius: 0.3,
  halfHeight: 0.25,
};
export const CAPSULE_OFFSET = MOVE.halfHeight + MOVE.radius; // ayak -> merkez

export class Player {
  constructor(game) {
    this.game = game;
    this.model = new FoxModel();
    game.renderer.scene.add(this.model.root);
    this.pos = new THREE.Vector3();
    this.vel = new THREE.Vector3();
    this.yaw = Math.PI;
    this.grounded = false;
    this.wasGrounded = false;
    this.coyote = 0;
    this.jumpBuf = 0;
    this.flapsUsed = 0;
    this.gliding = false;
    this.swimming = false;
    this.airTime = 0;
    this.glideStart = null;
    this.frozen = false;
    this.lastSafe = new THREE.Vector3();
    this.safeTimer = 0;
    this.stepTimer = 0;
    this.fallStartY = 0;
    this.speedNorm = 0;
    this.groundTag = null;
    this._desired = new THREE.Vector3();
  }

  spawn(x, y, z, yaw) {
    const ph = this.game.physics;
    if (!this.char) {
      this.char = ph.createCharacter(MOVE.radius, MOVE.halfHeight, { x, y: y + CAPSULE_OFFSET, z });
    }
    this.char.body.setTranslation({ x, y: y + CAPSULE_OFFSET, z }, true);
    this.char.body.setNextKinematicTranslation({ x, y: y + CAPSULE_OFFSET, z });
    ph.step();
    this.pos.set(x, y, z);
    this.vel.set(0, 0, 0);
    this.yaw = yaw;
    this.lastSafe.copy(this.pos);
    this.model.root.position.copy(this.pos);
    this.model.root.rotation.y = this.yaw;
  }

  get feathers() {
    return this.game.progress ? this.game.progress.featherCount() : 0;
  }

  waterLevelAt() {
    return this.game.world.terrain.waterLevel(this.pos.x, this.pos.z);
  }

  update(dt, input, camYaw) {
    const g = this.game;
    const events = g.events;
    const T = g.world.terrain;

    // --- girdi -> istenen yon (kamera eksenine gore)
    let mv = this.frozen ? { x: 0, y: 0 } : input.move();
    const sprint = !this.frozen && input.held('sprint');
    const fwd = new THREE.Vector3(-Math.sin(camYaw), 0, -Math.cos(camYaw));
    const right = new THREE.Vector3(-fwd.z, 0, fwd.x);
    const wish = new THREE.Vector3().addScaledVector(fwd, mv.y).addScaledVector(right, mv.x);
    const wishLen = Math.min(1, wish.length());
    if (wishLen > 0.001) wish.normalize();

    const wl = this.waterLevelAt();
    const submerged = this.pos.y < wl - 0.42;
    if (submerged && !this.swimming) {
      this.swimming = true;
      events.emit('splash', { pos: this.pos.clone(), strength: Math.min(1, Math.abs(this.vel.y) / 15) });
      this.vel.y *= 0.2;
      this.flapsUsed = 0;
      this.endGlide();
    } else if (this.swimming && this.pos.y > wl - 0.25 && this.grounded) {
      this.swimming = false;
    }

    // --- yatay hiz
    let maxSpeed = sprint ? MOVE.sprint : MOVE.walk;
    if (this.swimming) maxSpeed = sprint ? MOVE.swimSprint : MOVE.swim;
    if (this.gliding) maxSpeed = MOVE.glideSpeed;
    const target = wish.clone().multiplyScalar(maxSpeed * wishLen);
    const accel = this.grounded || this.swimming ? MOVE.groundAccel : MOVE.airAccel * (this.gliding ? 1.4 : 1);
    const hv = new THREE.Vector3(this.vel.x, 0, this.vel.z);
    const diff = target.clone().sub(hv);
    const maxDelta = accel * dt;
    if (diff.length() > maxDelta) diff.setLength(maxDelta);
    hv.add(diff);
    if (this.gliding && wishLen < 0.1) {
      // suzulurken birakilirsa da bakilan yone suzulmeye devam et
      const f = new THREE.Vector3(Math.sin(this.yaw), 0, Math.cos(this.yaw)).multiplyScalar(MOVE.glideSpeed * 0.75);
      hv.lerp(f, Math.min(1, dt * 1.5));
    }
    this.vel.x = hv.x;
    this.vel.z = hv.z;

    // --- ziplama / cirpma / suzulme
    if (!this.frozen && input.pressed('jump')) this.jumpBuf = MOVE.jumpBuffer;
    else this.jumpBuf = Math.max(0, this.jumpBuf - dt);
    this.coyote = this.grounded ? MOVE.coyote : Math.max(0, this.coyote - dt);

    const jumpHeld = !this.frozen && input.held('jump');
    if (this.swimming) {
      const target = wl - 0.48;
      this.vel.y += ((target - this.pos.y) * 9 - this.vel.y * 4) * dt;
      if (this.jumpBuf > 0 && this.pos.y > wl - 0.7) {
        this.vel.y = 8.2;
        this.jumpBuf = 0;
        this.swimming = false;
        events.emit('jump', { pos: this.pos.clone(), water: true });
      }
    } else {
      if (this.jumpBuf > 0 && this.coyote > 0) {
        this.vel.y = MOVE.jumpVel;
        this.jumpBuf = 0;
        this.coyote = 0;
        this.grounded = false;
        this.model.jump();
        events.emit('jump', { pos: this.pos.clone() });
      } else if (this.jumpBuf > 0 && !this.grounded && this.flapsUsed < this.feathers) {
        this.vel.y = Math.max(this.vel.y, MOVE.flapVel);
        this.flapsUsed++;
        this.jumpBuf = 0;
        this.model.doFlip();
        this.endGlide();
        events.emit('flap', { pos: this.pos.clone(), used: this.flapsUsed, total: this.feathers });
      }

      const inUpdraft = this.updraftAt();
      const canGlide = !this.grounded && jumpHeld && (this.vel.y < 0 || (inUpdraft && this.gliding)) && this.model.flip <= 0.05;
      if (canGlide && !this.gliding) {
        this.gliding = true;
        this.glideStart = this.pos.clone();
        events.emit('glideStart');
      } else if (this.gliding && (!jumpHeld || this.grounded)) {
        this.endGlide();
      }

      if (this.gliding) {
        if (inUpdraft) this.vel.y += (MOVE.updraftVel - this.vel.y) * Math.min(1, dt * 2.5);
        else this.vel.y += (MOVE.glideSink - this.vel.y) * Math.min(1, dt * 5);
      } else {
        this.vel.y += MOVE.gravity * dt;
        if (this.vel.y < -32) this.vel.y = -32;
      }
    }

    // --- sinir: acik denizde akinti geri iter
    const r = Math.hypot(this.pos.x, this.pos.z);
    if (r > BOUNDARY_RADIUS) {
      const push = (r - BOUNDARY_RADIUS) * 1.5 + 2;
      this.vel.x -= (this.pos.x / r) * push * dt * 4;
      this.vel.z -= (this.pos.z / r) * push * dt * 4;
      if (!this._warned) {
        this._warned = true;
        events.emit('toast', { key: 'hint.current' });
      }
    } else if (r < BOUNDARY_RADIUS - 10) this._warned = false;

    // --- kontrolcu ile hareket
    const ctrl = this.char.ctrl;
    if (this.vel.y > 0.1) ctrl.disableSnapToGround(); else ctrl.enableSnapToGround(0.35);
    this._desired.copy(this.vel).multiplyScalar(dt);
    ctrl.computeColliderMovement(this.char.collider, this._desired);
    const mvd = ctrl.computedMovement();
    const wasGrounded = this.grounded;
    this.grounded = ctrl.computedGrounded();

    // zemin etiketi (ziplatan mantar vb.)
    this.groundTag = null;
    let groundHandle = null;
    for (let i = 0; i < ctrl.numComputedCollisions(); i++) {
      const c = ctrl.computedCollision(i);
      if (!c || !c.collider) continue;
      const nrm = c.normal1; // carpilan yuzeyin disa bakan normali
      if (nrm && nrm.y > 0.6) {
        groundHandle = c.collider.handle;
        this.groundTag = g.physics.tags.get(groundHandle) || null;
      }
      if (nrm && nrm.y < -0.6 && this.vel.y > 0) this.vel.y = 0; // tavana carpti
    }
    if (!this.groundTag && this.grounded && g.world.bounceColliders.size) {
      const hit = g.physics.groundAt(this.pos.x, this.pos.z, this.pos.y + 0.5, this.char.collider);
      if (hit && this.pos.y - hit.y < 0.3) {
        groundHandle = hit.collider.handle;
        this.groundTag = g.physics.tags.get(groundHandle) || null;
      }
    }

    const t = this.char.body.translation();
    const nx = t.x + mvd.x;
    const ny = t.y + mvd.y;
    const nz = t.z + mvd.z;
    this.char.body.setNextKinematicTranslation({ x: nx, y: ny, z: nz });
    g.physics.step();
    const prevY = this.pos.y;
    this.pos.set(nx, ny - CAPSULE_OFFSET, nz);

    if (dt > 0) {
      // engele takilan yatay hizi sifirla (duvara yapisma olmasin)
      const realVx = mvd.x / dt;
      const realVz = mvd.z / dt;
      if (Math.abs(realVx) < Math.abs(this.vel.x) * 0.5) this.vel.x = realVx;
      if (Math.abs(realVz) < Math.abs(this.vel.z) * 0.5) this.vel.z = realVz;
    }

    // --- inis
    if (this.grounded) {
      if (!wasGrounded) {
        const impact = Math.max(0, -this.vel.y);
        this.model.land(impact);
        events.emit('land', { pos: this.pos.clone(), impact, tag: this.groundTag });
        this.endGlide();
        this.recordAir();
      }
      if (this.groundTag === 'bounce' && this.vel.y <= 0.5) {
        this.vel.y = MOVE.bounceVel;
        this.grounded = false;
        this.flapsUsed = 0;
        this.model.jump();
        events.emit('bounce', { pos: this.pos.clone(), handle: groundHandle });
      } else if (this.vel.y < 0) {
        this.vel.y = 0;
      }
      this.flapsUsed = 0;
      this.airTime = 0;
    } else if (!this.swimming) {
      this.airTime += dt;
      if (wasGrounded && this.vel.y <= 0) this.fallStartY = this.pos.y;
    } else {
      this.airTime = 0;
      this.flapsUsed = 0;
    }

    // --- guvenli nokta (dusme/sikisma kurtarmasi)
    this.safeTimer -= dt;
    if (this.grounded && !this.swimming && this.safeTimer <= 0) {
      this.safeTimer = 1;
      this.lastSafe.copy(this.pos);
    }
    if (this.pos.y < -25 || !Number.isFinite(this.pos.y)) {
      this.spawn(this.lastSafe.x, this.lastSafe.y + 0.5, this.lastSafe.z, this.yaw);
      events.emit('toast', { key: 'hint.rescued' });
    }

    // --- yonelim
    const hs = Math.hypot(this.vel.x, this.vel.z);
    if (hs > 0.4 && wishLen > 0.05) {
      const targetYaw = Math.atan2(this.vel.x, this.vel.z);
      let d = targetYaw - this.yaw;
      d = Math.atan2(Math.sin(d), Math.cos(d));
      this.yaw += d * Math.min(1, dt * (this.gliding ? 4 : 12));
    }
    this.speedNorm = hs / MOVE.walk;

    // --- istatistik izleri
    if (this.swimming) {
      this._swimAcc = (this._swimAcc || 0) + hs * dt;
      if (this._swimAcc >= 1) {
        const m = Math.floor(this._swimAcc);
        this._swimAcc -= m;
        g.stats.add('swim_m', m);
      }
    }
    if (this.gliding && this.glideStart) {
      const dist = Math.hypot(this.pos.x - this.glideStart.x, this.pos.z - this.glideStart.z);
      this.currentGlide = dist;
    }
    if (!this.grounded && !this.swimming && this.airTime > 1) g.stats.max('air_max', Math.floor(this.airTime));

    // adim sesi
    if (this.grounded && hs > 1) {
      this.stepTimer -= dt * hs;
      if (this.stepTimer <= 0) {
        this.stepTimer = 1.6;
        events.emit('step', { pos: this.pos.clone(), surface: this.surfaceType() });
      }
    }

    // --- model
    const m = this.model;
    m.root.position.copy(this.pos);
    if (this.swimming) m.root.position.y = Math.max(this.pos.y, wl - 0.35);
    m.root.rotation.y = this.yaw;
    m.root.rotation.z = this.gliding ? THREE.MathUtils.clamp(-(mv.x || 0) * 0.35, -0.35, 0.35) : 0;
    m.update(dt, {
      speed: this.speedNorm, grounded: this.grounded || this.swimming, vy: this.vel.y,
      gliding: this.gliding, swimming: this.swimming,
    });
    return prevY;
  }

  endGlide() {
    if (!this.gliding) return;
    this.gliding = false;
    if (this.glideStart) {
      const dist = Math.hypot(this.pos.x - this.glideStart.x, this.pos.z - this.glideStart.z);
      this.game.stats.max('glide_max', Math.floor(dist));
      this.glideStart = null;
      this.currentGlide = 0;
    }
  }

  recordAir() {
    if (this.airTime > 1) this.game.stats.max('air_max', Math.floor(this.airTime));
  }

  updraftAt() {
    for (const u of UPDRAFTS) {
      const d = Math.hypot(this.pos.x - u.x, this.pos.z - u.z);
      if (d < u.r && this.pos.y < u.top) return u;
    }
    return null;
  }

  surfaceType() {
    if (this.groundTag === 'bounce') return 'mushroom';
    const T = this.game.world.terrain;
    const h = T.height(this.pos.x, this.pos.z);
    if (this.pos.y - h > 0.4) return 'wood';
    if (h < 2.0) return 'sand';
    return 'grass';
  }
}
