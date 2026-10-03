// Oyuncu ayarlari: profil dosyasindan bagimsiz, kendi dosyasinda.
import { Platform } from './Platform.js';
import { detectLang } from './i18n.js';

export const DEFAULTS = {
  lang: null,
  master: 0.8,
  music: 0.6,
  sfx: 0.8,
  ambience: 0.7,
  quality: 'medium',
  sensitivity: 1.0,
  invertY: false,
  fullscreen: false,
  textScale: 1,
  cameraShake: true,
  autoCamera: true,
  showFps: false,
};

export class Settings {
  constructor() {
    this.values = { ...DEFAULTS };
    this.listeners = new Set();
  }

  async load() {
    const data = await Platform.readJSON('settings');
    if (data && typeof data === 'object') {
      for (const k of Object.keys(DEFAULTS)) if (data[k] !== undefined) this.values[k] = data[k];
    }
    if (!this.values.lang) this.values.lang = detectLang();
    return this.values;
  }

  get(k) {
    return this.values[k];
  }

  set(k, v) {
    this.values[k] = v;
    for (const fn of this.listeners) fn(k, v);
    clearTimeout(this._t);
    this._t = setTimeout(() => Platform.writeJSON('settings', this.values), 300);
  }

  onChange(fn) {
    this.listeners.add(fn);
  }
}
