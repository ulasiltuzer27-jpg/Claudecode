import type * as THREE from 'three';
import type { AudioManager } from '../audio/AudioManager';
import type { Enemy } from '../entities/Enemy';
import type { Player } from '../entities/Player';
import type { Projectile } from '../entities/Projectile';
import type { World } from '../world/World';
import type { Assets } from './Assets';
import type { EventBus } from './EventBus';
import type { Input } from './Input';

export type FloatKind = 'damage' | 'crit' | 'player' | 'heal' | 'xp' | 'gold' | 'block' | 'mana';

/**
 * Varlıkların (oyuncu, düşman, mermi) birbirine ve ortak sistemlere erişimi.
 * Game bunu uygular; varlıklar Game'i değil bu arayüzü tanır.
 */
export interface GameCtx {
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  assets: Assets;
  audio: AudioManager;
  world: World;
  bus: EventBus;
  input: Input;
  player: Player;
  enemies: Enemy[];
  projectiles: Projectile[];
  time: number;
  floatText(pos: THREE.Vector3, text: string, kind: FloatKind): void;
  shake(amount: number): void;
  /** Kamera yönü (yatay açı), saldırı/büyü nişanı için. */
  aimYaw(): number;
  /** Nişangahın baktığı dünya noktası. */
  aimPoint(out: THREE.Vector3): THREE.Vector3;
  spawnEnemy(type: string, x: number, z: number, opts?: { dormant?: boolean; campId?: string }): Enemy;
  /** Sahnede hep duran ışık havuzundan bir ışık al (yoksa null). */
  borrowLight(color: THREE.ColorRepresentation): THREE.PointLight | null;
  returnLight(light: THREE.PointLight): void;
}
