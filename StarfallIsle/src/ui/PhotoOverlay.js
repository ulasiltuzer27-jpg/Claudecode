import { el, esc } from './UI.js';
import { t } from '../core/i18n.js';

export class PhotoOverlay {
  constructor(ui) {
    this.ui = ui;
    this.root = el('div', 'photo-ui hidden');
    this.flashEl = el('div', 'flash');
    ui.root.appendChild(this.root);
    ui.root.appendChild(this.flashEl);
  }

  show(on, filterName) {
    this.root.classList.toggle('hidden', !on);
    if (!on) return;
    const g = this.ui.game.input;
    const k = (a) => `<span class="key ${g.device === 'pad' ? 'pad' : ''}">${esc(g.glyph(a))}</span>`;
    this.root.innerHTML = `
      <div class="ph-top">📷 ${esc(t('photo.title'))} · ${esc(filterName || '')}</div>
      <div class="ph-help">
        <span>${k('shot')} ${esc(t('photo.shoot'))}</span>
        <span>${k('filter')} ${esc(t('photo.filter'))}</span>
        <span>${k('hide')} ${esc(t('photo.hide'))}</span>
        <span>${k('back')} ${esc(t('photo.exit'))}</span>
      </div>`;
  }

  flash() {
    this.flashEl.classList.add('on');
    requestAnimationFrame(() => requestAnimationFrame(() => this.flashEl.classList.remove('on')));
  }
}
