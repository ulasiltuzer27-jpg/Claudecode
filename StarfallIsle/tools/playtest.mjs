// Uctan uca oyun testi: gercek tarayicida, gercek oyun dongusuyle.
// Hareket gercek klavye olaylariyla; uzun yollar (100 kabuk gibi) oyuncuyu
// esyanin ustune isinlayarak. Sonunda 25 basarimin hepsinin acildigi ve
// kaydin yeniden yuklenince korundugu dogrulanir.
import { startPreview, launch, waitForGame, settle, sleep } from './lib.mjs';

const srv = await startPreview();
const { browser, page, errors } = await launch(640, 360);
const results = [];
let failed = 0;

function check(name, cond, detail = '') {
  results.push(`${cond ? 'OK  ' : 'FAIL'} ${name}${detail ? ` (${detail})` : ''}`);
  if (!cond) failed++;
}

const G = (fn, arg) => page.evaluate(fn, arg);
const wait = (ms) => sleep(ms);

// Oyun zamaninin `sec` saniye ilerlemesini bekle (yazilimsal GPU'da kareler
// yavas; gercek saat yerine oyun saatine bakmak testi kararli kiliyor).
async function waitGame(sec) {
  const t0 = await G(() => window.__game.time);
  await page.waitForFunction((t) => window.__game.time >= t, t0 + sec, { timeout: 120000, polling: 50 });
}

async function pressKey(code, holdMs = 80) {
  await page.keyboard.down(code);
  await wait(holdMs);
  await page.keyboard.up(code);
}

// Diyalog acikken Enter'a basarak sonuna kadar ilerle; secenek varsa sec.
async function runDialogue(choiceIndex = 0, maxSteps = 40) {
  for (let i = 0; i < maxSteps; i++) {
    const st = await G(() => {
      const d = window.__game.dialogue;
      return { active: d.active, choices: !!(d.choiceEls && d.choiceEls.length), typing: d.typing };
    });
    if (!st.active) return;
    if (st.choices && !st.typing) {
      await G((i2) => window.__game.dialogue.pick(i2), choiceIndex);
    } else {
      await G(() => window.__game.dialogue.advance());
    }
    await wait(60);
  }
}

async function teleport(x, y, z, yaw = 0) {
  await G(({ x: a, y: b, z: c, yaw: w }) => {
    const g = window.__game;
    let yy = b;
    if (yy === null) {
      const hit = g.physics.groundAt(a, c, 200);
      yy = (hit ? hit.y : g.world.terrain.height(a, c)) + 0.05;
    }
    g.player.spawn(a, yy, c, w);
    g.cameraRig.snap(g.player.pos, w + Math.PI);
  }, { x, y, z, yaw });
  await settle(page, 3);
}

async function talk(id, choice = 0) {
  await G((i) => {
    const g = window.__game;
    const n = g.npcs.byId.get(i);
    g.player.spawn(n.pos.x + Math.sin(n.yaw) * 1.8, n.pos.y + 0.05, n.pos.z + Math.cos(n.yaw) * 1.8, n.yaw + Math.PI);
  }, id);
  await settle(page, 3);
  await pressKey('KeyE');
  await settle(page, 2);
  await runDialogue(choice);
}

try {
  await page.goto(`${srv.url}?test=1&fresh=1&quality=low`);
  await waitForGame(page);
  await G(() => window.__game.newGame(1));
  await settle(page, 10);

  // --- hareket
  const p0 = await G(() => window.__game.player.pos.toArray());
  await page.keyboard.down('KeyW');
  await waitGame(2);
  await page.keyboard.up('KeyW');
  await settle(page, 10);
  const p1 = await G(() => window.__game.player.pos.toArray());
  const moved = Math.hypot(p1[0] - p0[0], p1[2] - p0[2]);
  check('W ile yurume', moved > 4, `${moved.toFixed(1)} m`);
  check('iskelede kaldi (dusmedi)', p1[1] > 0.9, `y=${p1[1].toFixed(2)}`);

  // --- ziplama: tepe yuksekligi
  await settle(page, 5);
  const yBefore = await G(() => window.__game.player.pos.y);
  await G(() => { window.__game.peakY = -1e9; });
  await pressKey('Space', 60);
  await waitGame(1.2);
  const peak = await G(() => window.__game.peakY);
  check('ziplama yuksekligi ~1.8 m', peak - yBefore > 1.3 && peak - yBefore < 2.3, `${(peak - yBefore).toFixed(2)} m`);
  check('ziplama istatistigi', (await G(() => window.__game.stats.get('jumps'))) >= 1);

  // --- baykus: giris + ilk tuy + zirveye ucus
  await talk('owl');
  await wait(200);
  await runDialogue();
  const owl = await G(() => {
    const g = window.__game;
    return { intro: g.save.flags.owlIntro, feathers: g.progress.featherCount(), owlZ: g.npcs.byId.get('owl').pos.z };
  });
  check('baykus girisi', owl.intro === true);
  check('baykus tuyu verdi', owl.feathers === 1, `tuy=${owl.feathers}`);
  check('baykus zirveye uctu', owl.owlZ < -90, `z=${owl.owlZ.toFixed(1)}`);

  // --- kanat cirpma
  await teleport(-58, null, 50);
  await settle(page, 5);
  await pressKey('Space', 60);
  await waitGame(0.25);
  await pressKey('Space', 60);
  await waitGame(0.1);
  check('havada kanat cirpma', (await G(() => window.__game.player.flapsUsed)) === 1);
  await waitGame(1.5);

  // --- suzulme + havada kalma: yuksekten bas tusu basili
  await teleport(40, 60, 150, Math.PI);
  await page.keyboard.down('Space');
  await page.keyboard.down('KeyW');
  await waitGame(0.6);
  const gliding = await G(() => window.__game.player.gliding);
  await waitGame(12);
  await page.keyboard.up('KeyW');
  await page.keyboard.up('Space');
  await wait(400);
  check('suzulme basladi', gliding === true);
  const air = await G(() => ({ glide: window.__game.stats.get('glide_max'), air: window.__game.stats.get('air_max') }));
  check('suzulme mesafesi >= 60 m', air.glide >= 60, `${air.glide} m`);
  check('havada kalma >= 10 s', air.air >= 10, `${air.air} s`);

  // --- yuzme (gercek), sonra kalan metreler istatistikle
  await teleport(0, 0, 200, 0);
  await page.keyboard.down('KeyW');
  await page.keyboard.down('ShiftLeft');
  await waitGame(3);
  await page.keyboard.up('ShiftLeft');
  await page.keyboard.up('KeyW');
  const swim = await G(() => ({ s: window.__game.player.swimming, m: window.__game.stats.get('swim_m') }));
  check('suya girince yuzuyor', swim.s === true);
  check('yuzme istatistigi artiyor', swim.m >= 5, `${swim.m} m`);

  // --- toplanabilirler: her birinin ustune isinlan
  const items = await G(() => window.__game.collectibles.items.filter((i) => !i.taken).map((i) => ({ id: i.id, kind: i.kind, p: i.pos.toArray() })));
  for (const it of items) {
    await G(({ p }) => {
      const g = window.__game;
      g.player.spawn(p[0], p[1] - 0.6, p[2], 0);
    }, it);
    await settle(page, 2);
  }
  await settle(page, 4);
  const coll = await G(() => {
    const g = window.__game;
    return { shards: g.progress.shardCount(), feathers: g.progress.featherCount(), shells: g.save.shellsTotal, carrots: g.quests.carrots(), tools: g.quests.tools(), left: g.collectibles.items.filter((i) => !i.taken).map((i) => i.id) };
  });
  check('26 dunya parcasi toplandi', coll.shards === 26, `parca=${coll.shards}`);
  check('100 kabuk toplandi', coll.shells === 100, `kabuk=${coll.shells}`);
  check('dunyadaki 4 tuy (+baykus)', coll.feathers === 5, `tuy=${coll.feathers}`);
  check('3 havuc + 3 alet', coll.carrots === 3 && coll.tools === 3);
  check('toplanmamis esya kalmadi', coll.left.length === 0, coll.left.join(','));

  // --- gorevler
  await talk('rabbit');
  await talk('rabbit');
  await talk('beaver');
  await talk('beaver');
  const q = await G(() => ({ rabbit: window.__game.save.quests.rabbit, beaver: window.__game.save.quests.beaver, bridge: window.__game.world.bridgeBuilt }));
  check('tavsan gorevi', q.rabbit === 'done');
  check('kunduz gorevi + kopru', q.beaver === 'done' && q.bridge === true);

  // kopru yurunebilir mi: uzerine birak, altindaki zemin kopru olmali
  await wait(1500);
  const onBridge = await G(() => {
    const g = window.__game;
    const { a, b } = g.world.bridgeEnds;
    const mx = (a.x + b.x) / 2;
    const mz = (a.z + b.z) / 2;
    const hit = g.physics.groundAt(mx, mz, 30);
    return hit ? hit.y : -99;
  });
  check('kopru ortasi yurunebilir', onBridge > 0.5, `y=${onBridge.toFixed(2)}`);

  // --- dukkan: 4 urun (95 kabuk)
  for (let i = 0; i < 4; i++) {
    await talk('hedgehog', 0);
    await runDialogue();
  }
  const shop = await G(() => ({ s: window.__game.save.shells, rod: window.__game.save.flags.rod, hat: window.__game.save.flags.hat }));
  check('dukkandan hepsi alindi', shop.s === 5 && shop.rod && shop.hat, `kalan=${shop.s}`);

  // --- balik: gercek atis + vurma, sonra her tur icin yakalama
  await talk('bear');
  await G(() => window.__game.player.spawn(8, 1.3, 172, Math.PI / 2));
  await settle(page, 6);
  const canFish = await G(() => !!window.__game.fishing.canFish());
  check('iskeleden balik tutulabiliyor', canFish);
  await pressKey('KeyE');
  await settle(page, 3);
  check('olta atildi', (await G(() => window.__game.fishing.state)) === 'wait');
  await G(() => { window.__game.fishing.timer = 0; });
  await page.waitForFunction(() => window.__game.fishing.state === 'bite', null, { timeout: 60000 });
  await G(() => { window.__game.fishing.timer = 5; }); // yavas karede vurma penceresi kacmasin
  await pressKey('KeyE');
  await settle(page, 3);
  check('vurdu -> makara', (await G(() => window.__game.fishing.state)) === 'reel');
  for (const id of ['anchovy', 'seabass', 'trout', 'carp', 'moonfish', 'goldfish']) {
    await G((fid) => window.__game.testCatch(fid), id);
    await settle(page, 2);
  }
  await talk('bear');
  await talk('bear');
  const bear = await G(() => ({ q: window.__game.save.quests.bear, sp: window.__game.quests.species(), feathers: window.__game.progress.featherCount() }));
  check('6 balik turu', bear.sp === 6);
  check('ayi gorevi', bear.q === 'done');

  // --- yaris: geri sayim, sonra bayraga kos
  await talk('frog', 0);
  await page.waitForFunction(() => window.__game.race.state === 'running', null, { timeout: 120000 });
  const flag = await G(() => window.__game.world.raceFlagPos.toArray());
  await teleport(flag[0] - 1, null, flag[2] - 1);
  await page.waitForFunction(() => window.__game.dialogue.active, null, { timeout: 120000 });
  await runDialogue();
  await wait(300);
  const race = await G(() => ({ q: window.__game.save.quests.frog, stat: window.__game.stats.get('race') }));
  check('yaris kazanildi', race.q === 'done' && race.stat === 1);

  const totals = await G(() => ({ shards: window.__game.progress.shardCount(), feathers: window.__game.progress.featherCount() }));
  check('30/30 parca', totals.shards === 30, `${totals.shards}`);
  check('8/8 tuy', totals.feathers === 8, `${totals.feathers}`);

  // --- bolgeler
  for (const [x, z, y] of [[8, 112, null], [-58, 50, null], [-120, -6, null], [30, -20, null], [136, 50, null], [-86, -80, null], [104, -58, null], [6, -105, null], [-182, -108, null]]) {
    await teleport(x, y, z);
    await waitGame(0.7);
  }
  const reg = await G(() => window.__game.save.regions.slice().sort());
  check('9 bolge kesfedildi', reg.length === 9, reg.join(','));

  // --- final
  await talk('owl', 0);
  await wait(500);
  check('final basladi', (await G(() => window.__game.finale.active)) === true);
  await G(() => { const f = window.__game.finale; f.t = 15.2; f.stage = 2; f.ignite = 1; });
  await wait(2500);
  await G(() => window.__game.ui.credits.finish());
  await wait(500);
  const fin = await G(() => ({ flag: window.__game.save.flags.finale, state: window.__game.state, lit: window.__game.finale.lit }));
  check('final tamamlandi', fin.flag === true && fin.state === 'playing' && fin.lit === true, JSON.stringify(fin));

  // --- foto, gece, kalan sayaclar
  await pressKey('KeyP');
  await settle(page, 3);
  await pressKey('Enter');
  await settle(page, 3);
  await pressKey('Escape');
  await settle(page, 3);
  await G(() => window.__game.setHour(0.3));
  await waitGame(1.2);
  await G(() => {
    const g = window.__game;
    g.stats.add('jumps', Math.max(0, 500 - g.stats.get('jumps')));
    g.stats.add('swim_m', Math.max(0, 200 - g.stats.get('swim_m')));
    g.progress.refreshStats();
  });
  await wait(500);
  const ach = await G(() => {
    const g = window.__game;
    return { n: g.achievements.count(), list: g.achievementsMissing() };
  });
  check('25/25 basarim', ach.n === 25, `eksik: ${ach.list.join(',')}`);
  check('tamamlanma %100', (await G(() => window.__game.progress.completion())) === 100);

  // --- kalicilik: kaydet, sayfayi yeniden yukle, yuvayi ac
  await G(() => window.__game.toMainMenu());
  await wait(800);
  await page.goto(`${srv.url}?test=1&quality=low`);
  await waitForGame(page);
  await G(() => window.__game.loadSlot(1));
  await settle(page, 6);
  const after = await G(() => ({ shards: window.__game.progress.shardCount(), ach: window.__game.achievements.count(), finale: window.__game.save.flags.finale, bridge: window.__game.world.bridgeBuilt, lit: window.__game.finale.lit }));
  check('yuklemede ilerleme korundu', after.shards === 30 && after.finale && after.bridge && after.lit, JSON.stringify(after));
  check('yuklemede basarimlar korundu', after.ach === 25);
} catch (err) {
  failed++;
  results.push(`FAIL istisna: ${err.stack || err}`);
} finally {
  console.log(results.join('\n'));
  if (errors.length) {
    failed++;
    console.log('KONSOL HATALARI:\n' + errors.slice(0, 20).join('\n---\n'));
  }
  console.log(failed ? `\n${failed} denetim basarisiz` : `\nHepsi gecti (${results.length} denetim)`);
  await browser.close();
  srv.stop();
}
process.exit(failed ? 1 : 0);
