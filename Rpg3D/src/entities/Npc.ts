import * as THREE from 'three';
import type { Assets } from '../core/Assets';
import { Character, turnTowards } from './Character';

export type NpcRole = 'elder' | 'merchant' | 'smith' | 'villager';

/** Köylü: yerinde durur, yaklaşınca oyuncuya döner, konuşulabilir. */
export class Npc {
  readonly char: Character;
  readonly marker: THREE.Sprite;
  private baseFacing: number;
  private markerKind: '' | '!' | '?' = '';
  private t = Math.random() * 10;

  constructor(assets: Assets, readonly role: NpcRole, readonly name: string, model: string, x: number, y: number, z: number, rot: number, builtin?: [('right' | 'left'), string][]) {
    this.char = new Character(assets, model);
    this.char.position.set(x, y, z);
    this.char.root.rotation.y = rot;
    this.baseFacing = rot;
    for (const [slot, n] of builtin ?? []) this.char.showBuiltin(slot, n);
    this.char.play('Idle', { fade: 0.01 });
    this.marker = new THREE.Sprite(new THREE.SpriteMaterial({ transparent: true, depthTest: false }));
    this.marker.scale.set(0.9, 0.9, 1);
    this.marker.position.set(0, 3.2, 0);
    this.marker.renderOrder = 20;
    this.marker.visible = false;
    this.char.root.add(this.marker);
  }

  get position(): THREE.Vector3 {
    return this.char.position;
  }

  setMarker(kind: '' | '!' | '?'): void {
    if (kind === this.markerKind) return;
    this.markerKind = kind;
    this.marker.visible = kind !== '';
    if (!kind) return;
    const c = document.createElement('canvas');
    c.width = c.height = 64;
    const g = c.getContext('2d')!;
    g.font = 'bold 54px Georgia, serif';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.lineWidth = 6;
    g.strokeStyle = '#3a2400';
    g.strokeText(kind, 32, 36);
    g.fillStyle = kind === '!' ? '#ffd23a' : '#9fe0ff';
    g.fillText(kind, 32, 36);
    const mat = this.marker.material;
    mat.map?.dispose();
    mat.map = new THREE.CanvasTexture(c);
    mat.map.colorSpace = THREE.SRGBColorSpace;
    mat.needsUpdate = true;
  }

  update(dt: number, playerPos: THREE.Vector3): void {
    this.t += dt;
    const dx = playerPos.x - this.position.x;
    const dz = playerPos.z - this.position.z;
    const d = Math.hypot(dx, dz);
    const target = d < 8 ? Math.atan2(dx, dz) : this.baseFacing;
    this.char.root.rotation.y = turnTowards(this.char.root.rotation.y, target, 3 * dt);
    this.marker.position.y = 3.2 + Math.sin(this.t * 2.5) * 0.12;
    this.char.update(dt);
  }

  talk(): void {
    this.char.play('Interact', { loop: false, fade: 0.15, restart: true });
    setTimeout(() => this.char.play('Idle', { fade: 0.4 }), 1400);
  }
}
