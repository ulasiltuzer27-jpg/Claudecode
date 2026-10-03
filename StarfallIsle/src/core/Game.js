// Oyunun kalbi: sistemleri kurar, durum makinesini ve ana dongunun
// sirasini yonetir. Diger moduller birbirini buradan (game.*) bulur.
import * as THREE from 'three';
import { Renderer } from '../render/Renderer.js';
import { Sky } from '../render/Sky.js';
import { Particles } from '../render/Particles.js';
import { Physics, initPhysics } from '../physics/Physics.js';
import { World } from '../world/World.js';
import { renderMapCanvas } from '../world/TerrainMesh.js';
import { Player } from '../gameplay/Player.js';
import { CameraRig } from '../gameplay/CameraRig.js';
import { Collectibles } from '../gameplay/Collectibles.js';
import { Npcs } from '../gameplay/Npc.js';
import { Quests } from '../gameplay/Quests.js';
import { Race } from '../gameplay/Race.js';
import { Fishing } from '../gameplay/Fishing.js';
import { Finale } from '../gameplay/Lighthouse.js';
import { PhotoMode } from '../gameplay/PhotoMode.js';
import { Stats } from '../achievements/Stats.js';
import { AchievementTracker } from '../achievements/AchievementTracker.js';
import { AudioEngine } from '../audio/Audio.js';
import { UI } from '../ui/UI.js';
import { MainMenu, PauseMenu, JournalScreen } from '../ui/Screens.js';
import { Events } from './Events.js';
import { Input } from './Input.js';
import { Settings } from './Settings.js';
import { SaveManager, newSaveData } from './Save.js';
import { Platform } from './Platform.js';
import { t, setLang } from './i18n.js';
import * as WD from '../world/WorldData.js';
import { LAYOUT } from '../world/Terrain.js';

const DAY_SECONDS = 720; // 24 oyun saati = 12 gercek dakika
const VERSION = '0.1.0';

export class Game {
  constructor() {
    this.version = VERSION;
    this.events = new Events();
    this.settings = new Settings();
    this.saves = new SaveManager();
    this.state = 'loading';
    this.hour = 17.6;
    this.time = 0;
    this.save = null;
    this.slot = null;
    this.params = new URLSearchParams(location.search);
    this.testMode = this.params.has('test');
  }

  async init() {
    const loading = document.getElementById('loading');
    const fill = loading.querySelector('.loading-fill');
    const text = loading.querySelector('.loading-text');
    const progress = (label, f) => {
      fill.style.width = `${Math.round(f * 100)}%`;
      text.textContent = t(`loading.${label}`);
    };
    if (this.params.has('fresh')) Platform.clearAll();
    await this.settings.load();
    if (this.params.get('lang')) this.settings.values.lang = this.params.get('lang');
    setLang(this.settings.get('lang'));
    progress('engine', 0.02);

    const quality = this.params.get('quality') || this.settings.get('quality');
    this.renderer = new Renderer(document.getElementById('game'), quality);
    this.camera = this.renderer.camera;
    this.sky = new Sky(this.renderer.scene);
    this.particles = {
      additive: new Particles(this.renderer.scene, 3000, true),
      normal: new Particles(this.renderer.scene, 1500, false),
    };
    this.input = new Input(this.renderer.canvas);
    this.audio = new AudioEngine(this.settings);
    await initPhysics();
    this.physics = new Physics();

    this.world = new World(this);
    await this.world.build(progress);
    this.player = new Player(this);
    this.cameraRig = new CameraRig(this.camera, this);
    this.collectibles = new Collectibles(this);
    this.collectibles.build();
    this.npcs = new Npcs(this);
    this.npcs.build();
    this.quests = new Quests(this);
    this.race = new Race(this);
    this.fishing = new Fishing(this);
    this.finale = new Finale(this);
    this.photo = new PhotoMode(this);
    this.ui = new UI(this);
    this.dialogue = this.ui.dialogue;
    this.progress = new Progress(this);

    this.profile = await this.saves.loadProfile();
    this.stats = new Stats(this.profile.stats, this.events);
    this.achievements = new AchievementTracker(this.stats, this.profile.unlocked, this.events);
    this.achievements.syncToSteam();

    this.applySettings();
    this.settings.onChange((k) => this.applySettings(k));
    this.wireEvents();
    window.addEventListener('pointerdown', () => this.audio.unlock());
    window.addEventListener('keydown', () => this.audio.unlock());

    this.player.spawn(WD.START.x, WD.DOCK.y + 0.1, WD.START.z, WD.START.yaw);
    this.applySettings('quality');

    loading.classList.add('done');
    setTimeout(() => loading.remove(), 900);
    this.toMainMenu(true);
    this.last = performance.now();
    requestAnimationFrame((ts) => this.frame(ts));
    Platform.rendererReady({ version: VERSION, trees: this.world.treeCount });
    if (this.testMode) window.__game = this;
    return this;
  }

  // ---------------- durum ----------------
  setState(s) {
    this.state = s;
    const gameplay = s === 'playing';
    if (gameplay && !this.testMode) this.input.requestLock();
    if (!gameplay && s !== 'photo') this.input.releaseLock();
  }

  toMainMenu(first = false) {
    if (!first && this.save) this.autosave(true);
    this.save = null;
    this.ui.clear();
    if (this.dialogue.active) this.dialogue.close(false);
    if (this.race.state !== 'idle') this.race.cancel();
    if (this.fishing.state !== 'idle') this.fishing.stop();
    this.ui.hud.setVisible(false);
    this.ui.hud.prompt(null);
    this.setState('menu');
    this.hour = 17.6;
    this.menuAngle = 0.4;
    this.ui.push(new MainMenu(this.ui));
    this.audio.music.setMood('menu');
    Platform.steam.richPresence('status', 'Menu');
  }

  async newGame(slot) {
    this.slot = slot;
    this.save = newSaveData();
    this.startSession();
    this.stats.add('games_started', 1);
    this.autosave(true);
    setTimeout(() => this.hint(t('hint.move', { jump: this.input.glyph('jump') })), 1200);
  }

  async loadSlot(slot) {
    const data = await this.saves.loadSlot(slot);
    if (!data) return this.newGame(slot);
    this.slot = slot;
    this.save = data;
    this.startSession();
  }

  startSession() {
    const s = this.save;
    this.ui.clear();
    this.profile.lastSlot = this.slot;
    this.collectibles.applySave(s);
    // dunya durumunu kayda gore kur
    const owl = this.npcs.byId.get('owl');
    if (s.flags.owlIntro) this.npcs.moveOwlToPeak();
    else this.npcs.placeAt(owl, WD.NPCS.owl.x, WD.NPCS.owl.z, WD.NPCS.owl.yaw);
    if (s.flags.bridge) this.world.buildBridge(false);
    this.finale.setLit(!!s.flags.finale);
    this.player.model.setHat(!!(s.flags.hat && s.flags.hatOn));
    this.hour = s.hour ?? 8.5;
    const p = s.pos || [WD.START.x, WD.DOCK.y + 0.1, WD.START.z];
    this.player.spawn(p[0], p[1] + 0.05, p[2], s.yaw ?? WD.START.yaw);
    this.cameraRig.snap(this.player.pos, this.player.yaw + Math.PI);
    this.cameraRig.override = null;
    this.ui.hud.setVisible(true);
    this.setState('playing');
    this.audio.music.setMood('auto');
    this.progress.refreshStats();
    this.regionTimer = 0;
    this.currentRegion = null;
    Platform.steam.richPresence('status', t('presence.exploring'));
  }

  pause() {
    if (this.state !== 'playing') return;
    this.setState('paused');
    this.ui.push(new PauseMenu(this.ui));
  }

  resume() {
    this.ui.clear();
    this.setState('playing');
  }

  // ---------------- olaylar ----------------
  wireEvents() {
    const E = this.events;
    const P = this.particles;
    E.on('jump', ({ pos }) => {
      this.audio.sfx('jump');
      this.stats.add('jumps', 1);
      P.normal.emit({ pos, count: 6, spread: 0.25, vel: { x: 0, y: 0.8, z: 0 }, velSpread: 1.2, life: 0.5, size: 0.35, sizeEnd: 0.7, color: '#f2e6cf', alpha: 0.6, drag: 3 });
    });
    E.on('flap', ({ pos, used, total }) => {
      this.audio.sfx('flap');
      this.input.rumble(0.25, 80);
      P.additive.emit({ pos: pos.clone().add(new THREE.Vector3(0, 0.5, 0)), count: 18, radial: 3.5, gravity: -3, drag: 2, life: 0.8, size: 0.28, sizeEnd: 0.05, color: ['#ffd76a', '#fff2a8', '#ffb627'] });
      if (used === total && total > 0 && !this.flag('hintFlapsOut')) {
        this.setFlag('hintFlapsOut');
        this.hint(t('hint.flapsOut'));
      }
    });
    E.on('land', ({ pos, impact }) => {
      if (impact < 3) return;
      this.audio.sfx('land', { impact });
      P.normal.emit({ pos, count: Math.min(16, 4 + impact), spread: 0.3, vel: { x: 0, y: 0.5, z: 0 }, velSpread: 2, life: 0.6, size: 0.4, sizeEnd: 0.9, color: '#efe2c8', alpha: 0.55, drag: 4 });
      if (impact > 22) {
        this.cameraRig.shake = Math.min(1, impact / 40);
        this.input.rumble(0.4, 120);
      }
    });
    E.on('splash', ({ pos, strength }) => {
      this.audio.sfx('splash', { strength });
      const wl = this.world.terrain.waterLevel(pos.x, pos.z);
      P.normal.emit({ pos: new THREE.Vector3(pos.x, wl, pos.z), count: 24, radial: 3 + strength * 4, gravity: -12, life: 0.8, size: 0.2, color: '#ffffff', alpha: 0.85 });
    });
    E.on('step', ({ pos, surface }) => this.audio.sfx('step', { surface }));
    E.on('bounce', ({ pos }) => {
      this.audio.sfx('bounce');
      let best = null;
      let bd = Infinity;
      for (const m of WD.GIANT_MUSHROOMS) {
        const d = (m.x - pos.x) ** 2 + (m.z - pos.z) ** 2;
        if (d < bd) { bd = d; best = m; }
      }
      if (best) best.squish = 1;
      P.additive.emit({ pos, count: 12, radial: 3, gravity: -4, life: 0.7, size: 0.25, color: ['#ff9df0', '#ffffff', '#b98aff'] });
    });
    E.on('glideStart', () => {
      if (!this.flag('hintGlide')) {
        this.setFlag('hintGlide');
        this.hint(t('hint.glide'));
      }
    });
    E.on('toast', ({ key, params }) => this.ui.hud.toast(t(key, params)));
    E.on('achievement', (a) => {
      this.ui.hud.achievement(a);
      this.audio.sfx('achievement');
      this.saveProfile();
    });
    E.on('fishCaught', () => this.progress.refreshStats());
  }

  flag(k) {
    return !!this.save?.flags[k];
  }

  setFlag(k, v = true) {
    if (this.save) this.save.flags[k] = v;
  }

  hint(text, ms) {
    this.ui.hud.hint(text, ms || 6000);
  }

  onCollect(item) {
    const s = this.save;
    if (!s) return;
    if (!s.collected.includes(item.id)) s.collected.push(item.id);
    const P = this.particles;
    const at = item.pos.clone();
    switch (item.kind) {
      case 'shell':
        s.shells++;
        s.shellsTotal++;
        this.audio.sfx('shell');
        P.additive.emit({ pos: at, count: 8, radial: 2, gravity: -3, life: 0.6, size: 0.18, color: ['#ffd0c0', '#ffffff'] });
        if (!this.flag('hintShell')) { this.setFlag('hintShell'); this.hint(t('hint.shell')); }
        break;
      case 'shard':
        this.audio.sfx('shard');
        this.input.rumble(0.3, 120);
        P.additive.emit({ pos: at, count: 40, radial: 5, gravity: -2, drag: 1.5, life: 1.2, size: 0.35, sizeEnd: 0.05, color: ['#fff2a8', '#ffd76a', '#ffffff'] });
        this.ui.hud.toast(t('toast.shard', { n: this.progress.shardCount(), total: WD.SHARD_TOTAL }));
        this.shardMilestones();
        break;
      case 'feather':
        this.audio.sfx('feather');
        P.additive.emit({ pos: at, count: 30, radial: 4, gravity: -2, drag: 1.5, life: 1.1, size: 0.3, color: ['#ffd76a', '#ffb627'] });
        this.ui.hud.toast(t('toast.feather', { n: this.progress.featherCount() }));
        if (!this.flag('hintFeather2')) { this.setFlag('hintFeather2'); this.hint(t('hint.feather', { jump: this.input.glyph('jump') })); }
        break;
      case 'carrot':
      case 'tool':
        this.audio.sfx('item');
        P.additive.emit({ pos: at, count: 14, radial: 2.5, gravity: -3, life: 0.8, size: 0.22, color: ['#ffffff', '#ffe28a'] });
        this.ui.hud.toast(t(`toast.${item.id}`));
        break;
      default:
        break;
    }
    this.progress.refreshStats();
    this.autosave();
  }

  grantReward(id, kind) {
    const s = this.save;
    if (s.rewards.includes(id)) return;
    s.rewards.push(id);
    const pos = this.player.pos.clone().add(new THREE.Vector3(0, 1.2, 0));
    if (kind === 'feather') {
      this.audio.sfx('feather');
      this.particles.additive.emit({ pos, count: 30, radial: 4, gravity: -2, life: 1.1, size: 0.3, color: ['#ffd76a', '#ffb627'] });
      this.ui.hud.toast(t('toast.feather', { n: this.progress.featherCount() }));
    } else if (kind === 'shard') {
      this.audio.sfx('shard');
      this.particles.additive.emit({ pos, count: 40, radial: 5, gravity: -2, life: 1.2, size: 0.35, color: ['#fff2a8', '#ffd76a'] });
      this.ui.hud.toast(t('toast.shard', { n: this.progress.shardCount(), total: WD.SHARD_TOTAL }));
      this.shardMilestones();
    }
    this.progress.refreshStats();
    this.autosave();
  }

  shardMilestones() {
    const n = this.progress.shardCount();
    if (n >= WD.SHARDS_FOR_FINALE && !this.flag('hintReady') && !this.flag('finale')) {
      this.setFlag('hintReady');
      setTimeout(() => this.hint(t('hint.ready', { n: WD.SHARDS_FOR_FINALE }), 8000), 1500);
    }
  }

  flyOwlToPeak() {
    const owl = this.npcs.byId.get('owl');
    this.particles.normal.emit({ pos: owl.pos.clone().add(new THREE.Vector3(0, 0.8, 0)), count: 30, radial: 3, life: 0.9, size: 0.6, sizeEnd: 1.2, color: ['#ffffff', '#e8d6b8'], alpha: 0.8, drag: 3 });
    this.particles.additive.emit({ pos: owl.pos.clone().add(new THREE.Vector3(0, 0.8, 0)), count: 20, radial: 4, life: 1, size: 0.3, color: '#ffd76a' });
    this.audio.sfx('poof');
    this.npcs.moveOwlToPeak();
    this.setFlag('owlMoved');
    setTimeout(() => this.hint(t('hint.owlFlew'), 7000), 600);
    this.autosave();
  }

  setHour(h) {
    this.hour = h;
    if (this.save) this.save.hour = h;
  }

  rest(toHour) {
    this.setState('cutscene');
    this.player.frozen = true;
    let k = 0;
    const R = this.renderer;
    const step = () => {
      k += 0.03;
      if (k < 1) R.fade = k;
      else if (k < 1.05) { R.fade = 1; this.setHour(toHour); }
      else R.fade = Math.max(0, 2.05 - k);
      if (k < 2.05) requestAnimationFrame(step);
      else {
        R.fade = 0;
        this.player.frozen = false;
        this.setState('playing');
        this.autosave();
      }
    };
    requestAnimationFrame(step);
  }

  // ---------------- kayit ----------------
  autosave(now = false) {
    if (!this.save || !this.slot) return;
    clearTimeout(this._saveT);
    const doSave = () => {
      if (!this.save) return;
      const p = this.player;
      const safe = p.grounded && !p.swimming ? p.pos : p.lastSafe;
      this.save.pos = [safe.x, safe.y, safe.z];
      this.save.yaw = p.yaw;
      this.save.hour = this.hour;
      this.saves.saveSlot(this.slot, this.save);
      this.saveProfile();
    };
    if (now) doSave();
    else this._saveT = setTimeout(doSave, 800);
  }

  saveProfile() {
    this.profile.stats = this.stats.toJSON();
    this.profile.unlocked = this.achievements.unlocked;
    this.saves.saveProfile(this.profile);
  }

  applySettings(key) {
    const S = this.settings;
    if (!key || key === 'quality') {
      this.renderer.setQuality(S.get('quality'));
      const q = this.renderer.q;
      this.sky.setShadowQuality(q.shadows, q.shadowRange);
      this.sky.setFar(q.far);
      this.world.grass?.setCount(50000 * q.grass);
      this.particles.additive.setViewport(window.innerHeight * Math.min(window.devicePixelRatio, q.pixelRatio));
      this.particles.normal.setViewport(window.innerHeight * Math.min(window.devicePixelRatio, q.pixelRatio));
    }
    if (!key || key === 'lang') {
      setLang(S.get('lang'));
      for (const s of this.ui.stack) s.refresh();
    }
    if (!key || key === 'textScale') document.documentElement.style.setProperty('--ts', S.get('textScale'));
    if (!key || key === 'fullscreen') Platform.setFullscreen(S.get('fullscreen'));
    if (!key || ['master', 'music', 'sfx', 'ambience'].includes(key)) this.audio.applyVolumes();
  }

  mapCanvas() {
    if (!this._map) this._map = renderMapCanvas(this.world.terrain, 512, this.world.terrainColors);
    const c = document.createElement('canvas');
    c.width = c.height = 512;
    c.getContext('2d').drawImage(this._map, 0, 0);
    return c;
  }

  // ---------------- dongu ----------------
  frame(ts) {
    const dt = Math.min(0.05, (ts - this.last) / 1000);
    this.last = ts;
    try {
      this.update(dt);
    } catch (err) {
      console.error(err);
    }
    this.renderer.render(dt);
    this.input.endFrame();
    requestAnimationFrame((t2) => this.frame(t2));
  }

  update(dt) {
    const input = this.input;
    input.update();
    this.time += dt;
    const st = this.state;
    const playing = st === 'playing';

    // fps
    this._fpsAcc = (this._fpsAcc || 0) + dt;
    this._fpsN = (this._fpsN || 0) + 1;
    if (this._fpsAcc > 0.5) {
      this.fps = this._fpsN / this._fpsAcc;
      this.ui.hud.fps(this.fps, this.settings.get('showFps'));
      this._fpsAcc = 0;
      this._fpsN = 0;
    }

    // saat
    if (st !== 'menu' && st !== 'paused' && st !== 'loading' && st !== 'photo') {
      this.hour = (this.hour + (24 / DAY_SECONDS) * dt) % 24;
    }
    if (this.save && (playing || st === 'dialogue')) this.save.playTime += dt;

    // girdi yonlendirme
    if (st === 'menu' || st === 'paused') this.ui.handleInput(input);
    else if (this.ui.credits.active) this.ui.credits.handle(input);
    else if (st === 'dialogue') this.dialogue.handle(input);
    else if (st === 'photo') this.photo.update(dt, input);
    else if (playing) this.handleGameplayInput(input);

    // dunya
    const focus = st === 'menu' ? this.menuCamera(dt) : this.player.pos;
    this.sky.update(this.hour, focus, dt);
    this.world.update(dt, this.time, this.sky.state, this.player.pos, this.particles);
    this.world.grass.update(dt, st === 'menu' ? this.camera.position : this.player.pos, this.player.pos);

    if (st === 'playing' || st === 'dialogue' || st === 'cutscene') {
      const frozen = st !== 'playing' || this.fishing.state !== 'idle';
      const prevFrozen = this.player.frozen;
      if (st !== 'playing') this.player.frozen = true;
      this.player.update(dt, input, this.cameraRig.yaw);
      if (st !== 'playing') this.player.frozen = prevFrozen;
      if (!frozen || st === 'dialogue') this.cameraRig.update(dt, st === 'playing' ? input : null, this.player);
      else this.cameraRig.update(dt, null, this.player);
      this.collectibles.update(dt, this.player);
      this.race.update(dt);
      this.fishing.update(dt, input);
      this.dialogue.update(dt);
      this.tickGameplay(dt);
    } else if (st === 'photo') {
      this.collectibles.update(0, this.player);
    } else if (st === 'menu') {
      this.player.model.update(dt, { speed: 0, grounded: true, vy: 0 });
    }
    this.npcs.update(dt, this.player);
    this.finale.update(dt);
    this.particles.additive.update(dt);
    this.particles.normal.update(dt);
    this.audio.update(dt, this);

    if (this.save) {
      const p = this.progress;
      this.ui.hud.setCounts({ shards: p.shardCount(), shells: this.save.shells, feathers: p.featherCount(), flapsUsed: this.player.flapsUsed });
    }
  }

  menuCamera(dt) {
    this.menuAngle += dt * 0.025;
    const a = this.menuAngle;
    const r = 120;
    const target = new THREE.Vector3(10, 12, 10);
    this.camera.position.set(Math.sin(a) * r + 20, 46 + Math.sin(a * 0.7) * 6, Math.cos(a) * r + 30);
    this.camera.lookAt(target);
    if (this.camera.fov !== 55) { this.camera.fov = 55; this.camera.updateProjectionMatrix(); }
    return target;
  }

  handleGameplayInput(input) {
    if (input.pressed('pause')) {
      if (this.fishing.state !== 'idle') return; // balik tutarken Esc oltayi toplar
      if (this.race.state === 'countdown') return;
      return this.pause();
    }
    if (input.pressed('journal')) {
      this.setState('paused');
      this.ui.push(new PauseMenu(this.ui));
      this.ui.push(new JournalScreen(this.ui));
      return;
    }
    if (input.pressed('photo') && this.fishing.state === 'idle') return this.photo.enter();
    if (this.fishing.state !== 'idle' || this.race.state === 'countdown') return;

    const act = this.currentInteraction;
    if (act && input.pressed('interact')) act.run();
  }

  // Bolgeler, etkilesim ipuclari, ozel istatistikler
  tickGameplay(dt) {
    const p = this.player;
    const s = this.save;
    if (!s) return;

    // etkilesim
    this.currentInteraction = null;
    if (this.state === 'playing' && this.fishing.state === 'idle' && this.race.state === 'idle') {
      const npc = this.npcs.nearest(p.pos);
      if (npc) {
        this.currentInteraction = { label: t('prompt.talk', { name: t(`npc.${npc.id}`) }), run: () => this.quests.talk(npc) };
      } else if (this.world.campfirePos.distanceTo(p.pos) < 3.2) {
        this.currentInteraction = { label: t('prompt.rest'), run: () => this.campfireDialogue() };
      } else {
        const sign = this.nearSign();
        if (sign) this.currentInteraction = { label: t('prompt.read'), run: () => this.dialogue.start({ name: t('sign.name'), lines: t(sign.key) }) };
        else if (this.fishing.canFish()) this.currentInteraction = { label: t('prompt.fish'), run: () => this.fishing.start() };
      }
    }
    this.ui.hud.prompt('interact', this.currentInteraction?.label);

    // bolgeler (yarim saniyede bir)
    this.regionTimer = (this.regionTimer || 0) - dt;
    if (this.regionTimer <= 0) {
      this.regionTimer = 0.5;
      const r = this.regionAt(p.pos);
      if (r && !s.regions.includes(r.id)) {
        s.regions.push(r.id);
        this.ui.hud.regionBanner(t(`region.${r.id}`), t(`region.${r.id}.sub`));
        this.audio.sfx('discover');
        if (r.id === 'cave') this.stats.max('cave', 1);
        this.progress.refreshStats();
        this.autosave();
      }
      this.currentRegion = r ? r.id : null;
      if (r && r.id === 'peak' && p.pos.y > 36 && p.grounded) this.stats.max('peak', 1);
      if (this.hour < 1 && this.hour >= 0) this.stats.max('night', 1);
    }
    // periyodik kayit
    this.saveTimer = (this.saveTimer || 60) - dt;
    if (this.saveTimer <= 0) {
      this.saveTimer = 60;
      this.autosave(true);
    }
  }

  regionAt(pos) {
    for (const r of WD.REGIONS) {
      const d = Math.hypot(pos.x - r.x, pos.z - r.z);
      if (d < r.r && (r.minY === undefined || pos.y > r.minY)) return r;
    }
    return null;
  }

  nearSign() {
    const signs = [...WD.VILLAGE.signs.map((s, i) => ({ ...s, key: `sign.${i}` })), { ...WD.HINT_SIGN, key: 'sign.hint' }];
    for (const s of signs) {
      if (Math.hypot(this.player.pos.x - s.x, this.player.pos.z - s.z) < 2.2) return s;
    }
    return null;
  }

  campfireDialogue() {
    this.dialogue.start({
      name: t('campfire.name'),
      lines: t('campfire.text'),
      choices: [
        { label: t('campfire.morning'), action: () => this.rest(7) },
        { label: t('campfire.evening'), action: () => this.rest(19) },
        { label: t('campfire.midnight'), action: () => this.rest(23.5) },
        { label: t('choice.cancel'), action: () => {} },
      ],
    });
  }
}

// Kayittan turetilen sayilar (parca, tuy, tamamlanma).
class Progress {
  constructor(game) {
    this.game = game;
  }

  shardCount() {
    const s = this.game.save;
    if (!s) return 0;
    return s.collected.filter((id) => /^s\d\d$/.test(id)).length + s.rewards.filter((r) => WD.QUEST_SHARDS.includes(r)).length;
  }

  featherCount() {
    const s = this.game.save;
    if (!s) return 0;
    const fromRewards = ['owl_feather', 'shop_feather', 'rabbit_feather', 'bear_feather'].filter((r) => s.rewards.includes(r)).length;
    return s.collected.filter((id) => /^f\d$/.test(id)).length + fromRewards;
  }

  completion() {
    const g = this.game;
    const s = g.save;
    if (!s) return 0;
    const parts = [
      this.shardCount() / WD.SHARD_TOTAL,
      this.featherCount() / WD.FEATHER_TOTAL,
      Math.min(1, s.shellsTotal / 100),
      g.quests.species() / WD.FISH.length,
      g.quests.questsDone() / 4,
      s.regions.filter((r) => WD.MAIN_REGIONS.includes(r)).length / WD.MAIN_REGIONS.length,
      s.flags.finale ? 1 : 0,
      s.regions.includes('cave') ? 1 : 0,
    ];
    return Math.floor((parts.reduce((a, b) => a + b, 0) / parts.length) * 100 + 1e-6);
  }

  updateCompletion() {
    this.game.stats.max('completion', this.completion());
  }

  refreshStats() {
    const g = this.game;
    const s = g.save;
    if (!s) return;
    g.stats.max('shards_max', this.shardCount());
    g.stats.max('feathers_max', this.featherCount());
    g.stats.max('shells_max', s.shellsTotal);
    g.stats.max('species_max', g.quests.species());
    g.stats.max('regions_max', s.regions.filter((r) => WD.MAIN_REGIONS.includes(r)).length);
    g.stats.max('quests_max', g.quests.questsDone());
    g.stats.max('npcs_max', s.talked.length);
    this.updateCompletion();
  }
}

export { LAYOUT };
