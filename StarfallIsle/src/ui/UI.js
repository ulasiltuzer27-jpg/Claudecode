// Arayuz koku: HUD, diyalog, menu ekranlari yigini, jenerik, photo mode.
// Menu ekranlari klavye, fare ve gamepad ile ayni sekilde gezilir.
import './ui.css';
import '@fontsource/nunito/600.css';
import '@fontsource/nunito/700.css';
import '@fontsource/nunito/800.css';
import '@fontsource/nunito/900.css';
import '@fontsource/baloo-2/700.css';
import '@fontsource/baloo-2/800.css';
import { Hud } from './Hud.js';
import { DialogueBox } from './DialogueBox.js';
import { Credits } from './Credits.js';
import { PhotoOverlay } from './PhotoOverlay.js';

export function el(tag, cls, html) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (html !== undefined) e.innerHTML = html;
  return e;
}

export function esc(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

// Odak gezinmeli ekran tabani
export class Screen {
  constructor(ui, cls) {
    this.ui = ui;
    this.game = ui.game;
    this.el = el('div', `screen ${cls || ''}`);
    this.items = [];
    this.index = 0;
    this.columns = 1;
  }

  mount() {
    this.ui.root.appendChild(this.el);
    this.render();
  }

  unmount() {
    this.el.remove();
  }

  refresh() {
    const keep = this.index;
    this.items = [];
    this.el.innerHTML = '';
    this.render();
    this.focus(Math.min(keep, this.items.length - 1));
  }

  render() {}

  add(elem, handlers = {}) {
    const i = this.items.length;
    const item = { el: elem, ...handlers };
    this.items.push(item);
    elem.addEventListener('mouseenter', () => this.focus(i, true));
    elem.addEventListener('click', (e) => {
      e.stopPropagation();
      this.focus(i, true);
      this.activate(item, e);
    });
    if (i === 0) setTimeout(() => this.focus(this.index), 0);
    return item;
  }

  activate(item, e) {
    if (item.disabled) {
      this.game.audio.sfx('error');
      return;
    }
    if (item.select) {
      this.game.audio.sfx('uiConfirm');
      item.select();
    } else if (item.right && e) {
      const r = item.el.getBoundingClientRect();
      if (e.clientX < r.left + r.width / 2 && item.left) item.left(); else item.right();
      this.game.audio.sfx('uiMove');
    }
  }

  focus(i, silent = false) {
    if (!this.items.length) return;
    i = Math.max(0, Math.min(this.items.length - 1, i));
    if (i !== this.index && !silent) this.game.audio.sfx('uiMove');
    this.items.forEach((it, k) => it.el.classList.toggle('focus', k === i));
    this.index = i;
    const elx = this.items[i].el;
    if (elx.scrollIntoView) elx.scrollIntoView({ block: 'nearest' });
  }

  handle(input) {
    const it = this.items[this.index];
    if (input.pressed('up')) this.focus(this.index - this.columns);
    else if (input.pressed('down')) this.focus(this.index + this.columns);
    else if (input.pressed('left')) {
      if (it?.left) { it.left(); this.game.audio.sfx('uiMove'); } else if (this.columns > 1) this.focus(this.index - 1);
    } else if (input.pressed('right')) {
      if (it?.right) { it.right(); this.game.audio.sfx('uiMove'); } else if (this.columns > 1) this.focus(this.index + 1);
    } else if (input.pressed('confirm') && it) this.activate(it);
    else if (input.pressed('back')) this.onBack();
    this.onInput?.(input);
  }

  onBack() {
    this.game.audio.sfx('uiBack');
    this.ui.pop();
  }
}

export class UI {
  constructor(game) {
    this.game = game;
    this.root = document.getElementById('ui');
    this.stack = [];
    this.hud = new Hud(this);
    this.dialogue = new DialogueBox(this);
    this.credits = new Credits(this);
    this.photo = new PhotoOverlay(this);
  }

  push(screen) {
    this.stack.push(screen);
    screen.mount();
  }

  pop() {
    const s = this.stack.pop();
    if (s) s.unmount();
    if (!this.stack.length) this.onEmpty?.();
    return s;
  }

  replace(screen) {
    const s = this.stack.pop();
    if (s) s.unmount();
    this.push(screen);
  }

  clear() {
    while (this.stack.length) this.stack.pop().unmount();
  }

  top() {
    return this.stack[this.stack.length - 1];
  }

  handleInput(input) {
    if (this.credits.active) return this.credits.handle(input);
    const s = this.top();
    if (s) s.handle(input);
  }
}
