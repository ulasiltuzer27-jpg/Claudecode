import lootJson from '../data/loot.json';
import { Random } from '../core/Random';

interface LootTable { gold: [number, number]; rolls: number; table: [string, number][]; guaranteed?: string[] }
const TABLES = lootJson as unknown as Record<string, LootTable>;

export interface LootDrop { gold: number; items: { id: string; qty: number }[] }

/** Ağırlıklı ganimet atışı. "nothing" boş sonuç. */
export function rollLoot(tableId: string, r: Random): LootDrop {
  const t = TABLES[tableId];
  if (!t) throw new Error(`Bilinmeyen ganimet tablosu: ${tableId}`);
  const gold = r.int(t.gold[0], t.gold[1]);
  const items = new Map<string, number>();
  for (const g of t.guaranteed ?? []) items.set(g, (items.get(g) ?? 0) + 1);
  const total = t.table.reduce((s, [, w]) => s + w, 0);
  for (let k = 0; k < t.rolls; k++) {
    let x = r.next() * total;
    for (const [id, w] of t.table) {
      x -= w;
      if (x <= 0) {
        if (id !== 'nothing' && !(t.guaranteed ?? []).includes(id)) items.set(id, (items.get(id) ?? 0) + 1);
        break;
      }
    }
  }
  return { gold, items: [...items].map(([id, qty]) => ({ id, qty })) };
}

export const lootTables = (): string[] => Object.keys(TABLES);
