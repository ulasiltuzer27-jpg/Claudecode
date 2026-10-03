// Gorsel kontrol: bolgeler, gunun saatleri, adalilar, menuler ve finalden
// ekran goruntusu alir (tools/out/shots/). Konsol hatasi varsa 1 ile cikar.
//   node tools/capture.mjs            -> hepsi
//   ONLY=npc node tools/capture.mjs   -> adi "npc" iceren cekimler
import fs from 'node:fs';
import path from 'node:path';
import { startPreview, launch, waitForGame, settle, OUT, sleep } from './lib.mjs';

const DIR = path.join(OUT, 'shots');
fs.mkdirSync(DIR, { recursive: true });
const only = process.env.ONLY;
const W = +(process.env.W || 1280);
const H = +(process.env.H || 720);

const srv = await startPreview();
const { browser, page, errors } = await launch(W, H);

async function shot(name, setup, frames = 10) {
  if (only && !name.includes(only)) return;
  await page.evaluate(setup);
  await settle(page, frames);
  await page.screenshot({ path: path.join(DIR, `${name}.png`) });
  console.log('cekildi:', name);
}

// Oyuncuyu bir yere koy, kamerayi verilen yaw/pitch/mesafeyle yerlestir.
const place = (x, z, opts = {}) => `(() => {
  const g = window.__game;
  const o = ${JSON.stringify(opts)};
  const hit = g.physics.groundAt(${x}, ${z}, 200);
  const y = o.y !== undefined ? o.y : (hit ? hit.y : g.world.terrain.height(${x}, ${z}));
  g.player.spawn(${x}, y + 0.05, ${z}, o.yaw ?? 0);
  g.setHour(o.hour ?? 10);
  g.cameraRig.override = null;
  g.cameraRig.yaw = o.camYaw ?? (o.yaw ?? 0) + Math.PI;
  g.cameraRig.pitch = o.pitch ?? 0.3;
  g.cameraRig.targetDistance = g.cameraRig.distance = o.dist ?? 7;
  g.cameraRig.snap(g.player.pos);
  g.ui.hud.hint(null);
})()`;

try {
  await page.goto(`${srv.url}?test=1&fresh=1&quality=${process.env.Q || 'medium'}&lang=${process.env.LANG_UI || 'tr'}`);
  await waitForGame(page);
  await settle(page, 10);
  await shot('00_menu', () => {}, 6);
  await page.evaluate(() => window.__game.newGame(1));
  await sleep(300);

  const P = (x, z, o) => new Function(`return ${place(x, z, o)}`);
  await shot('01_start_dock', P(8, 176, { yaw: Math.PI, hour: 9 }));
  await shot('02_village', P(8, 112, { yaw: Math.PI, hour: 10.5, dist: 11, pitch: 0.35 }));
  await shot('03_village_night', P(8, 112, { yaw: 0.6, hour: 22.5, dist: 10, pitch: 0.3 }), 14);
  await shot('04_meadow', P(-52, 56, { yaw: -2.2, hour: 15, dist: 9 }));
  await shot('05_forest', P(-112, -8, { yaw: 1.5, hour: 11, dist: 8 }));
  await shot('06_lake', P(8, -2, { yaw: 1.4, hour: 17.8, dist: 9, pitch: 0.35 }));
  await shot('07_hollow', P(-76, -70, { yaw: -2.4, hour: 13, dist: 11, pitch: 0.35 }));
  await shot('08_windy', P(100, -48, { yaw: 2.6, hour: 16, dist: 11, pitch: 0.3 }));
  await shot('09_peak', P(6, -100, { yaw: Math.PI, hour: 18.6, dist: 12, pitch: 0.25 }));
  await shot('10_cove', P(128, 54, { yaw: 1.2, hour: 12, dist: 11, pitch: 0.3 }));
  // magara ici: groundAt cativa carpar, bu yuzden yer araziden alinir
  await shot('11_cave', () => {
    const g = window.__game;
    const d = g.world.anchors.get('domeInside');
    g.player.spawn(d.x - 1.2, g.world.terrain.height(d.x - 1.2, d.z) + 0.05, d.z, Math.PI / 2);
    g.setHour(22);
    g.cameraRig.override = { pos: d.clone().add({ x: -2.6, y: 0.9, z: 1.2 }), look: d.clone().add({ x: 2, y: 0, z: 0 }), speed: 100 };
    g.ui.hud.hint(null);
  }, 10);
  await page.evaluate(() => { window.__game.cameraRig.override = null; });
  await shot('12_sunrise_sea', P(-30, 140, { yaw: 2.2, hour: 6.4, dist: 8, pitch: 0.12 }));

  // adalilar: her birinin onune gecip yakin cekim
  for (const id of ['owl', 'hedgehog', 'rabbit', 'frog', 'bear', 'beaver']) {
    await shot(`20_npc_${id}`, new Function(`
      const g = window.__game;
      const n = g.npcs.byId.get('${id}');
      const fx = n.pos.x + Math.sin(n.yaw) * 2.6, fz = n.pos.z + Math.cos(n.yaw) * 2.6;
      ${place('fx', 'fz', {})};
      g.player.yaw = n.yaw + Math.PI;
      g.player.model.root.rotation.y = g.player.yaw;
      g.setHour(10.5);
      g.cameraRig.override = { pos: n.pos.clone().add({ x: Math.sin(n.yaw + 0.6) * 4.2, y: 1.6, z: Math.cos(n.yaw + 0.6) * 4.2 }), look: n.pos.clone().add({ x: 0, y: 0.7, z: 0 }), speed: 100 };
    `), 12);
  }

  // diyalog + HUD
  await shot('30_dialogue', () => {
    const g = window.__game;
    const n = g.npcs.byId.get('rabbit');
    g.cameraRig.override = null;
    g.player.spawn(n.pos.x + Math.sin(n.yaw) * 2.2, n.pos.y + 0.05, n.pos.z + Math.cos(n.yaw) * 2.2, n.yaw + Math.PI);
    g.cameraRig.yaw = n.yaw + 0.9;
    g.cameraRig.snap(g.player.pos);
    g.quests.talk(n);
    g.dialogue.finishTyping();
  }, 10);
  if (!only) await page.evaluate(() => window.__game.dialogue.close(false));

  // ucus: suzulme pozu
  await shot('31_glide', () => {
    const g = window.__game;
    g.player.spawn(20, 30, 60, Math.PI);
    g.player.gliding = true;
    g.player.vel.set(0, -2, -9);
    g.cameraRig.yaw = 0.3;
    g.cameraRig.pitch = 0.1;
    g.cameraRig.snap(g.player.pos);
    g.input.simulated.add('jump');
  }, 4);
  if (!only) await page.evaluate(() => window.__game.input.simulated.clear());

  // menuler
  await shot('40_pause', () => { const g = window.__game; g.player.spawn(8, 3.1, 104, 0); g.cameraRig.snap(g.player.pos); g.pause(); }, 6);
  await shot('41_achievements', () => { const g = window.__game; g.stats.add('jumps', 120); g.stats.max('shards_max', 7); g.ui.top().items[2].select(); }, 6);
  if (!only) await page.evaluate(() => window.__game.ui.pop());
  await shot('42_journal', () => window.__game.ui.top().items[1].select(), 6);
  if (!only) await page.evaluate(() => { const g = window.__game; g.ui.top().tab = 2; g.ui.top().refresh(); });
  await shot('43_collection', () => {}, 4);
  if (!only) await page.evaluate(() => window.__game.ui.pop());
  await shot('44_settings', () => window.__game.ui.top().items[3].select(), 6);
  if (!only) await page.evaluate(() => window.__game.resume());

  // final
  await shot('50_finale', () => {
    const g = window.__game;
    const L = g.world.anchors.get('lighthouse.lamp');
    g.player.spawn(L.x + 3, L.y - 16, L.z + 7, 0);
    g.finale.start();
    g.finale.t = 7;
    g.finale.stage = 2;
    g.finale.ignite = 1;
    g.setHour(20.6);
    g.renderer.fade = 0;
    for (let i = 0; i < 4; i++) g.finale.firework();
  }, 16);
} finally {
  if (errors.length) console.log('KONSOL HATALARI:\n' + errors.slice(0, 20).join('\n---\n'));
  await browser.close();
  srv.stop();
}
process.exit(errors.length ? 1 : 0);
