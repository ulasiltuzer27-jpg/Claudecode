import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import shopJson from '../data/shop.json';
import { AudioManager } from '../audio/AudioManager';
import { Enemy } from '../entities/Enemy';
import { LootDrop } from '../entities/Loot';
import { Npc, type NpcRole } from '../entities/Npc';
import { Player } from '../entities/Player';
import type { Projectile } from '../entities/Projectile';
import { ThirdPersonCamera } from '../entities/ThirdPersonCamera';
import { T } from '../i18n/tr';
import { item, slotFor } from '../rpg/Items';
import { rollLoot } from '../rpg/Loot';
import { QuestLog } from '../rpg/Quests';
import { Hud } from '../ui/Hud';
import { Icons } from '../ui/Icons';
import { buyPrice, DIALOG, Panels, rewardText, sellPrice, type Settings } from '../ui/Panels';
import { CAMPS, PLAYER_START, VILLAGE, zoneAt } from '../world/Layout';
import { SkySystem } from '../world/Sky';
import { NPC_SPOTS, World, type Chest } from '../world/World';
import { Assets } from './Assets';
import type { FloatKind, GameCtx } from './Context';
import { EventBus } from './EventBus';
import { Input } from './Input';
import { Random, rng } from './Random';
import { loadSave, readJson, removeKey, SAVE_KEY, SETTINGS_KEY, writeJson, type SaveData } from './Save';

type Mode = 'loading' | 'title' | 'menu' | 'play' | 'paused' | 'dead';

const DEFAULT_SETTINGS: Settings = { master: 0.8, music: 0.5, sfx: 0.9, sensitivity: 1, invertY: false, quality: 'medium' };

/**
 * Oyunun orkestratörü: render döngüsü, modlar (menü/oyun/duraklatma),
 * varlıkların güncellenmesi, etkileşim, görev ve diyalog akışı, müzik.
 * Varlıklar Game'i değil GameCtx arayüzünü görür.
 */
export class Game implements GameCtx {
  readonly renderer: THREE.WebGLRenderer;
  readonly scene = new THREE.Scene();
  readonly camera = new THREE.PerspectiveCamera(60, 1, 0.1, 1500);
  readonly assets = new Assets();
  readonly bus = new EventBus();
  readonly input: Input;
  readonly tpc: ThirdPersonCamera;
  audio!: AudioManager;
  world!: World;
  sky!: SkySystem;
  player!: Player;
  enemies: Enemy[] = [];
  projectiles: Projectile[] = [];
  npcs: Npc[] = [];
  loot: LootDrop[] = [];
  quests = new QuestLog();
  hud!: Hud;
  panels!: Panels;
  icons = new Icons();
  settings: Settings;
  mode: Mode = 'loading';
  time = 0;
  readonly clock = new THREE.Clock();
  private lightPool: { light: THREE.PointLight; busy: boolean }[] = [];
  private boss: Enemy | null = null;
  bossDefeated = false;
  private combatTimer = 0;
  private autosaveTimer = 0;
  private menuAngle = 0;
  private interactTarget: { kind: 'npc'; npc: Npc } | { kind: 'chest'; chest: Chest } | null = null;
  private birdTimer = 6;
  private fps = { frames: 0, acc: 0, value: 0 };
  ready = false;
  /** Test: render'ı dondur (ekran görüntüsü alırken CPU'yu yazılım GPU'ya kaptırmamak için). */
  frozen = false;
  frameCount = 0;
  readonly maxDt = Math.min(0.25, Number(new URLSearchParams(location.search).get('maxdt')) || 0.05);
  /** Teşhis: render'ı atla (yalnızca güncelleme maliyetini ölçmek için). */
  skipRender = false;

  constructor(private canvas: HTMLCanvasElement, readonly uiRoot: HTMLElement) {
    const saved = readJson<Settings>(SETTINGS_KEY);
    this.settings = { ...DEFAULT_SETTINGS, ...(saved ?? {}) };
    const q = new URLSearchParams(location.search).get('quality');
    if (q === 'low' || q === 'medium' || q === 'high') this.settings.quality = q;

    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: this.settings.quality !== 'low', powerPreference: 'high-performance' });
    const dpr = window.devicePixelRatio || 1;
    this.renderer.setPixelRatio(this.settings.quality === 'low' ? 1 : Math.min(dpr, this.settings.quality === 'high' ? 2 : 1.5));
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 0.85;
    const params = new URLSearchParams(location.search);
    this.renderer.shadowMap.enabled = params.get('shadows') !== '0';
    this.renderer.shadowMap.type = this.settings.quality === 'low' ? THREE.PCFShadowMap : THREE.PCFSoftShadowMap;
    if (params.get('scale')) this.renderer.setPixelRatio(Number(params.get('scale')));
    this.input = new Input(canvas);
    this.input.onLockFailed = () => this.hud?.toast('Fare kilidi kullanılamıyor: kamerayı fareyi gezdirerek döndür', 'info');
    this.tpc = new ThirdPersonCamera(this.camera);
    window.addEventListener('resize', () => this.resize());
    this.resize();
    canvas.addEventListener('click', () => {
      if (this.mode === 'play' && !this.panels.openPanel) this.input.requestLock();
    });
  }

  private resize(): void {
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.world?.particles.setViewportHeight(h * this.renderer.getPixelRatio());
  }

  // ===========================================================================
  // Yükleme
  // ===========================================================================

  async init(): Promise<void> {
    this.hud = new Hud(this.uiRoot, this.icons);
    this.panels = new Panels(this.uiRoot, this.icons);
    const loading = this.panels.loading();
    this.renderer.setAnimationLoop(() => this.frame());

    // three.js tek bir paylaşılan AudioContext kullanıyor; sesler onda çözülür.
    await this.assets.load(THREE.AudioContext.getContext(), (f, l) => loading.set(f, l));

    const pmrem = new THREE.PMREMGenerator(this.renderer);
    this.scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();

    this.world = new World(this.assets, this.settings.quality);
    this.world.build();
    this.scene.add(this.world.group);
    this.sky = new SkySystem(this.scene, this.settings.quality === 'low' ? 1024 : 2048, this.settings.quality === 'low' ? 200 : 360);
    this.resize();

    this.audio = new AudioManager(this.assets, this.scene);
    this.camera.add(this.audio.listener);
    this.scene.add(this.camera);
    this.applySettings();
    for (const a of this.world.ambience) this.audio.addLoop(a.sound, a.pos, a.volume, a.ref);

    for (let i = 0; i < 3; i++) {
      const light = new THREE.PointLight(0xffffff, 0, 14, 1.8);
      light.position.set(0, -50, 0);
      this.scene.add(light);
      this.lightPool.push({ light, busy: false });
    }

    this.icons.build(this.renderer, this.assets);
    this.hud.bakeMap(this.world.terrain);

    this.player = new Player(this.assets);
    this.scene.add(this.player.char.root);
    this.spawnNpcs();
    this.wireEvents();
    this.newGameState();

    // Bütün materyalleri bir kez derle: oyunun ilk saniyelerinde takılma olmasın.
    this.renderer.compile(this.scene, this.camera);
    this.ready = true;
    this.mode = 'title';
    this.showTitle();
  }

  private applySettings(): void {
    const s = this.settings;
    this.audio.volumes = { master: s.master, music: s.music, sfx: s.sfx };
    this.audio.applyVolumes();
    this.tpc.sensitivity = s.sensitivity;
    this.tpc.invertY = s.invertY;
    writeJson(SETTINGS_KEY, s);
  }

  private spawnNpcs(): void {
    const def = (role: NpcRole, model: string, builtin?: [('right' | 'left'), string][]) => {
      const s = NPC_SPOTS[role];
      const n = new Npc(this.assets, role, DIALOG[role].name, model, s.x, this.world.groundY(s.x, s.z), s.z, s.rot, builtin);
      this.scene.add(n.char.root);
      this.world.colliders.addCircle(s.x, s.z, 0.6, 99);
      this.npcs.push(n);
    };
    def('elder', 'mage', [['right', '2H_Staff']]);
    def('merchant', 'rogue');
    def('smith', 'barbarian', [['right', '1H_Axe']]);
    def('villager', 'barbarian', [['right', 'Mug']]);
  }

  // ===========================================================================
  // Oyun durumu
  // ===========================================================================

  /** Yeni oyun: oyuncu, envanter, görevler, düşmanlar, sandıklar sıfırlanır. */
  private newGameState(): void {
    const p = this.player;
    p.level = 1;
    p.xp = 0;
    p.inventory.load({ slots: [], equipment: { mainhand: 'rusty_sword', offhand: 'round_shield', ring: null }, gold: 15 });
    p.inventory.add('health_potion', 3);
    p.inventory.add('mana_potion', 1);
    p.hotbar = ['health_potion', 'mana_potion', 'big_health_potion', null];
    p.refreshEquipment();
    p.hp = p.stats.maxHp;
    p.mana = p.stats.maxMana;
    p.stamina = p.stats.maxStamina;
    p.respawn(PLAYER_START.x, PLAYER_START.z, this.world.groundY(PLAYER_START.x, PLAYER_START.z));
    p.facing = Math.PI;
    this.tpc.yaw = 0;
    this.tpc.snap();
    this.quests = new QuestLog();
    this.wireQuestLog();
    this.bossDefeated = false;
    for (const c of this.world.chests) if (c.opened) this.closeChest(c);
    for (const l of this.loot) l.obj.removeFromParent();
    this.loot = [];
    this.respawnAllEnemies();
    this.sky.timeOfDay = 0.36;
  }

  private closeChest(c: Chest): void {
    c.opened = false;
    if (c.lid) { c.lid.rotation.x += 1.1; c.lid.updateMatrix(); }
    else { c.obj.position.y += 0.15; c.obj.updateMatrix(); }
    c.obj.updateMatrixWorld(true);
  }

  private respawnAllEnemies(): void {
    for (const e of this.enemies) e.char.dispose();
    this.enemies = [];
    this.boss = null;
    const r = new Random(31337);
    for (const camp of CAMPS) {
      for (const grp of camp.enemies) {
        if (grp.type === 'bone_lord' && this.bossDefeated) continue;
        for (let k = 0; k < grp.count; k++) {
          const a = (k / grp.count) * Math.PI * 2 + r.range(0, 0.8);
          const d = grp.type === 'bone_lord' ? 0 : r.range(2.5, camp.r * 0.8);
          const e = this.spawnEnemy(grp.type, camp.x + Math.cos(a) * d, camp.z + Math.sin(a) * d, { dormant: grp.dormant, campId: camp.id });
          if (e.isBoss) this.boss = e;
        }
      }
    }
    // Yollarda dolaşan devriyeler
    for (const [x, z, t] of [[40, -40, 'skeleton_minion'], [-60, -50, 'skeleton_rogue'], [60, 70, 'skeleton_minion'], [-70, 80, 'skeleton_warrior']] as const) {
      this.spawnEnemy(t, x, z);
    }
  }

  spawnEnemy(type: string, x: number, z: number, opts: { dormant?: boolean; campId?: string } = {}): Enemy {
    const e = new Enemy(this.assets, type, x, z, this.world.groundY(x, z), opts);
    this.scene.add(e.char.root);
    this.enemies.push(e);
    return e;
  }

  private wireQuestLog(): void {
    this.quests.onChange = (id, status) => {
      const q = this.quests.def(id);
      if (status === 'complete') {
        this.hud.toast(`${T.questReady}: ${q.name}`, 'quest');
        this.audio.play2D('quest_accept', { ui: true, jitter: 0 });
      }
      if (status === 'available' && id !== 'q1_bones') this.hud.toast(`Bilge Ozan seninle konuşmak istiyor`, 'quest');
      this.refreshMarkers();
    };
    this.refreshMarkers();
  }

  private wireEvents(): void {
    this.player.inventory.onChange = () => {
      for (const q of this.quests.active()) for (const o of q.objectives) if (o.type === 'collect' && o.item) this.quests.onItemCount(o.item, this.player.inventory.count(o.item));
      this.refreshOpenPanel();
    };
    this.bus.on('enemyKilled', (e) => {
      this.quests.onKill(e.type);
      this.player.gainXp(e.xp, this);
      const enemy = this.enemies.find((x) => x.type === e.type && !x.alive && Math.abs(x.char.position.x - e.x) < 0.01);
      const pos = new THREE.Vector3(e.x, this.world.groundY(e.x, e.z) + 0.8, e.z);
      this.floatText(pos.clone().setY(pos.y + 2.2), `+${e.xp} XP`, 'xp');
      if (enemy) this.dropLoot(enemy.def.loot, pos);
    });
    this.bus.on('levelUp', (e) => this.hud.banner(T.levelUp(e.level), 'Can, mana ve hasar arttı'));
    this.bus.on('notify', (n) => this.hud.toast(n.text, n.kind));
    this.bus.on('playerDamaged', () => { this.hud.hurt(); this.combatTimer = 5; });
    this.bus.on('bossEngaged', () => {
      this.hud.toast('Kemik Lordu uyandı!', 'warn');
      this.combatTimer = 6;
    });
    this.bus.on('bossDefeated', () => {
      this.bossDefeated = true;
      this.hud.banner('Kemik Lordu düştü', 'Vadi özgür');
      this.save(true);
    });
    this.bus.on('playerDied', () => {
      this.mode = 'dead';
      this.input.releaseLock();
      this.panels.death(() => this.respawnPlayer());
    });
  }

  private refreshMarkers(): void {
    for (const n of this.npcs) {
      if (n.role !== 'elder') { n.setMarker(''); continue; }
      const f = this.quests.forGiver('elder');
      n.setMarker(!f ? '' : f.status === 'available' ? '!' : f.status === 'complete' ? '?' : '');
    }
  }

  private dropLoot(table: string, pos: THREE.Vector3): void {
    const drop = rollLoot(table, rng);
    if (drop.gold > 0) this.addLoot(new LootDrop(this.assets, 'gold', 'gold', drop.gold, pos));
    for (const it of drop.items) this.addLoot(new LootDrop(this.assets, 'item', it.id, it.qty, pos));
  }

  private addLoot(l: LootDrop): void {
    this.scene.add(l.obj);
    this.loot.push(l);
  }

  private respawnPlayer(): void {
    this.panels.hideScreen();
    const lost = Math.floor(this.player.inventory.gold * 0.1);
    this.player.inventory.addGold(-lost);
    this.player.respawn(PLAYER_START.x, PLAYER_START.z, this.world.groundY(PLAYER_START.x, PLAYER_START.z));
    this.tpc.snap();
    this.mode = 'play';
    this.input.requestLock();
    if (lost > 0) this.hud.toast(`-${lost} altın`, 'warn');
  }

  // ===========================================================================
  // Menüler
  // ===========================================================================

  private showTitle(): void {
    this.panels.hideScreen();
    // İlk tıklama ses kilidini açar; ondan önce ana menüye geçmiyoruz.
    const s = document.createElement('div');
    s.className = 'screen';
    s.innerHTML = `<h1 class="title">${T.title}</h1><div class="subtitle">${T.subtitle}</div><button class="btn">${T.clickToStart}</button>`;
    this.panels.root.appendChild(s);
    // Dokunmatik cihaz: oyun klavye + fare istiyor, baştan söyle.
    if (matchMedia('(pointer: coarse)').matches && !matchMedia('(any-pointer: fine)').matches) {
      const note = document.createElement('p');
      note.className = 'credits';
      note.style.marginTop = '22px';
      note.textContent = 'Gölge Vadisi klavye ve fareyle oynanır. En iyi deneyim için bilgisayardan aç.';
      s.appendChild(note);
    }
    const go = async () => {
      s.remove();
      await this.audio.unlock();
      this.audio.startLoops();
      this.showMainMenu();
    };
    s.querySelector('button')!.addEventListener('click', go);
    (window as unknown as { __startGame?: () => Promise<void> }).__startGame = go;
  }

  private showMainMenu(): void {
    this.mode = 'menu';
    this.hud.show(false);
    this.input.releaseLock();
    this.audio.setMusic('menu');
    this.panels.mainMenu({
      hasSave: !!loadSave(),
      onNew: () => { this.click(); removeKey(SAVE_KEY); this.newGameState(); this.startPlay(true); },
      onContinue: () => { this.click(); const d = loadSave(); if (d) { this.newGameState(); this.applySave(d); this.startPlay(false); } },
      onSettings: () => { this.click(); this.showSettings(() => this.showMainMenu()); },
      onCredits: () => { this.click(); this.panels.credits(() => this.showMainMenu()); },
    });
  }

  private showSettings(back: () => void): void {
    this.panels.settings({ ...this.settings }, (s) => { this.settings = s; this.applySettings(); }, () => { this.click(); back(); });
  }

  private click(): void {
    this.audio.play2D('ui_click', { ui: true, jitter: 0 });
  }

  private startPlay(fresh: boolean): void {
    this.panels.hideScreen();
    this.mode = 'play';
    this.hud.show(true);
    this.tpc.snap();
    this.input.clear();
    this.input.requestLock();
    if (fresh) {
      this.hud.banner(T.zone.village, 'Gölge Vadisi');
      setTimeout(() => this.hud.toast('Köy meydanındaki Bilge Ozan\'la konuş (E)', 'quest'), 1500);
    }
  }

  private pause(): void {
    this.mode = 'paused';
    this.input.releaseLock();
    this.audio.play2D('ui_open', { ui: true });
    this.panels.pause({
      onResume: () => this.resume(),
      onSave: () => { this.click(); this.save(); },
      onSettings: () => { this.click(); this.showSettings(() => this.pause()); },
      onMenu: () => { this.click(); this.save(true); this.showMainMenu(); },
    });
  }

  private resume(): void {
    this.panels.hideScreen();
    this.mode = 'play';
    this.audio.play2D('ui_close', { ui: true });
    this.input.clear();
    this.input.requestLock();
  }

  // ===========================================================================
  // Kayıt
  // ===========================================================================

  save(silent = false): boolean {
    const p = this.player;
    if (!p.alive) return false;
    const data: SaveData = {
      v: 1,
      savedAt: Date.now(),
      player: { x: p.position.x, z: p.position.z, facing: p.facing, level: p.level, xp: p.xp, hp: p.hp, mana: p.mana },
      inventory: p.inventory.serialize(),
      hotbar: p.hotbar,
      quests: this.quests.serialize(),
      chests: this.world.chests.filter((c) => c.opened).map((c) => c.id),
      bossDefeated: this.bossDefeated,
      timeOfDay: this.sky.timeOfDay,
    };
    const ok = writeJson(SAVE_KEY, data);
    if (!silent) this.hud.toast(ok ? T.saved : 'Kayıt başarısız (tarayıcı depolamayı engelliyor)', ok ? 'info' : 'warn');
    return ok;
  }

  private applySave(d: SaveData): void {
    const p = this.player;
    p.level = d.player.level;
    p.xp = d.player.xp;
    p.inventory.load(d.inventory);
    p.hotbar = d.hotbar;
    p.refreshEquipment();
    p.respawn(d.player.x, d.player.z, this.world.groundY(d.player.x, d.player.z));
    p.hp = Math.min(p.stats.maxHp, d.player.hp);
    p.mana = d.player.mana;
    p.facing = d.player.facing;
    this.quests.load(d.quests);
    this.refreshMarkers();
    for (const c of this.world.chests) if (d.chests.includes(c.id) && !c.opened) this.world.openChest(c);
    this.bossDefeated = d.bossDefeated;
    if (this.bossDefeated && this.boss) {
      this.boss.char.dispose();
      this.enemies = this.enemies.filter((e) => e !== this.boss);
      this.boss = null;
    }
    this.sky.timeOfDay = d.timeOfDay;
    this.tpc.yaw = p.facing + Math.PI;
  }

  // ===========================================================================
  // GameCtx
  // ===========================================================================

  floatText(pos: THREE.Vector3, text: string, kind: FloatKind): void {
    this.hud.float(pos, text, kind);
  }

  shake(a: number): void {
    this.tpc.shake(a);
  }

  aimYaw(): number {
    return this.tpc.yaw;
  }

  aimPoint(out: THREE.Vector3): THREE.Vector3 {
    const origin = this.camera.position.clone();
    const dir = new THREE.Vector3();
    this.camera.getWorldDirection(dir);
    // Önce ışına en yakın düşman (nişan yardımı), yoksa araziye yürü.
    let best: Enemy | null = null;
    let bestScore = Infinity;
    for (const e of this.enemies) {
      if (!e.alive) continue;
      const c = e.char.position.clone().setY(e.char.position.y + e.char.chestHeight);
      const t = c.clone().sub(origin).dot(dir);
      if (t < 3 || t > 45) continue;
      const off = origin.clone().addScaledVector(dir, t).distanceTo(c);
      if (off < 1.6 * e.scale && off < bestScore) { best = e; bestScore = off; }
    }
    if (best) return out.copy(best.char.position).setY(best.char.position.y + best.char.chestHeight);
    for (let t = 4; t < 60; t += 0.75) {
      out.copy(origin).addScaledVector(dir, t);
      if (out.y < this.world.groundY(out.x, out.z)) return out;
    }
    return out.copy(origin).addScaledVector(dir, 60);
  }

  borrowLight(color: THREE.ColorRepresentation): THREE.PointLight | null {
    const l = this.lightPool.find((x) => !x.busy);
    if (!l) return null;
    l.busy = true;
    l.light.color.set(color);
    l.light.intensity = 30;
    return l.light;
  }

  returnLight(light: THREE.PointLight): void {
    const l = this.lightPool.find((x) => x.light === light);
    if (!l) return;
    l.busy = false;
    l.light.intensity = 0;
    l.light.position.set(0, -50, 0);
  }

  // ===========================================================================
  // Döngü
  // ===========================================================================

  private frame(): void {
    const t0 = performance.now();
    const raw = this.clock.getDelta();
    // Normalde 20 FPS altında oyun yavaşlar (fizik adımı 50 ms'yi geçmesin).
    // Test modunda (?maxdt=) yazılım render'ının düşük FPS'inde oyun zamanı
    // gerçek zamana yakın aksın.
    const dt = Math.min(this.maxDt, raw);
    this.fps.frames++;
    this.fps.acc += raw;
    if (this.fps.acc >= 1) { this.fps.value = this.fps.frames / this.fps.acc; this.fps.frames = 0; this.fps.acc = 0; }
    if (!this.ready || this.frozen) return;
    this.time += dt;
    this.frameCount++;

    if (this.mode === 'play') this.updatePlay(dt);
    else if (this.mode === 'menu' || this.mode === 'title') this.updateMenuCamera(dt);
    else if (this.mode === 'dead') {
      this.player.update(dt, this);
      this.updateEnemies(dt);
      this.tpc.update(dt, this.player.position, (x, z) => this.world.groundY(x, z));
    }
    if (this.mode === 'paused' && this.input.hit('pause')) this.resume();

    this.sky.update(this.mode === 'paused' ? 0 : dt, this.mode === 'play' || this.mode === 'dead' ? this.player.position : new THREE.Vector3());
    this.world.cull(this.camera.position);
    if (this.mode !== 'paused') {
      this.world.update(dt);
      for (const n of this.npcs) n.update(dt, this.player.position);
    }
    const t1 = performance.now();
    if (!this.skipRender) this.renderer.render(this.scene, this.camera);
    const t2 = performance.now();
    this.perf.update = this.perf.update * 0.9 + (t1 - t0) * 0.1;
    this.perf.render = this.perf.render * 0.9 + (t2 - t1) * 0.1;
    this.input.endFrame();
  }

  /** Kare süresi dökümü (ms, üstel ortalama). */
  readonly perf = { update: 0, render: 0 };

  private updateMenuCamera(dt: number): void {
    this.menuAngle += dt * 0.05;
    const r = 52;
    this.camera.position.set(VILLAGE.x + Math.sin(this.menuAngle) * r, VILLAGE.height + 18, VILLAGE.z + Math.cos(this.menuAngle) * r);
    this.camera.lookAt(VILLAGE.x, VILLAGE.height + 3, VILLAGE.z);
    this.input.takeMouse();
  }

  private updatePlay(dt: number): void {
    this.input.gameplayEnabled = !this.panels.openPanel;
    // Etkileşim hedefi tuşlardan ÖNCE: E'ye basıldığı karede oyuncunun
    // gerçekten yanında durduğu şey açılsın (bir önceki karedeki değil).
    if (!this.panels.openPanel) this.updateInteraction();
    this.handleUiKeys();
    if (this.mode !== 'play') return;
    // Tuşlar panel açmış/kapatmış olabilir: durumu yeniden oku.
    const panel = this.panels.openPanel;

    const m = this.input.takeMouse();
    if (!panel) this.tpc.addMouse(m.dx, m.dy, m.wheel);

    // Panel açıkken dünya durur (envanter, diyalog, dükkân).
    if (!panel) {
      this.player.update(dt, this);
      this.updateEnemies(dt);
      for (const p of this.projectiles) p.update(dt, this);
      this.projectiles = this.projectiles.filter((p) => p.alive);
      this.updateLoot(dt);
      this.updateRespawns(dt);
      this.autosaveTimer += dt;
      if (this.autosaveTimer > 60) { this.autosaveTimer = 0; this.save(true); }
    }
    this.tpc.update(dt, this.player.position, (x, z) => this.world.groundY(x, z));
    this.updateMusic(dt);
    this.updateBirds(dt);
    this.hud.setBoss(this.boss && this.boss.alive && this.boss.engaged ? this.boss : null);
    this.hud.update(dt, this.player, this.quests, this.enemies, this.npcs, this.camera, this.tpc.yaw, this.player.castCd);
  }

  /**
   * Uzaktaki düşmanlar uyur: ne AI ne animasyon güncellenir, çizilmez de.
   * (Sis 360 birimde başlıyor ama bir karakter 10 ayrı mesh; 35 tanesi
   * her karede ~350 draw call demek.)
   */
  private updateEnemies(dt: number): void {
    const pp = this.player.position;
    for (const e of this.enemies) {
      const d = Math.hypot(e.char.position.x - pp.x, e.char.position.z - pp.z);
      const awake = d < 110 || e.engaged;
      e.char.root.visible = awake && !e.removed;
      if (awake) e.update(dt, this);
    }
  }

  private handleUiKeys(): void {
    const i = this.input;
    const panel = this.panels.openPanel;
    if (i.hit('pause')) {
      if (panel) { this.closePanel(); return; }
      this.pause();
      return;
    }
    if (panel === 'dialog') {
      for (let k = 1; k <= 3; k++) if (i.hitRaw(`slot${k}` as 'slot1') && this.panels.pressOption(k)) return;
      return;
    }
    if (i.hit('inventory')) { panel === 'inventory' ? this.closePanel() : this.openInventory(); return; }
    if (i.hit('quests')) { panel === 'quests' ? this.closePanel() : this.openQuests(); return; }
    if (panel === 'shop' && i.hit('interact')) { this.closePanel(); return; }
    if (!panel && i.hit('interact') && this.interactTarget) this.interact();
  }

  private openPanelCommon(): void {
    this.input.releaseLock();
    this.input.clear();
    this.hud.setPrompt(null);
  }

  closePanel(): void {
    const was = this.panels.openPanel;
    this.panels.closePanel();
    this.audio.play2D(was === 'quests' ? 'book_close' : 'ui_close', { ui: true });
    this.input.clear();
    this.input.requestLock();
  }

  private refreshOpenPanel(): void {
    if (this.panels.openPanel === 'inventory') this.openInventory(true);
    if (this.panels.openPanel === 'shop') this.openShop(true);
  }

  openInventory(silent = false): void {
    if (!silent) { this.openPanelCommon(); this.audio.play2D('ui_open', { ui: true }); }
    this.panels.inventory(this.player, {
      onSlot: (idx) => this.useSlot(idx),
      onAssign: (idx) => {
        const s = this.player.inventory.slots[idx];
        if (!s || item(s.id).type !== 'consumable') return;
        const hb = this.player.hotbar;
        if (hb.includes(s.id)) return;
        const free = hb.indexOf(null);
        hb[free >= 0 ? free : 3] = s.id;
        this.hud.toast(`${item(s.id).name} → hotbar ${(free >= 0 ? free : 3) + 1}`);
        this.openInventory(true);
      },
      onUnequip: (slot) => {
        if (this.player.inventory.unequip(slot)) { this.player.refreshEquipment(); this.audio.play2D('equip', { ui: true }); }
        else this.hud.toast(T.bagFull, 'warn');
        this.openInventory(true);
      },
      onClose: () => this.closePanel(),
    });
  }

  private useSlot(idx: number): void {
    const s = this.player.inventory.slots[idx];
    if (!s) return;
    const def = item(s.id);
    if (slotFor(def)) {
      if (this.player.inventory.equipFromSlot(idx)) {
        this.player.refreshEquipment();
        this.audio.play2D('equip', { ui: true });
      } else this.hud.toast(T.bagFull, 'warn');
    } else if (def.type === 'consumable') {
      this.player.consume(s.id, this);
    } else {
      this.hud.toast(def.desc);
    }
    this.openInventory(true);
  }

  openQuests(): void {
    this.openPanelCommon();
    this.audio.play2D('book_open', { ui: true });
    this.panels.quests(this.quests, () => this.closePanel());
  }

  private openShop(silent = false): void {
    if (!silent) { this.openPanelCommon(); this.audio.play2D('ui_open', { ui: true }); }
    const inv = this.player.inventory;
    this.panels.shop(this.player, (shopJson as Record<string, string[]>).merchant, {
      onBuy: (id) => {
        const price = buyPrice(id);
        if (inv.gold < price) { this.hud.toast(T.notEnoughGold, 'warn'); this.audio.play2D('ui_error', { ui: true }); return; }
        if (inv.add(id, 1) > 0) { this.hud.toast(T.bagFull, 'warn'); return; }
        inv.addGold(-price);
        this.audio.play2D('coins', { ui: true });
      },
      onSell: (idx) => {
        const s = inv.slots[idx];
        if (!s || item(s.id).type === 'quest') return;
        inv.remove(s.id, 1);
        inv.addGold(sellPrice(s.id));
        this.audio.play2D('coins', { ui: true });
      },
      onClose: () => this.closePanel(),
    });
  }

  // --- Etkileşim ---------------------------------------------------------------

  private updateInteraction(): void {
    const pp = this.player.position;
    let best: Game['interactTarget'] = null;
    let bestD = 3.4;
    for (const n of this.npcs) {
      const d = n.position.distanceTo(pp);
      if (d < bestD) { bestD = d; best = { kind: 'npc', npc: n }; }
    }
    for (const c of this.world.chests) {
      if (c.opened) continue;
      const d = c.pos.distanceTo(pp);
      if (d < bestD) { bestD = d; best = { kind: 'chest', chest: c }; }
    }
    this.interactTarget = best;
    if (!best || !this.player.alive) this.hud.setPrompt(null);
    else if (best.kind === 'npc') this.hud.setPrompt(`${T.talk} — ${best.npc.name}`);
    else this.hud.setPrompt(`${T.open} — Sandık`);
  }

  private interact(): void {
    const t = this.interactTarget;
    if (!t) return;
    if (t.kind === 'chest') { this.openChestFor(t.chest); return; }
    t.npc.talk();
    this.player.playInteract();
    this.openPanelCommon();
    this.audio.play2D('ui_open', { ui: true });
    this.talkTo(t.npc);
  }

  private openChestFor(c: Chest): void {
    this.world.openChest(c);
    this.player.playInteract();
    this.audio.play3D('chest_open', c.pos, { volume: 1 });
    const pos = c.pos.clone().setY(c.pos.y + 1);
    if (c.gold > 0) this.addLoot(new LootDrop(this.assets, 'gold', 'gold', c.gold, pos));
    for (const it of c.contents) this.addLoot(new LootDrop(this.assets, 'item', it.item, it.qty, pos));
  }

  private talkTo(npc: Npc): void {
    const d = DIALOG[npc.role] as Record<string, unknown> & { name: string; greet: string; bye: string };
    const close = () => this.closePanel();
    if (npc.role === 'merchant') {
      this.panels.dialog(d.name, d.greet, [
        { label: String(d.trade), action: () => { this.panels.closePanel(); this.openShop(true); } },
        { label: d.bye, action: close },
      ]);
      return;
    }
    if (npc.role !== 'elder') {
      this.panels.dialog(d.name, d.greet, [{ label: d.bye, action: close }]);
      return;
    }
    const f = this.quests.forGiver('elder');
    const texts = d as unknown as { offer: Record<string, string>; active: Record<string, string>; complete: Record<string, string>; done: string; accept: string; decline: string; turnIn: string };
    if (!f) {
      this.panels.dialog(d.name, texts.done, [{ label: d.bye, action: close }]);
      return;
    }
    const q = f.quest;
    if (f.status === 'available') {
      this.panels.dialog(`${d.name} — ${q.name}`, texts.offer[q.id], [
        {
          label: texts.accept,
          action: () => {
            this.quests.accept(q.id, (it) => this.player.inventory.count(it));
            this.audio.play2D('quest_accept', { ui: true, jitter: 0 });
            this.hud.toast(`${T.questNew}: ${q.name}`, 'quest');
            close();
          },
        },
        { label: texts.decline, action: close },
      ], rewardText(q));
    } else if (f.status === 'active') {
      this.panels.dialog(`${d.name} — ${q.name}`, texts.active[q.id], [{ label: d.bye, action: close }]);
    } else {
      this.panels.dialog(`${d.name} — ${q.name}`, texts.complete[q.id], [
        { label: texts.turnIn, action: () => { this.turnIn(q.id); close(); } },
      ], rewardText(q));
    }
  }

  private turnIn(id: string): void {
    const q = this.quests.def(id);
    // Toplama görevinin eşyası teslimde alınır.
    for (const o of q.objectives) if (o.type === 'collect' && o.item) this.player.inventory.remove(o.item, o.count);
    const reward = this.quests.turnIn(id);
    if (!reward) return;
    this.player.inventory.addGold(reward.gold);
    for (const [it, n] of reward.items) {
      const left = this.player.inventory.add(it, n);
      if (left > 0) this.addLoot(new LootDrop(this.assets, 'item', it, left, this.player.position.clone().setY(this.player.position.y + 1)));
    }
    this.player.gainXp(reward.xp, this);
    this.audio.play2D('quest_done', { jitter: 0 });
    this.hud.banner(T.questDone, q.name);
    this.save(true);
  }

  private updateLoot(dt: number): void {
    const pp = this.player.position;
    const target = pp.clone().setY(pp.y + 1);
    this.loot = this.loot.filter((l) => {
      l.update(dt, (x, z) => this.world.groundY(x, z));
      const d = l.obj.position.distanceTo(target);
      // Mıknatıs: altın uzaktan da gelir, eşya kesesi için yanına gitmek gerek.
      if (d < (l.kind === 'gold' ? 6 : 3.5) && this.player.alive) l.pull(target, dt);
      if (d > 1.1) return true;
      if (l.kind === 'gold') {
        this.player.inventory.addGold(l.qty);
        this.audio.play2D('coins', { volume: 0.8 });
        this.floatText(target.clone().setY(target.y + 1.6), T.goldPicked(l.qty), 'gold');
      } else {
        const left = this.player.inventory.add(l.id, l.qty);
        if (left === l.qty) { this.hud.setPrompt(T.bagFull); return true; }
        this.audio.play2D('loot', { volume: 0.9 });
        this.hud.toast(T.picked(item(l.id).name, l.qty - left), 'loot');
      }
      l.obj.removeFromParent();
      return false;
    });
  }

  private respawnTimers = new Map<number, number>();

  private updateRespawns(dt: number): void {
    for (const e of this.enemies) {
      if (!e.removed || !e.respawnable) continue;
      const t = (this.respawnTimers.get(e.id) ?? 0) + dt;
      this.respawnTimers.set(e.id, t);
      if (t > 75 && this.player.position.distanceTo(new THREE.Vector3(e.spawn.x, this.player.position.y, e.spawn.y)) > 50) {
        this.respawnTimers.delete(e.id);
        e.reset(this.world.groundY(e.spawn.x, e.spawn.y));
      }
    }
    // Yeniden doğmayan ölüleri (boss'un çağırdıkları) listeden at.
    this.enemies = this.enemies.filter((e) => {
      if (e.removed && !e.respawnable && !e.isBoss) { e.char.dispose(); return false; }
      return true;
    });
  }

  private updateMusic(dt: number): void {
    const pp = this.player.position;
    let fighting = false;
    for (const e of this.enemies) {
      if (!e.alive || !e.engaged) continue;
      if (e.char.position.distanceTo(pp) < 30) { fighting = true; break; }
    }
    if (fighting) this.combatTimer = Math.max(this.combatTimer, 4);
    this.combatTimer -= dt;
    const zone = zoneAt(pp.x, pp.z);
    let track: string = zone === 'village' ? 'village' : zone === 'ruins' ? 'ruins' : 'explore';
    if (this.combatTimer > 0) track = 'combat';
    if (this.boss && this.boss.alive && this.boss.engaged) track = 'boss';
    this.audio.setMusic(track, track === 'combat' || track === 'boss' ? 1.2 : 3);
  }

  private updateBirds(dt: number): void {
    this.birdTimer -= dt;
    if (this.birdTimer > 0) return;
    this.birdTimer = 7 + Math.random() * 14;
    const day = this.sky.daylight;
    const zone = zoneAt(this.player.position.x, this.player.position.z);
    if (day < 0.4 || zone === 'ruins') return;
    const a = Math.random() * Math.PI * 2;
    const p = this.player.position.clone().add(new THREE.Vector3(Math.cos(a) * 18, 8, Math.sin(a) * 18));
    this.audio.play3D('bird', p, { volume: 0.7, ref: 10 });
  }

  // ===========================================================================
  // Test kancası (smoke test bunu kullanır)
  // ===========================================================================

  debugApi() {
    return {
      get ready() { return game.ready; },
      get mode() { return game.mode; },
      get fps() { return Math.round(game.fps.value); },
      get frame() { return game.frameCount; },
      start: () => (window as unknown as { __startGame: () => Promise<void> }).__startGame(),
      newGame: () => { removeKey(SAVE_KEY); game.newGameState(); game.startPlay(true); },
      continueGame: () => { const d = loadSave(); if (!d) return false; game.newGameState(); game.applySave(d); game.startPlay(false); return true; },
      player: () => ({
        x: game.player.position.x, y: game.player.position.y, z: game.player.position.z,
        hp: game.player.hp, mana: game.player.mana, stamina: game.player.stamina, level: game.player.level, xp: game.player.xp,
        state: game.player.state, anim: game.player.char.currentName, gold: game.player.inventory.gold, grounded: game.player.grounded,
        ground: game.world.groundY(game.player.position.x, game.player.position.z), debug: { ...game.player.debug },
        equipment: { ...game.player.inventory.equipment },
      }),
      enemies: () => game.enemies.map((e) => ({ id: e.id, type: e.type, state: e.state, hp: e.hp, x: e.char.position.x, z: e.char.position.z, anim: e.char.currentName })),
      quests: () => Object.fromEntries([...game.quests.status].map(([k, v]) => [k, { status: v, progress: game.quests.progress.get(k) ?? [] }])),
      assets: () => ({ failures: game.assets.failures, models: Object.keys(game.assets.manifest.models).length, sounds: game.assets.sounds.size, clips: game.assets.clips.size }),
      audio: () => ({ state: game.audio.ctx.state, music: game.audio.currentMusic, musicPlaying: game.audio.musicPlaying(), ...game.audio.stats }),
      world: () => ({ vegetation: game.world.vegetationCount, colliders: game.world.colliders.all.length, chests: game.world.chests.map((c) => ({ id: c.id, opened: c.opened })) }),
      teleport: (x: number, z: number) => { game.player.position.set(x, game.world.groundY(x, z), z); game.tpc.snap(); },
      setCamera: (yaw: number, pitch = 0.32, dist = 7.5) => { game.tpc.yaw = yaw; game.tpc.pitch = pitch; game.tpc.distance = dist; },
      setTime: (t: number) => { game.sky.timeOfDay = t; },
      press: (a: Parameters<Input['simulate']>[0], down: boolean) => game.input.simulate(a, down),
      killNearest: () => {
        const alive = game.enemies.filter((e) => e.alive);
        alive.sort((a, b) => a.char.position.distanceTo(game.player.position) - b.char.position.distanceTo(game.player.position));
        alive[0]?.takeDamage(99999, game.player.position, game);
        return alive[0]?.type ?? null;
      },
      give: (id: string, n = 1) => game.player.inventory.add(id, n),
      damagePlayer: (n: number) => {
        const p = game.player.position;
        game.player.takeHit(n, new THREE.Vector3(p.x + Math.sin(game.player.facing), p.y, p.z + Math.cos(game.player.facing)), game);
      },
      count: (id: string) => game.player.inventory.count(id),
      bossBarVisible: () => !document.querySelector('.bossbar')?.classList.contains('hidden'),
      acceptQuest: (id: string) => game.quests.accept(id, (it) => game.player.inventory.count(it)),
      turnIn: (id: string) => game.turnIn(id),
      openInventory: () => game.openInventory(),
      openQuests: () => game.openQuests(),
      talk: (role: NpcRole) => { const n = game.npcs.find((x) => x.role === role); if (n) { game.openPanelCommon(); game.talkTo(n); } },
      closePanel: () => game.closePanel(),
      save: () => game.save(true),
      panel: () => game.panels.openPanel,
      lootCount: () => game.loot.length,
      meshStats: () => {
        const frustum = new THREE.Frustum().setFromProjectionMatrix(new THREE.Matrix4().multiplyMatrices(game.camera.projectionMatrix, game.camera.matrixWorldInverse));
        const out: Record<string, number> = {};
        game.scene.traverseVisible((o) => {
          const m = o as THREE.Mesh;
          if (!m.isMesh) return;
          if (m.frustumCulled && !frustum.intersectsObject(m)) return;
          let top: THREE.Object3D = m;
          while (top.parent && top.parent !== game.scene && top.parent !== game.world.group) top = top.parent;
          const key = m.name.startsWith('inst:') ? 'inst:' + m.name.split(':')[1] : (m as THREE.SkinnedMesh).isSkinnedMesh ? 'skinned' : top.name.split('_')[0] || 'other';
          out[key] = (out[key] ?? 0) + 1;
        });
        return out;
      },
      perf: () => ({ ...game.perf, calls: game.renderer.info.render.calls, triangles: game.renderer.info.render.triangles, programs: game.renderer.info.programs?.length ?? 0, geometries: game.renderer.info.memory.geometries, textures: game.renderer.info.memory.textures }),
      freeze: (v: boolean) => { game.frozen = v; if (!v) game.clock.getDelta(); },
      releaseLock: () => game.input.releaseLock(),
      toggle: (what: string, on: boolean) => {
        if (what === 'world') game.world.group.visible = on;
        if (what === 'shadows') { game.renderer.shadowMap.enabled = on; game.scene.traverse((o) => { const m = (o as THREE.Mesh).material as THREE.Material | undefined; if (m) m.needsUpdate = true; }); }
        if (what === 'ui') game.uiRoot.style.display = on ? '' : 'none';
        if (what === 'render') game.skipRender = !on;
        if (what === 'chars') for (const e of game.enemies) e.char.root.visible = on;
        if (what === 'everything') for (const c of game.scene.children) if (c !== game.camera) c.visible = on;
        if (what === 'sky') game.sky.sky.visible = on;
        if (what === 'batch') game.world.batcher.hidden = !on;
        if (what === 'particles') game.world.particles.points.visible = on;
        if (what === 'npcs') for (const n of game.npcs) n.char.root.visible = on;
        if (what === 'player') game.player.char.root.visible = on;
        if (what.startsWith('name:')) for (const c of game.world.group.children) if (c.name === what.slice(5)) c.visible = on;
      },
      audioOp: (op: string) => {
        if (op === 'stopLoops') game.audio.stopLoops();
        if (op === 'stopMusic') game.audio.setMusic('__none__', 0.01);
        if (op === 'suspend') game.audio.ctx.suspend();
      },
    };
  }
}

// debugApi içindeki getter'lar için modül düzeyinde referans.
let game: Game;
export function setGameRef(g: Game): void {
  game = g;
}
