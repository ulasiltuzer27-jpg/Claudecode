// Giris noktasi. Yukleme sirasinda bir hata olursa oyuncu bos bir ekran
// yerine ne oldugunu gorur (Steam incelemelerinde "siyah ekran" sikayeti
// en cok bu durumdan dogar).
import { Game } from './core/Game.js';

const game = new Game();
game.init().catch((err) => {
  console.error(err);
  const box = document.querySelector('#loading .loading-text');
  if (box) {
    box.style.opacity = 1;
    box.style.color = '#ffb0b0';
    box.textContent = `Yuklenemedi / Failed to load: ${err && err.message ? err.message : err}`;
  }
});
