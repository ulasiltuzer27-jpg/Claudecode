// Electron ana sureci: pencere, Steam (steamworks.js), kayit dosyalari.
//
// Steam kurali: Steam calismiyorsa (gelistirme, cevrimdisi, Steam disi
// dagitim) oyun sessizce yerel moda duser. Basarimlar yine oyun icinde
// acilir; bir sonraki Steam'li acilista oyun bunlari Steam'e yeniden
// bildirir (src/achievements/AchievementTracker.js -> syncToSteam).
const { app, BrowserWindow, ipcMain, Menu, shell } = require('electron');
const path = require('node:path');
const fs = require('node:fs');

const SMOKE = process.argv.includes('--smoke-test');
const ROOT = path.join(__dirname, '..');

function readAppId() {
  for (const p of [path.join(ROOT, 'steam', 'steam_appid.txt'), path.join(process.cwd(), 'steam_appid.txt')]) {
    try {
      const id = parseInt(fs.readFileSync(p, 'utf8').trim(), 10);
      if (Number.isFinite(id) && id > 0) return id;
    } catch { /* yok */ }
  }
  return 480; // Valve'in test uygulamasi (Spacewar)
}

const APP_ID = readAppId();
let steam = null;
let steamInfo = { available: false, reason: 'not-initialized', appId: APP_ID };

(function initSteam() {
  let steamworks;
  try {
    steamworks = require('steamworks.js');
  } catch (err) {
    steamInfo = { available: false, reason: `steamworks.js yuklenemedi: ${err.message}`, appId: APP_ID };
    return;
  }
  try {
    // Yayinda: oyun Steam disindan acildiysa Steam uzerinden yeniden baslat.
    if (app.isPackaged && APP_ID !== 480 && steamworks.restartAppIfNecessary(APP_ID)) {
      app.exit(0);
      return;
    }
    steam = steamworks.init(APP_ID);
    steamInfo = {
      available: true,
      appId: APP_ID,
      name: safe(() => steam.localplayer.getName(), ''),
      deck: safe(() => steam.utils.isSteamRunningOnSteamDeck(), false),
      language: safe(() => steam.apps.currentGameLanguage(), ''),
    };
    if (!SMOKE) steamworks.electronEnableSteamOverlay();
  } catch (err) {
    steam = null;
    steamInfo = { available: false, reason: String(err && err.message ? err.message : err), appId: APP_ID };
  }
})();

function safe(fn, fallback) {
  try { return fn(); } catch { return fallback; }
}

if (SMOKE) {
  // GPU'suz ortamda (CI) WebGL icin yazilimsal render
  app.commandLine.appendSwitch('use-angle', 'swiftshader');
  app.commandLine.appendSwitch('enable-unsafe-swiftshader');
  app.commandLine.appendSwitch('ignore-gpu-blocklist');
}
app.commandLine.appendSwitch('autoplay-policy', 'no-user-gesture-required');

// ---------------- kayit dosyalari ----------------
const NAME_RE = /^[a-z0-9_]{1,40}$/;
function saveDir() {
  const dir = path.join(app.getPath('userData'), 'saves');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}
function fileFor(name) {
  if (typeof name !== 'string' || !NAME_RE.test(name)) throw new Error(`gecersiz kayit adi: ${name}`);
  return path.join(saveDir(), `${name}.json`);
}

ipcMain.handle('fs:read', (_e, name) => {
  const f = fileFor(name);
  for (const candidate of [f, `${f}.bak`]) {
    try {
      return JSON.parse(fs.readFileSync(candidate, 'utf8'));
    } catch { /* bir sonrakini dene */ }
  }
  return null;
});

// Atomik yazim: once gecici dosya, sonra yeniden adlandirma. Elektrik
// kesilse bile ya eski ya yeni kayit kalir; yarim dosya kalmaz.
ipcMain.handle('fs:write', (_e, name, data) => {
  const f = fileFor(name);
  const json = JSON.stringify(data);
  if (json.length > 2_000_000) throw new Error('kayit cok buyuk');
  const tmp = `${f}.tmp`;
  fs.writeFileSync(tmp, json, 'utf8');
  if (fs.existsSync(f)) fs.copyFileSync(f, `${f}.bak`);
  fs.renameSync(tmp, f);
  return true;
});

ipcMain.handle('fs:remove', (_e, name) => {
  const f = fileFor(name);
  for (const p of [f, `${f}.bak`]) if (fs.existsSync(p)) fs.unlinkSync(p);
  return true;
});

// ---------------- Steam ----------------
ipcMain.handle('steam:status', () => steamInfo);
ipcMain.on('steam:activate', (_e, id) => {
  if (steam && typeof id === 'string') safe(() => steam.achievement.activate(id));
});
ipcMain.on('steam:setStat', (_e, name, value) => {
  if (steam && typeof name === 'string' && Number.isFinite(value)) safe(() => steam.stats.setInt(name, Math.floor(value)));
});
ipcMain.on('steam:store', () => {
  if (steam) safe(() => steam.stats.store());
});
ipcMain.on('steam:rp', (_e, key, value) => {
  if (steam && typeof key === 'string') safe(() => steam.localplayer.setRichPresence(key, value == null ? null : String(value)));
});

// ---------------- pencere ----------------
let win = null;

ipcMain.on('app:quit', () => app.quit());
ipcMain.handle('app:fullscreen', (_e, on) => {
  if (win) win.setFullScreen(!!on);
  return win ? win.isFullScreen() : false;
});

ipcMain.handle('shot:save', (_e, dataUrl) => {
  if (typeof dataUrl !== 'string' || !dataUrl.startsWith('data:image/png;base64,')) throw new Error('gecersiz goruntu');
  const dir = path.join(app.getPath('pictures'), 'Starfall Isle');
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, `starfall-${new Date().toISOString().replace(/[:.]/g, '-')}.png`);
  fs.writeFileSync(file, Buffer.from(dataUrl.slice('data:image/png;base64,'.length), 'base64'));
  return file;
});

ipcMain.on('renderer:ready', async (_e, info) => {
  if (!SMOKE) return;
  // Kopru uzerinden kayit gidis-donusu: preload + IPC + diske atomik yazim.
  let saveRoundTrip = false;
  try {
    saveRoundTrip = await win.webContents.executeJavaScript(`(async () => {
      await window.starfall.writeJSON('smoke_test', { a: 1, s: 'yildiz' });
      const r = await window.starfall.readJSON('smoke_test');
      await window.starfall.remove('smoke_test');
      return !!r && r.a === 1 && r.s === 'yildiz';
    })()`);
  } catch (err) {
    console.log(`RENDERER_ERROR kayit testi: ${err.message}`);
  }
  console.log(`SMOKE_${saveRoundTrip ? 'OK' : 'FAIL'} ${JSON.stringify({ renderer: info, saveRoundTrip, steam: steamInfo, saves: saveDir() })}`);
  setTimeout(() => app.exit(saveRoundTrip ? 0 : 3), 300);
});

function createWindow() {
  win = new BrowserWindow({
    width: 1280,
    height: 720,
    minWidth: 960,
    minHeight: 540,
    show: false,
    title: 'Starfall Isle',
    backgroundColor: '#0b1020',
    icon: path.join(ROOT, 'build', 'icon.png'),
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      sandbox: true,
      nodeIntegration: false,
      backgroundThrottling: false,
    },
  });
  win.once('ready-to-show', () => win.show());
  // Oyun disariya hicbir yere gitmez; baglantilar varsayilan tarayicida acilir.
  win.webContents.on('will-navigate', (e) => e.preventDefault());
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https:\/\//.test(url)) shell.openExternal(url);
    return { action: 'deny' };
  });
  if (SMOKE) {
    win.webContents.on('console-message', (_e, level, message) => {
      if (level >= 3) console.log(`RENDERER_ERROR ${message}`);
    });
  }
  win.loadFile(path.join(ROOT, 'dist', 'index.html'));
}

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (win) {
      if (win.isMinimized()) win.restore();
      win.focus();
    }
  });
  app.whenReady().then(() => {
    Menu.setApplicationMenu(null);
    createWindow();
    if (SMOKE) {
      setTimeout(() => {
        console.log('SMOKE_TIMEOUT');
        app.exit(2);
      }, 120000);
    }
  });
  app.on('window-all-closed', () => app.quit());
}
