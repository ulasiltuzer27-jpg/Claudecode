// Hizli duman testi: oyun aciliyor mu, konsolda hata var mi, yeni oyun baslar mi.
import fs from 'node:fs';
import path from 'node:path';
import { startPreview, launch, waitForGame, settle, OUT, sleep } from './lib.mjs';

fs.mkdirSync(OUT, { recursive: true });
const srv = await startPreview();
const { browser, page, errors } = await launch();
let ok = false;
try {
  const t0 = Date.now();
  await page.goto(`${srv.url}?test=1&fresh=1&quality=${process.env.Q || 'low'}`);
  await waitForGame(page);
  console.log(`menu hazir: ${((Date.now() - t0) / 1000).toFixed(1)} s`);
  await settle(page, 8);
  await page.screenshot({ path: path.join(OUT, 'smoke_menu.png') });
  await page.evaluate(() => window.__game.newGame(1));
  await sleep(500);
  await settle(page, 20);
  await page.screenshot({ path: path.join(OUT, 'smoke_play.png') });
  const info = await page.evaluate(() => {
    const g = window.__game;
    return { state: g.state, pos: g.player.pos.toArray().map((v) => +v.toFixed(2)), fps: g.fps, trees: g.world.treeCount, grounded: g.player.grounded };
  });
  console.log(JSON.stringify(info));
  ok = errors.length === 0;
} finally {
  if (errors.length) console.log('KONSOL HATALARI:\n' + errors.slice(0, 20).join('\n---\n'));
  await browser.close();
  srv.stop();
}
process.exit(ok ? 0 : 1);
