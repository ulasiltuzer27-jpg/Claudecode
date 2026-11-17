import * as THREE from 'three';
import { GLTFLoader, type GLTF } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { mergeSkinnedParts } from '../world/StaticBatcher';

export interface ModelEntry { file: string; kind: 'anims' | 'character' | 'prop'; bytes: number; animations?: string[] }
export interface SoundEntry { files: string[]; volume: number; loop?: boolean }
export interface MusicEntry { file: string; duration: number }
export interface Manifest {
  models: Record<string, ModelEntry>;
  sounds: Record<string, SoundEntry>;
  music: Record<string, MusicEntry>;
}

/**
 * Bütün modelleri ve ses efektlerini oyun başlamadan yükler.
 * Müzik yüklenmez: HTMLAudioElement ile akış halinde çalınır (6 parçanın
 * çözülmüş PCM'i ~140 MB tutardı).
 */
export class Assets {
  readonly base = 'assets/';
  manifest!: Manifest;
  private models = new Map<string, GLTF>();
  clips = new Map<string, THREE.AnimationClip>();
  sounds = new Map<string, AudioBuffer[]>();
  /** Yüklenemeyen dosyalar (smoke test bunun boş olmasını bekler). */
  failures: string[] = [];

  async load(audioCtx: BaseAudioContext, onProgress: (frac: number, label: string) => void): Promise<void> {
    const res = await fetch(`${this.base}manifest.json`);
    if (!res.ok) throw new Error(`manifest.json yüklenemedi (HTTP ${res.status})`);
    this.manifest = (await res.json()) as Manifest;

    const modelIds = Object.keys(this.manifest.models);
    const soundFiles = Object.entries(this.manifest.sounds).flatMap(([id, s]) => s.files.map((f, i) => ({ id, f, i })));
    const total = modelIds.length + soundFiles.length;
    let done = 0;
    const tick = (label: string) => onProgress(++done / total, label);

    const loader = new GLTFLoader();
    const modelJobs = modelIds.map(async (id) => {
      try {
        const gltf = await loader.loadAsync(this.base + this.manifest.models[id].file);
        prepareMaterials(gltf.scene);
        if (this.manifest.models[id].kind === 'character') mergeSkinnedParts(gltf.scene);
        this.models.set(id, gltf);
        if (id === 'anims') for (const c of gltf.animations) this.clips.set(c.name, c);
      } catch (err) {
        this.failures.push(`${id}: ${(err as Error).message}`);
      }
      tick(id);
    });

    const soundJobs = soundFiles.map(async ({ id, f, i }) => {
      try {
        const r = await fetch(this.base + f);
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        const buf = await audioCtx.decodeAudioData(await r.arrayBuffer());
        let arr = this.sounds.get(id);
        if (!arr) this.sounds.set(id, (arr = []));
        arr[i] = buf;
      } catch (err) {
        this.failures.push(`${f}: ${(err as Error).message}`);
      }
      tick(f);
    });

    await Promise.all([...modelJobs, ...soundJobs]);
    for (const [id, arr] of this.sounds) this.sounds.set(id, arr.filter(Boolean));
  }

  gltf(id: string): GLTF {
    const g = this.models.get(id);
    if (!g) throw new Error(`Model yüklenmedi: ${id}`);
    return g;
  }

  has(id: string): boolean {
    return this.models.has(id);
  }

  clip(name: string): THREE.AnimationClip {
    const c = this.clips.get(name);
    if (!c) throw new Error(`Animasyon klibi yok: ${name}`);
    return c;
  }

  musicUrl(id: string): string | null {
    const m = this.manifest.music[id];
    return m ? this.base + m.file : null;
  }
}

/**
 * KayKit modelleri tek bir küçük palet dokusu kullanıyor (renk şeritleri).
 * Mipmap'ler komşu şeritleri birbirine karıştırıp uzaktaki modelleri
 * çamurlaştırıyor; mipmap'siz doğrusal filtre renkleri temiz tutuyor.
 * Gölgeler de burada açılıyor.
 */
export function prepareMaterials(root: THREE.Object3D): void {
  root.traverse((o) => {
    const mesh = o as THREE.Mesh;
    if (!mesh.isMesh) return;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    for (const m of mats) {
      const std = m as THREE.MeshStandardMaterial;
      if (std.map) {
        std.map.generateMipmaps = false;
        std.map.minFilter = THREE.LinearFilter;
        std.map.magFilter = THREE.LinearFilter;
        std.map.needsUpdate = true;
      }
      // Paketler metalness=0/roughness=1 değil; çevre haritasıyla birlikte
      // fazla parlak plastik görünüyorlar. Hafif mat bir taban.
      if ('roughness' in std) std.roughness = Math.max(std.roughness ?? 1, 0.75);
      if ('metalness' in std) std.metalness = Math.min(std.metalness ?? 0, 0.2);
    }
  });
}
