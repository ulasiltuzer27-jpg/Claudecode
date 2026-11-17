import * as THREE from 'three';
import { clamp, lerp, SimplexNoise, smoothstep } from './Noise';
import {
  CAMPS, distToPaths, LAKE, PATH_WIDTH, PLAY_RADIUS, RUINS, VILLAGE, WATER_LEVEL, WORLD_HALF, dist2D,
} from './Layout';

export type Surface = 'grass' | 'dirt' | 'stone' | 'sand' | 'wood';

/**
 * Yükseklik haritası tabanlı arazi.
 *
 * Görünen mesh ile fizik AYNI üçgenlerden okunur: `heightAt` analitik
 * fonksiyonu değil, ızgaranın üçgenlerini enterpole eder. Aksi halde
 * karakterin ayağı eğimli yerlerde zemine gömülür ya da havada kalır
 * (ızgara aralığı 2.6 birim; fark 0.5 birimi bulabiliyor).
 */
export class Terrain {
  readonly size = WORLD_HALF * 2;
  readonly segments: number;
  readonly step: number;
  private heights: Float32Array;
  private noise: SimplexNoise;
  private detail: SimplexNoise;
  mesh!: THREE.Mesh;

  constructor(seed: number, segments = 200) {
    this.segments = segments;
    this.step = this.size / segments;
    this.noise = new SimplexNoise(seed);
    this.detail = new SimplexNoise(seed + 17);
    const n = segments + 1;
    this.heights = new Float32Array(n * n);
    for (let j = 0; j < n; j++) {
      for (let i = 0; i < n; i++) {
        const x = -WORLD_HALF + i * this.step;
        const z = -WORLD_HALF + j * this.step;
        this.heights[j * n + i] = this.rawHeight(x, z);
      }
    }
  }

  /** Analitik yükseklik (yalnızca ızgarayı üretirken kullanılır). */
  rawHeight(x: number, z: number): number {
    const n = this.noise;
    let h = 4 + n.fbm(x * 0.0055, z * 0.0055, 5) * 11 + this.detail.noise(x * 0.03, z * 0.03) * 0.8;

    // Kenarlarda dağlar: oyun alanını doğal bir duvarla kapatır.
    const r = Math.hypot(x, z);
    const ridge = 1 - Math.abs(n.noise(x * 0.012, z * 0.012));
    h += smoothstep(PLAY_RADIUS - 30, PLAY_RADIUS + 35, r) * (35 + ridge * 30);

    // Göl çanağı
    const dl = dist2D(x, z, LAKE.x, LAKE.z);
    h = lerp(h, WATER_LEVEL - 4, 1 - smoothstep(LAKE.r * 0.55, LAKE.r * 1.25, dl));

    // Köy ve harabe düzlükleri
    const dv = dist2D(x, z, VILLAGE.x, VILLAGE.z);
    h = lerp(h, VILLAGE.height, 1 - smoothstep(VILLAGE.r * 0.75, VILLAGE.r * 1.35, dv));
    const dr = dist2D(x, z, RUINS.x, RUINS.z);
    h = lerp(h, RUINS.height, 1 - smoothstep(RUINS.r * 0.8, RUINS.r * 1.4, dr));

    // Yollar hafifçe düzleşsin (uçurumdan geçen yol olmasın) ve suya girmesin.
    const dp = distToPaths(x, z);
    if (dp < PATH_WIDTH * 3) {
      const w = 1 - smoothstep(PATH_WIDTH, PATH_WIDTH * 3, dp);
      const smooth = 4 + n.fbm(x * 0.0055, z * 0.0055, 2) * 9;
      h = lerp(h, h * 0.5 + smooth * 0.5, w * 0.6);
      h = Math.max(h, lerp(h, WATER_LEVEL + 1.4, w));
    }
    // Kamplar kuru ve düz bir zeminde olsun (ilk sürümde doğu kampı bir
    // gölcüğün ortasına düşmüştü; iskeletler suyun içinde dikiliyordu).
    for (const c of CAMPS) {
      const dc = dist2D(x, z, c.x, c.z);
      if (dc > c.r * 1.6) continue;
      const w = 1 - smoothstep(c.r * 0.9, c.r * 1.6, dc);
      h = lerp(h, Math.max(h, WATER_LEVEL + 2.2), w);
    }
    return h;
  }

  /** Mesh üçgenleriyle birebir aynı yükseklik. */
  heightAt(x: number, z: number): number {
    const n = this.segments + 1;
    const fx = clamp((x + WORLD_HALF) / this.step, 0, this.segments - 1e-4);
    const fz = clamp((z + WORLD_HALF) / this.step, 0, this.segments - 1e-4);
    const i = Math.floor(fx);
    const j = Math.floor(fz);
    const u = fx - i;
    const v = fz - j;
    const h00 = this.heights[j * n + i];
    const h10 = this.heights[j * n + i + 1];
    const h01 = this.heights[(j + 1) * n + i];
    const h11 = this.heights[(j + 1) * n + i + 1];
    // Üçgen bölünmesi buildMesh() ile aynı: (00,01,10) ve (10,01,11)
    if (u + v <= 1) return h00 + (h10 - h00) * u + (h01 - h00) * v;
    return h11 + (h01 - h11) * (1 - u) + (h10 - h11) * (1 - v);
  }

  normalAt(x: number, z: number, out = new THREE.Vector3()): THREE.Vector3 {
    const e = 0.6;
    const hx = this.heightAt(x + e, z) - this.heightAt(x - e, z);
    const hz = this.heightAt(x, z + e) - this.heightAt(x, z - e);
    return out.set(-hx, 2 * e, -hz).normalize();
  }

  slopeAt(x: number, z: number): number {
    return 1 - this.normalAt(x, z).y;
  }

  surfaceAt(x: number, z: number): Surface {
    const h = this.heightAt(x, z);
    if (h < WATER_LEVEL + 0.9) return 'sand';
    if (dist2D(x, z, RUINS.x, RUINS.z) < RUINS.r * 0.95) return 'stone';
    if (distToPaths(x, z) < PATH_WIDTH) return 'dirt';
    if (dist2D(x, z, VILLAGE.x, VILLAGE.z) < 11) return 'stone';
    if (this.slopeAt(x, z) > 0.32 || h > 26) return 'stone';
    return 'grass';
  }

  /**
   * Bir noktanın rengi: hem mesh vertex rengi hem minimap.
   * Değerler sRGB olarak yazılır ve doğrusal uzaya çevrilir; three.js
   * vertex renklerini doğrusal kabul ediyor. (Çevrilmezse çimen beyaza
   * kaçıyor: 0.33 doğrusal ≈ 0.61 sRGB.)
   */
  colorAt(x: number, z: number, out = new THREE.Color()): THREE.Color {
    return this.srgbColorAt(x, z, out).convertSRGBToLinear();
  }

  /** sRGB renk (minimap canvas'ı için doğrudan kullanılabilir). */
  srgbColorAt(x: number, z: number, out = new THREE.Color()): THREE.Color {
    const s = this.surfaceAt(x, z);
    const tint = this.detail.noise(x * 0.05, z * 0.05);
    const meadow = this.noise.noise(x * 0.008 + 40, z * 0.008 - 40);
    switch (s) {
      case 'sand': return out.setRGB(0.83, 0.76, 0.55);
      case 'dirt': return out.setRGB(0.55 + tint * 0.04, 0.42 + tint * 0.03, 0.27);
      case 'stone': {
        const g = 0.5 + tint * 0.06;
        return out.setRGB(g, g * 0.98, g * 0.94);
      }
      default: {
        // Çimen: iki ton arası noise ile geçiş, yer yer sarımsı çayır.
        const a = new THREE.Color(0.33, 0.58, 0.24);
        const b = new THREE.Color(0.45, 0.66, 0.28);
        const c = new THREE.Color(0.62, 0.66, 0.30);
        out.copy(a).lerp(b, clamp(0.5 + tint * 0.8, 0, 1));
        if (meadow > 0.35) out.lerp(c, smoothstep(0.35, 0.7, meadow) * 0.6);
        return out;
      }
    }
  }

  buildMesh(): THREE.Mesh {
    const n = this.segments + 1;
    const positions = new Float32Array(n * n * 3);
    const colors = new Float32Array(n * n * 3);
    const c = new THREE.Color();
    for (let j = 0; j < n; j++) {
      for (let i = 0; i < n; i++) {
        const k = j * n + i;
        const x = -WORLD_HALF + i * this.step;
        const z = -WORLD_HALF + j * this.step;
        positions[k * 3] = x;
        positions[k * 3 + 1] = this.heights[k];
        positions[k * 3 + 2] = z;
        this.colorAt(x, z, c);
        colors[k * 3] = c.r;
        colors[k * 3 + 1] = c.g;
        colors[k * 3 + 2] = c.b;
      }
    }
    const index: number[] = [];
    for (let j = 0; j < this.segments; j++) {
      for (let i = 0; i < this.segments; i++) {
        const a = j * n + i; // 00
        const b = j * n + i + 1; // 10
        const d = (j + 1) * n + i; // 01
        const e = (j + 1) * n + i + 1; // 11
        // Saat yönünün tersine (yukarıdan bakınca) -> normal +y
        index.push(a, d, b, b, d, e);
      }
    }
    let geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.setIndex(index);
    // Düz gölgeli low-poly görünüm: üçgenleri ayır, yüz normali kullan.
    geo = geo.toNonIndexed();
    geo.computeVertexNormals();
    const mat = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.95, metalness: 0 });
    this.mesh = new THREE.Mesh(geo, mat);
    this.mesh.receiveShadow = true;
    this.mesh.name = 'terrain';
    return this.mesh;
  }
}
