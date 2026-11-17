import { item } from './Items';
import type { Equipment } from './Inventory';

/** Seviye eğrisi: bir sonraki seviyeye gereken XP. L1→2: 100, L5→6: ~560, L10→11: ~1580. */
export function xpToNext(level: number): number {
  return Math.round((100 * Math.pow(level, 1.5)) / 5) * 5;
}

export const MAX_LEVEL = 20;

export interface Derived {
  maxHp: number;
  maxMana: number;
  maxStamina: number;
  damage: number;
  attackSpeed: number;
  armor: number;
  block: number;
  spellPower: number;
}

/** Seviye + ekipmandan türetilen statlar. */
export function derive(level: number, eq: Equipment): Derived {
  const w = eq.mainhand ? item(eq.mainhand) : null;
  const s = eq.offhand ? item(eq.offhand) : null;
  const r = eq.ring ? item(eq.ring) : null;
  const L = level - 1;
  return {
    maxHp: 100 + L * 14 + (r?.hpBonus ?? 0),
    maxMana: 50 + L * 7,
    maxStamina: 100 + L * 4,
    damage: (w?.damage ?? 4) + L * 2 + (r?.damageBonus ?? 0),
    attackSpeed: w?.speed ?? 1,
    // İki elli silahla kalkan taşınamaz (UI de engelliyor); burada da yok say.
    armor: (w?.twoHanded ? 0 : s?.armor ?? 0) + L,
    block: w?.twoHanded ? 0.45 : s?.block ?? 0.35,
    spellPower: 22 + L * 4,
  };
}

/**
 * XP ekle; seviye atlamalarını döndür. Fazla XP sonraki seviyeye devreder,
 * tek seferde birden çok seviye atlanabilir (boss ödülü).
 */
export function addXp(state: { level: number; xp: number }, amount: number): number[] {
  const gained: number[] = [];
  state.xp += amount;
  while (state.level < MAX_LEVEL && state.xp >= xpToNext(state.level)) {
    state.xp -= xpToNext(state.level);
    state.level++;
    gained.push(state.level);
  }
  if (state.level >= MAX_LEVEL) state.xp = 0;
  return gained;
}
