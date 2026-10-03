// Adanin yukseklik haritasi. Three.js'e bagimli DEGIL: ayni kod hem oyunda
// hem `tools/verify.mjs` icinde node'da calisiyor, boylece dogrulama araci
// oyunun gordugu araziyi birebir goruyor.

import { createNoise2D, fbm, smoothstep, lerp, clamp } from './noise.js';

export const WORLD_SIZE = 512;          // metre, -256..256
export const CELLS = 256;               // 2 m hucre
export const CELL = WORLD_SIZE / CELLS;
export const VERTS = CELLS + 1;
export const SEA_LEVEL = 0;
export const BOUNDARY_RADIUS = 232;     // otesinde akinti oyuncuyu geri iter

// Elle tasarlanmis yer isaretleri. Bolgeler, yerlesim ve gorevler bu
// koordinatlara bagli; bir seyi tasimak icin tek yer burasi.
export const LAYOUT = {
  peak: { x: 6, z: -112 },
  peakMesa1: 31,          // 1 tuy gerektiren terasin yaricapi
  peakMesa2: 17,          // 4 tuy gerektiren zirvenin yaricapi
  peakCliff1: 2.7,        // m
  peakCliff2: 6.8,        // m
  windy: { x: 104, z: -58, r: 32, cliff: 4.0 },   // 2 tuy
  hollow: { x: -86, z: -80, r: 40 },
  lake: { x: 30, z: 2, r: 23, level: 5.2 },
  village: { x: 8, z: 108, r: 40, h: 3.0 },
  meadow: { x: -58, z: 50 },
  forest: { x: -120, z: -6 },
  cove: { x: 136, z: 50 },
  inlet: { ax: 88, az: 164, bx: 86, bz: 84, w: 7.5 },
  islet: { x: -182, z: -108, r: 16 },
};

export const PATHS = [
  // koy -> cayir -> orman -> vadi
  [[8, 100], [-14, 86], [-40, 62], [-62, 46], [-92, 22], [-116, -4], [-110, -40], [-92, -66]],
  // koy -> gol -> zirve etegi
  [[14, 96], [22, 70], [26, 40], [40, 24]],
  [[30, -22], [24, -46], [14, -70], [8, -84]],
  // gol -> ruzgarli kayaliklar
  [[50, -10], [74, -30], [90, -42]],
  // koy -> kunduz / kopru
  [[24, 108], [52, 108], [80, 108]],
  // kopru -> koy (cove)
  [[104, 108], [120, 86], [132, 62]],
];

function distToSegment(px, pz, ax, az, bx, bz) {
  const dx = bx - ax;
  const dz = bz - az;
  const len2 = dx * dx + dz * dz;
  let t = len2 > 0 ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0;
  t = clamp(t, 0, 1);
  const cx = ax + dx * t - px;
  const cz = az + dz * t - pz;
  return Math.sqrt(cx * cx + cz * cz);
}

export function distToPaths(x, z) {
  let best = Infinity;
  for (const path of PATHS) {
    for (let i = 0; i < path.length - 1; i++) {
      const d = distToSegment(x, z, path[i][0], path[i][1], path[i + 1][0], path[i + 1][1]);
      if (d < best) best = d;
    }
  }
  return best;
}

function plateau(d, r, w = 1.1) {
  return 1 - smoothstep(r - w, r + w, d);
}

export class Terrain {
  constructor(seed = 1337) {
    this.seed = seed;
    this.n1 = createNoise2D(seed);
    this.n2 = createNoise2D(seed + 101);
    this.n3 = createNoise2D(seed + 202);
    this.heights = new Float32Array(VERTS * VERTS);
    this.pathDist = new Float32Array(VERTS * VERTS);
    for (let iz = 0; iz < VERTS; iz++) {
      for (let ix = 0; ix < VERTS; ix++) {
        const x = -WORLD_SIZE / 2 + ix * CELL;
        const z = -WORLD_SIZE / 2 + iz * CELL;
        this.heights[iz * VERTS + ix] = this.rawHeight(x, z);
        this.pathDist[iz * VERTS + ix] = distToPaths(x, z);
      }
    }
  }

  coastRadius(x, z) {
    const a = Math.atan2(z, x);
    const c = Math.cos(a);
    const s = Math.sin(a);
    return 160 + 14 * this.n1(c * 1.4 + 7, s * 1.4 + 3) + 7 * this.n2(c * 3.1, s * 3.1);
  }

  // Gurultu + elle sekillendirilmis ozellikler. Bir kez hesaplanip izgaraya
  // yaziliyor; oyun icindeki tum sorgular izgaradan enterpole ediyor.
  rawHeight(x, z) {
    const L = LAYOUT;
    const r = Math.hypot(x, z);
    const R = this.coastRadius(x, z);
    const land = smoothstep(R + 26, R - 30, r);
    let h = lerp(-9, 5.5, land);

    // Zirve ve teraslarin etkisi: plato ustleri duz kalsin diye tepeler bastiriliyor.
    const dP = Math.hypot(x - L.peak.x, z - L.peak.z);
    const aP = Math.atan2(z - L.peak.z, x - L.peak.x);
    const peakCalm = 1 - smoothstep(18, 46, dP);
    const dW = Math.hypot(x - L.windy.x, z - L.windy.z);
    const windyCalm = 1 - smoothstep(L.windy.r - 6, L.windy.r + 10, dW);
    const dV = Math.hypot(x - L.village.x, z - L.village.z);
    const villageCalm = 1 - smoothstep(L.village.r - 14, L.village.r + 6, dV);

    const calm = Math.max(peakCalm, windyCalm, villageCalm);
    const hills = fbm(this.n1, x * 0.011, z * 0.011, 4) * 5.5 + fbm(this.n2, x * 0.045, z * 0.045, 3) * 0.7;
    h += hills * land * land * (1 - calm);

    // Fener zirvesi: genis tepe + iki keskin teras
    const wobble1 = 2.2 * this.n3(Math.cos(aP) * 2 + 11, Math.sin(aP) * 2 + 5);
    const wobble2 = 1.4 * this.n3(Math.cos(aP) * 2.5 + 31, Math.sin(aP) * 2.5 + 17);
    h += 24 * Math.exp(-((dP / 64) ** 2)) * land;
    h += L.peakCliff1 * plateau(dP, L.peakMesa1 + wobble1);
    h += L.peakCliff2 * plateau(dP, L.peakMesa2 + wobble2);

    // Ruzgarli Kayaliklar platosu
    const aW = Math.atan2(z - L.windy.z, x - L.windy.x);
    const wobbleW = 2.5 * this.n3(Math.cos(aW) * 2 + 51, Math.sin(aW) * 2 + 9);
    h += 8 * Math.exp(-((dW / 52) ** 2)) * land;
    h += L.windy.cliff * plateau(dW, L.windy.r + wobbleW);

    // Mantar Vadisi cukuru
    const dH = Math.hypot(x - L.hollow.x, z - L.hollow.z);
    h -= 3.6 * Math.exp(-((dH / L.hollow.r) ** 2)) * land;

    // Liman Koyu duzlugu
    h = lerp(h, L.village.h + fbm(this.n2, x * 0.05, z * 0.05, 2) * 0.25, villageCalm * land);

    // Kristal Gol: canak + kenar seti + ortada minik ada
    const dL = Math.hypot(x - L.lake.x, z - L.lake.z);
    if (dL < L.lake.r + 16) {
      const rim = L.lake.level + 0.55 + Math.max(0, dL - L.lake.r) * 0.08;
      const bowl = L.lake.level - 2.8 * (1 - clamp(dL / L.lake.r, 0, 1) ** 2) - 0.25;
      const inner = smoothstep(L.lake.r + 1.5, L.lake.r - 2.5, dL);
      const rimBand = smoothstep(L.lake.r + 16, L.lake.r + 4, dL);
      h = lerp(h, Math.max(h, rim), rimBand * (1 - inner));
      h = lerp(h, bowl, inner);
      h += 3.4 * Math.exp(-((dL / 4.5) ** 2));
    }

    // Koy ile Batik Gemi Koyu arasindaki fiyort
    const I = L.inlet;
    const dI = distToSegment(x, z, I.ax, I.az, I.bx, I.bz);
    const inletT = smoothstep(I.w + 7, I.w - 2, dI) * smoothstep(I.bz - 6, I.bz + 10, z);
    h = Math.min(h, lerp(h, -3.5, inletT));

    // Gizli magaranin adacigi
    const dS = Math.hypot(x - L.islet.x, z - L.islet.z);
    h = Math.max(h, -9 + 15.5 * Math.exp(-((dS / L.islet.r) ** 2)));

    return h;
  }

  // Izgara ustunde ucgen enterpolasyonu. Rapier heightfield'i ile ayni
  // izgarayi kullaniyor; fark santimetre mertebesinde.
  height(x, z) {
    const fx = (x + WORLD_SIZE / 2) / CELL;
    const fz = (z + WORLD_SIZE / 2) / CELL;
    if (fx < 0 || fz < 0 || fx >= CELLS || fz >= CELLS) return -9;
    const ix = Math.floor(fx);
    const iz = Math.floor(fz);
    const tx = fx - ix;
    const tz = fz - iz;
    const H = this.heights;
    const h00 = H[iz * VERTS + ix];
    const h10 = H[iz * VERTS + ix + 1];
    const h01 = H[(iz + 1) * VERTS + ix];
    const h11 = H[(iz + 1) * VERTS + ix + 1];
    const a = lerp(h00, h10, tx);
    const b = lerp(h01, h11, tx);
    return lerp(a, b, tz);
  }

  normal(x, z, out = { x: 0, y: 1, z: 0 }) {
    const e = 1.0;
    const hl = this.height(x - e, z);
    const hr = this.height(x + e, z);
    const hd = this.height(x, z - e);
    const hu = this.height(x, z + e);
    let nx = hl - hr;
    let ny = 2 * e;
    let nz = hd - hu;
    const len = Math.hypot(nx, ny, nz);
    out.x = nx / len; out.y = ny / len; out.z = nz / len;
    return out;
  }

  pathDistance(x, z) {
    const fx = clamp(Math.round((x + WORLD_SIZE / 2) / CELL), 0, CELLS);
    const fz = clamp(Math.round((z + WORLD_SIZE / 2) / CELL), 0, CELLS);
    return this.pathDist[fz * VERTS + fx];
  }

  // Bu noktadaki su yuzeyi (gol icinde gol seviyesi, digerlerinde deniz).
  waterLevel(x, z) {
    const L = LAYOUT.lake;
    if (Math.hypot(x - L.x, z - L.z) < L.r + 3) return L.level;
    return SEA_LEVEL;
  }

  isLake(x, z) {
    const L = LAYOUT.lake;
    return Math.hypot(x - L.x, z - L.z) < L.r + 3;
  }
}
