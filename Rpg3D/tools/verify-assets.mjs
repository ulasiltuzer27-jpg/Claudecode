#!/usr/bin/env node
// Asset doğrulayıcı: oyunu açmadan, saniyeler içinde.
//
//  1. manifest.json'daki her dosya diskte var; diskte manifest'e girmemiş
//     artık dosya yok.
//  2. Her dosyanın CREDITS.md'de kaynağı var ve lisansı CC0.
//  3. Ortak animasyon kütüphanesi (anims.glb) oyunun kullandığı bütün
//     klipleri içeriyor.
//  4. Her karakter, kütüphanedeki kliplerin hedeflediği BÜTÜN kemiklere
//     sahip. (Eksik kemik derlemede ya da yüklemede hata vermez; three.js
//     o izi sessizce atlar ve karakter yarım oynar. Bu yüzden burada.)
//  5. Karakterlerde silah takılan `handslot.r` / `handslot.l` düğümleri var.
//  6. Her ses dosyası ffprobe ile okunuyor ve süresi > 0.
//
// Çıkış kodu: hata varsa 1.

import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync, existsSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const ASSETS = join(ROOT, 'public', 'assets');

// Oyun kodunun ve verisinin ismen çağırdığı animasyon klipleri: elle bir
// liste tutmak yerine kaynaktan toplanır (liste kodla ayrışmasın diye).
// Klip adı biçimi: Büyük harf/rakamla başlar, alt çizgi içerir (Running_A)
// ya da tek kelimelik bilinen adlardan biri.
const SINGLE = new Set(['Idle', 'Block', 'Blocking', 'Cheer', 'Interact', 'PickUp', 'Taunt', 'Throw']);
function usedClips() {
  const files = [
    ...readdirSync(join(ROOT, 'src/entities')).map((f) => join(ROOT, 'src/entities', f)),
    join(ROOT, 'src/rpg/Combat.ts'),
    join(ROOT, 'src/data/enemies.json'),
  ];
  const out = new Set();
  for (const f of files) {
    for (const m of readFileSync(f, 'utf8').matchAll(/['"]([A-Z0-9][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+|[A-Z][a-zA-Z]+)['"]/g)) {
      const name = m[1];
      if (name.includes('_') || SINGLE.has(name)) out.add(name);
    }
  }
  return [...out].sort();
}
const REQUIRED_CLIPS = usedClips();

let errors = 0;
let checks = 0;
const fail = (msg) => { errors++; console.log(`  HATA  ${msg}`); };
const ok = (cond, msg) => { checks++; if (!cond) fail(msg); return cond; };

function readGlbJson(file) {
  const b = readFileSync(file);
  if (b.toString('ascii', 0, 4) !== 'glTF') throw new Error(`glb değil: ${file}`);
  const len = b.readUInt32LE(12);
  return JSON.parse(b.toString('utf8', 20, 20 + len));
}

function walk(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? walk(join(dir, e.name)) : [join(dir, e.name)]);
}

const manifest = JSON.parse(readFileSync(join(ASSETS, 'manifest.json'), 'utf8'));
const credits = readFileSync(join(ASSETS, 'CREDITS.md'), 'utf8');

// --- 1. manifest <-> disk ----------------------------------------------------
console.log('1. manifest <-> disk');
const listed = new Set();
for (const m of Object.values(manifest.models)) listed.add(m.file);
for (const s of Object.values(manifest.sounds)) s.files.forEach((f) => listed.add(f));
for (const m of Object.values(manifest.music)) listed.add(m.file);
for (const f of listed) ok(existsSync(join(ASSETS, f)), `manifest'te var, diskte yok: ${f}`);
for (const abs of walk(ASSETS)) {
  const rel = relative(ASSETS, abs).split('\\').join('/');
  if (rel === 'manifest.json' || rel === 'CREDITS.md') continue;
  ok(listed.has(rel), `diskte var, manifest'te yok (artık dosya): ${rel}`);
}
for (const [id, m] of Object.entries(manifest.models)) {
  ok(statSync(join(ASSETS, m.file)).size === m.bytes, `boyut tutmuyor: ${id}`);
}

// --- 2. kaynak + lisans -------------------------------------------------------
console.log('2. CREDITS.md');
for (const f of listed) ok(credits.includes(`\`${f}\``), `CREDITS.md'de kaynağı yok: ${f}`);
const licenses = [...credits.matchAll(/- Lisans: (.+)/g)].map((m) => m[1].trim());
ok(licenses.length > 0, 'CREDITS.md lisans satırı içermiyor');
for (const l of licenses) ok(l === 'CC0 1.0', `CC0 olmayan lisans: ${l}`);

// --- 3-5. animasyon + kemikler -----------------------------------------------
console.log(`3. animasyon kütüphanesi (kodda ${REQUIRED_CLIPS.length} klip adı bulundu)`);
const anims = readGlbJson(join(ASSETS, manifest.models.anims.file));
const clipNames = new Set((anims.animations ?? []).map((a) => a.name));
for (const c of REQUIRED_CLIPS) ok(clipNames.has(c), `anims.glb'de klip yok: ${c}`);
const targeted = new Set();
for (const a of anims.animations ?? []) for (const ch of a.channels) targeted.add(anims.nodes[ch.target.node].name);
ok(!targeted.has('kneeIK.l') && !targeted.has('control-heel-roll.r'), 'IK/kontrol kanalları ayıklanmamış');

console.log('4-5. karakter kemikleri');
for (const [id, m] of Object.entries(manifest.models)) {
  if (m.kind !== 'character') continue;
  const j = readGlbJson(join(ASSETS, m.file));
  ok(!(j.animations?.length), `${id}: animasyonlar ayıklanmamış (${j.animations?.length})`);
  ok(j.skins?.length === 1, `${id}: tek skin bekleniyordu (${j.skins?.length})`);
  const names = new Set(j.nodes.map((n) => n.name));
  const missing = [...targeted].filter((n) => !names.has(n));
  ok(missing.length === 0, `${id}: kliplerin hedeflediği kemikler eksik: ${missing.join(', ')}`);
  for (const slot of ['handslot.r', 'handslot.l']) ok(names.has(slot), `${id}: ${slot} yok`);
}

// --- 6. ses -------------------------------------------------------------------
console.log('6. ses dosyaları (ffprobe)');
const probe = (f) => Number(execFileSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', join(ASSETS, f)], { encoding: 'utf8' }).trim());
for (const [id, s] of Object.entries(manifest.sounds)) {
  ok(s.files.length > 0, `${id}: varyasyon yok`);
  ok(s.volume > 0 && s.volume <= 1, `${id}: volume aralık dışı (${s.volume})`);
  for (const f of s.files) {
    const d = probe(f);
    ok(d > 0.005 && d < 60, `${f}: süre şüpheli (${d} sn)`);
  }
}
for (const [id, m] of Object.entries(manifest.music)) {
  const d = probe(m.file);
  ok(d > 20, `müzik ${id}: çok kısa (${d} sn)`);
}

const bytes = [...listed].reduce((s, f) => s + statSync(join(ASSETS, f)).size, 0);
console.log(`\n${checks} kontrol, ${errors} hata · ${listed.size} dosya, ${(bytes / 1024 / 1024).toFixed(1)} MB`);
process.exit(errors ? 1 : 0);
