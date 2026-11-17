import itemsJson from '../data/items.json';

export type ItemType = 'weapon' | 'shield' | 'ring' | 'consumable' | 'junk' | 'quest';
export type Rarity = 'common' | 'uncommon' | 'rare';

export interface ItemDef {
  id: string;
  name: string;
  type: ItemType;
  rarity: Rarity;
  value: number;
  desc: string;
  model?: string;
  damage?: number;
  speed?: number;
  twoHanded?: boolean;
  armor?: number;
  block?: number;
  damageBonus?: number;
  hpBonus?: number;
  heal?: number;
  mana?: number;
  stack?: number;
}

export const ITEMS: Record<string, ItemDef> = Object.fromEntries(
  Object.entries(itemsJson as Record<string, Omit<ItemDef, 'id'>>).map(([id, d]) => [id, { id, ...d } as ItemDef]),
);

export function item(id: string): ItemDef {
  const d = ITEMS[id];
  if (!d) throw new Error(`Bilinmeyen eşya: ${id}`);
  return d;
}

export const RARITY_COLOR: Record<Rarity, string> = {
  common: '#e8e2d0',
  uncommon: '#6fd36f',
  rare: '#5aa8ff',
};

export const maxStack = (id: string): number => item(id).stack ?? 1;

export type EquipSlot = 'mainhand' | 'offhand' | 'ring';

export function slotFor(def: ItemDef): EquipSlot | null {
  if (def.type === 'weapon') return 'mainhand';
  if (def.type === 'shield') return 'offhand';
  if (def.type === 'ring') return 'ring';
  return null;
}
