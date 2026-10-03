// Girdi: klavye + fare + gamepad, "eylem" katmaniyla. Oyun kodu tus adi
// bilmez; 'jump', 'interact' gibi eylemler sorar. Son kullanilan cihaz
// (klavye/gamepad) izlenir ki ekrandaki ipuclari dogru tusu gostersin.

const KEYS = {
  jump: ['Space'],
  interact: ['KeyE', 'KeyF'],
  sprint: ['ShiftLeft', 'ShiftRight'],
  pause: ['Escape'],
  journal: ['Tab', 'KeyJ'],
  photo: ['KeyP'],
  up: ['KeyW', 'ArrowUp'],
  down: ['KeyS', 'ArrowDown'],
  left: ['KeyA', 'ArrowLeft'],
  right: ['KeyD', 'ArrowRight'],
  confirm: ['Enter', 'Space', 'KeyE'],
  back: ['Escape', 'Backspace'],
  camLeft: ['KeyQ'],
  camRight: ['KeyR'],
  shot: ['Enter', 'F12'],
  filter: ['KeyF'],
  hide: ['KeyH'],
  rise: ['KeyE'],
  sink: ['KeyQ'],
  tabPrev: ['KeyQ', 'BracketLeft'],
  tabNext: ['KeyE', 'BracketRight'],
};

// Standart gamepad eslemesi (Xbox duzeni)
const PAD = {
  jump: [0],
  interact: [2],
  sprint: [1, 5, 7],
  pause: [9],
  journal: [8],
  photo: [3],
  up: [12],
  down: [13],
  left: [14],
  right: [15],
  confirm: [0],
  back: [1],
  shot: [0],
  filter: [5],
  hide: [2],
  rise: [7],
  sink: [6],
  tabPrev: [4],
  tabNext: [5],
  camLeft: [],
  camRight: [],
};

const DEAD = 0.18;

function deadzone(v) {
  const a = Math.abs(v);
  if (a < DEAD) return 0;
  return Math.sign(v) * ((a - DEAD) / (1 - DEAD));
}

export class Input {
  constructor(canvas) {
    this.canvas = canvas;
    this.down = new Set();
    this.pressedKeys = new Set();
    this.releasedKeys = new Set();
    this.mouse = { dx: 0, dy: 0, wheel: 0, buttons: 0 };
    this.pad = { buttons: [], prev: [], axes: [0, 0, 0, 0], connected: false };
    this.device = 'kbm';
    this.locked = false;
    this.enabled = true;
    this.simulated = new Set(); // testler icin

    window.addEventListener('keydown', (e) => {
      if (e.code === 'Tab' || e.code === 'Space' || e.code === 'F12' || e.code.startsWith('Arrow')) e.preventDefault();
      if (!e.repeat) this.pressedKeys.add(e.code);
      this.down.add(e.code);
      this.device = 'kbm';
    });
    window.addEventListener('keyup', (e) => {
      this.down.delete(e.code);
      this.releasedKeys.add(e.code);
    });
    window.addEventListener('blur', () => this.down.clear());
    window.addEventListener('mousemove', (e) => {
      if (this.locked || this.mouse.buttons & 2 || this.mouse.buttons & 1) {
        this.mouse.dx += e.movementX || 0;
        this.mouse.dy += e.movementY || 0;
      }
    });
    canvas.addEventListener('mousedown', (e) => {
      this.mouse.buttons |= 1 << e.button;
      this.device = 'kbm';
    });
    window.addEventListener('mouseup', (e) => { this.mouse.buttons &= ~(1 << e.button); });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    window.addEventListener('wheel', (e) => { this.mouse.wheel += Math.sign(e.deltaY); }, { passive: true });
    document.addEventListener('pointerlockchange', () => {
      this.locked = document.pointerLockElement === canvas;
    });
  }

  requestLock() {
    if (this.locked) return;
    try {
      const p = this.canvas.requestPointerLock?.();
      if (p && p.catch) p.catch(() => {});
    } catch { /* desteklenmiyor */ }
  }

  releaseLock() {
    if (document.pointerLockElement) document.exitPointerLock();
  }

  update() {
    const pads = navigator.getGamepads ? navigator.getGamepads() : [];
    let gp = null;
    for (const p of pads) if (p && p.connected) { gp = p; break; }
    this.pad.prev = this.pad.buttons;
    if (gp) {
      this.pad.connected = true;
      this.pad.buttons = gp.buttons.map((b) => b.pressed || b.value > 0.5);
      this.pad.axes = [deadzone(gp.axes[0] || 0), deadzone(gp.axes[1] || 0), deadzone(gp.axes[2] || 0), deadzone(gp.axes[3] || 0)];
      if (this.pad.buttons.some(Boolean) || this.pad.axes.some((a) => a !== 0)) this.device = 'pad';
      this.padRef = gp;
    } else {
      this.pad.connected = false;
      this.pad.buttons = [];
      this.pad.axes = [0, 0, 0, 0];
    }
  }

  // Ayni karedeki sonraki alt adimlar "yeni basildi" olaylarini tekrar gormesin.
  consumeEdges() {
    this.pressedKeys.clear();
    this.releasedKeys.clear();
    this.pad.prev = this.pad.buttons;
    this.mouse.dx = 0;
    this.mouse.dy = 0;
    this.mouse.wheel = 0;
  }

  endFrame() {
    this.pressedKeys.clear();
    this.releasedKeys.clear();
    this.mouse.dx = 0;
    this.mouse.dy = 0;
    this.mouse.wheel = 0;
  }

  held(action) {
    if (!this.enabled) return false;
    if (this.simulated.has(action)) return true;
    for (const k of KEYS[action] || []) if (this.down.has(k)) return true;
    for (const b of PAD[action] || []) if (this.pad.buttons[b]) return true;
    return false;
  }

  pressed(action) {
    if (!this.enabled) return false;
    for (const k of KEYS[action] || []) if (this.pressedKeys.has(k)) return true;
    for (const b of PAD[action] || []) if (this.pad.buttons[b] && !this.pad.prev[b]) return true;
    return false;
  }

  released(action) {
    for (const k of KEYS[action] || []) if (this.releasedKeys.has(k)) return true;
    for (const b of PAD[action] || []) if (!this.pad.buttons[b] && this.pad.prev[b]) return true;
    return false;
  }

  // Hareket vektoru: x sag, y ileri
  move() {
    if (!this.enabled) return { x: 0, y: 0 };
    let x = 0;
    let y = 0;
    if (this.down.has('KeyW') || this.simulated.has('up')) y += 1;
    if (this.down.has('KeyS') || this.simulated.has('down')) y -= 1;
    if (this.down.has('KeyA') || this.simulated.has('left')) x -= 1;
    if (this.down.has('KeyD') || this.simulated.has('right')) x += 1;
    if (this.down.has('ArrowUp')) y += 1;
    if (this.down.has('ArrowDown')) y -= 1;
    if (this.down.has('ArrowLeft')) x -= 1;
    if (this.down.has('ArrowRight')) x += 1;
    x += this.pad.axes[0];
    y -= this.pad.axes[1];
    const len = Math.hypot(x, y);
    if (len > 1) { x /= len; y /= len; }
    return { x, y };
  }

  look() {
    let x = this.mouse.dx * 0.0025;
    let y = this.mouse.dy * 0.0025;
    x += this.pad.axes[2] * 0.045;
    y += this.pad.axes[3] * 0.035;
    if (this.held('camLeft')) x -= 0.03;
    if (this.held('camRight')) x += 0.03;
    return { x, y };
  }

  rumble(strength = 0.4, ms = 120) {
    const act = this.padRef?.vibrationActuator;
    if (act && act.playEffect) {
      act.playEffect('dual-rumble', { duration: ms, strongMagnitude: strength, weakMagnitude: strength * 0.6 }).catch(() => {});
    }
  }

  glyph(action) {
    if (this.device === 'pad') {
      const map = { jump: 'A', interact: 'X', sprint: 'B', pause: '☰', journal: '⧉', photo: 'Y', confirm: 'A', back: 'B', shot: 'A', filter: 'RB', hide: 'X', tabPrev: 'LB', tabNext: 'RB', left: '✥' };
      return map[action] || '?';
    }
    const map = { jump: 'Space', interact: 'E', sprint: 'Shift', pause: 'Esc', journal: 'Tab', photo: 'P', confirm: 'Enter', back: 'Esc', shot: 'Enter', filter: 'F', hide: 'H', tabPrev: 'Q', tabNext: 'E', left: '← →' };
    return map[action] || '?';
  }
}
