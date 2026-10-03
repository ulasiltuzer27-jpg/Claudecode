// Elle yerlestirilmis icerik. Kayit dosyalari burada tanimlanan kimlikleri
// (s01, f2, sh_17...) saklar; bir kimligi degistirmek eski kayitlardaki
// toplanmis esyayi "geri getirir". Yeni esya eklerken yeni kimlik verin.
//
// Konum kurali: { x, z } zorunlu. y verilmezse araziye oturtulur ve `dy`
// kadar yukari kaldirilir. `on` verilirse ilgili yapinin capasina
// (Props.anchors) gore konumlanir.

import { LAYOUT } from './Terrain.js';

export const START = { x: 8, z: 176, yaw: Math.PI };

export const DOCK = { x: 8, z0: 157, length: 25, y: 1.25 };

export const VILLAGE = {
  campfire: { x: 8, z: 104 },
  cottages: [
    { id: 'hut', x: -16, z: 126, yaw: Math.PI * 0.85, wall: '#fff4e0', roof: '#e2584b' },
    { id: 'c2', x: -20, z: 98, yaw: Math.PI * 0.42, wall: '#dcefff', roof: '#4b8fe2' },
    { id: 'c3', x: -4, z: 82, yaw: Math.PI * 0.12, wall: '#fff4e0', roof: '#55b073' },
    { id: 'c4', x: 22, z: 82, yaw: -Math.PI * 0.15, wall: '#ffe1dc', roof: '#f2994a' },
    { id: 'c5', x: 34, z: 102, yaw: -Math.PI * 0.48, wall: '#fff4e0', roof: '#e2584b' },
    { id: 'c6', x: 30, z: 124, yaw: -Math.PI * 0.8, wall: '#dcefff', roof: '#4b8fe2' },
  ],
  shop: { x: -6, z: 110, yaw: Math.PI / 2 },
  lanterns: [[0, 96], [16, 96], [16, 114], [0, 114], [8, 140], [-8, 134], [24, 134], [8, 124]],
  crates: [[-13.4, 128.6, 0.2], [-12.2, 129.4, 0.5], [-12.8, 129.0, 0, 0.8]],
  barrels: [[-10, 112], [-11, 113.5], [38, 108]],
  benches: [[20, 106, -Math.PI / 2], [-2, 100, Math.PI / 2]],
  signs: [{ x: 4, z: 92, yaw: 0.6 }, { x: 46, z: 106, yaw: -1.2 }, { x: -30, z: 70, yaw: 0.9 }],
  fences: [
    [[-28, 112], [-34, 104], [-32, 94]],
    [[40, 90], [46, 96], [46, 112]],
  ],
  palms: [[-26, 140], [-12, 146], [24, 146], [38, 138], [46, 132], [-36, 134], [56, 128], [18, 152]],
};

export const LANDMARKS = {
  lighthouse: { x: LAYOUT.peak.x, z: LAYOUT.peak.z - 2, yaw: 0 },
  windmill: { x: 98, z: -62, yaw: Math.PI * 0.75 },
  bridge: { a: { x: 75, z: 104 }, b: { x: 99, z: 104 } },
  shipwreck: { x: 146, z: 58, yaw: 0.9 },
  dome: { x: LAYOUT.islet.x, z: LAYOUT.islet.z, yaw: 0 }, // giris +X: adaya bakar
  raceFlag: { x: -60, z: 30 },
  rabbitGarden: { x: -50, z: 50 },
  stump: { x: -128, z: -22 },
  sandcastle: { x: 36, z: 142 },
};

// Gizli magara icin yon tabelasi: kiyidan adacigi gosterir
export const HINT_SIGN = { x: -128, z: -96, yaw: -2.2 };

export const NPCS = {
  owl: { kind: 'owl', x: 8, z: 150, yaw: Math.PI, voice: 0.8 },
  owlPeak: { kind: 'owl', x: LAYOUT.peak.x + 3, z: LAYOUT.peak.z + 6, yaw: 0.3, voice: 0.8 },
  hedgehog: { kind: 'hedgehog', x: -4.2, z: 112.4, yaw: Math.PI / 2 + 0.35, voice: 1.3 },
  frog: { kind: 'frog', x: 6, z: -6, yaw: -0.8, voice: 1.5 },
  bear: { kind: 'bear', x: 132, z: 50, yaw: 0.9, voice: 0.6 },
  rabbit: { kind: 'rabbit', x: -46, z: 54, yaw: 2.6, voice: 1.4 },
  beaver: { kind: 'beaver', x: 70, z: 108, yaw: -2.0, voice: 1.1 },
};

// 26 yerlestirilmis yildiz parcasi (+4 gorev odulu = 30)
export const SHARDS = [
  { id: 's01', on: 'hutRoof' },
  { id: 's02', on: 'boat' },
  { id: 's03', on: 'shopRoof' },
  { id: 's04', x: -74, z: 40, dy: 1.0, on: 'meadowRock' },
  { id: 's05', x: -50, z: 50, dy: 0.9 },
  { id: 's06', on: 'meadowStack' },
  { id: 's07', x: -102, z: 22, dy: 0.9 },
  { id: 's08', on: 'stump' },
  { id: 's09', x: -142, z: 2, dy: 0.9 },
  { id: 's10', x: 30, z: 2, dy: 1.0 },
  { id: 's11', on: 'lakeRock' },
  { id: 's12', on: 'mushroom2', dy: 4.2 },
  { id: 's13', on: 'mushroom5' },
  { id: 's14', x: -92, z: -84, dy: 0.9 },
  { id: 's15', x: 112, z: -44, dy: 0.9 },
  { id: 's16', on: 'spire' },
  { id: 's17', x: 30, z: -108, dy: 0.9 },
  { id: 's18', x: LAYOUT.peak.x - 6, z: LAYOUT.peak.z + 8, dy: 0.9 },
  { id: 's19', x: -10, z: -136, dy: 0.9 },
  { id: 's20', on: 'nest' },
  { id: 's21', on: 'deck' },
  { id: 's22', on: 'seaStack' },
  { id: 's23', on: 'domeInside' },
  { id: 's24', x: -128, z: 72, dy: 0.9 },
  { id: 's25', on: 'inletStack' },
  { id: 's26', on: 'sandcastle' },
];

export const QUEST_SHARDS = ['q_beaver', 'q_frog', 'q_bear', 'q_shop'];
export const SHARD_TOTAL = 30;
export const SHARDS_FOR_FINALE = 20;

export const FEATHERS = [
  { id: 'f1', on: 'meadowStump' },
  { id: 'f2', on: 'forestLedge' },
  { id: 'f3', on: 'mushroom4', dy: 4.4 },
  { id: 'f4', x: 104, z: -54, dy: 1.0 },
];
export const FEATHER_TOTAL = 8; // 4 dunyada + baykus + kirpi + tavsan + ayi

export const CARROTS = [
  { id: 'carrot1', x: -104, z: 6 },
  { id: 'carrot2', x: -70, z: 64 },
  { id: 'carrot3', x: 14, z: 30 },
];

export const TOOLS = [
  { id: 'tool_hammer', kind: 'hammer', x: -78, z: -72 },
  { id: 'tool_saw', kind: 'saw', x: 90, z: -46 },
  { id: 'tool_shovel', kind: 'shovel', x: 36, z: 136 },
];

// 20 grup x 5 kabuk = 100. [x, z, yon(derece)]
export const SHELL_GROUPS = [
  [30, 140, 0], [38, 130, 30], [-2, 150, 90], [20, 148, 0], [-24, 132, 120],
  [-40, 76, 40], [-80, 52, 0], [-112, 36, 70], [-124, 42, 90], [-128, -40, 80],
  [-104, -60, 20], [-62, -96, 150], [-20, -70, 60], [48, -24, 10], [70, -36, 40],
  [56, 60, 90], [86, 30, 120], [126, 44, 0], [118, 84, 60], [134, 22, 100],
];

export const GIANT_MUSHROOMS = [
  { id: 'mushroom1', x: -82, z: -70, cap: 2.2, h: 2.4, color: '#e8524a' },
  { id: 'mushroom2', x: -90, z: -76, cap: 2.0, h: 3.6, color: '#5aa8e8' },
  { id: 'mushroom3', x: -78, z: -86, cap: 2.4, h: 2.8, color: '#ff8fb1' },
  { id: 'mushroom4', x: -96, z: -86, cap: 2.0, h: 4.6, color: '#e8524a' },
  { id: 'mushroom5', x: -88, z: -94, cap: 1.8, h: 8.6, color: '#b98aff' },
  { id: 'mushroom6', x: -72, z: -78, cap: 1.6, h: 3.2, color: '#ffcf4a' },
];

export const UPDRAFTS = [
  { x: 108, z: -46, r: 3.2, top: 40 },
  { x: 140, z: 52, r: 2.8, top: 18 },
];

// Kurbaga yarisinin rotasi (kurbaga ayni yolu ziplaya ziplaya izler)
export const RACE_PATH = [[6, -6], [-6, -2], [-22, 4], [-38, 14], [-50, 22], [-60, 30]];
export const RACE_FROG_SPEED = 6.6;

export const REGIONS = [
  { id: 'cave', x: LAYOUT.islet.x, z: LAYOUT.islet.z, r: 5.5, secret: true },
  { id: 'peak', x: LAYOUT.peak.x, z: LAYOUT.peak.z, r: 33, minY: 26 },
  { id: 'windy', x: LAYOUT.windy.x, z: LAYOUT.windy.z, r: 40, minY: 12 },
  { id: 'hollow', x: LAYOUT.hollow.x, z: LAYOUT.hollow.z, r: 34 },
  { id: 'lake', x: LAYOUT.lake.x, z: LAYOUT.lake.z, r: 33 },
  { id: 'cove', x: LAYOUT.cove.x, z: LAYOUT.cove.z, r: 38 },
  { id: 'forest', x: LAYOUT.forest.x, z: LAYOUT.forest.z, r: 46 },
  { id: 'meadow', x: LAYOUT.meadow.x, z: LAYOUT.meadow.z, r: 40 },
  { id: 'village', x: LAYOUT.village.x, z: LAYOUT.village.z + 8, r: 52 },
];
export const MAIN_REGIONS = REGIONS.filter((r) => !r.secret).map((r) => r.id);

export const SHOP_ITEMS = [
  { id: 'rod', price: 10 },
  { id: 'feather', price: 25 },
  { id: 'shard', price: 40 },
  { id: 'hat', price: 20 },
];

export const FISH = [
  { id: 'anchovy', water: 'sea', time: 'any', weight: 5, diff: 0.25, size: [9, 16] },
  { id: 'seabass', water: 'sea', time: 'day', weight: 3, diff: 0.45, size: [30, 60] },
  { id: 'trout', water: 'lake', time: 'any', weight: 5, diff: 0.3, size: [20, 40] },
  { id: 'carp', water: 'lake', time: 'day', weight: 3, diff: 0.5, size: [35, 70] },
  { id: 'moonfish', water: 'any', time: 'night', weight: 3, diff: 0.55, size: [25, 45] },
  { id: 'goldfish', water: 'lake', time: 'any', weight: 0.8, diff: 0.7, size: [8, 14] },
];

// Ekstra kayalar (platform). Yildiz/tuy yerlesimleri bunlara bagli.
export const PLATFORM_ROCKS = [
  { id: 'meadowRock', x: -74, z: 40, s: 1.8 },
  { id: 'meadowStack', x: -36, z: 22, s: 1.6, stack: [1.6, 1.25, 0.95] },
  { id: 'lakeRock', x: 41, z: 6, s: 2.0, stack: [2.0, 1.6, 1.2] },
  { id: 'forestLedge', x: -132, z: -32, s: 1.5, stack: [1.5, 1.1] },
  { id: 'seaStack', x: 158, z: 40, s: 2.2, stack: [2.4, 1.9] },
  { id: 'inletStack', x: 80, z: 140, s: 2.6, stack: [2.6, 2.1, 1.6] },
];
