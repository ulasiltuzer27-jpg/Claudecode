/**
 * Dünyanın elle tasarlanmış iskeleti: bölgeler, yollar, kamplar.
 * Arazi üreteci, bitki saçıcı, düşman yerleştirici ve müzik bölgeleri hepsi
 * buradan okur; bir kampı taşımak tek satır.
 *
 * Eksenler: +x doğu, -z kuzey. Birim: KayKit birimi (karakter ≈ 2.4).
 */
export interface P2 { x: number; z: number }

export const WORLD_HALF = 260;
/** Oyuncu bu yarıçapın dışına çıkamaz (dağlar başlar). */
export const PLAY_RADIUS = 205;
export const WATER_LEVEL = 0;

export const VILLAGE = { x: 0, z: 0, r: 42, height: 3 };
export const LAKE = { x: -95, z: 35, r: 34 };
export const RUINS = { x: 115, z: -125, r: 34, height: 9 };

export const PLAYER_START: P2 = { x: 3, z: 14 };

/** İskelet kampları. `dormant`: yerde yatan, yaklaşınca kalkan iskeletler. */
export interface CampDef {
  id: string;
  x: number;
  z: number;
  r: number;
  enemies: { type: string; count: number; dormant?: boolean }[];
  /** Görev sandığı vb. */
  chest?: string;
}

export const CAMPS: CampDef[] = [
  {
    id: 'east_field', x: 72, z: 18, r: 12,
    enemies: [{ type: 'skeleton_minion', count: 4, dormant: true }],
  },
  {
    id: 'south_meadow', x: -20, z: 92, r: 12,
    enemies: [{ type: 'skeleton_minion', count: 3 }, { type: 'skeleton_rogue', count: 1 }],
  },
  {
    id: 'north_wood', x: -38, z: -98, r: 14,
    enemies: [
      { type: 'skeleton_minion', count: 2 },
      { type: 'skeleton_warrior', count: 1 },
      { type: 'skeleton_mage', count: 2 },
    ],
    chest: 'amulet_chest',
  },
  {
    id: 'ruins_gate', x: 92, z: -92, r: 10,
    enemies: [{ type: 'skeleton_warrior', count: 2 }, { type: 'skeleton_mage', count: 1 }],
  },
  {
    id: 'ruins_inner', x: 118, z: -130, r: 12,
    enemies: [{ type: 'bone_lord', count: 1 }, { type: 'skeleton_minion', count: 2, dormant: true }],
  },
];

/** Yollar: kalınlık boyunca toprak yüzey (renk + ayak sesi). */
export const PATHS: P2[][] = [
  // köy -> doğu kampı -> harabe kapısı -> harabe
  [{ x: 8, z: 0 }, { x: 40, z: 6 }, { x: 64, z: 16 }, { x: 80, z: -20 }, { x: 88, z: -70 }, { x: 98, z: -100 }, { x: 112, z: -118 }],
  // köy -> kuzey ormanı
  [{ x: -2, z: -10 }, { x: -6, z: -45 }, { x: -24, z: -72 }, { x: -36, z: -92 }],
  // köy -> güney çayırı
  [{ x: 0, z: 12 }, { x: -6, z: 50 }, { x: -18, z: 84 }],
  // köy -> göl kıyısı
  [{ x: -12, z: 4 }, { x: -40, z: 14 }, { x: -62, z: 26 }],
];
export const PATH_WIDTH = 2.6;

export const dist2D = (ax: number, az: number, bx: number, bz: number): number => Math.hypot(ax - bx, az - bz);

/** Bir noktanın en yakın yol parçasına uzaklığı. */
export function distToPaths(x: number, z: number): number {
  let best = Infinity;
  for (const path of PATHS) {
    for (let i = 0; i < path.length - 1; i++) {
      const a = path[i];
      const b = path[i + 1];
      const dx = b.x - a.x;
      const dz = b.z - a.z;
      const len2 = dx * dx + dz * dz;
      const t = Math.max(0, Math.min(1, ((x - a.x) * dx + (z - a.z) * dz) / len2));
      const d = Math.hypot(x - (a.x + dx * t), z - (a.z + dz * t));
      if (d < best) best = d;
    }
  }
  return best;
}

export type Zone = 'village' | 'ruins' | 'lake' | 'wild';

export function zoneAt(x: number, z: number): Zone {
  if (dist2D(x, z, VILLAGE.x, VILLAGE.z) < VILLAGE.r + 8) return 'village';
  if (dist2D(x, z, RUINS.x, RUINS.z) < RUINS.r + 16) return 'ruins';
  if (dist2D(x, z, LAKE.x, LAKE.z) < LAKE.r + 10) return 'lake';
  return 'wild';
}
