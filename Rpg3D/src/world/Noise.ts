import { Random } from '../core/Random';

/**
 * 2D simplex noise (Stefan Gustavson'ın kamu malı algoritması), seed'li
 * permütasyon tablosuyla. Çıktı yaklaşık [-1, 1].
 */
export class SimplexNoise {
  private perm = new Uint8Array(512);
  private static readonly grad = [
    [1, 1], [-1, 1], [1, -1], [-1, -1], [1, 0], [-1, 0], [0, 1], [0, -1],
  ];

  constructor(seed: number) {
    const r = new Random(seed);
    const p = new Uint8Array(256);
    for (let i = 0; i < 256; i++) p[i] = i;
    for (let i = 255; i > 0; i--) {
      const j = Math.floor(r.next() * (i + 1));
      [p[i], p[j]] = [p[j], p[i]];
    }
    for (let i = 0; i < 512; i++) this.perm[i] = p[i & 255];
  }

  noise(xin: number, yin: number): number {
    const F2 = 0.5 * (Math.sqrt(3) - 1);
    const G2 = (3 - Math.sqrt(3)) / 6;
    const s = (xin + yin) * F2;
    const i = Math.floor(xin + s);
    const j = Math.floor(yin + s);
    const t = (i + j) * G2;
    const x0 = xin - (i - t);
    const y0 = yin - (j - t);
    const i1 = x0 > y0 ? 1 : 0;
    const j1 = x0 > y0 ? 0 : 1;
    const x1 = x0 - i1 + G2;
    const y1 = y0 - j1 + G2;
    const x2 = x0 - 1 + 2 * G2;
    const y2 = y0 - 1 + 2 * G2;
    const ii = i & 255;
    const jj = j & 255;
    const g = SimplexNoise.grad;
    const corner = (gi: number, x: number, y: number) => {
      let tt = 0.5 - x * x - y * y;
      if (tt < 0) return 0;
      tt *= tt;
      const gr = g[gi & 7];
      return tt * tt * (gr[0] * x + gr[1] * y);
    };
    const n0 = corner(this.perm[ii + this.perm[jj]], x0, y0);
    const n1 = corner(this.perm[ii + i1 + this.perm[jj + j1]], x1, y1);
    const n2 = corner(this.perm[ii + 1 + this.perm[jj + 1]], x2, y2);
    return 70 * (n0 + n1 + n2);
  }

  /** Fraktal (fBm): katmanlı noise, doğal görünümlü arazi için. */
  fbm(x: number, y: number, octaves = 4, lacunarity = 2, gain = 0.5): number {
    let amp = 1;
    let freq = 1;
    let sum = 0;
    let norm = 0;
    for (let o = 0; o < octaves; o++) {
      sum += amp * this.noise(x * freq, y * freq);
      norm += amp;
      amp *= gain;
      freq *= lacunarity;
    }
    return sum / norm;
  }
}

export const smoothstep = (e0: number, e1: number, x: number): number => {
  const t = Math.min(1, Math.max(0, (x - e0) / (e1 - e0)));
  return t * t * (3 - 2 * t);
};
export const lerp = (a: number, b: number, t: number): number => a + (b - a) * t;
export const clamp = (v: number, lo: number, hi: number): number => (v < lo ? lo : v > hi ? hi : v);
