// Statik dogrulama: oyunu acmadan, node'da saniyeler icinde.
// Oyunun kendi modullerini (arazi, yerlesim, fizik, yapilar) calistirir ve
// tasarim kararlarinin tuttugunu olcer:
//  - TR/EN ceviri anahtarlari esit ve koddaki her anahtar var
//  - 25 basarimin semasi, istatistikleri koda bagli, esikler ulasilabilir
//  - Rapier heightfield yonelimi arazi ile ayni
//  - ziplama/cirpma fizigi teras yuksekliklerine uygun (1 / 2 / 4 tuy)
//  - her yildiz/tuy/kabuk carpisma icinde degil, zemine yakin, su ustunde
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from 'three';
import { ROOT } from './lib.mjs';
import { Terrain, LAYOUT, BOUNDARY_RADIUS } from '../src/world/Terrain.js';
import * as WD from '../src/world/WorldData.js';
import { Physics, initPhysics } from '../src/physics/Physics.js';
import { World } from '../src/world/World.js';
import { QUALITY } from '../src/render/Renderer.js';
import { MOVE } from '../src/gameplay/Player.js';
import { ICON_GLYPHS } from '../src/achievements/iconArt.js';

let fails = 0;
let checks = 0;
const ok = (name, cond, detail = '') => {
  checks++;
  if (!cond) fails++;
  if (!cond || process.env.VERBOSE) console.log(`${cond ? 'OK  ' : 'FAIL'} ${name}${detail ? ` — ${detail}` : ''}`);
};
const read = (p) => fs.readFileSync(path.join(ROOT, p), 'utf8');
const json = (p) => JSON.parse(read(p));

// ---------- ceviriler ----------
const tr = json('src/i18n/tr.json');
const en = json('src/i18n/en.json');
const trKeys = Object.keys(tr);
const enKeys = Object.keys(en);
ok('TR ve EN ayni anahtarlar', trKeys.length === enKeys.length && trKeys.every((k) => k in en),
  [...trKeys.filter((k) => !(k in en)), ...enKeys.filter((k) => !(k in tr))].join(', '));
for (const k of trKeys) {
  if (Array.isArray(tr[k]) !== Array.isArray(en[k])) ok(`dizi/metin uyumu: ${k}`, false);
  const ph = (v) => [...JSON.stringify(v).matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',');
  if (ph(tr[k]) !== ph(en[k])) ok(`yer tutucular ayni: ${k}`, false, `${ph(tr[k])} / ${ph(en[k])}`);
}

function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((d) => (d.isDirectory() ? walk(path.join(dir, d.name)) : [path.join(dir, d.name)]));
}
const srcFiles = walk(path.join(ROOT, 'src')).filter((f) => f.endsWith('.js'));
const code = srcFiles.map((f) => fs.readFileSync(f, 'utf8')).join('\n');
const used = new Set();
for (const m of code.matchAll(/\bt\(\s*'([a-zA-Z0-9_.]+)'/g)) used.add(m[1]);
for (const m of code.matchAll(/(?:say|ask)\(\s*'([a-zA-Z0-9_.]+)'/g)) used.add(m[1]);
for (const m of code.matchAll(/'((?:dlg|hint|journal|settings|shop|sign|photo|toast|quest|race|region|choice|fish|ui|menu)\.[a-zA-Z0-9_.]+)'/g)) used.add(m[1]);
// sablonla uretilen anahtarlar
const achData = json('src/achievements/achievements.json');
for (const a of achData.achievements) { used.add(`ach.${a.id}.name`); used.add(`ach.${a.id}.desc`); }
for (const r of WD.REGIONS) { used.add(`region.${r.id}`); used.add(`region.${r.id}.sub`); }
for (const id of Object.keys(WD.NPCS).filter((k) => k !== 'owlPeak')) used.add(`npc.${id}`);
for (const f of WD.FISH) { used.add(`fish.${f.id}`); used.add(`fish.hint.${f.id}`); }
for (const s of WD.SHOP_ITEMS) { used.add(`shop.${s.id}`); used.add(`dlg.hedgehog.bought.${s.id}`); }
for (const c of [...WD.CARROTS, ...WD.TOOLS]) used.add(`toast.${c.id}`);
for (const q of ['rabbit', 'beaver', 'frog', 'bear']) for (const s of ['title', 'desc', 'done']) used.add(`quest.${q}.${s}`);
for (const f of ['none', 'warm', 'sepia', 'mono', 'dream', 'poster']) used.add(`photo.filter.${f}`);
for (const l of ['engine', 'terrain', 'layout', 'mesh', 'water', 'village', 'landmarks', 'trees', 'details', 'done']) used.add(`loading.${l}`);
WD.VILLAGE.signs.forEach((_, i) => used.add(`sign.${i}`));
const missing = [...used].filter((k) => !(k in tr) && !k.endsWith('.'));
ok(`koddaki ${used.size} ceviri anahtarinin hepsi var`, missing.length === 0, missing.join(', '));

// ---------- basarimlar ----------
const stats = new Set(achData.stats.map((s) => s.key));
ok('25 basarim', achData.achievements.length === 25, `${achData.achievements.length}`);
const ids = achData.achievements.map((a) => a.id);
ok('basarim kimlikleri benzersiz ve Steam API bicimi', new Set(ids).size === ids.length && ids.every((i) => /^ACH_[A-Z0-9_]+$/.test(i)));
for (const a of achData.achievements) {
  ok(`${a.id}: istatistik tanimli`, stats.has(a.stat), a.stat);
  ok(`${a.id}: esik pozitif tamsayi`, Number.isInteger(a.threshold) && a.threshold > 0);
  ok(`${a.id}: ikon sembolu var`, ICON_GLYPHS.includes(a.icon.glyph), a.icon.glyph);
}
for (const s of stats) {
  const re = new RegExp(`stats\\.(add|max)\\(\\s*'${s}'`);
  ok(`istatistik kodda guncelleniyor: ${s}`, re.test(code));
}
const maxOf = (stat) => ({
  shards_max: WD.SHARDS.length + WD.QUEST_SHARDS.length,
  feathers_max: WD.FEATHERS.length + 4,
  shells_max: WD.SHELL_GROUPS.length * 5,
  species_max: WD.FISH.length,
  regions_max: WD.MAIN_REGIONS.length,
  quests_max: 4,
  npcs_max: Object.keys(WD.NPCS).filter((k) => k !== 'owlPeak').length,
  finale: 1, peak: 1, race: 1, cave: 1, night: 1, speedrun: 1, completion: 100,
}[stat]);
for (const a of achData.achievements) {
  const m = maxOf(a.stat);
  if (m !== undefined) ok(`${a.id}: esik icerikle ulasilabilir`, a.threshold <= m, `${a.threshold} <= ${m}`);
}
ok('30 yildiz parcasi', WD.SHARDS.length + WD.QUEST_SHARDS.length === WD.SHARD_TOTAL);
ok('8 altin tuy', WD.FEATHERS.length + 4 === WD.FEATHER_TOTAL);
ok('kimlikler benzersiz', new Set([...WD.SHARDS, ...WD.FEATHERS].map((s) => s.id)).size === WD.SHARDS.length + WD.FEATHERS.length);

// ---------- arazi ----------
const T1 = new Terrain(1337);
const T2 = new Terrain(1337);
let same = true;
for (let i = 0; i < T1.heights.length; i += 97) if (T1.heights[i] !== T2.heights[i]) { same = false; break; }
ok('dunya uretimi deterministik', same);
const T = T1;

// ---------- fizik + yapilar ----------
await initPhysics();
const physics = new Physics();
const fakeGame = { renderer: { scene: new THREE.Scene(), q: QUALITY.low }, physics };
fakeGame.world = new World(fakeGame);
await fakeGame.world.build(() => {});
const W = fakeGame.world;

let worst = 0;
for (const [x, z] of [[0, 0], [37.3, -12.9], [-120.5, 33.1], [6, -112], [104, -58], [-150.2, 80.7], [88, 120]]) {
  const hit = physics.groundAt(x, z, 400);
  // yalniz arazinin oldugu yerlerde karsilastir (yapi ustune dusmesin)
  const gh = T.height(x, z);
  if (hit && Math.abs(hit.y - gh) < 3) worst = Math.max(worst, Math.abs(hit.y - gh));
}
ok('Rapier heightfield yonelimi arazi ile ayni', worst < 0.12, `en buyuk fark ${worst.toFixed(3)} m`);

// ---------- ziplama fizigi vs teraslar ----------
const g = -MOVE.gravity;
const apex = (n) => MOVE.jumpVel ** 2 / (2 * g) + n * (MOVE.flapVel ** 2 / (2 * g));
const need = (h) => { let n = 0; while (apex(n) < h + 0.3) n++; return n; };
ok('zirve terasi 1: 1 tuy', need(LAYOUT.peakCliff1) === 1, `apex0=${apex(0).toFixed(2)} cliff=${LAYOUT.peakCliff1}`);
ok('ruzgarli kayaliklar: 2 tuy', need(LAYOUT.windy.cliff) === 2, `apex1=${apex(1).toFixed(2)} cliff=${LAYOUT.windy.cliff}`);
ok('fener zirvesi: 4 tuy', need(LAYOUT.peakCliff2) === 4, `apex3=${apex(3).toFixed(2)} apex4=${apex(4).toFixed(2)} cliff=${LAYOUT.peakCliff2}`);
// tuy ilerleyisi: baykus(1) + cayir kutugu f1 -> 2 => kayaliklar f4 -> 3; vadi f3 mantarla -> 4 => zirve
ok('tuy ilerleyisi zirveye yetiyor', ['f1', 'f3', 'f4'].every((id) => WD.FEATHERS.some((f) => f.id === id)) && 1 + 3 >= need(LAYOUT.peakCliff2));
const bounceApex = MOVE.bounceVel ** 2 / (2 * g);
ok('mantar ziplamasi yuksek esyalara yetiyor', bounceApex > 4.4, `${bounceApex.toFixed(2)} m`);

// ---------- toplanabilirlerin yerlesimi ----------
const { Collectibles } = await import('../src/gameplay/Collectibles.js');
const col = Object.create(Collectibles.prototype);
col.game = fakeGame;
const placed = [
  // Zemin bosluğu: tek ziplama + bir cirpma (3.24 m) icinde olmali; mantar
  // ustundekiler (dy > 2) ziplatma ile (5.16 m) ulasilir.
  ...[...WD.SHARDS, ...WD.FEATHERS].map((d) => ({ id: d.id, p: col.resolve(d), maxGap: d.dy > 2 ? 5.0 : 3.2 })),
  ...WD.CARROTS.map((d) => ({ id: d.id, p: col.resolve({ ...d, dy: 0.05 }), maxGap: 0.6 })),
  ...WD.TOOLS.map((d) => ({ id: d.id, p: col.resolve({ ...d, dy: 0.05 }), maxGap: 0.6 })),
];
WD.SHELL_GROUPS.forEach(([cx, cz, deg], gi) => {
  const a = (deg * Math.PI) / 180;
  for (let k = 0; k < 5; k++) {
    const x = cx + Math.cos(a) * (k - 2) * 1.7;
    const z = cz + Math.sin(a) * (k - 2) * 1.7;
    placed.push({ id: `sh_${gi * 5 + k}`, p: col.resolve({ x, z, dy: 0.02 }), maxGap: 0.6, shell: true });
  }
});
const bad = [];
for (const it of placed) {
  const p = it.p;
  const probe = { x: p.x, y: p.y + 0.3, z: p.z };
  const inside = physics.pointInside(probe);
  const hit = physics.groundAt(p.x, p.z, p.y + 0.1);
  const gap = hit ? p.y - hit.y : 99;
  const wl = T.waterLevel(p.x, p.z);
  const groundIsWater = !hit || hit.y < wl - 0.2;
  const r = Math.hypot(p.x, p.z);
  const problems = [];
  if (inside) problems.push('carpisma icinde');
  if (gap > it.maxGap) problems.push(`zemin ${gap.toFixed(1)} m asagida`);
  if (p.y < wl + 0.25) problems.push('su altinda');
  if (it.shell && groundIsWater) problems.push('kabuk suda');
  if (r > BOUNDARY_RADIUS - 5) problems.push('sinir disinda');
  if (problems.length) bad.push(`${it.id}@(${p.x.toFixed(0)},${p.y.toFixed(1)},${p.z.toFixed(0)}): ${problems.join(', ')}`);
}
ok(`${placed.length} esyanin yerlesimi gecerli`, bad.length === 0, `\n    ${bad.join('\n    ')}`);

// adalilar kuru zeminde
const npcBad = [];
for (const [id, n] of Object.entries(WD.NPCS)) {
  const hit = physics.groundAt(n.x, n.z, 200);
  if (!hit || hit.y < T.waterLevel(n.x, n.z) + 0.2) npcBad.push(id);
}
ok('adalilar kuru zeminde', npcBad.length === 0, npcBad.join(','));

// baslangic iskelede
const start = physics.groundAt(WD.START.x, WD.START.z, 50);
ok('baslangic noktasi iskele ustunde', start && Math.abs(start.y - WD.DOCK.y) < 0.3, start ? start.y.toFixed(2) : 'yok');

console.log(fails ? `\n${fails}/${checks} denetim BASARISIZ` : `\nHepsi gecti (${checks} denetim)`);
process.exit(fails ? 1 : 0);
