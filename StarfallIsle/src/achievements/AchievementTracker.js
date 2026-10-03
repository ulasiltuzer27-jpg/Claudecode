// Basarimlari istatistiklere gore acar; yerel profilde saklar ve Steam'e
// bildirir. Steam'e ulasilamasa da oyun icinde acilir; bir sonraki
// acilista yerel kayitlar Steam'e yeniden gonderilir (idempotent).
import data from './achievements.json';
import { Platform } from '../core/Platform.js';

export const ACHIEVEMENTS = data.achievements;

export class AchievementTracker {
  constructor(stats, unlocked, events) {
    this.stats = stats;
    this.unlocked = { ...(unlocked || {}) };
    this.events = events;
    this.byStat = new Map();
    for (const a of ACHIEVEMENTS) {
      if (!this.byStat.has(a.stat)) this.byStat.set(a.stat, []);
      this.byStat.get(a.stat).push(a);
    }
    events.on('stat', ({ key, value }) => {
      this.check(key, value);
      this.pushStat(key, value);
    });
  }

  check(key, value) {
    for (const a of this.byStat.get(key) || []) {
      if (!this.unlocked[a.id] && value >= a.threshold) this.unlock(a);
    }
  }

  checkAll() {
    for (const [key] of this.byStat) this.check(key, this.stats.get(key));
  }

  unlock(a) {
    this.unlocked[a.id] = Date.now();
    Platform.steam.activate(a.id);
    Platform.steam.store();
    this.events.emit('achievement', a);
  }

  pushStat(key, value) {
    Platform.steam.setStat(key, value);
    clearTimeout(this._storeT);
    this._storeT = setTimeout(() => Platform.steam.store(), 2000);
  }

  // Acilista: yerelde acik olan her seyi Steam'e tekrar bildir.
  syncToSteam() {
    for (const id of Object.keys(this.unlocked)) Platform.steam.activate(id);
    for (const [k, v] of Object.entries(this.stats.values)) Platform.steam.setStat(k, v);
    Platform.steam.store();
  }

  isUnlocked(id) {
    return !!this.unlocked[id];
  }

  progress(a) {
    return Math.min(1, this.stats.get(a.stat) / a.threshold);
  }

  count() {
    return Object.keys(this.unlocked).filter((id) => ACHIEVEMENTS.some((a) => a.id === id)).length;
  }
}
