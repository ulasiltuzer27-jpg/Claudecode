import questsJson from '../data/quests.json';

export interface Objective { type: 'kill' | 'collect'; target?: string; item?: string; count: number; text: string }
export interface QuestDef {
  id: string;
  name: string;
  giver: string;
  summary: string;
  objectives: Objective[];
  reward: { xp: number; gold: number; items: [string, number][] };
  next?: string;
}
export type QuestStatus = 'locked' | 'available' | 'active' | 'complete' | 'done';

export const QUESTS: QuestDef[] = questsJson as QuestDef[];

/**
 * Görev günlüğü. Saf mantık: oyun olayları (`onKill`, `onItemCount`) dışarıdan
 * bildirilir. Zincir: bir görev teslim edilince `next` açılır.
 *
 *  locked -> available -> active -> complete -> done
 *                          ^ hedefler dolunca  ^ NPC'ye teslim
 */
export class QuestLog {
  status = new Map<string, QuestStatus>();
  progress = new Map<string, number[]>();
  onChange: ((id: string, status: QuestStatus) => void) | null = null;

  constructor(private defs: QuestDef[] = QUESTS) {
    defs.forEach((q, i) => this.status.set(q.id, i === 0 ? 'available' : 'locked'));
  }

  def(id: string): QuestDef {
    const d = this.defs.find((q) => q.id === id);
    if (!d) throw new Error(`Bilinmeyen görev: ${id}`);
    return d;
  }

  get(id: string): QuestStatus {
    return this.status.get(id) ?? 'locked';
  }

  private set(id: string, s: QuestStatus) {
    this.status.set(id, s);
    this.onChange?.(id, s);
  }

  /** Bu NPC'nin şu an ilgili olduğu görev (teslim > aktif > verilebilir). */
  forGiver(giver: string): { quest: QuestDef; status: QuestStatus } | null {
    const order: QuestStatus[] = ['complete', 'active', 'available'];
    for (const s of order) {
      const q = this.defs.find((d) => d.giver === giver && this.get(d.id) === s);
      if (q) return { quest: q, status: s };
    }
    return null;
  }

  accept(id: string, itemCount: (item: string) => number = () => 0): boolean {
    if (this.get(id) !== 'available') return false;
    const d = this.def(id);
    this.progress.set(id, d.objectives.map(() => 0));
    this.set(id, 'active');
    // Toplama görevinin eşyası zaten çantadaysa hemen say.
    for (const o of d.objectives) if (o.type === 'collect' && o.item) this.onItemCount(o.item, itemCount(o.item));
    return true;
  }

  active(): QuestDef[] {
    return this.defs.filter((d) => this.get(d.id) === 'active' || this.get(d.id) === 'complete');
  }

  private check(id: string) {
    const d = this.def(id);
    const p = this.progress.get(id)!;
    if (d.objectives.every((o, i) => p[i] >= o.count)) this.set(id, 'complete');
    else if (this.get(id) === 'complete') this.set(id, 'active');
    else this.onChange?.(id, 'active');
  }

  /** Bir düşman öldü. target bir ön ek: "skeleton" bütün iskelet türlerini sayar. */
  onKill(enemyType: string): void {
    for (const d of this.defs) {
      if (this.get(d.id) !== 'active') continue;
      const p = this.progress.get(d.id)!;
      let touched = false;
      d.objectives.forEach((o, i) => {
        if (o.type === 'kill' && o.target && enemyType.startsWith(o.target) && p[i] < o.count) {
          p[i]++;
          touched = true;
        }
      });
      if (touched) this.check(d.id);
    }
  }

  /** Bir eşyanın çantadaki toplam sayısı değişti. */
  onItemCount(itemId: string, total: number): void {
    for (const d of this.defs) {
      const s = this.get(d.id);
      if (s !== 'active' && s !== 'complete') continue;
      const p = this.progress.get(d.id)!;
      let touched = false;
      d.objectives.forEach((o, i) => {
        if (o.type === 'collect' && o.item === itemId) {
          const v = Math.min(total, o.count);
          if (p[i] !== v) { p[i] = v; touched = true; }
        }
      });
      if (touched) this.check(d.id);
    }
  }

  /** Teslim et: ödülü döndürür (null: teslim edilemez). */
  turnIn(id: string): QuestDef['reward'] | null {
    if (this.get(id) !== 'complete') return null;
    const d = this.def(id);
    this.set(id, 'done');
    if (d.next && this.get(d.next) === 'locked') this.set(d.next, 'available');
    return d.reward;
  }

  serialize() {
    return { status: [...this.status], progress: [...this.progress] };
  }

  load(data: ReturnType<QuestLog['serialize']>): void {
    this.status = new Map(data.status);
    this.progress = new Map(data.progress);
  }
}
