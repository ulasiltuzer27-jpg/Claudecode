/**
 * Klavye + fare girdisi. Oyun mantığı tuş kodlarını değil EYLEMLERİ sorar
 * (`input.down('run')`), böylece tuş ataması tek yerde durur.
 *
 * Fare: oyun sırasında pointer lock. Kilit yokken (menü açıkken ya da
 * headless testte) fare hareketi kamerayı döndürmez.
 */
export type Action =
  | 'forward' | 'back' | 'left' | 'right'
  | 'run' | 'jump' | 'dodge'
  | 'attack' | 'block' | 'cast'
  | 'interact' | 'inventory' | 'quests' | 'pause'
  | 'slot1' | 'slot2' | 'slot3' | 'slot4';

const KEYMAP: Record<string, Action> = {
  KeyW: 'forward', ArrowUp: 'forward',
  KeyS: 'back', ArrowDown: 'back',
  KeyA: 'left', ArrowLeft: 'left',
  KeyD: 'right', ArrowRight: 'right',
  ShiftLeft: 'run', ShiftRight: 'run',
  Space: 'jump',
  KeyC: 'dodge', ControlLeft: 'dodge',
  KeyQ: 'cast',
  KeyE: 'interact', KeyF: 'interact',
  KeyI: 'inventory', Tab: 'inventory',
  KeyJ: 'quests', KeyL: 'quests',
  Escape: 'pause', KeyP: 'pause',
  Digit1: 'slot1', Digit2: 'slot2', Digit3: 'slot3', Digit4: 'slot4',
};

export class Input {
  private held = new Set<Action>();
  private pressed = new Set<Action>();
  private mouseDX = 0;
  private mouseDY = 0;
  private wheel = 0;
  locked = false;
  /** false iken (menü/diyalog açık) oyun eylemleri yutulur, ama `pause` hep çalışır. */
  gameplayEnabled = true;

  constructor(private canvas: HTMLCanvasElement) {
    window.addEventListener('keydown', (e) => {
      const a = KEYMAP[e.code];
      if (!a) return;
      // Tab ve Space tarayıcıda odak değiştirir / sayfayı kaydırır.
      if (e.code === 'Tab' || e.code === 'Space' || e.code.startsWith('Arrow')) e.preventDefault();
      if (!e.repeat) this.pressed.add(a);
      this.held.add(a);
    });
    window.addEventListener('keyup', (e) => {
      const a = KEYMAP[e.code];
      if (a) this.held.delete(a);
    });
    window.addEventListener('blur', () => this.held.clear());

    canvas.addEventListener('mousedown', (e) => {
      if (e.button === 0) { this.pressed.add('attack'); this.held.add('attack'); }
      if (e.button === 2) { this.pressed.add('block'); this.held.add('block'); }
    });
    window.addEventListener('mouseup', (e) => {
      if (e.button === 0) this.held.delete('attack');
      if (e.button === 2) this.held.delete('block');
    });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
    window.addEventListener('mousemove', (e) => {
      if (!this.locked) return;
      this.mouseDX += e.movementX;
      this.mouseDY += e.movementY;
    });
    canvas.addEventListener('wheel', (e) => {
      this.wheel += Math.sign(e.deltaY);
      e.preventDefault();
    }, { passive: false });
    document.addEventListener('pointerlockchange', () => {
      this.locked = document.pointerLockElement === canvas;
    });
  }

  requestLock(): void {
    if (this.locked) return;
    // Headless tarayıcıda ve bazı iframe'lerde pointer lock yok: sessizce geç.
    try {
      const p = this.canvas.requestPointerLock() as unknown;
      if (p && typeof (p as Promise<void>).catch === 'function') (p as Promise<void>).catch(() => {});
    } catch {
      /* desteklenmiyor */
    }
  }

  releaseLock(): void {
    if (document.pointerLockElement) document.exitPointerLock();
  }

  /** Eylem basılı mı (oyun eylemleri menü açıkken false döner). */
  down(a: Action): boolean {
    if (!this.gameplayEnabled && a !== 'pause') return false;
    return this.held.has(a);
  }

  /** Eylem bu karede mi basıldı. */
  hit(a: Action): boolean {
    if (!this.gameplayEnabled && a !== 'pause' && a !== 'inventory' && a !== 'quests' && a !== 'interact') return false;
    return this.pressed.has(a);
  }

  /** Kapı kontrolü olmadan: panel açıkken de (diyalog seçenekleri için) çalışır. */
  hitRaw(a: Action): boolean {
    return this.pressed.has(a);
  }

  /** Bu karedeki fare hareketini al ve sıfırla. */
  takeMouse(): { dx: number; dy: number; wheel: number } {
    const r = { dx: this.mouseDX, dy: this.mouseDY, wheel: this.wheel };
    this.mouseDX = this.mouseDY = this.wheel = 0;
    return r;
  }

  /** Kare sonunda çağrılır. */
  endFrame(): void {
    this.pressed.clear();
  }

  /** Testler ve otomatik sürüş için: eylemi programatik olarak bas/bırak. */
  simulate(a: Action, isDown: boolean): void {
    if (isDown) { this.pressed.add(a); this.held.add(a); } else this.held.delete(a);
  }

  clear(): void {
    this.held.clear();
    this.pressed.clear();
  }
}
