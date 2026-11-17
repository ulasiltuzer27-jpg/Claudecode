import * as THREE from 'three';
import type { Assets } from '../core/Assets';
import { Random } from '../core/Random';
import { Colliders } from './Colliders';
import { CAMPS, distToPaths, LAKE, PATH_WIDTH, PLAY_RADIUS, RUINS, VILLAGE, WATER_LEVEL, dist2D } from './Layout';
import { SimplexNoise, smoothstep } from './Noise';
import { Particles } from './Particles';
import { packOf, placeModel, placementMatrix, scaleOf, type Placement } from './Props';
import { StaticBatcher } from './StaticBatcher';
import { Terrain } from './Terrain';

export interface Chest {
  id: string;
  pos: THREE.Vector3;
  obj: THREE.Object3D;
  lid: THREE.Object3D | null;
  opened: boolean;
  /** Görev sandığı: içindeki eşya görev aktifken verilir. */
  contents: { item: string; qty: number }[];
  gold: number;
}

export interface FireSource { pos: THREE.Vector3; light: THREE.PointLight | null; scale: number; base: number }

/** Köydeki NPC'lerin duracağı yerler (World kurarken binalara göre seçildi). */
export const NPC_SPOTS = {
  elder: { x: -4.5, z: 5.5, rot: Math.PI * 0.85 },
  merchant: { x: 17.5, z: 8.5, rot: -Math.PI * 0.6 },
  smith: { x: 14.5, z: -9.5, rot: -Math.PI * 0.3 },
  villager: { x: -12, z: -6, rot: Math.PI * 0.3 },
};

export class World {
  readonly group = new THREE.Group();
  readonly terrain: Terrain;
  readonly colliders = new Colliders();
  readonly chests: Chest[] = [];
  readonly fires: FireSource[] = [];
  readonly particles = new Particles(3500);
  /** Döngüsel ortam sesi kaynakları (AudioManager bağlar). */
  readonly ambience: { sound: string; pos: THREE.Vector3; volume: number; ref: number }[] = [];
  private boxCache = new Map<string, THREE.Box3>();
  private time = 0;
  readonly batcher = new StaticBatcher();

  constructor(private assets: Assets, private quality: 'low' | 'medium' | 'high') {
    this.terrain = new Terrain(1337, quality === 'low' ? 150 : 200);
    this.group.name = 'world';
  }

  build(): void {
    this.group.add(this.terrain.buildMesh());
    this.buildWater();
    this.buildVillage();
    this.buildRuins();
    this.buildCamps();
    this.buildVegetation();
    this.batcher.build(this.group);
    this.group.add(this.particles.points);
    this.ambience.push({ sound: 'amb_river', pos: new THREE.Vector3(LAKE.x + LAKE.r * 0.7, WATER_LEVEL + 1, LAKE.z - LAKE.r * 0.3), volume: 0.8, ref: 10 });
  }

  groundY(x: number, z: number): number {
    return this.terrain.heightAt(x, z);
  }

  /** Model yerel kutusu (ölçeksiz), çarpışma kutusu üretmek için. */
  private localBox(id: string): THREE.Box3 {
    let b = this.boxCache.get(id);
    if (!b) {
      const scene = this.assets.gltf(id).scene;
      scene.updateMatrixWorld(true);
      b = new THREE.Box3().setFromObject(scene);
      this.boxCache.set(id, b);
    }
    return b;
  }

  /**
   * Bir modeli dünyaya koy. Varsayılan: statik birleştiriciye gider (ayrı
   * nesne olarak kalmaz). `keep`: hareket eden/değişen nesneler (sandık).
   */
  private place(id: string, x: number, z: number, rot = 0, scale = 1, opts: { collide?: 'box' | 'circle' | false; shrink?: number; yOffset?: number; height?: number; keep?: boolean } = {}): THREE.Object3D {
    const y = this.groundY(x, z) + (opts.yOffset ?? 0);
    const holder = opts.keep ? this.group : new THREE.Group();
    const obj = placeModel(this.assets, id, { x, y, z, rot, scale }, holder);
    if (!opts.keep) this.batcher.addObject(packOf(id), obj);
    const collide = opts.collide ?? 'box';
    if (collide) {
      const b = this.localBox(id);
      const s = scale * scaleOf(id);
      const shrink = opts.shrink ?? 0.9;
      const cx = ((b.min.x + b.max.x) / 2) * s;
      const cz = ((b.min.z + b.max.z) / 2) * s;
      const wx = x + cx * Math.cos(rot) + cz * Math.sin(rot);
      const wz = z - cx * Math.sin(rot) + cz * Math.cos(rot);
      const hw = ((b.max.x - b.min.x) / 2) * s * shrink;
      const hd = ((b.max.z - b.min.z) / 2) * s * shrink;
      const height = opts.height ?? (b.max.y - b.min.y) * s;
      if (collide === 'box') this.colliders.addBox(wx, wz, hw, hd, rot, height);
      else this.colliders.addCircle(wx, wz, Math.max(hw, hd), height);
    }
    return obj;
  }

  /** Binayı köy meydanına bakacak şekilde döndür. */
  private facing(x: number, z: number, tx = VILLAGE.x, tz = VILLAGE.z): number {
    return Math.atan2(tx - x, tz - z);
  }

  // -------------------------------------------------------------------------

  private buildWater(): void {
    const geo = new THREE.CircleGeometry(PLAY_RADIUS + 80, 64);
    geo.rotateX(-Math.PI / 2);
    const mat = new THREE.MeshStandardMaterial({
      color: 0x2f74a8, roughness: 0.12, metalness: 0.15, transparent: true, opacity: 0.82,
    });
    const water = new THREE.Mesh(geo, mat);
    water.position.y = WATER_LEVEL;
    water.receiveShadow = true;
    water.name = 'water';
    this.group.add(water);
  }

  private addFire(x: number, z: number, scale: number, withLight: boolean, y?: number): void {
    const pos = new THREE.Vector3(x, y ?? this.groundY(x, z) + 0.3, z);
    let light: THREE.PointLight | null = null;
    if (withLight) {
      light = new THREE.PointLight(0xff9a40, 40 * scale, 22 * scale, 1.6);
      light.position.copy(pos).add(new THREE.Vector3(0, 1, 0));
      this.group.add(light);
    }
    this.fires.push({ pos, light, scale, base: 40 * scale });
    this.ambience.push({ sound: 'amb_fire', pos, volume: 0.9 * scale, ref: 3 * scale });
  }

  private buildVillage(): void {
    const B: [string, number, number][] = [
      ['b_tavern', -21, -13],
      ['b_blacksmith', 19, -15],
      ['b_market', 23, 15],
      ['b_church', -23, 23],
      ['b_home_A', -13, -26],
      ['b_home_B', 9, -27],
      ['b_home_A', 12, 30],
      ['b_home_B', -31, -2],
      ['b_windmill', -33, -30],
      ['b_tower_A', 34, -28],
    ];
    for (const [id, x, z] of B) this.place(id, x, z, this.facing(x, z));

    // Kuyu meydanın ortasında
    this.place('b_well', 0, -3, 0, 1, { collide: 'circle', shrink: 0.75 });

    // Kamp ateşi: kütükler + alev + ışık
    this.place('p_resource_lumber', 6, 6, 0.4, 0.6, { collide: 'circle', shrink: 0.7, height: 0.8 });
    this.addFire(6, 6, 1, true);

    // Demirci önü
    this.place('p_weaponrack', 13, -17, this.facing(13, -17), 1, { collide: 'box' });
    this.place('p_barrel', 24, -9, 0.3, 1, { collide: 'circle', height: 1.3 });
    this.place('p_barrel', 25.5, -10.5, 1.1, 1, { collide: 'circle', height: 1.3 });
    // Pazar önü
    this.place('p_crate_a_big', 16, 19, 0.2, 1, { height: 1.2 });
    this.place('p_crate_b_small', 15, 21, 0.9, 1, { height: 1 });
    this.place('p_sack', 27, 9, 0.5, 1, { collide: 'circle' });
    this.place('p_wheelbarrow', -26, -22, 1.4, 1);
    this.place('p_resource_lumber', -28, -36, 0.2, 1);
    this.place('p_tent', -10, 22, this.facing(-10, 22), 1);
    this.place('p_target', 32, 4, -Math.PI / 2, 1, { collide: 'circle', shrink: 0.4 });
    this.place('p_bucket_water', 2.5, -0.5, 0, 1, { collide: false });
    this.place('p_flag_red', 9, -2, 0, 1, { collide: 'circle', shrink: 0.2 });
    this.place('p_flag_red', -9, 3, 0, 1, { collide: 'circle', shrink: 0.2 });

    // Köyün kuzey ve güney kenarına çit sıraları (yollar açık kalıyor)
    const fence = (x0: number, z0: number, x1: number, z1: number) => {
      const len = Math.hypot(x1 - x0, z1 - z0);
      const seg = 3.9;
      const n = Math.floor(len / seg);
      const rot = Math.atan2(x1 - x0, z1 - z0);
      for (let i = 0; i < n; i++) {
        const t = (i + 0.5) / n;
        const x = x0 + (x1 - x0) * t;
        const z = z0 + (z1 - z0) * t;
        // Çit modeli yerel x=-1'de duruyor (köşe pivotu); merkezle.
        this.place('p_fence_wood_straight', x + Math.cos(rot) * 3.4, z - Math.sin(rot) * 3.4, rot, 1, { collide: 'box', shrink: 1, height: 1.6 });
      }
    };
    fence(-44, 8, -44, 34);
    fence(44, -18, 44, 0);
    fence(16, 44, 40, 32);
  }

  private buildRuins(): void {
    const cx = RUINS.x;
    const cz = RUINS.z;
    // Taş zemin: 4x4 karolar
    for (let i = -4; i <= 4; i++) {
      for (let j = -4; j <= 4; j++) {
        if (Math.hypot(i, j) > 4.6) continue;
        const id = (i * 7 + j * 3) % 5 === 0 ? 'd_floor_tile_large_rocks' : 'd_floor_tile_large';
        this.place(id, cx + i * 4, cz + j * 4, ((i + j) & 3) * (Math.PI / 2), 1, { collide: false, yOffset: 0.02 });
      }
    }
    // Duvar halkası: aralarda yıkık parçalar ve girişler
    const R = 20;
    const n = 26;
    for (let k = 0; k < n; k++) {
      const a = (k / n) * Math.PI * 2;
      // Güneybatıda (köye bakan taraf) ve doğuda giriş boşlukları
      const deg = (a * 180) / Math.PI;
      if ((deg > 200 && deg < 245) || (deg > 345 || deg < 8)) continue;
      const x = cx + Math.cos(a) * R;
      const z = cz + Math.sin(a) * R;
      const rot = -a + Math.PI / 2;
      const variant = k % 5 === 0 ? 'd_wall_broken' : k % 4 === 0 ? 'd_wall_cracked' : k % 7 === 0 ? 'd_wall_half' : 'd_wall';
      this.place(variant, x, z, rot, 1, { collide: 'box', shrink: 1 });
      if (k % 3 === 0) this.place('d_pillar', cx + Math.cos(a + 0.12) * R, cz + Math.sin(a + 0.12) * R, rot, 1, { collide: 'box', shrink: 0.8 });
    }
    // İç avlu: sütunlar, moloz, sancak
    const inner: [string, number, number, number][] = [
      ['d_pillar_decorated', -8, -8, 0], ['d_pillar_decorated', 8, -8, 0], ['d_pillar_decorated', -8, 8, 0], ['d_pillar_decorated', 8, 8, 0],
      ['d_column', -12, 0, 0], ['d_column', 12, 2, 0],
      ['d_rubble_large', 4, 14, 0.6], ['d_rubble_half', -13, -9, 1.2], ['d_rubble_half', 15, -6, 2.4],
      ['d_sword_shield_broken', -3, 10, 0.4], ['d_barrel_large', 10, 12, 0], ['d_box_large', 11.5, 13.5, 0.5],
      ['d_table_medium_broken', -10, 11, 0.8],
    ];
    for (const [id, dx, dz, rot] of inner) this.place(id, cx + dx, cz + dz, rot, 1, { collide: id.includes('sword') ? false : 'box', shrink: 0.85 });
    // Meşaleler: dört süslü sütunun önünde
    for (const [dx, dz] of [[-8, -8], [8, -8], [-8, 8], [8, 8]] as const) {
      const x = cx + dx * 0.78;
      const z = cz + dz * 0.78;
      this.place('d_torch_lit', x, z, 0, 1.2, { collide: 'circle', shrink: 0.3 });
      this.addFire(x, z, 0.55, Math.abs(dx) === 8 && dz === -8 || (dx === 8 && dz === 8), this.groundY(x, z) + 2.2);
    }
    // Boss sandığı (ödül)
    this.addChest('ruins_chest', cx + 2, cz - 14, Math.PI, true, [{ item: 'big_health_potion', qty: 2 }, { item: 'spike_shield', qty: 1 }], 120);
    // Harabeye giden yolun başında yıkık bina
    this.place('b_destroyed', RUINS.x - 34, RUINS.z + 24, 0.7, 1);
  }

  private buildCamps(): void {
    const r = new Random(99);
    for (const c of CAMPS) {
      if (c.id === 'ruins_inner') continue;
      // Kamp merkezi: sönmüş ateş + moloz + kırık eşya
      this.place('p_resource_lumber', c.x, c.z, r.range(0, 6), 0.55, { collide: 'circle', shrink: 0.7, height: 0.8 });
      for (let k = 0; k < 3; k++) {
        const a = r.range(0, Math.PI * 2);
        const d = r.range(c.r * 0.6, c.r * 0.95);
        const id = r.pick(['d_rubble_half', 'd_barrel_large', 'd_sword_shield_broken', 'p_tent', 'd_table_medium_broken']);
        this.place(id, c.x + Math.cos(a) * d, c.z + Math.sin(a) * d, r.range(0, 6), 1, { collide: id === 'd_sword_shield_broken' ? false : 'box', shrink: 0.8 });
      }
      if (c.chest) this.addChest(c.chest, c.x + 3, c.z - 3, 0.6, false, [{ item: 'amulet_elder', qty: 1 }], 35);
    }
    // Yol kenarı: tek tük sandık (keşif ödülü)
    this.addChest('lake_chest', LAKE.x + 30, LAKE.z - 26, 2.1, false, [{ item: 'mana_potion', qty: 2 }], 25);
    this.addChest('south_chest', -42, 120, 0.2, false, [{ item: 'iron_axe', qty: 1 }], 40);
  }

  private addChest(id: string, x: number, z: number, rot: number, gold: boolean, contents: Chest['contents'], goldAmount: number): void {
    const model = gold ? 'd_chest_gold' : 'd_chest';
    const obj = this.place(model, x, z, rot, 1, { collide: 'box', shrink: 0.9, height: 1, keep: true });
    // Kapak düğümü varsa açılırken döndürülecek.
    let lid: THREE.Object3D | null = null;
    obj.traverse((o) => { if (!lid && /lid|top/i.test(o.name)) lid = o; });
    this.chests.push({ id, pos: new THREE.Vector3(x, this.groundY(x, z), z), obj, lid, opened: false, contents, gold: goldAmount });
  }

  private buildVegetation(): void {
    let count = 0;
    const m4 = new THREE.Matrix4();
    const add = (id: string, p: Placement) => {
      count++;
      const grass = id.startsWith('n_grass');
      const small = grass || id.startsWith('n_bush') || (id.startsWith('n_rock') && id !== 'n_rock_1_j');
      this.batcher.addInstance('nature', this.assets.gltf(id).scene, placementMatrix(id, p, m4), {
        castShadow: !grass && !id.startsWith('n_bush'),
        maxDist: (grass ? 70 : small ? 150 : 270) * view,
      });
    };
    const rnd = new Random(4242);
    const forest = new SimplexNoise(777);
    const t = this.terrain;
    const R = PLAY_RADIUS + 25;
    // Düşük kalite gerçekten hafif: çimen yok, yalnızca az poligonlu ağaçlar,
    // daha seyrek orman, kısa görüş mesafesi (sis de yaklaşıyor, bkz. Sky).
    const low = this.quality === 'low';
    const grassFactor = low ? 0 : this.quality === 'medium' ? 0.6 : 1;
    const view = low ? 0.55 : 1;

    const clearOf = (x: number, z: number, pad: number) => {
      if (dist2D(x, z, VILLAGE.x, VILLAGE.z) < VILLAGE.r + pad) return false;
      if (dist2D(x, z, RUINS.x, RUINS.z) < RUINS.r * 0.75 + pad) return false;
      if (distToPaths(x, z) < PATH_WIDTH + pad) return false;
      for (const c of CAMPS) if (dist2D(x, z, c.x, c.z) < c.r + pad) return false;
      return true;
    };
    const place = (id: string, x: number, z: number, s: number): Placement => {
      const p = { x, y: t.heightAt(x, z) - 0.1, z, rot: rnd.range(0, Math.PI * 2), scale: s };
      add(id, p);
      return p;
    };

    // Üçgen sayıları: 2_a 336, 5_a 384, 4_a 404, 1_a 530, 1_b 636, 2_c 648, 6_a 704, 3_a 978
    const TREES = low ? ['n_tree_2_a', 'n_tree_5_a', 'n_tree_4_a'] : ['n_tree_1_a', 'n_tree_1_b', 'n_tree_2_a', 'n_tree_2_c', 'n_tree_3_a', 'n_tree_4_a', 'n_tree_5_a', 'n_tree_6_a'];
    const BARE = ['n_tree_bare_1_a', 'n_tree_bare_2_a'];
    const BUSH = ['n_bush_1_a', 'n_bush_2_a', 'n_bush_4_a'];
    const ROCKS = ['n_rock_1_a', 'n_rock_2_a', 'n_rock_3_a', 'n_rock_3_e'];

    // Ağaçlar: 6x6 hücre, her hücrede en fazla bir ağaç (jitter'lı)
    const cell = 6.2;
    for (let gx = -R; gx < R; gx += cell) {
      for (let gz = -R; gz < R; gz += cell) {
        const x = gx + rnd.range(0, cell);
        const z = gz + rnd.range(0, cell);
        if (Math.hypot(x, z) > R) continue;
        const h = t.heightAt(x, z);
        if (h < WATER_LEVEL + 1.2 || t.slopeAt(x, z) > 0.4) continue;
        if (!clearOf(x, z, 3)) continue;
        // Kuzey yarı daha ormanlık; harabe çevresi çıplak ağaçlı.
        let dens = smoothstep(-0.1, 0.5, forest.fbm(x * 0.011, z * 0.011, 3)) * 0.75;
        dens += smoothstep(-20, -120, z) * 0.25;
        const nearRuins = dist2D(x, z, RUINS.x, RUINS.z) < RUINS.r + 40;
        if (rnd.next() < (dens * 0.8 + 0.025) * (low ? 0.6 : 1)) {
          const id = nearRuins && rnd.chance(0.7) ? rnd.pick(BARE) : rnd.pick(TREES);
          const s = rnd.range(0.85, 1.25);
          place(id, x, z, s);
          this.colliders.addCircle(x, z, 0.55 * s, 99);
        } else if (rnd.next() < 0.18 + dens * 0.3) {
          place(rnd.pick(BUSH), x, z, rnd.range(0.8, 1.4));
        }
      }
    }
    // Kayalar: seyrek, eğimde ve dağ eteğinde sık
    for (let k = 0; k < 900; k++) {
      const a = rnd.range(0, Math.PI * 2);
      const d = Math.sqrt(rnd.next()) * R;
      const x = Math.cos(a) * d;
      const z = Math.sin(a) * d;
      const h = t.heightAt(x, z);
      if (h < WATER_LEVEL + 0.5 || !clearOf(x, z, 1.5)) continue;
      const mountain = smoothstep(PLAY_RADIUS - 40, PLAY_RADIUS, d);
      if (rnd.next() > 0.2 + mountain * 0.8 + t.slopeAt(x, z)) continue;
      if (rnd.chance(0.08)) {
        place('n_rock_1_j', x, z, rnd.range(0.8, 1.5));
        this.colliders.addCircle(x, z, 2.2, 99);
      } else {
        const s = rnd.range(0.7, 1.8);
        place(rnd.pick(ROCKS), x, z, s);
        this.colliders.addCircle(x, z, 0.55 * s, 1.0 * s);
      }
    }
    // Çimen tutamları: çarpışmasız, gölgesiz, kalite ayarına bağlı
    const gcell = 3.2 / Math.sqrt(Math.max(grassFactor, 0.01));
    for (let gx = -R; grassFactor > 0 && gx < R; gx += gcell) {
      for (let gz = -R; gz < R; gz += gcell) {
        const x = gx + rnd.range(0, gcell);
        const z = gz + rnd.range(0, gcell);
        if (Math.hypot(x, z) > PLAY_RADIUS) continue;
        if (t.surfaceAt(x, z) !== 'grass' || rnd.next() > 0.45) continue;
        if (dist2D(x, z, VILLAGE.x, VILLAGE.z) < 12) continue;
        place(rnd.chance(0.5) ? 'n_grass_1_a' : 'n_grass_2_a', x, z, rnd.range(0.8, 1.3));
      }
    }
    // Göl kıyısında saz/çalı
    for (let k = 0; k < 60; k++) {
      const a = rnd.range(0, Math.PI * 2);
      const d = LAKE.r * rnd.range(0.85, 1.15);
      const x = LAKE.x + Math.cos(a) * d;
      const z = LAKE.z + Math.sin(a) * d;
      if (t.heightAt(x, z) < WATER_LEVEL + 0.2) continue;
      place(rnd.pick(BUSH), x, z, rnd.range(0.8, 1.3));
    }

    this.vegetationCount = count;
  }

  vegetationCount = 0;

  /** Kamera konumuna göre birleşik parçaları göster/gizle. */
  cull(cam: THREE.Vector3): void {
    this.batcher.cull(cam);
  }

  get batchCount(): number {
    return this.batcher.chunks.length;
  }

  update(dt: number): void {
    this.time += dt;
    for (const f of this.fires) {
      if (Math.random() < 0.7) this.particles.flame(f.pos, f.scale);
      if (f.light) f.light.intensity = f.base * (0.85 + Math.sin(this.time * 13 + f.pos.x) * 0.08 + Math.random() * 0.12);
    }
    for (const c of this.chests) {
      if (!c.opened && Math.random() < 0.25) this.particles.sparkle(c.pos.clone().setY(c.pos.y + 1.2), 0xffe08a);
    }
    this.particles.update(dt);
  }

  /** Oyuncu derin suya giremez (yüzme yok). */
  walkable(x: number, z: number): boolean {
    if (Math.hypot(x, z) > PLAY_RADIUS) return false;
    return this.terrain.heightAt(x, z) > WATER_LEVEL - 1.1;
  }

  openChest(c: Chest): void {
    c.opened = true;
    if (c.lid) {
      c.lid.matrixAutoUpdate = true;
      c.lid.rotation.x -= 1.1;
      c.lid.updateMatrix();
      c.obj.updateMatrixWorld(true);
    } else {
      // Kapak ayrı düğüm değilse sandık "boşaldı" diye hafifçe batırılır.
      c.obj.position.y -= 0.15;
      c.obj.updateMatrix();
      c.obj.updateMatrixWorld(true);
    }
  }
}
