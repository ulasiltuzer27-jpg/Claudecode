import './ui/styles.css';
import { Game, setGameRef } from './core/Game';

const canvas = document.getElementById('game') as HTMLCanvasElement;
const ui = document.getElementById('ui') as HTMLDivElement;
const game = new Game(canvas, ui);
setGameRef(game);
// Otomatik testlerin (tools/smoke.mjs) ve meraklıların oyuna eriştiği kapı.
(window as unknown as { __game: ReturnType<Game['debugApi']> }).__game = game.debugApi();

game.init().catch((err: Error) => {
  console.error(err);
  ui.innerHTML = `<div class="screen"><h1 class="title" style="font-size:40px">Bir şeyler ters gitti</h1><p class="credits">${err.message}</p></div>`;
});
