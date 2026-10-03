// Jenerik: final sonrasi kayan yazilar. Atlanabilir.
import { el, esc } from './UI.js';
import { t } from '../core/i18n.js';

export class Credits {
  constructor(ui) {
    this.ui = ui;
    this.active = false;
  }

  show(onDone) {
    this.active = true;
    this.onDone = onDone;
    this.t = 0;
    const sections = t('credits.sections');
    const root = el('div', 'credits');
    let html = `<div class="roll"><h1>${esc(t('game.title'))}</h1><p>${esc(t('credits.thanks'))}</p>`;
    for (const s of sections) {
      const [head, ...rows] = s.split('|');
      html += `<h3>${esc(head)}</h3>${rows.map((r) => `<p>${esc(r)}</p>`).join('')}`;
    }
    html += `<h3>&nbsp;</h3><p>★</p><p>${esc(t('credits.end'))}</p></div><div class="skip">${esc(t('credits.skip'))}</div>`;
    root.innerHTML = html;
    this.ui.root.appendChild(root);
    this.root = root;
    this.roll = root.querySelector('.roll');
    this.ui.game.renderer.fade = 0;
    const tick = (now) => {
      if (!this.active) return;
      if (!this.last) this.last = now;
      const dt = Math.min(0.05, (now - this.last) / 1000);
      this.last = now;
      this.t += dt;
      const h = this.roll.offsetHeight;
      const y = window.innerHeight - this.t * 52;
      this.roll.style.transform = `translateY(${y - window.innerHeight}px)`;
      if (y < -h) this.finish();
      else requestAnimationFrame(tick);
    };
    this.last = 0;
    requestAnimationFrame(tick);
  }

  handle(input) {
    if (this.t > 1.5 && (input.pressed('confirm') || input.pressed('back') || input.pressed('jump'))) this.finish();
  }

  finish() {
    if (!this.active) return;
    this.active = false;
    this.root.remove();
    const cb = this.onDone;
    this.onDone = null;
    cb?.();
  }
}
