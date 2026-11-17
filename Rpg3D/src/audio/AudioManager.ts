import * as THREE from 'three';
import type { Assets } from '../core/Assets';

export interface VolumeSettings { master: number; music: number; sfx: number }

interface Voice { audio: THREE.PositionalAudio; anchor: THREE.Object3D; busyUntil: number }

/**
 * Ses sistemi.
 *
 *   kaynaklar ──► sfxBus ──┐
 *   UI sesleri ─► uiBus ───┼─► listener (master) ─► hoparlör
 *   <audio> ────► musicBus ┘
 *
 * - 3D sesler sabit bir PositionalAudio havuzundan çalınır (her vuruşta
 *   yeni düğüm yaratmak GC ve panner maliyeti demek).
 * - Müzik HTMLAudioElement ile akar; iki eleman arasında crossfade.
 * - Döngüsel ortam sesleri (ateş, nehir) mesafeye göre kendiliğinden kısılır.
 */
export class AudioManager {
  readonly listener = new THREE.AudioListener();
  readonly ctx: AudioContext;
  private sfxBus: GainNode;
  private uiBus: GainNode;
  private musicBus: GainNode;
  private voices: Voice[] = [];
  private loops: THREE.PositionalAudio[] = [];
  private music: { el: HTMLAudioElement; gain: GainNode; id: string }[] = [];
  private activeMusic = -1;
  currentMusic = '';
  volumes: VolumeSettings = { master: 0.8, music: 0.55, sfx: 0.9 };
  /** Test/teşhis: çalınan seslerin sayacı. */
  readonly stats = { played: 0, byId: {} as Record<string, number> };

  constructor(private assets: Assets, private scene: THREE.Scene, voiceCount = 24) {
    this.ctx = this.listener.context as AudioContext;
    this.sfxBus = this.ctx.createGain();
    this.uiBus = this.ctx.createGain();
    this.musicBus = this.ctx.createGain();
    for (const b of [this.sfxBus, this.uiBus, this.musicBus]) b.connect(this.listener.getInput());
    for (let i = 0; i < voiceCount; i++) {
      const anchor = new THREE.Object3D();
      const audio = new THREE.PositionalAudio(this.listener);
      audio.gain.disconnect();
      audio.gain.connect(this.sfxBus);
      audio.setDistanceModel('inverse');
      audio.setRefDistance(4);
      audio.setRolloffFactor(1.3);
      anchor.add(audio);
      scene.add(anchor);
      this.voices.push({ audio, anchor, busyUntil: 0 });
    }
    for (let i = 0; i < 2; i++) {
      const el = new Audio();
      el.loop = true;
      el.preload = 'auto';
      el.crossOrigin = 'anonymous';
      const src = this.ctx.createMediaElementSource(el);
      const gain = this.ctx.createGain();
      gain.gain.value = 0;
      src.connect(gain).connect(this.musicBus);
      this.music.push({ el, gain, id: '' });
    }
    this.applyVolumes();
  }

  /** Tarayıcılar ilk kullanıcı etkileşiminden önce sesi askıda tutar. */
  async unlock(): Promise<void> {
    if (this.ctx.state !== 'running') await this.ctx.resume().catch(() => {});
  }

  applyVolumes(): void {
    this.listener.setMasterVolume(this.volumes.master);
    this.sfxBus.gain.value = this.volumes.sfx;
    this.uiBus.gain.value = this.volumes.sfx * 0.8;
    this.musicBus.gain.value = this.volumes.music;
  }

  private pick(id: string): { buf: AudioBuffer; vol: number } | null {
    const bufs = this.assets.sounds.get(id);
    const entry = this.assets.manifest.sounds[id];
    if (!bufs || bufs.length === 0 || !entry) return null;
    this.stats.played++;
    this.stats.byId[id] = (this.stats.byId[id] ?? 0) + 1;
    return { buf: bufs[Math.floor(Math.random() * bufs.length)], vol: entry.volume };
  }

  /** Konumsuz ses (UI, oyuncunun kendi ayak sesi). */
  play2D(id: string, o: { volume?: number; rate?: number; ui?: boolean; jitter?: number } = {}): void {
    const p = this.pick(id);
    if (!p || this.ctx.state !== 'running') return;
    const src = this.ctx.createBufferSource();
    src.buffer = p.buf;
    const j = o.jitter ?? 0.06;
    src.playbackRate.value = (o.rate ?? 1) * (1 - j + Math.random() * j * 2);
    const g = this.ctx.createGain();
    g.gain.value = p.vol * (o.volume ?? 1);
    src.connect(g).connect(o.ui ? this.uiBus : this.sfxBus);
    src.start();
  }

  /** Dünyada bir noktadan gelen ses. */
  play3D(id: string, pos: THREE.Vector3, o: { volume?: number; rate?: number; ref?: number } = {}): void {
    const p = this.pick(id);
    if (!p || this.ctx.state !== 'running') return;
    const now = this.ctx.currentTime;
    // Boş ses yoksa en erken bitecek olanı çal (en eski vuruş kesilir).
    let v = this.voices.find((x) => x.busyUntil <= now);
    if (!v) v = this.voices.reduce((a, b) => (a.busyUntil < b.busyUntil ? a : b));
    if (v.audio.isPlaying) v.audio.stop();
    v.anchor.position.copy(pos);
    v.anchor.updateMatrixWorld();
    v.audio.setBuffer(p.buf);
    v.audio.setRefDistance(o.ref ?? 4);
    v.audio.setVolume(p.vol * (o.volume ?? 1));
    const rate = (o.rate ?? 1) * (0.94 + Math.random() * 0.12);
    v.audio.setPlaybackRate(rate);
    v.audio.play();
    v.busyUntil = now + p.buf.duration / rate;
  }

  /** Dünyaya sabitlenmiş döngüsel ses (kamp ateşi, nehir). */
  addLoop(id: string, pos: THREE.Vector3, volume: number, ref: number): void {
    const bufs = this.assets.sounds.get(id);
    const entry = this.assets.manifest.sounds[id];
    if (!bufs?.length || !entry) return;
    const anchor = new THREE.Object3D();
    anchor.position.copy(pos);
    const audio = new THREE.PositionalAudio(this.listener);
    audio.gain.disconnect();
    audio.gain.connect(this.sfxBus);
    audio.setBuffer(bufs[0]);
    audio.setLoop(true);
    audio.setRefDistance(ref);
    audio.setRolloffFactor(1.6);
    audio.setVolume(entry.volume * volume);
    anchor.add(audio);
    this.scene.add(anchor);
    this.loops.push(audio);
  }

  /** Döngüleri başlat (ses kilidi açıldıktan sonra). */
  startLoops(): void {
    if (this.ctx.state !== 'running') return;
    for (const l of this.loops) if (!l.isPlaying) l.play();
  }

  stopLoops(): void {
    for (const l of this.loops) if (l.isPlaying) l.stop();
  }

  /** Müziği değiştir; aynı parça çalıyorsa bir şey yapma. */
  setMusic(id: string, fadeSec = 2.5): void {
    if (id === this.currentMusic) return;
    const url = this.assets.musicUrl(id);
    this.currentMusic = id;
    const t = this.ctx.currentTime;
    if (this.activeMusic >= 0) {
      const old = this.music[this.activeMusic];
      old.gain.gain.cancelScheduledValues(t);
      old.gain.gain.setValueAtTime(old.gain.gain.value, t);
      old.gain.gain.linearRampToValueAtTime(0, t + fadeSec);
      const el = old.el;
      setTimeout(() => { if (old.id !== this.currentMusic) el.pause(); }, fadeSec * 1000 + 100);
    }
    if (!url) { this.activeMusic = -1; return; }
    const next = (this.activeMusic + 1) % 2;
    const m = this.music[next];
    m.id = id;
    if (!m.el.src.endsWith(url)) m.el.src = url;
    m.el.currentTime = 0;
    m.el.play().catch(() => {});
    m.gain.gain.cancelScheduledValues(t);
    m.gain.gain.setValueAtTime(0, t);
    m.gain.gain.linearRampToValueAtTime(1, t + fadeSec);
    this.activeMusic = next;
  }

  /** Müzik elemanı gerçekten çalıyor mu (smoke test). */
  musicPlaying(): boolean {
    const m = this.music[this.activeMusic];
    return !!m && !m.el.paused;
  }
}
