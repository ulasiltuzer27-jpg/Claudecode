// Adayi kurar: arazi, su, bitki ortusu, yapilar, carpisma ve "capalar"
// (yildiz/tuy gibi esyalarin oturdugu noktalar). Ayrica dunyadaki surekli
// animasyonlari (yel degirmeni, bayrak, kayik, duman, ates bocekleri) isletir.
import * as THREE from 'three';
import { Terrain, LAYOUT, SEA_LEVEL, WORLD_SIZE, VERTS } from './Terrain.js';
import { buildTerrain, regionTints } from './TerrainMesh.js';
import { mulberry32, smoothstep } from './noise.js';
import * as WD from './WorldData.js';
import * as N from '../models/Nature.js';
import * as B from '../models/Buildings.js';
import { vcMaterial, merge, mesh, prep, jitter, part } from '../models/geom.js';
import { P } from '../models/palette.js';
import { Water, seaGeometry, lakeGeometry } from '../render/Water.js';
import { Grass } from '../render/Grass.js';
import { Clouds } from '../render/Clouds.js';

const tmpM = new THREE.Matrix4();
const tmpQ = new THREE.Quaternion();
const tmpS = new THREE.Vector3();
const tmpP = new THREE.Vector3();
const UP = new THREE.Vector3(0, 1, 0);

function swayMaterial(uTime, opts = {}) {
  const m = vcMaterial(opts).clone();
  m.onBeforeCompile = (shader) => {
    shader.uniforms.uTime = uTime;
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nuniform float uTime;')
      .replace('#include <begin_vertex>', /* glsl */`
        vec3 transformed = vec3(position);
        #ifdef USE_INSTANCING
          float ph = instanceMatrix[3].x * 0.13 + instanceMatrix[3].z * 0.11;
        #else
          float ph = 0.0;
        #endif
        float sway = max(position.y - 1.2, 0.0);
        transformed.x += sin(uTime * 1.3 + ph) * 0.03 * sway;
        transformed.z += cos(uTime * 1.05 + ph) * 0.022 * sway;
      `);
  };
  return m;
}

export class World {
  constructor(game) {
    this.game = game;
    this.scene = game.renderer.scene;
    this.physics = game.physics;
    this.anchors = new Map();
    this.exclusions = [];
    this.glowParts = [];
    this.animated = [];
    this.smokeSources = [];
    this.bounceColliders = new Set();
    this.uTime = { value: 0 };
    this.lighthouse = null;
    this.bridgeBuilt = false;
  }

  async build(progress = () => {}) {
    const step = async (label, frac, fn) => {
      progress(label, frac);
      await new Promise((r) => setTimeout(r, 0));
      fn();
    };
    await step('terrain', 0.05, () => { this.terrain = new Terrain(1337); });
    await step('layout', 0.15, () => this.collectExclusions());
    await step('mesh', 0.25, () => {
      const t = buildTerrain(this.terrain, this.exclusions);
      this.terrainMesh = t.mesh;
      this.heightTex = t.heightTex;
      this.maskTex = t.maskTex;
      this.terrainColors = t.colors;
      this.scene.add(t.mesh);
      this.physics.addTerrain(this.terrain);
    });
    await step('water', 0.35, () => {
      this.sea = new Water(this.scene, this.heightTex, { level: SEA_LEVEL, worldSize: WORLD_SIZE, texSize: VERTS, geometry: seaGeometry(), amp: 0.14 });
      this.lake = new Water(this.scene, this.heightTex, {
        level: LAYOUT.lake.level, worldSize: WORLD_SIZE, texSize: VERTS, geometry: lakeGeometry(LAYOUT.lake.r + 4), amp: 0.04,
        shallow: '#5fe0d0', deep: '#2a7fb8',
      });
      this.lake.mesh.position.set(LAYOUT.lake.x, LAYOUT.lake.level, LAYOUT.lake.z);
    });
    await step('village', 0.45, () => this.buildVillage());
    await step('landmarks', 0.55, () => this.buildLandmarks());
    await step('trees', 0.7, () => this.buildVegetation());
    await step('details', 0.85, () => {
      this.buildGlow();
      const q = this.game.renderer.q;
      this.grass = new Grass(this.scene, this.heightTex, this.maskTex, WORLD_SIZE, VERTS, 50000 * q.grass);
      this.clouds = new Clouds(this.scene);
    });
    this.physics.step();
    progress('done', 1);
  }

  collectExclusions() {
    const ex = this.exclusions;
    for (const c of WD.VILLAGE.cottages) ex.push({ x: c.x, z: c.z, r: 5.5 });
    ex.push({ x: WD.VILLAGE.shop.x, z: WD.VILLAGE.shop.z, r: 2.8 });
    ex.push({ x: WD.VILLAGE.campfire.x, z: WD.VILLAGE.campfire.z, r: 11 });
    ex.push({ x: WD.LANDMARKS.lighthouse.x, z: WD.LANDMARKS.lighthouse.z, r: 5 });
    ex.push({ x: WD.LANDMARKS.windmill.x, z: WD.LANDMARKS.windmill.z, r: 4 });
    ex.push({ x: WD.LANDMARKS.dome.x, z: WD.LANDMARKS.dome.z, r: 7 });
    ex.push({ x: WD.LANDMARKS.raceFlag.x, z: WD.LANDMARKS.raceFlag.z, r: 2 });
    for (const m of WD.GIANT_MUSHROOMS) ex.push({ x: m.x, z: m.z, r: m.cap * 0.5 });
    for (const r of WD.PLATFORM_ROCKS) ex.push({ x: r.x, z: r.z, r: r.s * 1.2 });
    for (const n of Object.values(WD.NPCS)) ex.push({ x: n.x, z: n.z, r: 1.6 });
  }

  isExcluded(x, z, pad = 0) {
    for (const e of this.exclusions) {
      if ((x - e.x) ** 2 + (z - e.z) ** 2 < (e.r + pad) ** 2) return true;
    }
    return false;
  }

  groundY(x, z) {
    return this.terrain.height(x, z);
  }

  footprintY(x, z, r) {
    let m = Infinity;
    for (const [dx, dz] of [[0, 0], [r, 0], [-r, 0], [0, r], [0, -r]]) m = Math.min(m, this.terrain.height(x + dx, z + dz));
    return m;
  }

  // Bir yapiyi dunyaya yerlestir: mesh + carpisma + parilti + capalar
  place(res, x, y, z, yaw = 0, name = null, tag = null) {
    res.group.position.set(x, y, z);
    res.group.rotation.y = yaw;
    this.scene.add(res.group);
    res.group.updateMatrixWorld(true);
    const cols = [];
    for (const d of res.colliders) {
      const c = this.physics.addCollider(d, { x, y, z, yaw }, tag);
      if (c) cols.push(c);
    }
    for (const g of res.glow) {
      g.applyMatrix4(res.group.matrixWorld);
      this.glowParts.push(g);
    }
    if (name) {
      for (const [k, v] of Object.entries(res.anchors)) {
        this.anchors.set(`${name}.${k}`, v.clone().applyMatrix4(res.group.matrixWorld));
      }
    }
    res.colliderList = cols;
    return res;
  }

  buildVillage() {
    const V = WD.VILLAGE;
    for (const c of V.cottages) {
      const res = B.cottage({ wall: c.wall, roof: c.roof, seed: c.x * 3 + c.z });
      const y = this.footprintY(c.x, c.z, 2.4);
      this.place(res, c.x, y, c.z, c.yaw, c.id);
      if (res.anchors.smoke) this.smokeSources.push(this.anchors.get(`${c.id}.smoke`));
    }
    this.anchors.set('hutRoof', this.anchors.get('hut.roofTop').clone().add(new THREE.Vector3(0, 0.8, 0)));

    const shop = B.shopStall();
    const sy = this.footprintY(V.shop.x, V.shop.z, 1.5);
    this.place(shop, V.shop.x, sy, V.shop.z, V.shop.yaw, 'shop');
    this.anchors.set('shopRoof', new THREE.Vector3(V.shop.x, sy + 3.75, V.shop.z));
    this.shopAwning = this.physics.addCollider({ type: 'box', hx: 1.6, hy: 0.15, hz: 0.85, pos: [0, 2.5, 0.15], rot: [0.28, 0, 0] }, { x: V.shop.x, y: sy, z: V.shop.z, yaw: V.shop.yaw });
    this.physics.addCollider({ type: 'box', hx: 0.6, hy: 0.25, hz: 0.06, pos: [0, 3.05, 0.2] }, { x: V.shop.x, y: sy, z: V.shop.z, yaw: V.shop.yaw });

    const cf = B.campfire();
    const cy = this.groundY(V.campfire.x, V.campfire.z);
    this.place(cf, V.campfire.x, cy, V.campfire.z, 0, 'campfire');
    this.campfirePos = new THREE.Vector3(V.campfire.x, cy + 0.3, V.campfire.z);
    const fireLight = new THREE.PointLight('#ff9a4a', 0, 14, 1.6);
    fireLight.position.set(V.campfire.x, cy + 1.2, V.campfire.z);
    this.scene.add(fireLight);
    this.fireLight = fireLight;

    for (const [x, z] of V.lanterns) this.place(B.lantern(), x, this.groundY(x, z), z, Math.atan2(V.campfire.x - x, V.campfire.z - z) - Math.PI / 2);
    for (const [x, z, yaw = 0] of V.crates) this.place(B.crate(), x, this.groundY(x, z), z, yaw);
    // kulube catisina tirmanmak icin ust uste sandiklar
    const [hx, hz] = V.crates[2];
    this.place(B.crate(), hx, this.groundY(hx, hz) + 0.8, hz, 0.8);
    this.place(B.barrel(), hx + 0.9, this.groundY(hx + 0.9, hz - 0.9), hz - 0.9, 0);
    for (const [x, z] of V.barrels) this.place(B.barrel(), x, this.groundY(x, z), z, 0);
    for (const [x, z, yaw] of V.benches) this.place(B.bench(), x, this.groundY(x, z), z, yaw);
    for (const s of V.signs) this.place(B.signpost(), s.x, this.groundY(s.x, s.z), s.z, s.yaw);
    this.place(B.signpost(['#b98aff', '#7fd0ff']), WD.HINT_SIGN.x, this.groundY(WD.HINT_SIGN.x, WD.HINT_SIGN.z), WD.HINT_SIGN.z, WD.HINT_SIGN.yaw);
    for (const pts of V.fences) {
      const res = B.fence(pts, this.terrain);
      this.place(res, 0, 0, 0, 0);
    }

    // iskele + kayik
    const D = WD.DOCK;
    const dock = B.dock(D.length);
    this.place(dock, D.x, D.y, D.z0, 0, 'dock');
    const boat = B.rowboat();
    this.place(boat, D.x + 2.9, 0.38, D.z0 + D.length - 4, 0.15, 'boat');
    this.boat = boat.group;
    this.anchors.set('boat', new THREE.Vector3(D.x + 2.9, 1.6, D.z0 + D.length - 4));
    this.animated.push((t) => {
      this.boat.position.y = 0.38 + Math.sin(t * 1.3) * 0.06;
      this.boat.rotation.z = Math.sin(t * 1.1) * 0.04;
    });

    // kumdan kale
    const sc = WD.LANDMARKS.sandcastle;
    const sy2 = this.groundY(sc.x, sc.z);
    const castle = { group: new THREE.Group(), colliders: [], glow: [], anchors: { top: new THREE.Vector3(0, 2.6, 0) } };
    castle.group.add(mesh(merge([
      N.box(P.sand, 2.4, 0.9, 2.4, { pos: [0, 0.45, 0] }),
      ...[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([a, b]) => part(new THREE.CylinderGeometry(0.4, 0.45, 1.5, 8), P.sand, { pos: [a * 1.1, 0.75, b * 1.1] })),
      ...[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([a, b]) => N.cone('#e8c98a', 0.45, 0.5, { pos: [a * 1.1, 1.75, b * 1.1] }, 8)),
      part(new THREE.CylinderGeometry(0.55, 0.65, 1.4, 8), P.sand, { pos: [0, 1.6, 0] }),
      part(new THREE.CylinderGeometry(0.02, 0.02, 0.8, 4), '#ffffff', { pos: [0, 2.6, 0] }),
      N.box('#ff5a5a', 0.4, 0.25, 0.02, { pos: [0.2, 2.85, 0] }),
    ])));
    castle.colliders.push({ type: 'box', hx: 1.4, hy: 0.45, hz: 1.4, pos: [0, 0.45, 0] });
    castle.colliders.push({ type: 'cyl', r: 0.65, hh: 0.7, pos: [0, 1.6, 0] });
    for (const [a, b] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) castle.colliders.push({ type: 'cyl', r: 0.45, hh: 0.75, pos: [a * 1.1, 0.75, b * 1.1] });
    this.place(castle, sc.x, sy2 - 0.15, sc.z, 0.3, 'castle');
    this.anchors.set('sandcastle', new THREE.Vector3(sc.x, sy2 + 2.5, sc.z));
  }

  buildLandmarks() {
    const L = WD.LANDMARKS;
    // fener
    const lh = B.lighthouse();
    const ly = this.footprintY(L.lighthouse.x, L.lighthouse.z, 3);
    this.place(lh, L.lighthouse.x, ly, L.lighthouse.z, L.lighthouse.yaw, 'lighthouse');
    this.lighthouse = lh;

    // yel degirmeni
    const wm = B.windmill();
    const wy = this.footprintY(L.windmill.x, L.windmill.z, 2.4);
    this.place(wm, L.windmill.x, wy, L.windmill.z, L.windmill.yaw, 'windmill');
    this.animated.push((t, dt) => { wm.sails.rotation.z += dt * 0.6; });

    // kopru: once yarim (kazik), kunduz gorevi bitince tamamlaniyor
    const a = new THREE.Vector3(L.bridge.a.x, this.groundY(L.bridge.a.x, L.bridge.a.z) + 0.15, L.bridge.a.z);
    const b = new THREE.Vector3(L.bridge.b.x, this.groundY(L.bridge.b.x, L.bridge.b.z) + 0.15, L.bridge.b.z);
    this.bridgeEnds = { a, b };
    const stubs = new THREE.Group();
    for (const p of [a, b]) {
      for (const s of [-1.1, 1.1]) {
        stubs.add(mesh(part(new THREE.CylinderGeometry(0.1, 0.12, 1.6, 6), P.woodDark, { pos: [p.x, p.y + 0.2, p.z + s] })));
      }
    }
    this.scene.add(stubs);
    this.bridgeStubs = stubs;

    // batik gemi
    const sw = B.shipwreck();
    const swy = Math.max(-0.4, this.groundY(L.shipwreck.x, L.shipwreck.z) + 0.6);
    this.place(sw, L.shipwreck.x, swy, L.shipwreck.z, L.shipwreck.yaw, 'ship');
    this.anchors.set('nest', this.anchors.get('ship.nest'));
    this.anchors.set('deck', new THREE.Vector3(L.shipwreck.x, swy + 1.3, L.shipwreck.z));

    // gizli magara
    const dome = B.rockDome();
    const dy = this.groundY(L.dome.x, L.dome.z);
    this.place(dome, L.dome.x, dy - 0.3, L.dome.z, L.dome.yaw, 'dome');
    this.anchors.set('domeInside', new THREE.Vector3(L.dome.x, dy + 1.0, L.dome.z));
    const caveLight = new THREE.PointLight('#7ef0ff', 45, 14, 1.4);
    caveLight.position.set(L.dome.x, dy + 2, L.dome.z);
    this.scene.add(caveLight);

    // yaris bayragi
    const flag = B.flagPole('#ffcf4a');
    const fy = this.groundY(L.raceFlag.x, L.raceFlag.z);
    this.place(flag, L.raceFlag.x, fy, L.raceFlag.z, 0, 'flag');
    this.raceFlagPos = new THREE.Vector3(L.raceFlag.x, fy, L.raceFlag.z);
    this.animated.push((t) => { flag.flag.rotation.y = Math.sin(t * 2.2) * 0.25; flag.flag.scale.x = 1 + Math.sin(t * 5) * 0.05; });

    // tavsanin bahcesi (cit + havuc sirasi)
    const g = L.rabbitGarden;
    const gx = g.x;
    const gz = g.z;
    const fpts = [[gx - 4, gz - 3], [gx + 4, gz - 3], [gx + 4, gz + 3], [gx + 1, gz + 3]];
    this.place(B.fence(fpts, this.terrain), 0, 0, 0, 0);
    this.place(B.fence([[gx - 2, gz + 3], [gx - 4, gz + 3], [gx - 4, gz - 3]], this.terrain), 0, 0, 0, 0);
    const rows = [];
    for (let i = 0; i < 4; i++) {
      for (let k = 0; k < 6; k++) {
        const x = gx - 2.8 + k * 1.1;
        const z = gz - 1.8 + i * 1.2;
        const y = this.groundY(x, z);
        rows.push(part(new THREE.ConeGeometry(0.05, 0.25, 3), '#5fbf4a', { pos: [x, y + 0.12, z] }));
        rows.push(part(new THREE.SphereGeometry(0.12, 5, 3), '#8a6a4a', { pos: [x, y + 0.02, z], scale: [1.4, 0.4, 1.4] }));
      }
    }
    this.scene.add(mesh(merge(rows), { castShadow: false }));

    // kutuk (orman)
    const st = L.stump;
    const sty = this.groundY(st.x, st.z);
    const stump = { group: new THREE.Group(), colliders: [{ type: 'cyl', r: 1.5, hh: 1.6, pos: [0, 1.4, 0] }], glow: [], anchors: {} };
    const sg = prep(new THREE.CylinderGeometry(1.45, 1.8, 3.2, 12, 2), P.bark);
    jitter(sg, 0.12, 5);
    sg.translate(0, 1.4, 0);
    stump.group.add(mesh(merge([
      sg,
      part(new THREE.CylinderGeometry(1.35, 1.35, 0.05, 12), '#d9b98a', { pos: [0, 3.02, 0] }),
      part(new THREE.TorusGeometry(0.9, 0.04, 4, 16), '#b8925f', { pos: [0, 3.05, 0], rot: [Math.PI / 2, 0, 0] }),
      part(new THREE.TorusGeometry(0.5, 0.04, 4, 16), '#b8925f', { pos: [0, 3.05, 0], rot: [Math.PI / 2, 0, 0] }),
      ...[0, 1.6, 3.4, 4.8].map((a) => part(new THREE.CylinderGeometry(0.1, 0.35, 1.6, 6), P.barkDark, { pos: [Math.cos(a) * 1.7, 0.1, Math.sin(a) * 1.7], rot: [Math.sin(a) * 1.2, 0, -Math.cos(a) * 1.2] })),
    ])));
    this.place(stump, st.x, sty - 0.2, st.z, 0, 'stump');
    this.anchors.set('stump', new THREE.Vector3(st.x, sty + 3.8, st.z));

    // cayirdaki kucuk kutuk (ilk tuy)
    const ms = { x: -40, z: 40 };
    const msy = this.groundY(ms.x, ms.z);
    const mstump = { group: new THREE.Group(), colliders: [{ type: 'cyl', r: 0.7, hh: 0.5, pos: [0, 0.4, 0] }], glow: [], anchors: {} };
    mstump.group.add(mesh(merge([
      part(new THREE.CylinderGeometry(0.65, 0.8, 1.0, 10), P.bark, { pos: [0, 0.4, 0] }),
      part(new THREE.CylinderGeometry(0.6, 0.6, 0.04, 10), '#d9b98a', { pos: [0, 0.91, 0] }),
    ])));
    this.place(mstump, ms.x, msy - 0.1, ms.z, 0);
    this.anchors.set('meadowStump', new THREE.Vector3(ms.x, msy + 1.9, ms.z));

    // platform kayalari
    for (const r of WD.PLATFORM_ROCKS) this.placeRockStack(r);

    // dev mantarlar (ziplatan)
    for (const [i, m] of WD.GIANT_MUSHROOMS.entries()) {
      const y = this.groundY(m.x, m.z) - 0.2;
      const geo = N.giantMushroomGeo(m.color, m.cap, m.h, i + 3);
      const mm = mesh(geo, { material: vcMaterial({ flat: false, roughness: 0.6 }) });
      mm.position.set(m.x, y, m.z);
      this.scene.add(mm);
      this.physics.addCollider({ type: 'cyl', r: m.cap * 0.26, hh: m.h / 2, pos: [0, m.h / 2, 0] }, { x: m.x, y, z: m.z, yaw: 0 });
      const cap = this.physics.addCollider({ type: 'cyl', r: m.cap * 0.95, hh: m.cap * 0.22, pos: [0, m.h - 0.1 + m.cap * 0.22, 0] }, { x: m.x, y, z: m.z, yaw: 0 }, 'bounce');
      this.bounceColliders.add(cap.handle);
      this.anchors.set(m.id, new THREE.Vector3(m.x, y + m.h + m.cap * 0.5 + 0.9, m.z));
      const base = mm.scale.clone();
      m.mesh = mm;
      m.squish = 0;
      this.animated.push((t, dt) => {
        m.squish = Math.max(0, m.squish - dt * 3);
        const s = Math.sin(m.squish * Math.PI * 3) * m.squish * 0.25;
        mm.scale.set(base.x * (1 + s), base.y * (1 - s), base.z * (1 + s));
      });
    }

    // ruzgar sutunu kayasi (ruzgarli kayaliklar)
    const sp = { x: 116, z: -38 };
    const spy = this.groundY(sp.x, sp.z);
    const spire = prep(new THREE.CylinderGeometry(1.4, 2.6, 15, 8, 5), P.rock);
    jitter(spire, 0.6, 21);
    spire.translate(0, 7.5, 0);
    const spireTop = prep(new THREE.CylinderGeometry(1.6, 1.5, 0.6, 8), P.grassMoss);
    spireTop.translate(0, 15.1, 0);
    const spm = mesh(merge([spire, spireTop]));
    spm.position.set(sp.x, spy - 0.5, sp.z);
    this.scene.add(spm);
    this.physics.addCollider({ type: 'cyl', r: 1.9, hh: 7.7, pos: [0, 7.6, 0] }, { x: sp.x, y: spy - 0.5, z: sp.z, yaw: 0 });
    this.anchors.set('spire', new THREE.Vector3(sp.x, spy - 0.5 + 16.3, sp.z));
  }

  placeRockStack(r) {
    const stack = r.stack || [r.s];
    let y = this.groundY(r.x, r.z) - stack[0] * 0.25;
    for (const [i, s] of stack.entries()) {
      const g = N.rockGeo(Math.floor(r.x * 7 + r.z * 3 + i), true);
      const scale = s;
      g.scale(scale * 1.1, scale, scale * 1.1);
      const m = mesh(g);
      m.position.set(r.x + (i ? 0.3 : 0), y, r.z);
      m.rotation.y = i * 1.3;
      this.scene.add(m);
      const pts = [];
      const p = g.attributes.position;
      for (let k = 0; k < p.count; k += 3) pts.push(p.getX(k), p.getY(k), p.getZ(k));
      this.physics.addCollider({ type: 'hull', points: pts, rot: [0, i * 1.3, 0] }, { x: r.x + (i ? 0.3 : 0), y, z: r.z, yaw: 0 });
      g.computeBoundingBox();
      y += g.boundingBox.max.y * 0.82;
    }
    this.physics.step();
    const hit = this.physics.groundAt(r.x + (stack.length > 1 ? 0.3 : 0), r.z, y + 10);
    this.anchors.set(r.id, new THREE.Vector3(r.x + (stack.length > 1 ? 0.3 : 0), (hit ? hit.y : y) + 0.95, r.z));
  }

  buildBridge(animate = false) {
    if (this.bridgeBuilt) return;
    this.bridgeBuilt = true;
    const { a, b } = this.bridgeEnds;
    const res = B.bridge(a, b);
    this.place(res, 0, 0, 0, 0);
    if (animate) {
      res.group.position.y = 8;
      let t = 0;
      const fn = (_, dt) => {
        t += dt;
        res.group.position.y = Math.max(0, 8 - t * t * 9);
        return res.group.position.y <= 0;
      };
      this.animated.push(fn);
    }
  }

  buildVegetation() {
    const T = this.terrain;
    const rng = mulberry32(9001);
    const n = { x: 0, y: 1, z: 0 };
    const types = { round: [], blossom: [], pine: [], birch: [], palm: [] };

    const addTree = (type, x, z, s, yaw) => {
      const y = T.height(x, z) - 0.1;
      types[type].push({ x, y, z, s, yaw });
      const r = type === 'palm' ? 0.2 : type === 'pine' ? 0.28 : 0.32;
      this.physics.addCollider({ type: 'cyl', r: r * s, hh: 1.6 * s, pos: [0, 1.6 * s, 0] }, { x, y, z, yaw: 0 });
    };

    const lakeR = LAYOUT.lake.r + 8;
    for (let gx = -230; gx < 230; gx += 3.6) {
      for (let gz = -230; gz < 230; gz += 3.6) {
        const x = gx + (rng() - 0.5) * 3.2;
        const z = gz + (rng() - 0.5) * 3.2;
        const h = T.height(x, z);
        if (h < T.waterLevel(x, z) + 1.3) continue;
        T.normal(x, z, n);
        if (n.y < 0.86) continue;
        if (T.pathDistance(x, z) < 3.2) continue;
        if (Math.hypot(x - LAYOUT.lake.x, z - LAYOUT.lake.z) < lakeR) continue;
        if (this.isExcluded(x, z, 2.5)) continue;
        const tint = regionTints(x, z);
        const dV = Math.hypot(x - LAYOUT.village.x, z - LAYOUT.village.z);
        const dP = Math.hypot(x - LAYOUT.peak.x, z - LAYOUT.peak.z);
        if (dP < 34) continue; // zirve teraslari acik kalsin
        let dens = 0.035 + tint.forest * 0.55 + tint.meadow * 0.03 + tint.hollow * 0.1;
        dens += smoothstep(36, 48, dP) * (1 - smoothstep(60, 80, dP)) * 0.2;
        if (dV < 50) dens *= 0.12;
        if (tint.windy > 0.5) dens *= 0.4;
        if (rng() > dens) continue;
        let type;
        const r = rng();
        if (tint.forest > 0.4) type = r < 0.4 ? 'pine' : r < 0.65 ? 'birch' : 'round';
        else if (dP < 80) type = r < 0.8 ? 'pine' : 'round';
        else if (tint.meadow > 0.35) type = r < 0.55 ? 'blossom' : 'round';
        else if (tint.hollow > 0.4) type = r < 0.5 ? 'birch' : 'round';
        else type = r < 0.55 ? 'round' : r < 0.85 ? 'pine' : 'birch';
        addTree(type, x, z, 0.8 + rng() * 0.6, rng() * Math.PI * 2);
      }
    }
    for (const [x, z] of WD.VILLAGE.palms) addTree('palm', x, z, 0.9 + rng() * 0.3, rng() * 6);
    // kumsallarda birkac palmiye daha
    for (let i = 0; i < 220 && types.palm.length < 30; i++) {
      const a = rng() * Math.PI * 2;
      const rr = 130 + rng() * 40;
      const x = Math.cos(a) * rr;
      const z = Math.sin(a) * rr;
      const h = T.height(x, z);
      if (h < 1.0 || h > 2.6 || this.isExcluded(x, z, 3) || T.pathDistance(x, z) < 3) continue;
      addTree('palm', x, z, 0.9 + rng() * 0.3, rng() * 6);
    }

    const geos = {
      round: [N.roundTree(1), N.roundTree(2)],
      blossom: [N.roundTree(3, true)],
      pine: [N.pineTree(1), N.pineTree(2)],
      birch: [N.birchTree(1)],
      palm: [N.palmTree(1)],
    };
    const mat = swayMaterial(this.uTime, { roughness: 0.9 });
    this.treeCount = 0;
    for (const [type, list] of Object.entries(types)) {
      const variants = geos[type];
      variants.forEach((geo, vi) => {
        const items = list.filter((_, i) => i % variants.length === vi);
        if (!items.length) return;
        const im = new THREE.InstancedMesh(geo, mat, items.length);
        items.forEach((t, i) => {
          tmpQ.setFromAxisAngle(UP, t.yaw);
          tmpM.compose(tmpP.set(t.x, t.y, t.z), tmpQ, tmpS.set(t.s, t.s, t.s));
          im.setMatrixAt(i, tmpM);
          const k = 0.9 + rng() * 0.2;
          im.setColorAt(i, new THREE.Color(k, k, k));
        });
        im.castShadow = true;
        im.receiveShadow = true;
        im.computeBoundingSphere();
        this.scene.add(im);
        this.treeCount += items.length;
      });
    }

    // kayalar
    const rocks = [];
    for (let gx = -230; gx < 230; gx += 8) {
      for (let gz = -230; gz < 230; gz += 8) {
        const x = gx + (rng() - 0.5) * 7;
        const z = gz + (rng() - 0.5) * 7;
        const h = T.height(x, z);
        if (h < -1.5) continue;
        T.normal(x, z, n);
        if (T.pathDistance(x, z) < 2.5 || this.isExcluded(x, z, 2)) continue;
        if (Math.hypot(x - LAYOUT.lake.x, z - LAYOUT.lake.z) < LAYOUT.lake.r - 2) continue;
        let p = 0.03;
        if (n.y < 0.86 && n.y > 0.55) p += 0.25;
        if (h > -1.5 && h < 2.2) p += 0.06;
        if (Math.hypot(x - LAYOUT.village.x, z - LAYOUT.village.z) < 40) p *= 0.2;
        if (rng() > p) continue;
        rocks.push({ x, z, y: h - 0.25, s: 0.4 + rng() * 1.5, yaw: rng() * 6, v: Math.floor(rng() * 4) });
      }
    }
    const rockGeos = [0, 1, 2, 3].map((i) => N.rockGeo(i + 11, true));
    const rockMat = vcMaterial({ roughness: 0.95 });
    rockGeos.forEach((geo, vi) => {
      const items = rocks.filter((r) => r.v === vi);
      if (!items.length) return;
      const im = new THREE.InstancedMesh(geo, rockMat, items.length);
      const pos = geo.attributes.position;
      items.forEach((r, i) => {
        tmpQ.setFromAxisAngle(UP, r.yaw);
        tmpM.compose(tmpP.set(r.x, r.y, r.z), tmpQ, tmpS.set(r.s, r.s, r.s));
        im.setMatrixAt(i, tmpM);
        if (r.s > 0.7) {
          const pts = [];
          for (let k = 0; k < pos.count; k += 3) pts.push(pos.getX(k) * r.s, pos.getY(k) * r.s, pos.getZ(k) * r.s);
          this.physics.addCollider({ type: 'hull', points: pts, rot: [0, r.yaw, 0] }, { x: r.x, y: r.y, z: r.z, yaw: 0 });
        }
      });
      im.castShadow = true;
      im.receiveShadow = true;
      im.computeBoundingSphere();
      this.scene.add(im);
    });

    // calilar, cicekler, kucuk mantarlar (carpismasiz)
    const bushes = [];
    const flowers = [];
    const shrooms = [];
    const q = this.game.renderer.q;
    for (let gx = -230; gx < 230; gx += 1.6) {
      for (let gz = -230; gz < 230; gz += 1.6) {
        const x = gx + (rng() - 0.5) * 1.5;
        const z = gz + (rng() - 0.5) * 1.5;
        const h = T.height(x, z);
        if (h < T.waterLevel(x, z) + 0.6) continue;
        const r = rng();
        const tint = regionTints(x, z);
        const pd = T.pathDistance(x, z);
        if (pd < 1.8) continue;
        T.normal(x, z, n);
        if (n.y < 0.84) continue;
        if (this.isExcluded(x, z, 0.5)) continue;
        if (h < 1.9) continue;
        const fp = (0.004 + tint.meadow * 0.16 + tint.peak * 0.02) * q.flowers;
        if (r < fp) {
          flowers.push({ x, z, y: h, s: 0.8 + rng() * 0.6, yaw: rng() * 6, c: Math.floor(rng() * 6) });
        } else if (r < fp + 0.006 + tint.forest * 0.02) {
          bushes.push({ x, z, y: h - 0.1, s: 0.6 + rng() * 0.8, yaw: rng() * 6 });
        } else if (r < fp + 0.006 + tint.forest * 0.02 + (tint.forest + tint.hollow) * 0.03) {
          shrooms.push({ x, z, y: h, s: 0.6 + rng() * 1.2, yaw: rng() * 6, v: rng() < 0.7 ? 0 : 1 });
        }
      }
    }
    const inst = (geo, list, material, colorFn, shadow = false) => {
      if (!list.length) return null;
      const im = new THREE.InstancedMesh(geo, material, list.length);
      list.forEach((o, i) => {
        tmpQ.setFromAxisAngle(UP, o.yaw);
        tmpM.compose(tmpP.set(o.x, o.y, o.z), tmpQ, tmpS.set(o.s, o.s, o.s));
        im.setMatrixAt(i, tmpM);
        if (colorFn) im.setColorAt(i, colorFn(o));
      });
      im.castShadow = shadow;
      im.receiveShadow = true;
      im.computeBoundingSphere();
      this.scene.add(im);
      return im;
    };
    inst(N.bushGeo(1), bushes, swayMaterial(this.uTime, { roughness: 0.9 }), null, true);
    const petal = ['#ff7aa2', '#ffd84a', '#ffffff', '#c58bff', '#ff6b5a', '#7fc8ff'].map((c) => new THREE.Color(c));
    inst(N.flowerGeo(), flowers, vcMaterial({ flat: false, roughness: 0.8 }), (o) => petal[o.c]);
    inst(N.mushroomGeo(P.mushroomRed, 1), shrooms.filter((s) => s.v === 0), vcMaterial({ flat: false }), null);
    inst(N.mushroomGeo(P.mushroomBlue, 2), shrooms.filter((s) => s.v === 1), vcMaterial({ flat: false }), null);

    // ormandaki peri halkasi (s07 icinde)
    const ring = [];
    const rc = { x: -102, z: 22 };
    for (let i = 0; i < 11; i++) {
      const a = (i / 11) * Math.PI * 2;
      const x = rc.x + Math.cos(a) * 2.4;
      const z = rc.z + Math.sin(a) * 2.4;
      ring.push({ x, z, y: T.height(x, z), s: 1.3 + (i % 3) * 0.3, yaw: a, v: 0 });
    }
    inst(N.mushroomGeo('#ff8fb1', 7), ring, vcMaterial({ flat: false }), null);
  }

  buildGlow() {
    this.glowMat = new THREE.MeshStandardMaterial({ color: '#cfe8ff', emissive: '#ffc965', emissiveIntensity: 0, roughness: 0.3 });
    const glowGeo = merge(this.glowParts);
    this.glowMesh = new THREE.Mesh(glowGeo, this.glowMat);
    this.scene.add(this.glowMesh);
  }

  // Gece etkisi: pencereler ve fenerler yanar, kamp atesi isik verir.
  update(dt, t, sky, playerPos, particles) {
    this.uTime.value = t;
    for (let i = this.animated.length - 1; i >= 0; i--) {
      if (this.animated[i](t, dt) === true) this.animated.splice(i, 1);
    }
    const night = sky.night;
    const lit = smoothstep(0.15, 0.6, night);
    this.glowMat.emissiveIntensity = lit * 2.2;
    this.glowMat.color.setRGB(0.81 - lit * 0.3, 0.9 - lit * 0.35, 1 - lit * 0.5);
    this.fireLight.intensity = (1.5 + lit * 5) * (0.85 + Math.sin(t * 13) * 0.08 + Math.sin(t * 7.3) * 0.07);
    this.sea.update(dt, sky);
    this.lake.update(dt, sky);
    if (this.clouds) this.clouds.update(dt, sky);

    if (particles) {
      this._smokeT = (this._smokeT || 0) - dt;
      if (this._smokeT <= 0) {
        this._smokeT = 0.35;
        for (const s of this.smokeSources) {
          if (s.distanceToSquared(playerPos) > 120 * 120) continue;
          particles.normal.emit({ pos: s, count: 1, spread: 0.15, vel: { x: 0.3, y: 1.0, z: 0.1 }, velSpread: 0.15, life: 4, size: 0.7, sizeEnd: 2.2, color: ['#eef0f2', '#dfe3e8'], alpha: 0.45, drag: 0.2 });
        }
      }
      // kamp atesi
      if (this.campfirePos.distanceToSquared(playerPos) < 80 * 80) {
        particles.additive.emit({ pos: this.campfirePos, count: 2, spread: 0.25, vel: { x: 0, y: 1.6, z: 0 }, velSpread: 0.3, life: 0.8, size: 0.5, sizeEnd: 0.05, color: ['#ffb347', '#ff7a2a', '#ffd27a'], alpha: 0.9 });
      }
      // ruzgar sutunlari
      for (const u of WD.UPDRAFTS) {
        if ((u.x - playerPos.x) ** 2 + (u.z - playerPos.z) ** 2 > 90 * 90) continue;
        const y0 = this.groundY(u.x, u.z);
        particles.normal.emit({
          pos: new THREE.Vector3(u.x, y0 + 1 + Math.random() * 6, u.z), count: 1, spread: { x: u.r * 0.8, y: 0.5, z: u.r * 0.8 },
          vel: { x: 0, y: 9, z: 0 }, velSpread: 0.6, life: 2.4, size: 0.18, sizeEnd: 0.05, color: '#ffffff', alpha: 0.7,
        });
      }
      // ates bocekleri (gece, cayir ve orman)
      if (night > 0.5) {
        this._ffT = (this._ffT || 0) - dt;
        if (this._ffT <= 0) {
          this._ffT = 0.12;
          const a = Math.random() * Math.PI * 2;
          const r = 4 + Math.random() * 18;
          const x = playerPos.x + Math.cos(a) * r;
          const z = playerPos.z + Math.sin(a) * r;
          const tint = regionTints(x, z);
          if (tint.meadow + tint.forest + tint.hollow > 0.25 && this.terrain.height(x, z) > 1) {
            particles.additive.emit({ pos: new THREE.Vector3(x, this.terrain.height(x, z) + 0.6 + Math.random() * 1.6, z), count: 1, vel: { x: 0, y: 0.1, z: 0 }, life: 4, size: 0.22, color: ['#d8ff6a', '#fff27a'], alpha: 1, wobble: 2.5 });
          }
        }
      }
    }
  }
}
