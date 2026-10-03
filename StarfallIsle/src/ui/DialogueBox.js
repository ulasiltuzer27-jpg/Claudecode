// Diyalog kutusu: daktilo efekti + konusmaci "mirildanmasi" + secenekler.
import { el, esc } from './UI.js';
import { t } from '../core/i18n.js';

export class DialogueBox {
  constructor(ui) {
    this.ui = ui;
    this.active = false;
    this.queue = null;
  }

  get game() {
    return this.ui.game;
  }

  start({ npc = null, name = null, lines, choices = null, onEnd = null }) {
    const g = this.game;
    if (this.active) this.close(false);
    this.npc = npc;
    this.lines = Array.isArray(lines) ? lines : [lines];
    this.choices = choices;
    this.onEnd = onEnd;
    this.lineIdx = 0;
    this.active = true;
    if (npc) npc.talking = true;
    g.player.model.lookTarget = npc ? npc.pos.clone().setY(npc.pos.y + 0.8) : null;
    this.root = el('div', 'dialogue');
    const displayName = name || (npc ? t(`npc.${npc.id}`) : '');
    this.root.innerHTML = `<div class="d-box">${displayName ? `<div class="d-name">${esc(displayName)}</div>` : ''}<div class="d-text"></div><div class="d-next">▼</div></div>`;
    this.textEl = this.root.querySelector('.d-text');
    this.nextEl = this.root.querySelector('.d-next');
    this.root.addEventListener('click', () => this.advance());
    this.ui.root.appendChild(this.root);
    g.setState('dialogue');
    this.showLine();
  }

  showLine() {
    this.full = this.lines[this.lineIdx] || '';
    this.shown = 0;
    this.typing = true;
    this.nextEl.style.visibility = 'hidden';
    this.textEl.textContent = '';
  }

  update(dt) {
    if (!this.active || !this.typing) return;
    const before = Math.floor(this.shown);
    this.shown += dt * 48;
    const now = Math.min(this.full.length, Math.floor(this.shown));
    if (now > before) {
      this.textEl.textContent = this.full.slice(0, now);
      const ch = this.full[now - 1];
      if (ch && /[\p{L}\p{N}]/u.test(ch)) this.game.audio.voiceBlip(this.npc?.def?.voice ?? 1, ch);
    }
    if (now >= this.full.length) this.finishTyping();
  }

  finishTyping() {
    this.typing = false;
    this.textEl.textContent = this.full;
    const last = this.lineIdx >= this.lines.length - 1;
    if (last && this.choices) this.showChoices();
    else this.nextEl.style.visibility = 'visible';
  }

  showChoices() {
    this.nextEl.style.visibility = 'hidden';
    const box = el('div', 'd-choices');
    this.choiceEls = this.choices.map((c, i) => {
      const b = el('button', 'choice', esc(c.label));
      b.addEventListener('mouseenter', () => this.focusChoice(i));
      b.addEventListener('click', (e) => { e.stopPropagation(); this.pick(i); });
      box.appendChild(b);
      return b;
    });
    this.root.appendChild(box);
    this.choiceIdx = 0;
    this.focusChoice(0);
  }

  focusChoice(i) {
    this.choiceIdx = (i + this.choiceEls.length) % this.choiceEls.length;
    this.choiceEls.forEach((b, k) => b.classList.toggle('focus', k === this.choiceIdx));
  }

  pick(i) {
    const c = this.choices[i];
    this.game.audio.sfx('uiConfirm');
    this.close(false);
    c.action?.();
    if (!this.active && this.game.state === 'dialogue') this.game.setState('playing');
  }

  advance() {
    if (!this.active) return;
    if (this.typing) return this.finishTyping();
    if (this.choiceEls && this.choiceEls.length) return;
    if (this.lineIdx < this.lines.length - 1) {
      this.lineIdx++;
      this.showLine();
      return;
    }
    this.close(true);
  }

  handle(input) {
    if (!this.active) return;
    if (this.choiceEls && this.choiceEls.length && !this.typing) {
      if (input.pressed('up')) { this.focusChoice(this.choiceIdx - 1); this.game.audio.sfx('uiMove'); }
      if (input.pressed('down')) { this.focusChoice(this.choiceIdx + 1); this.game.audio.sfx('uiMove'); }
      if (input.pressed('confirm') || input.pressed('interact')) this.pick(this.choiceIdx);
      else if (input.pressed('back')) this.pick(this.choices.length - 1);
      return;
    }
    if (input.pressed('confirm') || input.pressed('interact') || input.pressed('jump')) this.advance();
  }

  close(runEnd) {
    const g = this.game;
    this.active = false;
    this.root?.remove();
    this.root = null;
    this.choiceEls = null;
    if (this.npc) this.npc.talking = false;
    g.player.model.lookTarget = null;
    const cb = this.onEnd;
    this.onEnd = null;
    if (g.state === 'dialogue') g.setState('playing');
    if (runEnd && cb) cb();
  }
}
