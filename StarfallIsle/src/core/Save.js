// Kayit yuvalari (3) + profil. Yuvada oyun ilerlemesi; profilde istatistik,
// basarimlar ve son kullanilan yuva. Basarimlar hesaba aittir (Steam gibi),
// bu yuzden yuva silinse de kaybolmaz.
import { Platform } from './Platform.js';

export const SAVE_VERSION = 1;
export const SLOTS = 3;

export function newSaveData() {
  return {
    version: SAVE_VERSION,
    created: Date.now(),
    updated: Date.now(),
    playTime: 0,
    pos: null,
    yaw: Math.PI,
    hour: 8.5,
    collected: [],
    rewards: [],
    shells: 0,
    shellsTotal: 0,
    quests: { rabbit: 'none', beaver: 'none', frog: 'none', bear: 'none' },
    flags: {},
    fish: {},
    regions: [],
    talked: [],
    finaleTime: null,
  };
}

function migrate(d) {
  if (!d || typeof d !== 'object') return null;
  const base = newSaveData();
  const out = { ...base, ...d };
  out.quests = { ...base.quests, ...(d.quests || {}) };
  out.flags = { ...(d.flags || {}) };
  out.version = SAVE_VERSION;
  return out;
}

export class SaveManager {
  async loadProfile() {
    const p = await Platform.readJSON('profile');
    return { version: 1, stats: {}, unlocked: {}, lastSlot: null, ...(p || {}) };
  }

  saveProfile(profile) {
    return Platform.writeJSON('profile', profile);
  }

  async loadSlot(i) {
    return migrate(await Platform.readJSON(`slot_${i}`));
  }

  saveSlot(i, data) {
    data.updated = Date.now();
    return Platform.writeJSON(`slot_${i}`, data);
  }

  deleteSlot(i) {
    return Platform.remove(`slot_${i}`);
  }

  async listSlots() {
    const out = [];
    for (let i = 1; i <= SLOTS; i++) out.push({ slot: i, data: await this.loadSlot(i) });
    return out;
  }
}
