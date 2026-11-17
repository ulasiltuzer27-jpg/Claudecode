import * as THREE from 'three';
import type { GameCtx } from '../core/Context';
import { WATER_LEVEL } from '../world/Layout';

export type ProjectileKind = 'fireball' | 'bolt';

const geoCache = new Map<ProjectileKind, THREE.SphereGeometry>();
const matCache = new Map<ProjectileKind, THREE.MeshBasicMaterial>();

/**
 * Ateş topu (oyuncu) ve büyü oku (iskelet büyücü).
 * Basit: düz uçar, arazi/engel/hedefe değince patlar. Ateş topu alan hasarı.
 */
export class Projectile {
  readonly mesh: THREE.Mesh;
  readonly light: THREE.PointLight | null;
  alive = true;
  private life = 2.4;
  private prev = new THREE.Vector3();

  constructor(
    ctx: GameCtx,
    readonly kind: ProjectileKind,
    readonly owner: 'player' | 'enemy',
    from: THREE.Vector3,
    readonly vel: THREE.Vector3,
    readonly damage: number,
  ) {
    if (!geoCache.has(kind)) geoCache.set(kind, new THREE.SphereGeometry(kind === 'fireball' ? 0.32 : 0.24, 12, 8));
    if (!matCache.has(kind)) matCache.set(kind, new THREE.MeshBasicMaterial({ color: kind === 'fireball' ? 0xffb050 : 0xc890ff }));
    this.mesh = new THREE.Mesh(geoCache.get(kind)!, matCache.get(kind)!);
    this.mesh.position.copy(from);
    ctx.scene.add(this.mesh);
    // Işık havuzdan ödünç alınır: sahneye yeni PointLight eklemek ışık
    // sayısını değiştirir ve BÜTÜN materyalleri yeniden derletir (takılma).
    this.light = kind === 'fireball' ? ctx.borrowLight(0xff8a30) : null;
  }

  update(dt: number, ctx: GameCtx): void {
    if (!this.alive) return;
    this.life -= dt;
    this.prev.copy(this.mesh.position);
    this.mesh.position.addScaledVector(this.vel, dt);
    const p = this.mesh.position;
    if (this.light) this.light.position.copy(p);
    if (this.kind === 'fireball') ctx.world.particles.fireball(p);
    else ctx.world.particles.magic(p);

    const ground = ctx.world.groundY(p.x, p.z);
    let hit = p.y <= ground + 0.1 || p.y <= WATER_LEVEL || this.life <= 0;
    if (!hit && ctx.world.colliders.blocked(p.x, p.z, 0.2)) {
      // Yüksek engel (duvar/ağaç gövdesi). Alçak kayaların üstünden geçer.
      const near = ctx.world.colliders.near(p.x, p.z, 1);
      hit = near.some((c) => c.height > p.y - ground);
    }

    if (this.owner === 'player') {
      for (const e of ctx.enemies) {
        if (!e.alive) continue;
        const c = e.char.position;
        const dy = p.y - (c.y + e.char.chestHeight * 0.8);
        if (Math.hypot(p.x - c.x, p.z - c.z) < e.radius + 0.4 && Math.abs(dy) < 1.6 * e.scale) { hit = true; break; }
      }
    } else {
      const pl = ctx.player;
      const c = pl.char.position;
      if (pl.alive && Math.hypot(p.x - c.x, p.z - c.z) < 0.9 && p.y > c.y && p.y < c.y + 2.6) {
        pl.takeHit(this.damage, this.prev.clone(), ctx, { ranged: true });
        this.explode(ctx, false);
        return;
      }
    }
    if (hit) this.explode(ctx, this.owner === 'player');
  }

  private explode(ctx: GameCtx, aoe: boolean): void {
    this.alive = false;
    const p = this.mesh.position;
    if (this.kind === 'fireball') {
      ctx.world.particles.explosion(p);
      ctx.audio.play3D('fireball_hit', p, { ref: 6 });
      ctx.shake(0.15);
    } else {
      ctx.world.particles.explosion(p, 0x9a60ff);
      ctx.audio.play3D('bone_hit', p, { volume: 0.6 });
    }
    if (aoe) {
      for (const e of ctx.enemies) {
        if (!e.alive) continue;
        const d = e.char.position.distanceTo(p);
        if (d < 3.2 + e.radius) {
          const falloff = 1 - Math.min(1, d / (3.2 + e.radius)) * 0.5;
          e.takeDamage(this.damage * falloff, p, ctx, { knockback: 5, source: 'spell' });
        }
      }
    }
    this.mesh.removeFromParent();
    if (this.light) ctx.returnLight(this.light);
  }
}
