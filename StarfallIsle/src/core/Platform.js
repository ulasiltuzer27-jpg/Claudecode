// Calisma ortami koprusu. Electron'da preload `window.starfall` nesnesini
// verir (dosya kaydi + Steam); tarayicida localStorage'a duser. Oyunun geri
// kalani hangi ortamda calistigini bilmez.

const bridge = typeof window !== 'undefined' ? window.starfall : undefined;
const PREFIX = 'starfall:';

function lsGet(name) {
  try {
    const raw = localStorage.getItem(PREFIX + name);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function lsSet(name, data) {
  try {
    localStorage.setItem(PREFIX + name, JSON.stringify(data));
    return true;
  } catch {
    return false;
  }
}

export const Platform = {
  isElectron: !!bridge,

  async readJSON(name) {
    if (bridge) {
      try {
        return await bridge.readJSON(name);
      } catch (err) {
        console.warn('[kayit] okunamadi', name, err);
        return null;
      }
    }
    return lsGet(name);
  },

  async writeJSON(name, data) {
    if (bridge) {
      try {
        return await bridge.writeJSON(name, data);
      } catch (err) {
        console.warn('[kayit] yazilamadi', name, err);
        return false;
      }
    }
    return lsSet(name, data);
  },

  async remove(name) {
    if (bridge) return bridge.remove(name);
    try { localStorage.removeItem(PREFIX + name); } catch { /* yok say */ }
    return true;
  },

  clearAll() {
    if (bridge) return;
    try {
      for (const k of Object.keys(localStorage)) if (k.startsWith(PREFIX)) localStorage.removeItem(k);
    } catch { /* yok say */ }
  },

  steam: {
    async status() {
      if (!bridge) return { available: false, reason: 'browser' };
      return bridge.steamStatus();
    },
    activate(id) {
      if (bridge) bridge.steamActivate(id);
    },
    setStat(name, value) {
      if (bridge) bridge.steamSetStat(name, Math.floor(value));
    },
    store() {
      if (bridge) bridge.steamStore();
    },
    richPresence(key, value) {
      if (bridge) bridge.steamRichPresence(key, value);
    },
  },

  quit() {
    if (bridge) bridge.quit();
  },

  async setFullscreen(on) {
    if (bridge) return bridge.setFullscreen(on);
    try {
      if (on && !document.fullscreenElement) await document.documentElement.requestFullscreen();
      if (!on && document.fullscreenElement) await document.exitFullscreen();
    } catch { /* tarayici izin vermedi */ }
    return on;
  },

  async saveScreenshot(dataUrl) {
    if (bridge) return bridge.saveScreenshot(dataUrl);
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = `starfall-${new Date().toISOString().replace(/[:.]/g, '-')}.png`;
    a.click();
    return a.download;
  },

  rendererReady(info) {
    if (bridge) bridge.rendererReady(info);
  },
};
