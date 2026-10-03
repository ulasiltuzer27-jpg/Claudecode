// Oyun sayfasina acilan dar kopru. Sayfa Node'a dogrudan erisemez;
// yalnizca asagidaki islevleri cagirabilir (contextIsolation + sandbox).
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('starfall', {
  readJSON: (name) => ipcRenderer.invoke('fs:read', name),
  writeJSON: (name, data) => ipcRenderer.invoke('fs:write', name, data),
  remove: (name) => ipcRenderer.invoke('fs:remove', name),
  steamStatus: () => ipcRenderer.invoke('steam:status'),
  steamActivate: (id) => ipcRenderer.send('steam:activate', id),
  steamSetStat: (name, value) => ipcRenderer.send('steam:setStat', name, value),
  steamStore: () => ipcRenderer.send('steam:store'),
  steamRichPresence: (key, value) => ipcRenderer.send('steam:rp', key, value),
  quit: () => ipcRenderer.send('app:quit'),
  setFullscreen: (on) => ipcRenderer.invoke('app:fullscreen', on),
  saveScreenshot: (dataUrl) => ipcRenderer.invoke('shot:save', dataUrl),
  rendererReady: (info) => ipcRenderer.send('renderer:ready', info),
});
