/**
 * Hasar hesabı. Saf fonksiyonlar: rastgelelik dışarıdan verilir ki testler
 * belirleyici olsun.
 */
export interface DamageInput {
  base: number;
  multiplier?: number;
  armor: number;
  /** 0..1 rastgele sayılar */
  rollVariance: number;
  rollCrit: number;
  critChance?: number;
  /** Savunan blokluyor ve saldırı önden geliyorsa blok oranı (0..1). */
  blocked?: number;
}

export interface DamageResult { amount: number; crit: boolean; blocked: boolean }

/** Zırh azaltımı: armor / (armor + 40). 10 zırh -> %20, 40 zırh -> %50. Hiçbir zaman %75'i geçmez. */
export function armorReduction(armor: number): number {
  if (armor <= 0) return 0;
  return Math.min(0.75, armor / (armor + 40));
}

export function computeDamage(d: DamageInput): DamageResult {
  const variance = 0.9 + d.rollVariance * 0.2;
  const crit = d.rollCrit < (d.critChance ?? 0.1);
  let amount = d.base * (d.multiplier ?? 1) * variance * (crit ? 1.75 : 1);
  amount *= 1 - armorReduction(d.armor);
  const blocked = (d.blocked ?? 0) > 0;
  if (blocked) amount *= 1 - (d.blocked ?? 0);
  return { amount: Math.max(1, Math.round(amount)), crit, blocked };
}

/**
 * Vuruş yayı: saldıranın baktığı yöne göre hedef `halfAngle` içinde ve
 * `range + targetRadius` mesafede mi. XZ düzleminde.
 */
export function inArc(
  ax: number, az: number, facing: number,
  tx: number, tz: number, range: number, halfAngle: number, targetRadius = 0,
): boolean {
  const dx = tx - ax;
  const dz = tz - az;
  const d = Math.hypot(dx, dz);
  if (d > range + targetRadius) return false;
  if (d < 0.6) return true; // iç içe: açıya bakma
  const ang = Math.atan2(dx, dz);
  let diff = ang - facing;
  while (diff > Math.PI) diff -= Math.PI * 2;
  while (diff < -Math.PI) diff += Math.PI * 2;
  return Math.abs(diff) <= halfAngle;
}

/** Saldırı önden mi geliyor (blok için): savunanın baktığı yön ±75°. */
export function fromFront(defFacing: number, dx: number, dz: number): boolean {
  return inArc(0, 0, defFacing, dx, dz, Infinity, (75 * Math.PI) / 180);
}

export const COMBO = {
  oneHanded: ['1H_Melee_Attack_Slice_Diagonal', '1H_Melee_Attack_Slice_Horizontal', '1H_Melee_Attack_Chop'],
  twoHanded: ['2H_Melee_Attack_Slice', '2H_Melee_Attack_Chop', '2H_Melee_Attack_Spin'],
  multipliers: [1, 1.15, 1.6],
  /** Klip oranları: vuruş anı, sonraki vuruşun kuyruğa alınabileceği an, bitiş. */
  hitAt: 0.38,
  queueFrom: 0.3,
  endAt: 0.78,
};
