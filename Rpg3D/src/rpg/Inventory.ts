import { item, maxStack, slotFor, type EquipSlot } from './Items';

export interface Stack { id: string; qty: number }
export interface Equipment { mainhand: string | null; offhand: string | null; ring: string | null }

export const INVENTORY_SIZE = 24;

/**
 * Envanter: sabit sayıda yuva, yığınlanabilir eşyalar, ekipman yuvaları ve altın.
 * Değişiklik bildirimini `onChange` ile yapar; UI ve görev sistemi dinler.
 */
export class Inventory {
  slots: (Stack | null)[] = new Array(INVENTORY_SIZE).fill(null);
  equipment: Equipment = { mainhand: null, offhand: null, ring: null };
  gold = 0;
  onChange: (() => void) | null = null;

  private changed() {
    this.onChange?.();
  }

  count(id: string): number {
    let n = 0;
    for (const s of this.slots) if (s?.id === id) n += s.qty;
    for (const v of Object.values(this.equipment)) if (v === id) n++;
    return n;
  }

  /** Ekle; sığmayan miktarı döndür (0 = hepsi girdi). */
  add(id: string, qty = 1): number {
    item(id); // bilinmeyen eşyada patla
    const cap = maxStack(id);
    let left = qty;
    if (cap > 1) {
      for (const s of this.slots) {
        if (left <= 0) break;
        if (s && s.id === id && s.qty < cap) {
          const put = Math.min(cap - s.qty, left);
          s.qty += put;
          left -= put;
        }
      }
    }
    for (let i = 0; i < this.slots.length && left > 0; i++) {
      if (this.slots[i]) continue;
      const put = Math.min(cap, left);
      this.slots[i] = { id, qty: put };
      left -= put;
    }
    if (left !== qty) this.changed();
    return left;
  }

  /** Kaldır; gerçekten kaldırılan miktarı döndür. Sondaki yığınlardan başlar. */
  remove(id: string, qty = 1): number {
    let left = qty;
    for (let i = this.slots.length - 1; i >= 0 && left > 0; i--) {
      const s = this.slots[i];
      if (!s || s.id !== id) continue;
      const take = Math.min(s.qty, left);
      s.qty -= take;
      left -= take;
      if (s.qty === 0) this.slots[i] = null;
    }
    if (left !== qty) this.changed();
    return qty - left;
  }

  freeSlots(): number {
    return this.slots.filter((s) => !s).length;
  }

  /**
   * Yuvadaki eşyayı kuşan. Eski ekipman envantere döner. İki elli silah
   * kalkanı da çıkarır. Döner: başarılı mı.
   */
  equipFromSlot(index: number): boolean {
    const s = this.slots[index];
    if (!s) return false;
    const def = item(s.id);
    const slot = slotFor(def);
    if (!slot) return false;
    // Kalkan takılırken iki elli silah varsa silahı çıkarmak gerekir.
    const needsFree = (slot === 'mainhand' && def.twoHanded && this.equipment.offhand ? 1 : 0)
      + (slot === 'offhand' && this.equipment.mainhand && item(this.equipment.mainhand).twoHanded ? 1 : 0);
    const prev = this.equipment[slot];
    this.slots[index] = null;
    if (this.freeSlots() < needsFree) {
      this.slots[index] = s;
      return false;
    }
    this.equipment[slot] = s.id;
    if (prev) this.slots[index] = { id: prev, qty: 1 };
    if (slot === 'mainhand' && def.twoHanded && this.equipment.offhand) {
      this.add(this.equipment.offhand, 1);
      this.equipment.offhand = null;
    }
    if (slot === 'offhand' && this.equipment.mainhand && item(this.equipment.mainhand).twoHanded) {
      this.add(this.equipment.mainhand, 1);
      this.equipment.mainhand = null;
    }
    this.changed();
    return true;
  }

  unequip(slot: EquipSlot): boolean {
    const id = this.equipment[slot];
    if (!id || this.freeSlots() === 0) return false;
    this.equipment[slot] = null;
    this.add(id, 1);
    this.changed();
    return true;
  }

  addGold(n: number): void {
    this.gold = Math.max(0, this.gold + n);
    this.changed();
  }

  serialize() {
    return { slots: this.slots, equipment: this.equipment, gold: this.gold };
  }

  load(data: ReturnType<Inventory['serialize']>): void {
    this.slots = data.slots.slice(0, INVENTORY_SIZE);
    while (this.slots.length < INVENTORY_SIZE) this.slots.push(null);
    this.equipment = { ...data.equipment };
    this.gold = data.gold;
    this.changed();
  }
}
