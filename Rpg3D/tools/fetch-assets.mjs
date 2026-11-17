#!/usr/bin/env node
// Gölge Vadisi — asset indirici.
//
// Bütün model, ses ve müzik dosyalarını internetteki CC0 kaynaklardan indirir,
// oyuna uygun hale getirir ve public/assets/ altına yazar:
//
//   * Modeller: KayKit (Kay Lousberg) paketleri. .gltf + .bin + .png üçlüsü
//     tek bir .glb'ye paketlenir; karakterlerde kullanılmayan animasyonlar
//     ayıklanır (karakter başına ~3.6 MB -> ~1 MB).
//   * Sesler: Kenney (RPG Audio, Foley, Impact, Interface) + Ninja Adventure.
//     Hepsi ffmpeg ile Ogg Vorbis'e çevrilir; efektler mono (3D konumlu ses
//     için), müzik stereo ve -18 LUFS'a normalize.
//
// Kaynaklar commit SHA'sına sabitlenmiştir: aynı komut her zaman aynı baytları
// üretir. Git LFS'te duran dosyalar (jam-ready-assets aynası) indirildikten
// sonra LFS işaretçisindeki sha256 ve boyutla doğrulanır; tutmazsa betik durur.
//
// Çıktı: public/assets/manifest.json (oyunun okuduğu tek liste) ve
//        public/assets/CREDITS.md (her dosyanın kaynağı + lisansı).
//
// Kullanım:  node tools/fetch-assets.mjs [--force]
//   --force  önbelleği (.cache/) yok sayıp her şeyi yeniden indirir.

import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, posix, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { dedup, prune, resample } from '@gltf-transform/functions';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const CACHE = join(ROOT, '.cache');
const OUT = join(ROOT, 'public', 'assets');
const FORCE = process.argv.includes('--force');

// ---------------------------------------------------------------------------
// Kaynak depolar (commit'e sabit)
// ---------------------------------------------------------------------------

const REPOS = {
  adventurers: {
    url: 'https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0',
    sha: '672074b73ba276876a19e8816ecdc5241817ab47',
    lfs: false,
    pack: 'KayKit Character Pack: Adventurers 1.0',
    author: 'Kay Lousberg (kaylousberg.com)',
    license: 'CC0 1.0',
  },
  skeletons: {
    url: 'https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0',
    sha: '15b62b9bad122f72926c10fb14d622c73819fa54',
    lfs: false,
    pack: 'KayKit Character Pack: Skeletons 1.0',
    author: 'Kay Lousberg (kaylousberg.com)',
    license: 'CC0 1.0',
  },
  hexagon: {
    url: 'https://github.com/KayKit-Game-Assets/KayKit-Medieval-Hexagon-Pack-1.0',
    sha: '84fa4e91af6a88989be7c99e0891cede11f2ca38',
    lfs: false,
    pack: 'KayKit Medieval Hexagon Pack 1.0',
    author: 'Kay Lousberg (kaylousberg.com)',
    license: 'CC0 1.0',
  },
  dungeon: {
    url: 'https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0',
    sha: 'b0ca9bd96a8072ab36a3a5464f00ed1e06a16d07',
    lfs: false,
    pack: 'KayKit Dungeon Remastered 1.0',
    author: 'Kay Lousberg (kaylousberg.com)',
    license: 'CC0 1.0',
  },
  // series-ai/jam-ready-assets: CC0 paketlerin LFS aynası. Her paket kendi
  // License.txt'sini taşıyor; aşağıdaki pack/author alanları dosya bazında
  // `packOf()` ile yolun ilk klasöründen çıkarılıyor.
  jam: {
    url: 'https://github.com/series-ai/jam-ready-assets',
    sha: 'b6272bc5afd7738c87c59392ed745a1b7c56aa4a',
    lfs: true,
  },
};

/** jam aynasında yolun ilk klasörü -> paket bilgisi. Hepsi CC0 (License.txt'leri okundu). */
const JAM_PACKS = {
  'kaykit-forest-nature': { pack: 'KayKit Forest Nature Pack', author: 'Kay Lousberg (kaylousberg.com)' },
  'kenney-rpg-audio': { pack: 'Kenney RPG Audio', author: 'Kenney (kenney.nl)' },
  'kenney-foley-sounds': { pack: 'Kenney Foley Sounds', author: 'Kenney (kenney.nl)' },
  'kenney-impact-sounds': { pack: 'Kenney Impact Sounds', author: 'Kenney (kenney.nl)' },
  'kenney-interface-sounds': { pack: 'Kenney Interface Sounds', author: 'Kenney (kenney.nl)' },
  'ninja-adventure': { pack: 'Ninja Adventure Asset Pack', author: 'Pixel-boy & AAA (pixel-boy.itch.io)' },
};

// ---------------------------------------------------------------------------
// Neler indirilecek
// ---------------------------------------------------------------------------

// Karakterlerde tutulacak animasyonlar. Geri kalan ~60 klip atılıyor.
const KEEP_ANIMS = new Set([
  'Idle', 'Idle_Combat', 'Unarmed_Idle', '2H_Melee_Idle',
  'Walking_A', 'Walking_B', 'Walking_Backwards', 'Walking_D_Skeletons',
  'Running_A', 'Running_B', 'Running_C',
  'Jump_Start', 'Jump_Idle', 'Jump_Land',
  '1H_Melee_Attack_Chop', '1H_Melee_Attack_Slice_Diagonal', '1H_Melee_Attack_Slice_Horizontal',
  '1H_Melee_Attack_Stab', '1H_Melee_Attack_Jump_Chop',
  '2H_Melee_Attack_Chop', '2H_Melee_Attack_Slice', '2H_Melee_Attack_Spin',
  'Block', 'Blocking', 'Block_Hit',
  'Dodge_Forward', 'Dodge_Backward',
  'Hit_A', 'Hit_B',
  'Death_A', 'Death_A_Pose', 'Death_C_Skeletons',
  'Spellcast_Shoot', 'Spellcast_Raise', 'Spellcasting', 'Spellcast_Summon',
  'Use_Item', 'PickUp', 'Interact', 'Cheer', 'Taunt',
  'Sit_Floor_Idle', 'Sit_Chair_Idle',
  'Skeletons_Awaken_Floor', 'Skeletons_Inactive_Floor_Pose', 'Spawn_Ground_Skeletons',
]);

const ADV = 'addons/kaykit_character_pack_adventures';
const SKL = 'addons/kaykit_character_pack_skeletons';
const HEX = 'addons/kaykit_medieval_hexagon_pack/Assets/gltf';
const DUN = 'addons/kaykit_dungeon_remastered/Assets/gltf';
const FOREST = 'kaykit-forest-nature/3D/nature/Assets/gltf/Color1';

/** @type {{id:string, repo:keyof REPOS, path:string, kind:'anims'|'character'|'prop'}[]} */
const MODELS = [
  // Ortak animasyon kütüphanesi. KayKit'in iki karakter paketi aynı iskeleti
  // (Rig_Medium, aynı kemik adları) kullanıyor; klipler bir kez tutulup her
  // karaktere uygulanıyor. Skeleton_Minion kaynak, çünkü iskeletlere özgü
  // klipleri (Skeletons_Awaken_Floor vb.) de içeren üst küme o.
  { id: 'anims', repo: 'skeletons', path: `${SKL}/Characters/gltf/Skeleton_Minion.glb`, kind: 'anims' },

  // Karakterler (animasyonları atılır, `anims` kullanılır)
  { id: 'knight', repo: 'adventurers', path: `${ADV}/Characters/gltf/Knight.glb`, kind: 'character' },
  { id: 'mage', repo: 'adventurers', path: `${ADV}/Characters/gltf/Mage.glb`, kind: 'character' },
  { id: 'barbarian', repo: 'adventurers', path: `${ADV}/Characters/gltf/Barbarian.glb`, kind: 'character' },
  { id: 'rogue', repo: 'adventurers', path: `${ADV}/Characters/gltf/Rogue_Hooded.glb`, kind: 'character' },
  { id: 'skeleton_minion', repo: 'skeletons', path: `${SKL}/Characters/gltf/Skeleton_Minion.glb`, kind: 'character' },
  { id: 'skeleton_warrior', repo: 'skeletons', path: `${SKL}/Characters/gltf/Skeleton_Warrior.glb`, kind: 'character' },
  { id: 'skeleton_mage', repo: 'skeletons', path: `${SKL}/Characters/gltf/Skeleton_Mage.glb`, kind: 'character' },
  { id: 'skeleton_rogue', repo: 'skeletons', path: `${SKL}/Characters/gltf/Skeleton_Rogue.glb`, kind: 'character' },

  // Silahlar / kalkanlar (el yuvasına takılır)
  ...['sword_1handed', 'sword_2handed', 'axe_1handed', 'axe_2handed', 'dagger', 'staff', 'wand',
    'shield_round', 'shield_badge', 'shield_spikes', 'spellbook_open']
    .map((n) => ({ id: `w_${n}`, repo: 'adventurers', path: `${ADV}/Assets/gltf/${n}.gltf`, kind: 'prop' })),
  ...['Skeleton_Blade', 'Skeleton_Axe', 'Skeleton_Staff', 'Skeleton_Shield_Small_A', 'Skeleton_Shield_Large_A']
    .map((n) => ({ id: `w_${n.toLowerCase()}`, repo: 'skeletons', path: `${SKL}/Assets/gltf/${n}.gltf`, kind: 'prop' })),

  // Köy binaları ve eşyalar (Medieval Hexagon)
  ...['building_home_A_blue', 'building_home_B_blue', 'building_tavern_blue', 'building_blacksmith_blue',
    'building_market_blue', 'building_well_blue', 'building_windmill_blue', 'building_church_blue',
    'building_tower_A_blue']
    .map((n) => ({ id: n.replace('building_', 'b_').replace('_blue', ''), repo: 'hexagon', path: `${HEX}/buildings/blue/${n}.gltf`, kind: 'prop' })),
  { id: 'b_destroyed', repo: 'hexagon', path: `${HEX}/buildings/neutral/building_destroyed.gltf`, kind: 'prop' },
  ...['barrel', 'crate_A_big', 'crate_B_small', 'sack', 'tent', 'weaponrack', 'wheelbarrow', 'resource_lumber',
    'flag_red', 'target', 'bucket_water']
    .map((n) => ({ id: `p_${n.toLowerCase()}`, repo: 'hexagon', path: `${HEX}/decoration/props/${n}.gltf`, kind: 'prop' })),
  ...['fence_wood_straight', 'fence_stone_straight']
    .map((n) => ({ id: `p_${n}`, repo: 'hexagon', path: `${HEX}/buildings/neutral/${n}.gltf`, kind: 'prop' })),

  // Harabe / zindan parçaları (Dungeon Remastered, .gltf.glb = tek dosya glb)
  ...['wall', 'wall_broken', 'wall_arched', 'wall_cracked', 'wall_half', 'pillar', 'pillar_decorated', 'column',
    'floor_tile_large', 'floor_tile_large_rocks', 'floor_dirt_large', 'rubble_large', 'rubble_half',
    'torch_lit', 'torch_mounted', 'barrel_large', 'banner_red', 'sword_shield_broken', 'coin_stack_small',
    'coin', 'bottle_A_green', 'bottle_A_labeled_brown', 'bottle_C_brown', 'candle_triple', 'stairs', 'box_large',
    'keg', 'table_medium_broken']
    .map((n) => ({ id: `d_${n.toLowerCase()}`, repo: 'dungeon', path: `${DUN}/${n}.gltf.glb`, kind: 'prop' })),
  { id: 'd_chest', repo: 'dungeon', path: `${DUN}/chest.glb`, kind: 'prop' },
  { id: 'd_chest_gold', repo: 'dungeon', path: `${DUN}/chest_gold.glb`, kind: 'prop' },

  // Doğa (Forest Nature, LFS aynası)
  ...['Tree_1_A', 'Tree_1_B', 'Tree_2_A', 'Tree_2_C', 'Tree_3_A', 'Tree_4_A', 'Tree_5_A', 'Tree_6_A',
    'Tree_Bare_1_A', 'Tree_Bare_2_A', 'Bush_1_A', 'Bush_2_A', 'Bush_4_A', 'Rock_1_A', 'Rock_1_J', 'Rock_2_A',
    'Rock_3_A', 'Rock_3_E', 'Grass_1_A', 'Grass_2_A']
    .map((n) => ({ id: `n_${n.toLowerCase()}`, repo: 'jam', path: `${FOREST}/${n}_Color1.gltf`, kind: 'prop' })),
];

const RPG = 'kenney-rpg-audio/audio/Audio';
const FOLEY = 'kenney-foley-sounds/audio/Audio';
const IMPACT = 'kenney-impact-sounds/audio/Audio';
const UI = 'kenney-interface-sounds/audio/Audio';
const NINJA = 'ninja-adventure/2D/top-down-rpg/Audio';
const range = (n, f) => Array.from({ length: n }, (_, i) => f(i));

/**
 * Ses efektleri: id -> varyasyon listesi. Oyun her çalışta rastgele bir
 * varyasyon seçer (aynı adım sesinin tekrar tekrar çalması kulağı yorar).
 * volume: kaynak paketler farklı seviyelerde kaydedilmiş, kabaca eşitlemek için.
 */
const SOUNDS = {
  step_grass: { volume: 0.55, files: range(5, (i) => `${IMPACT}/footstep_grass_00${i}.ogg`) },
  step_dirt: { volume: 0.5, files: range(5, (i) => `${RPG}/footstep0${i}.ogg`) },
  step_stone: { volume: 0.5, files: range(5, (i) => `${IMPACT}/footstep_concrete_00${i}.ogg`) },
  step_wood: { volume: 0.5, files: range(5, (i) => `${IMPACT}/footstep_wood_00${i}.ogg`) },
  swing: { volume: 0.7, files: range(4, (i) => `${FOLEY}/Woosh/woosh${i + 1}.ogg`) },
  swing_heavy: { volume: 0.8, files: range(3, (i) => `${FOLEY}/Woosh/woosh${i + 5}.ogg`) },
  hit: { volume: 0.8, files: range(4, (i) => `${FOLEY}/Swords/sword${i + 1}.ogg`) },
  hit_heavy: { volume: 0.9, files: range(3, (i) => `${IMPACT}/impactPunch_heavy_00${i}.ogg`) },
  block: { volume: 0.8, files: range(4, (i) => `${FOLEY}/Swords/swordMetal${i + 1}.ogg`) },
  bone_hit: { volume: 0.7, files: range(3, (i) => `${IMPACT}/impactWood_light_00${i}.ogg`) },
  bone_break: { volume: 0.9, files: range(3, (i) => `${IMPACT}/impactWood_heavy_00${i}.ogg`) },
  player_hurt: { volume: 0.7, files: ['Hit1', 'Hit2', 'Hit3'].map((n) => `${NINJA}/Sounds/Hit & Impact/${n}.wav`) },
  dodge: { volume: 0.6, files: [`${FOLEY}/Woosh/woosh8.ogg`, `${RPG}/cloth3.ogg`] },
  jump: { volume: 0.5, files: [`${RPG}/cloth1.ogg`, `${RPG}/cloth2.ogg`] },
  land: { volume: 0.6, files: [`${IMPACT}/impactSoft_medium_000.ogg`, `${IMPACT}/impactSoft_medium_001.ogg`] },
  fireball_cast: { volume: 0.7, files: [`${NINJA}/Sounds/Elemental/Fireball.wav`, `${NINJA}/Sounds/Elemental/Fireball2.wav`] },
  fireball_hit: { volume: 0.8, files: [`${NINJA}/Sounds/Elemental/Explosion.wav`, `${NINJA}/Sounds/Elemental/Explosion2.wav`] },
  enemy_cast: { volume: 0.6, files: [`${NINJA}/Sounds/Magic & Skill/Magic1.wav`, `${NINJA}/Sounds/Magic & Skill/Magic2.wav`] },
  heal: { volume: 0.7, files: [`${NINJA}/Sounds/Magic & Skill/Heal.wav`] },
  coins: { volume: 0.7, files: [`${RPG}/handleCoins.ogg`, `${RPG}/handleCoins2.ogg`] },
  loot: { volume: 0.6, files: [`${RPG}/handleSmallLeather.ogg`, `${RPG}/handleSmallLeather2.ogg`, `${RPG}/dropLeather.ogg`] },
  equip: { volume: 0.7, files: range(3, (i) => `${RPG}/drawKnife${i + 1}.ogg`) },
  chest_open: { volume: 0.8, files: [`${RPG}/doorOpen_1.ogg`, `${RPG}/creak1.ogg`] },
  book_open: { volume: 0.7, files: [`${RPG}/bookOpen.ogg`] },
  book_close: { volume: 0.7, files: [`${RPG}/bookClose.ogg`] },
  page: { volume: 0.6, files: range(3, (i) => `${RPG}/bookFlip${i + 1}.ogg`) },
  enemy_alert: { volume: 0.7, files: [`${IMPACT}/impactPlank_medium_000.ogg`, `${IMPACT}/impactPlank_medium_001.ogg`] },
  ui_click: { volume: 0.5, files: [`${UI}/click_001.ogg`, `${UI}/click_002.ogg`] },
  ui_open: { volume: 0.5, files: [`${UI}/maximize_001.ogg`] },
  ui_close: { volume: 0.5, files: [`${UI}/minimize_001.ogg`] },
  ui_error: { volume: 0.5, files: [`${UI}/error_001.ogg`] },
  ui_confirm: { volume: 0.5, files: [`${UI}/confirmation_001.ogg`] },
  level_up: { volume: 0.8, files: [`${NINJA}/Jingles/LevelUp1.wav`] },
  quest_accept: { volume: 0.7, files: [`${NINJA}/Jingles/Secret1.wav`] },
  quest_done: { volume: 0.8, files: [`${NINJA}/Jingles/Success1.wav`] },
  game_over: { volume: 0.8, files: [`${NINJA}/Jingles/GameOver.wav`] },
  // Döngüsel ortam sesleri
  amb_wind: { volume: 0.35, loop: true, files: [`${NINJA}/Sounds/Ambient/Wind.wav`] },
  amb_river: { volume: 0.5, loop: true, files: [`${NINJA}/Sounds/Ambient/River.wav`] },
  amb_fire: { volume: 0.6, loop: true, files: [`${NINJA}/Sounds/Elemental/Fire.wav`] },
  bird: { volume: 0.4, files: [`${NINJA}/Sounds/Creature/Bird.wav`] },
};

/** Müzik: id -> parça. Bölgeye ve savaş durumuna göre aralarında crossfade yapılır. */
const MUSIC = {
  menu: `${NINJA}/Musics/38 - Intro.ogg`,
  village: `${NINJA}/Musics/33 - Calm Village.ogg`,
  explore: `${NINJA}/Musics/35 - Adventure.ogg`,
  ruins: `${NINJA}/Musics/30 - Ruins.ogg`,
  combat: `${NINJA}/Musics/17 - Fight.ogg`,
  boss: `${NINJA}/Musics/24 - Final Area.ogg`,
};

// ---------------------------------------------------------------------------
// Yardımcılar
// ---------------------------------------------------------------------------

const sha256 = (buf) => createHash('sha256').update(buf).digest('hex');
const log = (...a) => console.log(...a);

function rawUrl(repoKey, path) {
  const r = REPOS[repoKey];
  const slug = r.url.replace('https://github.com/', '');
  const enc = path.split('/').map(encodeURIComponent).join('/');
  return r.lfs
    ? `https://media.githubusercontent.com/media/${slug}/${r.sha}/${enc}`
    : `https://raw.githubusercontent.com/${slug}/${r.sha}/${enc}`;
}

function sourceInfo(repoKey, path) {
  const r = REPOS[repoKey];
  if (repoKey === 'jam') {
    const top = path.split('/')[0];
    const p = JAM_PACKS[top];
    if (!p) throw new Error(`jam paketi tanımsız: ${top}`);
    return { pack: p.pack, author: p.author, license: 'CC0 1.0', url: `${r.url}/blob/${r.sha}/${path}` };
  }
  return { pack: r.pack, author: r.author, license: r.license, url: `${r.url}/blob/${r.sha}/${path}` };
}

/** Blobless + checkout'suz klon: yalnızca ağaç, LFS işaretçileri istenince iner. */
function ensureClone(repoKey) {
  const r = REPOS[repoKey];
  const dir = join(CACHE, 'git', repoKey);
  if (!existsSync(join(dir, '.git'))) {
    mkdirSync(dir, { recursive: true });
    execFileSync('git', ['init', '-q', dir]);
    execFileSync('git', ['-C', dir, 'remote', 'add', 'origin', r.url]);
  }
  try {
    execFileSync('git', ['-C', dir, 'cat-file', '-e', `${r.sha}^{commit}`], { stdio: 'ignore' });
  } catch {
    log(`  git: ${r.url} @ ${r.sha.slice(0, 10)}`);
    execFileSync('git', ['-C', dir, 'fetch', '-q', '--depth', '1', '--filter=blob:none', 'origin', r.sha]);
  }
  return dir;
}

/** LFS işaretçisini oku: { oid, size } */
function lfsPointer(repoKey, path) {
  const dir = ensureClone(repoKey);
  const text = execFileSync('git', ['-C', dir, '-c', 'core.quotePath=false', 'cat-file', '-p', `${REPOS[repoKey].sha}:${path}`], {
    encoding: 'utf8',
  });
  const oid = /oid sha256:([0-9a-f]{64})/.exec(text)?.[1];
  const size = Number(/size (\d+)/.exec(text)?.[1]);
  if (!oid || !size) throw new Error(`LFS işaretçisi değil: ${path}\n${text.slice(0, 200)}`);
  return { oid, size };
}

async function download(url, attempt = 1) {
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status} ${url}`);
    return Buffer.from(await res.arrayBuffer());
  } catch (err) {
    if (attempt >= 4) throw err;
    await new Promise((r) => setTimeout(r, 500 * 2 ** attempt));
    return download(url, attempt + 1);
  }
}

/** Depodan bir dosya getir (önbellekli). LFS ise sha256 + boyut doğrulanır. */
async function getFile(repoKey, path) {
  const url = rawUrl(repoKey, path);
  const cacheFile = join(CACHE, 'files', sha256(url).slice(0, 24));
  if (!FORCE && existsSync(cacheFile)) return readFileSync(cacheFile);
  const buf = await download(url);
  if (REPOS[repoKey].lfs) {
    const ptr = lfsPointer(repoKey, path);
    const got = sha256(buf);
    if (got !== ptr.oid || buf.length !== ptr.size) {
      throw new Error(`LFS doğrulaması tutmadı: ${path}\n  beklenen ${ptr.oid} (${ptr.size} B)\n  gelen    ${got} (${buf.length} B)`);
    }
  } else if (buf.slice(0, 40).toString('utf8').startsWith('version https://git-lfs')) {
    throw new Error(`Beklenmedik LFS işaretçisi: ${path}`);
  }
  mkdirSync(dirname(cacheFile), { recursive: true });
  writeFileSync(cacheFile, buf);
  return buf;
}

const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);

/** Animation.dispose() kanal ve sampler'ları bırakır; onlar accessor'ları tutar ve prune silemez. */
function disposeAnimation(a) {
  for (const ch of a.listChannels()) ch.dispose();
  for (const sm of a.listSamplers()) sm.dispose();
  a.dispose();
}

/**
 * Animasyon kanallarını incelt:
 *  - IK hedefleri ve kontrol düğümleri (`kneeIK.l`, `control-heel-roll.r`...)
 *    deriye ağırlık vermiyor; IK çözümü zaten gerçek kemiklerin rotasyonuna
 *    "bake" edilmiş. Bu kanallar ölü ağırlık.
 *  - Ölçek kanalı baştan sona 1 ise gereksiz.
 *  - Kök dışı bir kemiğin konum kanalı dinlenme pozuna eşit ve sabitse
 *    gereksiz; üstelik atılması klibi başka karakterlere uygularken DOĞRU
 *    olanı sağlıyor: her karakter kendi kemik uzunluklarını korur.
 * @returns atılan kanal sayısı
 */
function slimAnimations(root) {
  const EPS = 1e-4;
  const isControl = (name) => /IK|control-/.test(name);
  let dropped = 0;
  for (const anim of root.listAnimations()) {
    for (const ch of anim.listChannels()) {
      const node = ch.getTargetNode();
      const path = ch.getTargetPath();
      const name = node?.getName() ?? '';
      const out = ch.getSampler()?.getOutput()?.getArray();
      let drop = !node || isControl(name);
      if (!drop && out && path === 'scale') drop = out.every((v) => Math.abs(v - 1) < EPS);
      if (!drop && out && path === 'translation' && name !== 'hips' && name !== 'root') {
        const rest = node.getTranslation();
        drop = true;
        for (let i = 0; i < out.length; i++) if (Math.abs(out[i] - rest[i % 3]) > EPS) { drop = false; break; }
      }
      if (drop) {
        const sampler = ch.getSampler();
        ch.dispose();
        sampler?.dispose();
        dropped++;
      }
    }
  }
  return dropped;
}

async function loadDocument(repoKey, path) {
  if (path.endsWith('.glb')) return io.readBinary(new Uint8Array(await getFile(repoKey, path)));
  const json = JSON.parse((await getFile(repoKey, path)).toString('utf8'));
  const resources = {};
  const base = posix.dirname(path);
  for (const ref of [...(json.buffers ?? []), ...(json.images ?? [])]) {
    if (!ref.uri || ref.uri.startsWith('data:')) continue;
    const rel = decodeURIComponent(ref.uri);
    resources[ref.uri] = new Uint8Array(await getFile(repoKey, posix.normalize(posix.join(base, rel))));
  }
  return io.readJSON({ json, resources });
}

function ffmpeg(args) {
  execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', ...args]);
}

function probeDuration(file) {
  const out = execFileSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', file], {
    encoding: 'utf8',
  });
  return Number(out.trim());
}

// ---------------------------------------------------------------------------
// Ana akış
// ---------------------------------------------------------------------------

async function main() {
  for (const dir of ['models', 'audio', 'music']) {
    rmSync(join(OUT, dir), { recursive: true, force: true });
    mkdirSync(join(OUT, dir), { recursive: true });
  }
  const manifest = { generated: 'tools/fetch-assets.mjs', models: {}, sounds: {}, music: {} };
  const credits = [];

  log(`Modeller (${MODELS.length})`);
  for (const m of MODELS) {
    const doc = await loadDocument(m.repo, m.path);
    const root = doc.getRoot();
    if (m.kind === 'anims') {
      for (const a of root.listAnimations()) if (!KEEP_ANIMS.has(a.getName())) disposeAnimation(a);
      for (const mesh of root.listMeshes()) mesh.dispose();
      for (const skin of root.listSkins()) skin.dispose();
      const dropped = slimAnimations(root);
      // tolerance: baked klipte doğrusal aradeğerle geri üretilebilen kareler atılır.
      // 1e-3 radyan (~0.06°) gözle fark edilmiyor, dosyayı belirgin küçültüyor.
      await doc.transform(resample({ tolerance: 1e-3 }), prune({ keepLeaves: true }), dedup());
      log(`  (anims: ${dropped} gereksiz kanal atıldı)`);
    } else if (m.kind === 'character') {
      for (const a of root.listAnimations()) disposeAnimation(a);
      await doc.transform(prune(), dedup());
    } else {
      await doc.transform(prune(), dedup());
    }
    const file = `models/${m.id}.glb`;
    const glb = await io.writeBinary(doc);
    writeFileSync(join(OUT, file), glb);
    const animations = root.listAnimations().map((a) => a.getName());
    manifest.models[m.id] = { file, kind: m.kind, bytes: glb.byteLength, ...(animations.length ? { animations } : {}) };
    credits.push({ file, ...sourceInfo(m.repo, m.path) });
    log(`  ${m.id.padEnd(26)} ${(glb.byteLength / 1024).toFixed(0).padStart(6)} KB${animations.length ? `  ${animations.length} anim` : ''}`);
  }

  log(`Ses efektleri (${Object.keys(SOUNDS).length})`);
  const tmp = join(CACHE, 'tmp');
  mkdirSync(tmp, { recursive: true });
  for (const [id, s] of Object.entries(SOUNDS)) {
    const files = [];
    for (const [i, src] of s.files.entries()) {
      const buf = await getFile('jam', src);
      const inFile = join(tmp, `in${src.slice(src.lastIndexOf('.'))}`);
      writeFileSync(inFile, buf);
      const file = `audio/${id}_${i}.ogg`;
      // Mono: PositionalAudio zaten tek kanal kullanıyor, dosya da yarıya iniyor.
      ffmpeg(['-i', inFile, '-ac', '1', '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '4', join(OUT, file)]);
      files.push(file);
      credits.push({ file, ...sourceInfo('jam', src) });
    }
    manifest.sounds[id] = { files, volume: s.volume, ...(s.loop ? { loop: true } : {}) };
    log(`  ${id.padEnd(16)} ${files.length} varyasyon`);
  }

  log(`Müzik (${Object.keys(MUSIC).length})`);
  for (const [id, src] of Object.entries(MUSIC)) {
    const buf = await getFile('jam', src);
    const inFile = join(tmp, 'music.ogg');
    writeFileSync(inFile, buf);
    const file = `music/${id}.ogg`;
    ffmpeg(['-i', inFile, '-af', 'loudnorm=I=-18:TP=-1.5:LRA=11', '-ar', '44100', '-ac', '2', '-c:a', 'libvorbis', '-q:a', '3', join(OUT, file)]);
    const duration = probeDuration(join(OUT, file));
    manifest.music[id] = { file, duration: Math.round(duration * 10) / 10 };
    credits.push({ file, ...sourceInfo('jam', src) });
    log(`  ${id.padEnd(10)} ${duration.toFixed(0)} sn  ${(statSync(join(OUT, file)).size / 1024).toFixed(0)} KB`);
  }
  rmSync(tmp, { recursive: true, force: true });

  writeFileSync(join(OUT, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');

  const byPack = new Map();
  for (const c of credits) {
    if (!byPack.has(c.pack)) byPack.set(c.pack, { ...c, files: [] });
    byPack.get(c.pack).files.push(c);
  }
  let md = '# Asset kaynakları ve lisanslar\n\n';
  md += 'Bu klasördeki her dosya `tools/fetch-assets.mjs` tarafından aşağıdaki\n';
  md += 'kaynaklardan indirildi. Hepsi **CC0 1.0** (kamu malı): ticari kullanım\n';
  md += 'dahil serbest, atıf zorunlu değil ama burada yine de veriliyor.\n\n';
  md += 'Modeller .glb\'ye paketlendi (animasyonlar ayıklanıp ortak `anims.glb`\'ye\n';
  md += 'taşındı); sesler Ogg Vorbis\'e çevrildi, müzik -18 LUFS\'a normalize edildi.\n\n';
  for (const [pack, p] of byPack) {
    md += `## ${pack}\n\n- Yazar: ${p.author}\n- Lisans: ${p.license}\n\n| Dosya | Kaynak |\n|---|---|\n`;
    for (const f of p.files) md += `| \`${f.file}\` | [${f.url.split('/').pop()}](${f.url}) |\n`;
    md += '\n';
  }
  writeFileSync(join(OUT, 'CREDITS.md'), md);

  const total = credits.reduce((s, c) => s + statSync(join(OUT, c.file)).size, 0);
  log(`\nTamam: ${credits.length} dosya, ${(total / 1024 / 1024).toFixed(1)} MB -> public/assets/`);
}

main().catch((err) => {
  console.error(`\nHATA: ${err.message}`);
  process.exit(1);
});
