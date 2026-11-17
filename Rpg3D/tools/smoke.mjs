#!/usr/bin/env node
// Uçtan uca smoke test: derlenmiş oyunu headless Chromium'da açar ve
// GERÇEK klavye/fare olaylarıyla sürer. Oyun içi kısayol yalnızca iki yerde:
// ışınlanma ve "en yakın düşmanı öldür" (test dakikalarca sürmesin diye).
//
//   npm run build && node tools/smoke.mjs
//
// Ekran görüntüleri: capture/*.png. Çıkış kodu: hata varsa 1.
// Önizleme sunucusunu kendisi başlatır; SMOKE_URL verilirse onu kullanır.
//
// Yazılım GPU'su (SwiftShader) bu sahneyi saniyede 1-3 karede çiziyor.
// Bu yüzden: düşük kalite, gölgesiz, ve ?maxdt=0.25 (kare başına en fazla
// 250 ms oyun zamanı; normal oyunda 50 ms). Adımlar sabit süre beklemez,
// koşul gerçekleşene kadar yoklar.

import { spawn } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const CAPTURE = join(ROOT, 'capture');
mkdirSync(CAPTURE, { recursive: true });
const QUERY = process.env.SMOKE_QUERY ?? '?quality=low&shadows=0&maxdt=0.25';

let failures = 0;
let passed = 0;
function check(cond, msg, extra = '') {
  if (cond) { passed++; console.log(`  ok    ${msg}`); }
  else { failures++; console.log(`  HATA  ${msg}${extra ? `  (${extra})` : ''}`); }
  return cond;
}

async function startServer() {
  if (process.env.SMOKE_URL) return { url: process.env.SMOKE_URL, stop: () => {} };
  const port = 4300 + Math.floor(Math.random() * 500);
  const proc = spawn(process.execPath, [join(ROOT, 'node_modules/vite/bin/vite.js'), 'preview', '--port', String(port), '--strictPort'], { cwd: ROOT, stdio: 'pipe' });
  const url = `http://127.0.0.1:${port}/`;
  for (let i = 0; i < 60; i++) {
    try { if ((await fetch(url)).ok) return { url, stop: () => proc.kill() }; } catch { /* henüz değil */ }
    await new Promise((r) => setTimeout(r, 250));
  }
  proc.kill();
  throw new Error('vite preview başlamadı (önce `npm run build`?)');
}

const server = await startServer();
const browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required'] });
const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
const errors = [];
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', (e) => errors.push(e.message));

const G = (fn, arg) => page.evaluate(fn, arg);
const wait = (ms) => page.waitForTimeout(ms);
const P = () => G(() => window.__game.player());

/** Koşul doğru olana kadar yokla. Döner: son değer (ya da false). */
async function until(fn, timeout = 20000, every = 150) {
  const end = Date.now() + timeout;
  let v;
  while (Date.now() < end) {
    v = await fn();
    if (v) return v;
    await wait(every);
  }
  return v ?? false;
}

/** En az n oyun karesi işlenene kadar bekle (ışınlanma sonrası vb.). */
async function frames(n = 3) {
  const f0 = await G(() => window.__game.frame);
  await until(async () => (await G(() => window.__game.frame)) >= f0 + n, 30000, 50);
}

async function shot(name) {
  await G(() => window.__game?.freeze?.(true)).catch(() => {});
  await page.screenshot({ path: join(CAPTURE, `${name}.png`), timeout: 120000 });
  await G(() => window.__game?.freeze?.(false)).catch(() => {});
}

try {
  console.log('1. Yükleme');
  await page.goto(`${server.url}${QUERY}`);
  await page.waitForFunction(() => window.__game?.ready, null, { timeout: 240000 });
  const a = await G(() => window.__game.assets());
  check(a.failures.length === 0, 'bütün model ve sesler yüklendi', a.failures.join('; '));
  check(a.clips >= 40, `animasyon klipleri: ${a.clips}`);
  check(a.sounds >= 40, `ses efektleri çözüldü: ${a.sounds}`);

  console.log('2. Menü ve ses');
  await page.click('.screen .btn');
  check(await until(() => G(() => window.__game.mode === 'menu')), 'ana menü açıldı');
  const au0 = await G(() => window.__game.audio());
  check(au0.state === 'running', `AudioContext çalışıyor (${au0.state})`);
  check(au0.music === 'menu' && au0.musicPlaying, `menü müziği çalıyor (${au0.music})`);
  await shot('01_menu');
  await page.click('text=Yeni Oyun');
  check(await until(() => G(() => window.__game.mode === 'play')), 'oyun başladı');
  // Gerçek tıklama headless'ta da pointer lock alıyor; testte fare kamerayı sürmüyor.
  await G(() => window.__game.releaseLock());
  await page.mouse.move(480, 270);
  await shot('02_village');

  console.log('3. Hareket');
  const p0 = await P();
  const anims = new Set();
  await page.keyboard.down('KeyW');
  const moved = await until(async () => {
    const p = await P();
    anims.add(p.anim);
    return Math.hypot(p.x - p0.x, p.z - p0.z) > 3 && p;
  }, 30000);
  await page.keyboard.up('KeyW');
  check(!!moved, 'W ile ileri yürüdü', moved ? '' : JSON.stringify(await P()));
  check(anims.has('Walking_A'), 'yürüme klibi oynadı', [...anims].join(','));
  const p1 = await P();
  check(Math.abs(p1.y - p1.ground) < 0.05, 'ayak zeminde', `y=${p1.y.toFixed(2)} zemin=${p1.ground.toFixed(2)}`);
  check(p1.debug.steps > 0, `ayak sesi tetiklendi (${p1.debug.steps})`);
  await page.keyboard.down('ShiftLeft');
  await page.keyboard.down('KeyW');
  check(await until(async () => (await P()).anim === 'Running_A', 20000), 'Shift ile koştu (Running_A)');
  await page.keyboard.up('KeyW');
  await page.keyboard.up('ShiftLeft');
  await until(async () => (await P()).anim === 'Idle', 10000);
  await page.keyboard.press('Space');
  check(await until(async () => { const p = await P(); return !p.grounded && p.y > p.ground + 0.2; }, 10000, 50), 'Space ile zıpladı');
  check(await until(async () => (await P()).grounded, 15000), 'yere indi');

  console.log('4. Saldırı, büyü, yuvarlanma');
  await until(async () => (await P()).state === 'move', 5000);
  await page.mouse.click(480, 270);
  check(await until(async () => (await P()).anim.startsWith('1H_Melee_Attack'), 10000, 50), 'sol tık ile kılıç savurdu');
  await until(async () => (await P()).state === 'move', 10000);
  const manaBefore = (await P()).mana;
  await page.keyboard.press('KeyQ');
  check(await until(async () => (await P()).debug.casts === 1, 15000), 'Q ile ateş topu atıldı');
  check((await P()).mana < manaBefore, 'mana harcandı');
  await until(async () => (await P()).state === 'move', 10000);
  await page.keyboard.press('KeyC');
  check(await until(async () => (await P()).debug.dodges === 1, 10000, 50), 'C ile yuvarlandı');
  await until(async () => (await P()).state === 'move', 10000);

  console.log('5. Diyalog ve görev');
  // Bilge Ozan (-4.5, 5.5); etkileşim menzili 3.4 birim.
  await G(() => window.__game.teleport(-4.5, 8.2));
  await frames();
  await G(() => window.__game.setCamera(0));
  await until(async () => (await P()).state === 'move', 5000);
  await wait(400);
  await page.keyboard.press('KeyE');
  check(await until(() => G(() => window.__game.panel() === 'dialog')), 'E ile Bilge Ozan\'la konuşuldu');
  await shot('03_dialog');
  await page.keyboard.press('Digit1');
  check(await until(async () => (await G(() => window.__game.quests())).q1_bones.status === 'active'), 'görev kabul edildi (1 tuşu)');

  console.log('6. Düşmanlar');
  // Doğu kampı (72,18): yerde yatan iskeletler yaklaşınca kalkmalı.
  await G(() => window.__game.teleport(64, 18));
  await frames();
  await G(() => window.__game.setCamera(-Math.PI / 2, 0.35, 9));
  const camp = async () => (await G(() => window.__game.enemies())).filter((e) => Math.hypot(e.x - 72, e.z - 18) < 16);
  check((await camp()).length === 4, `doğu kampında 4 iskelet (${(await camp()).length})`);
  check(await until(async () => (await camp()).some((e) => ['awaken', 'alert', 'chase', 'attack'].includes(e.state)), 20000), 'yerdeki iskeletler uyandı');
  check(await until(async () => (await camp()).some((e) => e.state === 'chase' || e.state === 'attack'), 30000), 'iskeletler oyuncuya saldırıya geçti');
  await shot('04_combat');
  check(await until(async () => (await P()).hp < 100, 40000), 'iskelet oyuncuyu vurdu', `can=${(await P()).hp}`);
  // Gerçek kılıç vuruşu (yumuşak hedef kilidi en yakın iskelete döndürür).
  const hit = await until(async () => {
    await page.mouse.click(480, 270);
    return (await P()).debug.hits > 0;
  }, 40000, 400);
  check(hit, `kılıç iskelete isabet etti (${(await P()).debug.hits})`);
  await shot('05_fight');
  const xp0 = (await P()).xp;
  const lvl0 = (await P()).level;
  const gold0 = (await P()).gold;
  for (let k = 0; k < 5; k++) { await G(() => window.__game.killNearest()); await wait(300); }
  const q1 = await until(async () => { const q = (await G(() => window.__game.quests())).q1_bones; return q.status === 'complete' && q; }, 15000);
  check(!!q1, 'görev hedefi tamamlandı (5 iskelet)', JSON.stringify((await G(() => window.__game.quests())).q1_bones));
  const pl = await P();
  check(pl.xp > xp0 || pl.level > lvl0, `XP kazanıldı (seviye ${pl.level}, xp ${pl.xp})`);
  check(await until(async () => (await P()).gold > gold0, 40000), `ganimet altını kendiliğinden toplandı (${gold0} -> ${(await P()).gold})`);

  console.log('7. Envanter');
  await page.keyboard.press('KeyI');
  check(await until(() => G(() => window.__game.panel() === 'inventory')), 'I ile envanter açıldı');
  await shot('06_inventory');
  await page.keyboard.press('Escape');
  check(await until(() => G(() => window.__game.panel() === '')), 'Esc ile kapandı');

  console.log('8. Teslim ve kayıt');
  await G(() => window.__game.teleport(-4.5, 8.2));
  await frames();
  await G(() => window.__game.setCamera(0));
  await wait(500);
  await page.keyboard.press('KeyE');
  check(await until(() => G(() => window.__game.panel() === 'dialog')), 'teslim için konuşuldu');
  await page.keyboard.press('Digit1');
  const turned = await until(async () => { const q = await G(() => window.__game.quests()); return q.q1_bones.status === 'done' && q.q2_amulet.status === 'available'; });
  check(turned, 'görev teslim edildi, sonraki görev açıldı');
  const audioStats = await G(() => window.__game.audio());
  check(audioStats.played > 15, `ses efektleri çalındı (${audioStats.played})`, JSON.stringify(audioStats.byId));
  console.log(`  bilgi çalınan sesler: ${Object.entries(audioStats.byId).map(([k, v]) => `${k}×${v}`).join(' ')}`);
  const before = await P();
  check(await G(() => window.__game.save()), 'kaydedildi');
  await page.reload();
  await page.waitForFunction(() => window.__game?.ready, null, { timeout: 240000 });
  await G(() => window.__game.start());
  await until(() => G(() => window.__game.mode === 'menu'));
  check(await G(() => window.__game.continueGame()), 'kayıt yüklendi');
  const after = await P();
  const qs2 = await G(() => window.__game.quests());
  check(after.level === before.level && Math.abs(after.xp - before.xp) < 0.01 && after.gold === before.gold,
    'seviye/XP/altın korundu', `${before.level}/${before.xp}/${before.gold} -> ${after.level}/${after.xp}/${after.gold}`);
  check(qs2.q1_bones.status === 'done' && qs2.q2_amulet.status === 'available', 'görev durumu korundu');
  check(Math.hypot(after.x - before.x, after.z - before.z) < 0.5, 'konum korundu');
  await G(() => window.__game.releaseLock());

  console.log('9. Tılsım sandığı (2. görev)');
  await G(() => window.__game.teleport(-4.5, 8.2));
  await frames();
  await wait(400);
  await page.keyboard.press('KeyE');
  await until(() => G(() => window.__game.panel() === 'dialog'));
  await page.keyboard.press('Digit1');
  check(await until(async () => (await G(() => window.__game.quests())).q2_amulet.status === 'active'), '2. görev kabul edildi');
  // Kuzey ormanı kampı (-38,-98), sandık kampın 3 birim kuzeydoğusunda.
  await G(() => window.__game.teleport(-33, -99.5));
  await frames();
  await G(() => window.__game.setCamera(Math.PI * 0.75, 0.4, 8));
  await wait(400);
  await page.keyboard.press('KeyE');
  check(await until(async () => (await G(() => window.__game.world())).chests.find((c) => c.id === 'amulet_chest').opened, 10000), 'E ile sandık açıldı');
  check(await until(async () => (await G(() => window.__game.count('amulet_elder'))) === 1, 30000), 'tılsım çantaya girdi');
  check(await until(async () => (await G(() => window.__game.quests())).q2_amulet.status === 'complete', 10000), '2. görev hedefi tamamlandı');
  await shot('09_chest');

  console.log('10. Dükkân');
  await G(() => window.__game.teleport(17.5, 11.2));
  await frames();
  await G(() => window.__game.setCamera(Math.PI, 0.3, 8));
  await wait(500);
  await page.keyboard.press('KeyE');
  await until(() => G(() => window.__game.panel() === 'dialog'));
  await page.keyboard.press('Digit1');
  check(await until(() => G(() => window.__game.panel() === 'shop')), 'tüccarla alışveriş paneli açıldı');
  const goldShop = (await P()).gold;
  const pots = await G(() => window.__game.count('health_potion'));
  await page.click('.shop .grid .slot >> nth=0');
  check(await until(async () => (await G(() => window.__game.count('health_potion'))) === pots + 1), 'can iksiri satın alındı');
  check((await P()).gold === goldShop - 24, `altın düştü (${goldShop} -> ${(await P()).gold})`);
  await shot('10_shop');
  await page.keyboard.press('Escape');
  await until(() => G(() => window.__game.panel() === ''));

  console.log('11. Ölüm ve yeniden doğma');
  const goldDeath = (await P()).gold;
  await G(() => window.__game.damagePlayer(9999));
  check(await until(() => G(() => window.__game.mode === 'dead'), 20000), 'oyuncu öldü, ölüm ekranı');
  await shot('11_death');
  await page.click('text=Yeniden Doğ');
  check(await until(() => G(() => window.__game.mode === 'play')), 'yeniden doğdu');
  const re = await P();
  check(re.hp === (await G(() => window.__game.player())).hp && re.hp > 100 && re.state === 'move', `can doldu (${re.hp})`);
  check(re.gold === goldDeath - Math.floor(goldDeath * 0.1), `altının %10'u kaybedildi (${goldDeath} -> ${re.gold})`);
  await G(() => window.__game.releaseLock());

  console.log('12. Harabe ve boss');
  await G(() => window.__game.teleport(102, -110));
  await frames();
  await G(() => window.__game.setCamera(Math.PI * 0.8, 0.3, 11));
  check(!!(await G(() => window.__game.enemies())).find((e) => e.type === 'bone_lord'), 'Kemik Lordu harabede');
  const mus = await until(async () => { const m = (await G(() => window.__game.audio())).music; return ['boss', 'combat', 'ruins'].includes(m) && m; }, 20000);
  check(!!mus, `harabe/savaş müziğine geçti (${mus || (await G(() => window.__game.audio())).music})`);
  await wait(1500);
  await shot('07_ruins');
  // Avlunun ortasına: boss uyanır, boss barı ve boss müziği gelir.
  await G(() => window.__game.teleport(112, -122));
  await frames();
  await G(() => window.__game.setCamera(Math.PI * 0.85, 0.32, 12));
  check(await until(async () => (await G(() => window.__game.enemies())).find((e) => e.type === 'bone_lord')?.state !== 'idle', 30000), 'Kemik Lordu uyandı');
  check(await until(() => G(() => window.__game.bossBarVisible()), 20000), 'boss can barı göründü');
  check(await until(async () => (await G(() => window.__game.audio())).music === 'boss', 20000), 'boss müziği çalıyor');
  await wait(1500);
  await shot('12_boss');

  console.log('13. Gece');
  await G(() => window.__game.teleport(4, 18));
  await frames();
  await G(() => window.__game.setTime(0.93));
  await G(() => window.__game.setCamera(0.4, 0.3, 9));
  await wait(2500);
  await shot('08_night');

  check(errors.length === 0, 'konsolda hata yok', errors.slice(0, 5).join(' | '));
} catch (err) {
  failures++;
  console.log(`  HATA  beklenmeyen: ${err.message.split('\n')[0]}`);
  await shot('99_failure').catch(() => {});
} finally {
  await browser.close();
  server.stop();
}

console.log(`\n${passed} başarılı, ${failures} hata · görüntüler: capture/`);
process.exit(failures ? 1 : 0);
