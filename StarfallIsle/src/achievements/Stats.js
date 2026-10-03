// Profil duzeyindeki istatistikler (Steam istatistiklerinin yerel aynasi).
// 'sum' birikir, 'max' yalnizca rekor kirilinca degisir.
import data from './achievements.json';

export const STAT_DEFS = Object.fromEntries(data.stats.map((s) => [s.key, s]));

export class Stats {
  constructor(values = {}, events) {
    this.values = {};
    for (const k of Object.keys(STAT_DEFS)) this.values[k] = Number(values[k]) || 0;
    this.events = events;
  }

  get(key) {
    return this.values[key] || 0;
  }

  add(key, amount = 1) {
    if (!STAT_DEFS[key]) throw new Error(`Bilinmeyen istatistik: ${key}`);
    this.values[key] = this.get(key) + amount;
    this.events?.emit('stat', { key, value: this.values[key] });
  }

  max(key, value) {
    if (!STAT_DEFS[key]) throw new Error(`Bilinmeyen istatistik: ${key}`);
    if (value > this.get(key)) {
      this.values[key] = value;
      this.events?.emit('stat', { key, value });
    }
  }

  toJSON() {
    return { ...this.values };
  }
}
