import { describe, expect, it } from 'vitest';
import { nextState, type AIContext } from '../src/entities/EnemyAI';
import { Colliders } from '../src/world/Colliders';
import { CAMPS, PATHS, VILLAGE, WATER_LEVEL } from '../src/world/Layout';
import { Terrain } from '../src/world/Terrain';

const ctx = (o: Partial<AIContext>): AIContext => ({
  state: 'idle', stateTime: 0, hp: 50, distToPlayer: 50, distToSpawn: 0, playerAlive: true, aggroRange: 15,
  attackRange: 2.3, cooldownReady: true, ranged: false, animDone: false, provoked: false, justHit: false,
  atPatrolTarget: false, isBoss: false, ...o,
});

describe('düşman yapay zekası', () => {
  it('uzaktaki oyuncuyu görmez, yakındakini görür', () => {
    expect(nextState(ctx({ distToPlayer: 30 }))).toBe('idle');
    expect(nextState(ctx({ distToPlayer: 10 }))).toBe('alert');
    expect(nextState(ctx({ state: 'patrol', distToPlayer: 10 }))).toBe('alert');
  });
  it('yerde yatan iskelet yalnızca çok yakında ya da vurulunca kalkar', () => {
    expect(nextState(ctx({ state: 'dormant', distToPlayer: 10 }))).toBe('dormant');
    expect(nextState(ctx({ state: 'dormant', distToPlayer: 6 }))).toBe('awaken');
    expect(nextState(ctx({ state: 'dormant', distToPlayer: 30, justHit: true }))).toBe('awaken');
    expect(nextState(ctx({ state: 'awaken', animDone: true }))).toBe('chase');
  });
  it('menzil + bekleme süresi -> saldırı', () => {
    expect(nextState(ctx({ state: 'chase', distToPlayer: 2 }))).toBe('attack');
    expect(nextState(ctx({ state: 'chase', distToPlayer: 2, cooldownReady: false }))).toBe('chase');
    expect(nextState(ctx({ state: 'attack', animDone: true, distToPlayer: 2 }))).toBe('chase');
  });
  it('ip: spawn\'dan çok uzaklaşınca geri döner, varınca durur', () => {
    expect(nextState(ctx({ state: 'chase', distToSpawn: 50, distToPlayer: 5 }))).toBe('return');
    expect(nextState(ctx({ state: 'return', distToSpawn: 1, distToPlayer: 40 }))).toBe('idle');
    expect(nextState(ctx({ state: 'chase', playerAlive: false }))).toBe('return');
  });
  it('vurulma sendeletir ama boss sendelemez', () => {
    expect(nextState(ctx({ state: 'attack', justHit: true, distToPlayer: 2 }))).toBe('hit');
    expect(nextState(ctx({ state: 'attack', justHit: true, isBoss: true, distToPlayer: 2 }))).toBe('attack');
  });
  it('uzaktan vurulan düşman kışkırır', () => {
    expect(nextState(ctx({ state: 'idle', distToPlayer: 40, provoked: true }))).toBe('alert');
  });
  it('ölü ölü kalır', () => {
    expect(nextState(ctx({ hp: 0 }))).toBe('dead');
    expect(nextState(ctx({ state: 'dead', hp: 10, distToPlayer: 1 }))).toBe('dead');
  });
});

describe('çarpışma', () => {
  it('daire dışarı itilir', () => {
    const c = new Colliders();
    c.addCircle(0, 0, 1);
    const r = c.resolve(0.5, 0, 0.5);
    expect(r.hit).toBe(true);
    expect(Math.hypot(r.x, r.z)).toBeCloseTo(1.5, 5);
  });
  it('döndürülmüş kutu: kenardan ve köşeden doğru itilir', () => {
    const c = new Colliders();
    c.addBox(10, 10, 2, 1, Math.PI / 2); // 90°: yerel x dünya -z boyunca
    // Kutu dünya uzayında x yönünde ±1, z yönünde ±2 kaplar
    const r = c.resolve(10 + 1.2, 10, 0.5);
    expect(r.x).toBeCloseTo(11.5, 5);
    const inside = c.resolve(10, 10 + 1.9, 0.5);
    expect(Math.abs(inside.z - 10)).toBeCloseTo(2.5, 5);
  });
  it('engelin üstündeyken (zıplamış) itmez', () => {
    const c = new Colliders();
    c.addCircle(0, 0, 1, 1.0);
    expect(c.resolve(0.5, 0, 0.5, 1.5).hit).toBe(false);
  });
});

describe('arazi', () => {
  const t = new Terrain(1337, 120);
  it('fizik yüksekliği ızgara köşelerinde ham yükseklikle aynı', () => {
    for (let k = 0; k < 50; k++) {
      const i = (k * 37) % 120;
      const j = (k * 53) % 120;
      const x = -260 + i * t.step;
      const z = -260 + j * t.step;
      expect(t.heightAt(x, z)).toBeCloseTo(t.rawHeight(x, z), 4);
    }
  });
  it('köy düz ve kuru', () => {
    for (const [dx, dz] of [[0, 0], [10, 5], [-15, 20]]) expect(t.heightAt(VILLAGE.x + dx, VILLAGE.z + dz)).toBeCloseTo(VILLAGE.height, 0);
  });
  it('kamplar ve yollar suyun üstünde', () => {
    for (const c of CAMPS) expect(t.heightAt(c.x, c.z), c.id).toBeGreaterThan(WATER_LEVEL + 1);
    for (const path of PATHS) {
      for (let i = 0; i < path.length - 1; i++) {
        for (let s = 0; s <= 1; s += 0.1) {
          const x = path[i].x + (path[i + 1].x - path[i].x) * s;
          const z = path[i].z + (path[i + 1].z - path[i].z) * s;
          expect(t.heightAt(x, z), `${x.toFixed(0)},${z.toFixed(0)}`).toBeGreaterThan(WATER_LEVEL + 0.3);
        }
      }
    }
  });
});
