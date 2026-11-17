import * as THREE from 'three';
import type { Assets } from '../core/Assets';
import { item, RARITY_COLOR } from '../rpg/Items';
import { scaleOf } from '../world/Props';

/**
 * Yerdeki ganimet: altın yığını ya da eşya kesesi. Hafifçe zıplar ve döner,
 * oyuncu yaklaşınca kendiliğinden toplanır (mıknatıs).
 */
export class LootDrop {
  readonly obj = new THREE.Group();
  picked = false;
  private t = Math.random() * 6;
  private vel = new THREE.Vector3();
  private settled = false;
  readonly color: string;

  constructor(assets: Assets, readonly kind: 'gold' | 'item', readonly id: string, readonly qty: number, pos: THREE.Vector3) {
    const model = kind === 'gold' ? 'd_coin_stack_small' : (item(id).model ?? 'p_sack');
    const m = assets.gltf(model).scene.clone(true);
    const s = kind === 'gold' ? 0.55 : model === 'p_sack' ? 2.4 : model.startsWith('w_') ? 0.8 : scaleOf(model);
    m.scale.setScalar(s);
    if (model.startsWith('w_')) m.rotation.z = Math.PI / 2.2; // silah yere yatsın
    this.obj.add(m);
    this.color = kind === 'gold' ? '#ffd860' : RARITY_COLOR[item(id).rarity];
    // Işık sütunu: nadirlik renginde, uzaktan görülsün.
    const beam = new THREE.Mesh(
      new THREE.CylinderGeometry(0.06, 0.14, 3.2, 6, 1, true),
      new THREE.MeshBasicMaterial({ color: this.color, transparent: true, opacity: 0.35, depthWrite: false, blending: THREE.AdditiveBlending }),
    );
    beam.position.y = 1.6;
    this.obj.add(beam);
    this.obj.position.copy(pos);
    this.vel.set((Math.random() - 0.5) * 4, 5 + Math.random() * 2, (Math.random() - 0.5) * 4);
  }

  update(dt: number, groundY: (x: number, z: number) => number): void {
    this.t += dt;
    const p = this.obj.position;
    if (!this.settled) {
      this.vel.y -= 22 * dt;
      p.addScaledVector(this.vel, dt);
      const g = groundY(p.x, p.z) + 0.2;
      if (p.y <= g) { p.y = g; this.settled = true; }
    } else {
      p.y = groundY(p.x, p.z) + 0.25 + Math.sin(this.t * 3) * 0.08;
    }
    this.obj.children[0].rotation.y += dt * 1.6;
  }

  /** Oyuncuya doğru çekil. */
  pull(target: THREE.Vector3, dt: number): void {
    this.obj.position.lerp(target, Math.min(1, dt * 10));
  }
}
