/** Sistemler arası gevşek bağ: görev sistemi düşman ölümünü bilmek için Enemy'yi tanımak zorunda değil. */
export interface GameEvents {
  enemyKilled: { type: string; x: number; z: number; xp: number };
  itemAdded: { id: string; qty: number };
  itemRemoved: { id: string; qty: number };
  goldChanged: { gold: number; delta: number };
  levelUp: { level: number };
  questChanged: { id: string };
  questCompleted: { id: string };
  playerDamaged: { amount: number };
  playerDied: Record<string, never>;
  bossEngaged: { id: string };
  bossDefeated: { id: string };
  notify: { text: string; kind?: 'info' | 'quest' | 'loot' | 'warn' };
}

type Handler<T> = (payload: T) => void;

export class EventBus {
  private handlers = new Map<keyof GameEvents, Set<Handler<never>>>();

  on<K extends keyof GameEvents>(type: K, fn: Handler<GameEvents[K]>): () => void {
    let set = this.handlers.get(type);
    if (!set) this.handlers.set(type, (set = new Set()));
    set.add(fn as Handler<never>);
    return () => set!.delete(fn as Handler<never>);
  }

  emit<K extends keyof GameEvents>(type: K, payload: GameEvents[K]): void {
    const set = this.handlers.get(type);
    if (!set) return;
    for (const fn of set) (fn as Handler<GameEvents[K]>)(payload);
  }
}
