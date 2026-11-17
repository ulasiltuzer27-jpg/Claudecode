import type { Stack, Equipment } from '../rpg/Inventory';
import type { QuestStatus } from '../rpg/Quests';

export const SAVE_KEY = 'golge-vadisi-save-v1';
export const SETTINGS_KEY = 'golge-vadisi-settings-v1';

export interface SaveData {
  v: 1;
  savedAt: number;
  player: { x: number; z: number; facing: number; level: number; xp: number; hp: number; mana: number };
  inventory: { slots: (Stack | null)[]; equipment: Equipment; gold: number };
  hotbar: (string | null)[];
  quests: { status: [string, QuestStatus][]; progress: [string, number[]][] };
  chests: string[];
  bossDefeated: boolean;
  timeOfDay: number;
}

/** localStorage gizli pencerede ya da engelliyken hata fırlatabilir: hepsini yut. */
export function readJson<T>(key: string): T | null {
  try {
    const s = localStorage.getItem(key);
    return s ? (JSON.parse(s) as T) : null;
  } catch {
    return null;
  }
}

export function writeJson(key: string, value: unknown): boolean {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    return true;
  } catch {
    return false;
  }
}

export function removeKey(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    /* yok say */
  }
}

export function loadSave(): SaveData | null {
  const d = readJson<SaveData>(SAVE_KEY);
  return d && d.v === 1 ? d : null;
}
