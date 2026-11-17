import * as THREE from 'three';
import { clone as cloneSkinned } from 'three/examples/jsm/utils/SkeletonUtils.js';
import type { Assets } from '../core/Assets';

export interface PlayOptions {
  fade?: number;
  loop?: boolean;
  timeScale?: number;
  /** Aynı klip zaten çalıyorsa baştan başlat. */
  restart?: boolean;
}

/**
 * Animasyonlu bir KayKit karakteri: model kopyası + AnimationMixer.
 *
 * Bütün karakterler aynı iskeleti kullandığı için klipler ortak
 * kütüphaneden (anims.glb) gelir ve her karakterin kendi mixer'ında
 * action'a dönüşür. `root` dünyada konumlanan ve döndürülen düğüm;
 * model onun altında.
 */
export class Character {
  readonly root = new THREE.Group();
  readonly model: THREE.Object3D;
  readonly mixer: THREE.AnimationMixer;
  private actions = new Map<string, THREE.AnimationAction>();
  current: THREE.AnimationAction | null = null;
  currentName = '';
  private slots: Record<'right' | 'left', THREE.Object3D | null> = { right: null, left: null };
  private held: Record<'right' | 'left', THREE.Object3D | null> = { right: null, left: null };
  /** Göğüs hizası (vuruş, büyü çıkışı, can barı için). */
  readonly chestHeight: number;
  readonly headHeight: number;

  constructor(private assets: Assets, modelId: string, scale = 1) {
    this.model = cloneSkinned(assets.gltf(modelId).scene);
    this.model.scale.setScalar(scale);
    this.root.add(this.model);
    this.root.name = modelId;
    this.mixer = new THREE.AnimationMixer(this.model);
    this.model.traverse((o) => {
      if (o.name === 'handslotr') this.slots.right = o;
      if (o.name === 'handslotl') this.slots.left = o;
      // İskeletli mesh'in sınır küresi bind pozundan hesaplanır; animasyonda
      // (koşu, yere yığılma) uzuvlar dışarı taşar ve karakter kamera kenarında
      // "kaybolur". Küreyi büyüt ama culling'i kapatma: 35 karakter x 10 mesh
      // her karede çizilirdi.
      const sm = o as THREE.SkinnedMesh;
      if (sm.isSkinnedMesh) {
        sm.computeBoundingSphere();
        sm.boundingSphere!.radius = Math.max(sm.boundingSphere!.radius * 1.6, 2.2);
      }
    });
    // Modelin elinde gelen hazır eşyaları (kılıç, kupa, büyü kitabı) gizle;
    // ne taşınacağına Character karar verir.
    for (const slot of [this.slots.right, this.slots.left]) {
      if (!slot) continue;
      for (const c of slot.children) c.visible = false;
    }
    this.headHeight = 2.35 * scale;
    this.chestHeight = 1.5 * scale;
  }

  get position(): THREE.Vector3 {
    return this.root.position;
  }

  /** Modelin kendi elinde gelen bir eşyayı göster (ör. büyücünün asası). */
  showBuiltin(slot: 'right' | 'left', nodeName: string): void {
    const s = this.slots[slot];
    if (!s) return;
    for (const c of s.children) if (c.name === nodeName) c.visible = true;
  }

  /** El yuvasına silah/kalkan modeli tak (null: boşalt). */
  equip(slot: 'right' | 'left', modelId: string | null): void {
    const s = this.slots[slot];
    if (!s) return;
    if (this.held[slot]) {
      s.remove(this.held[slot]!);
      this.held[slot] = null;
    }
    if (!modelId) return;
    const obj = this.assets.gltf(modelId).scene.clone(true);
    obj.traverse((o) => { o.castShadow = true; });
    s.add(obj);
    this.held[slot] = obj;
  }

  heldObject(slot: 'right' | 'left'): THREE.Object3D | null {
    return this.held[slot];
  }

  private action(name: string): THREE.AnimationAction | null {
    let a = this.actions.get(name);
    if (!a) {
      const clip = this.assets.clips.get(name);
      if (!clip) return null;
      a = this.mixer.clipAction(clip);
      this.actions.set(name, a);
    }
    return a;
  }

  clipDuration(name: string): number {
    return this.assets.clips.get(name)?.duration ?? 0;
  }

  /** Döngüsel ya da tek seferlik bir klibe yumuşak geçiş. */
  play(name: string, o: PlayOptions = {}): THREE.AnimationAction | null {
    const a = this.action(name);
    if (!a) return null;
    const fade = o.fade ?? 0.18;
    const loop = o.loop ?? true;
    if (this.current === a && !o.restart) {
      a.timeScale = o.timeScale ?? 1;
      return a;
    }
    a.reset();
    a.enabled = true;
    a.setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce, loop ? Infinity : 1);
    a.clampWhenFinished = !loop;
    a.timeScale = o.timeScale ?? 1;
    a.setEffectiveWeight(1);
    if (this.current && this.current !== a) {
      a.crossFadeFrom(this.current, fade, true);
    } else if (this.current === a) {
      a.fadeIn(Math.min(fade, 0.08));
    }
    a.play();
    this.current = a;
    this.currentName = name;
    return a;
  }

  /** Tek seferlik klibin ilerleme oranı (0..1); başka klip çalıyorsa 1. */
  progress(name = this.currentName): number {
    const a = this.actions.get(name);
    if (!a || this.current !== a) return 1;
    const d = a.getClip().duration;
    return d > 0 ? Math.min(1, a.time / d) : 1;
  }

  update(dt: number): void {
    this.mixer.update(dt);
  }

  /** Dünya uzayında el yuvasının konumu (büyü çıkış noktası vb.). */
  slotWorldPosition(slot: 'right' | 'left', out = new THREE.Vector3()): THREE.Vector3 {
    const s = this.slots[slot];
    if (!s) return out.copy(this.root.position).setY(this.root.position.y + this.chestHeight);
    return s.getWorldPosition(out);
  }

  dispose(): void {
    this.mixer.stopAllAction();
    this.root.removeFromParent();
  }
}

/** Açıyı [-π, π] aralığına sar. */
export function wrapAngle(a: number): number {
  while (a > Math.PI) a -= Math.PI * 2;
  while (a < -Math.PI) a += Math.PI * 2;
  return a;
}

/** Bir açıdan diğerine en kısa yoldan, en fazla `maxStep` kadar dön. */
export function turnTowards(current: number, target: number, maxStep: number): number {
  const d = wrapAngle(target - current);
  if (Math.abs(d) <= maxStep) return target;
  return current + Math.sign(d) * maxStep;
}
