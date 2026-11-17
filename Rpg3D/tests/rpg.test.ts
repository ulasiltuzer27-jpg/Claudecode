import { describe, expect, it } from 'vitest';
import { Random } from '../src/core/Random';
import { armorReduction, computeDamage, fromFront, inArc } from '../src/rpg/Combat';
import { Inventory } from '../src/rpg/Inventory';
import { ITEMS } from '../src/rpg/Items';
import { lootTables, rollLoot } from '../src/rpg/Loot';
import { QuestLog, QUESTS } from '../src/rpg/Quests';
import { addXp, derive, MAX_LEVEL, xpToNext } from '../src/rpg/Stats';
import enemies from '../src/data/enemies.json';
import shop from '../src/data/shop.json';
import dialog from '../src/data/dialog.json';

describe('seviye', () => {
  it('eğri artan ve 5\'in katı', () => {
    for (let l = 1; l < MAX_LEVEL; l++) {
      expect(xpToNext(l + 1)).toBeGreaterThan(xpToNext(l));
      expect(xpToNext(l) % 5).toBe(0);
    }
    expect(xpToNext(1)).toBe(100);
  });
  it('fazla XP devreder, tek seferde birden çok seviye', () => {
    const s = { level: 1, xp: 0 };
    const gained = addXp(s, 100 + xpToNext(2) + 7);
    expect(gained).toEqual([2, 3]);
    expect(s).toEqual({ level: 3, xp: 7 });
  });
  it('azami seviyede durur', () => {
    const s = { level: MAX_LEVEL - 1, xp: 0 };
    addXp(s, 1e9);
    expect(s.level).toBe(MAX_LEVEL);
    expect(s.xp).toBe(0);
  });
  it('türetilmiş statlar ekipmanı yansıtır', () => {
    const bare = derive(1, { mainhand: null, offhand: null, ring: null });
    const eq = derive(1, { mainhand: 'iron_axe', offhand: 'badge_shield', ring: 'ring_vigor' });
    expect(eq.damage).toBe(14);
    expect(eq.armor).toBe(8);
    expect(eq.maxHp).toBe(bare.maxHp + 40);
    // İki elli silahla kalkan sayılmaz
    const two = derive(1, { mainhand: 'great_sword', offhand: 'badge_shield', ring: null });
    expect(two.armor).toBe(0);
  });
});

describe('hasar', () => {
  it('zırh azaltımı sınırlı', () => {
    expect(armorReduction(0)).toBe(0);
    expect(armorReduction(40)).toBeCloseTo(0.5);
    expect(armorReduction(1e6)).toBe(0.75);
  });
  it('kritik 1.75x, blok oranı uygulanır, en az 1', () => {
    const base = { base: 20, armor: 0, rollVariance: 0.5 };
    expect(computeDamage({ ...base, rollCrit: 0.99 }).amount).toBe(20);
    const crit = computeDamage({ ...base, rollCrit: 0 });
    expect(crit.crit).toBe(true);
    expect(crit.amount).toBe(35);
    const blocked = computeDamage({ ...base, rollCrit: 0.99, blocked: 0.75 });
    expect(blocked.blocked).toBe(true);
    expect(blocked.amount).toBe(5);
    expect(computeDamage({ base: 0.1, armor: 500, rollVariance: 0, rollCrit: 1 }).amount).toBe(1);
  });
  it('vuruş yayı: önde vurur, arkada vurmaz', () => {
    // facing 0 = +z
    expect(inArc(0, 0, 0, 0, 2, 2.5, Math.PI / 4)).toBe(true);
    expect(inArc(0, 0, 0, 0, -2, 2.5, Math.PI / 4)).toBe(false);
    expect(inArc(0, 0, 0, 2, 0, 2.5, Math.PI / 4)).toBe(false);
    expect(inArc(0, 0, Math.PI / 2, 2, 0, 2.5, Math.PI / 4)).toBe(true);
    expect(inArc(0, 0, 0, 0, 3, 2.5, Math.PI / 4, 0.6)).toBe(true); // hedef yarıçapı
  });
  it('blok yalnızca önden', () => {
    expect(fromFront(0, 0, 3)).toBe(true);
    expect(fromFront(0, 0, -3)).toBe(false);
  });
});

describe('envanter', () => {
  it('yığınlar ve taşanı döndürür', () => {
    const inv = new Inventory();
    expect(inv.add('health_potion', 25)).toBe(0);
    expect(inv.slots[0]).toEqual({ id: 'health_potion', qty: 20 });
    expect(inv.slots[1]).toEqual({ id: 'health_potion', qty: 5 });
    expect(inv.count('health_potion')).toBe(25);
    for (let i = 0; i < 22; i++) inv.add('rusty_sword');
    expect(inv.freeSlots()).toBe(0);
    expect(inv.add('dagger')).toBe(1);
    expect(inv.add('health_potion', 20)).toBe(5); // ikinci yığına 15 sığar
  });
  it('kaldırma sondan başlar, gerçek miktarı döndürür', () => {
    const inv = new Inventory();
    inv.add('bone_fragment', 3);
    expect(inv.remove('bone_fragment', 5)).toBe(3);
    expect(inv.count('bone_fragment')).toBe(0);
  });
  it('kuşanınca eski ekipman çantaya döner; iki elli silah kalkanı çıkarır', () => {
    const inv = new Inventory();
    inv.equipment = { mainhand: 'rusty_sword', offhand: 'round_shield', ring: null };
    inv.add('great_sword');
    expect(inv.equipFromSlot(0)).toBe(true);
    expect(inv.equipment.mainhand).toBe('great_sword');
    expect(inv.equipment.offhand).toBeNull();
    expect(inv.count('rusty_sword')).toBe(1);
    expect(inv.count('round_shield')).toBe(1);
    // Kalkan geri takılınca iki elli silah çıkar
    const idx = inv.slots.findIndex((s) => s?.id === 'round_shield');
    expect(inv.equipFromSlot(idx)).toBe(true);
    expect(inv.equipment.mainhand).toBeNull();
    expect(inv.equipment.offhand).toBe('round_shield');
  });
  it('kuşanılamayan eşya kuşanılmaz', () => {
    const inv = new Inventory();
    inv.add('health_potion');
    expect(inv.equipFromSlot(0)).toBe(false);
  });
  it('kayıt gidiş-dönüş', () => {
    const inv = new Inventory();
    inv.add('mana_potion', 3);
    inv.gold = 42;
    inv.equipment.ring = 'bone_ring';
    const copy = new Inventory();
    copy.load(JSON.parse(JSON.stringify(inv.serialize())));
    expect(copy.count('mana_potion')).toBe(3);
    expect(copy.gold).toBe(42);
    expect(copy.equipment.ring).toBe('bone_ring');
  });
});

describe('görevler', () => {
  it('zincir: kabul -> ilerleme -> teslim -> sonraki', () => {
    const q = new QuestLog();
    expect(q.get('q1_bones')).toBe('available');
    expect(q.get('q2_amulet')).toBe('locked');
    expect(q.accept('q1_bones')).toBe(true);
    for (let i = 0; i < 4; i++) q.onKill('skeleton_minion');
    q.onKill('bone_lord'); // önek eşleşmez
    expect(q.get('q1_bones')).toBe('active');
    q.onKill('skeleton_mage');
    expect(q.get('q1_bones')).toBe('complete');
    q.onKill('skeleton_rogue'); // fazladan sayılmaz
    expect(q.progress.get('q1_bones')).toEqual([5]);
    const reward = q.turnIn('q1_bones');
    expect(reward?.xp).toBe(160);
    expect(q.get('q1_bones')).toBe('done');
    expect(q.get('q2_amulet')).toBe('available');
    expect(q.turnIn('q1_bones')).toBeNull();
  });
  it('toplama görevi: eşya zaten çantadaysa kabulde sayılır, bırakılırsa geri döner', () => {
    const q = new QuestLog();
    q.status.set('q2_amulet', 'available');
    q.accept('q2_amulet', (id) => (id === 'amulet_elder' ? 1 : 0));
    expect(q.get('q2_amulet')).toBe('complete');
    q.onItemCount('amulet_elder', 0);
    expect(q.get('q2_amulet')).toBe('active');
  });
  it('forGiver önceliği: teslim > aktif > verilebilir', () => {
    const q = new QuestLog();
    expect(q.forGiver('elder')?.status).toBe('available');
    q.accept('q1_bones');
    expect(q.forGiver('elder')?.status).toBe('active');
    expect(q.forGiver('merchant')).toBeNull();
  });
  it('kayıt gidiş-dönüş', () => {
    const q = new QuestLog();
    q.accept('q1_bones');
    q.onKill('skeleton_minion');
    const copy = new QuestLog();
    copy.load(JSON.parse(JSON.stringify(q.serialize())));
    expect(copy.get('q1_bones')).toBe('active');
    expect(copy.progress.get('q1_bones')).toEqual([1]);
  });
});

describe('veri bütünlüğü', () => {
  it('görev ödülleri, ganimet tabloları, dükkân ve düşmanlar yalnızca var olan eşyalara başvurur', () => {
    for (const q of QUESTS) for (const [id] of q.reward.items) expect(ITEMS[id], id).toBeDefined();
    for (const id of (shop as Record<string, string[]>).merchant) expect(ITEMS[id], id).toBeDefined();
    for (const t of lootTables()) {
      const r = new Random(1);
      for (let i = 0; i < 200; i++) for (const it of rollLoot(t, r).items) expect(ITEMS[it.id], it.id).toBeDefined();
    }
    for (const e of Object.values(enemies)) expect(lootTables()).toContain(e.loot);
  });
  it('her görevin diyaloğu var', () => {
    const elder = (dialog as unknown as { elder: { offer: Record<string, string>; active: Record<string, string>; complete: Record<string, string> } }).elder;
    for (const q of QUESTS) {
      expect(elder.offer[q.id], q.id).toBeTruthy();
      expect(elder.active[q.id], q.id).toBeTruthy();
      expect(elder.complete[q.id], q.id).toBeTruthy();
    }
  });
  it('boss tablosu garantili eşyayı her zaman verir', () => {
    const r = new Random(7);
    for (let i = 0; i < 50; i++) expect(rollLoot('boss', r).items.some((x) => x.id === 'battle_axe')).toBe(true);
  });
});
