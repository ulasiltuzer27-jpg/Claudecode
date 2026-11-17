/**
 * Düşman yapay zekası: saf durum makinesi. Enemy her kare bağlamı
 * (mesafeler, bekleme süreleri) doldurup `nextState` sorar; animasyon ve
 * hareket Enemy'de. Bu ayrım AI geçişlerinin testte, tarayıcısız
 * sınanabilmesi için.
 *
 *   dormant ──(oyuncu yaklaştı)──► awaken ──► chase
 *   idle ⇄ patrol ──(aggro)──► alert ──► chase ⇄ attack
 *   chase ──(spawn'dan çok uzak / oyuncu kaçtı)──► return ──► idle
 *   * ──(hasar)──► hit ──► chase        * ──(can 0)──► dead
 */
export type AIState = 'dormant' | 'awaken' | 'idle' | 'patrol' | 'alert' | 'chase' | 'attack' | 'hit' | 'return' | 'dead';

export interface AIContext {
  state: AIState;
  /** Mevcut durumda geçen süre (sn). */
  stateTime: number;
  hp: number;
  distToPlayer: number;
  distToSpawn: number;
  playerAlive: boolean;
  aggroRange: number;
  attackRange: number;
  cooldownReady: boolean;
  ranged: boolean;
  /** Mevcut tek seferlik animasyon bitti mi (awaken/alert/attack/hit). */
  animDone: boolean;
  /** Son birkaç saniyede hasar aldı mı (uzaktan vurulan düşman da uyanır). */
  provoked: boolean;
  /** Bu kare hasar aldı mı. */
  justHit: boolean;
  /** Patrol hedefine vardı mı. */
  atPatrolTarget: boolean;
  isBoss: boolean;
}

export const LEASH = 38;
export const LEASH_BOSS = 30;

export function nextState(c: AIContext): AIState {
  if (c.hp <= 0) return 'dead';
  if (c.state === 'dead') return 'dead';

  // Hasar her şeyi böler (saldırı ortasında bile), boss hariç: boss sendelemez.
  if (c.justHit && !c.isBoss && c.state !== 'dormant' && c.state !== 'awaken') return 'hit';

  const sees = c.playerAlive && (c.distToPlayer <= c.aggroRange || c.provoked);
  const leash = c.isBoss ? LEASH_BOSS : LEASH;

  switch (c.state) {
    case 'dormant':
      return c.playerAlive && (c.distToPlayer < c.aggroRange * 0.55 || c.justHit) ? 'awaken' : 'dormant';
    case 'awaken':
      return c.animDone ? 'chase' : 'awaken';
    case 'idle':
      if (sees) return 'alert';
      return c.stateTime > 3 + (c.distToSpawn % 2) ? 'patrol' : 'idle';
    case 'patrol':
      if (sees) return 'alert';
      return c.atPatrolTarget || c.stateTime > 8 ? 'idle' : 'patrol';
    case 'alert':
      return c.animDone || c.stateTime > 0.8 ? 'chase' : 'alert';
    case 'hit':
      return c.animDone || c.stateTime > 0.55 ? 'chase' : 'hit';
    case 'attack':
      return c.animDone ? 'chase' : 'attack';
    case 'chase':
      if (!c.playerAlive) return 'return';
      if (c.distToSpawn > leash || c.distToPlayer > c.aggroRange * 2.2) return 'return';
      if (c.distToPlayer <= c.attackRange && c.cooldownReady) return 'attack';
      return 'chase';
    case 'return':
      if (c.distToSpawn < 2) return 'idle';
      // Geri dönerken çok yakına gelen oyuncu yeniden kışkırtır.
      if (c.playerAlive && c.distToPlayer < c.aggroRange * 0.5 && c.distToSpawn < leash * 0.8) return 'chase';
      return 'return';
  }
}
