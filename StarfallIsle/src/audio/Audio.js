// Ses motoru: hicbir ses dosyasi yok, hepsi WebAudio ile sentezleniyor.
// Lisans derdi yok, oyun boyutu kucuk, ve her ses oyunun paletine gore
// ayarlanabiliyor. Otobusler: master -> {music, sfx, ambience}, ortak yanki.
import { Music } from './Music.js';
import { Ambience } from './Ambience.js';

export class AudioEngine {
  constructor(settings) {
    this.settings = settings;
    this.ctx = null;
    this.ready = false;
    this.music = new Music(this);
    this.ambience = new Ambience(this);
    this.voiceT = 0;
  }

  // Tarayicilar sesi ilk kullanici etkilesimine kadar acmaz.
  unlock() {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') this.ctx.resume();
      return;
    }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    this.ctx = new AC();
    const c = this.ctx;
    this.master = c.createGain();
    this.master.connect(c.destination);
    const comp = c.createDynamicsCompressor();
    comp.threshold.value = -14;
    comp.ratio.value = 3;
    this.master.disconnect();
    this.master.connect(comp);
    comp.connect(c.destination);
    this.busMusic = c.createGain();
    this.busSfx = c.createGain();
    this.busAmb = c.createGain();
    for (const b of [this.busMusic, this.busSfx, this.busAmb]) b.connect(this.master);
    this.reverb = c.createConvolver();
    this.reverb.buffer = this.impulse(2.6, 2.2);
    this.reverbGain = c.createGain();
    this.reverbGain.gain.value = 0.35;
    this.reverb.connect(this.reverbGain);
    this.reverbGain.connect(this.master);
    this.noiseBuf = this.makeNoise(2);
    this.ready = true;
    this.applyVolumes();
    this.music.start();
    this.ambience.start();
  }

  applyVolumes() {
    if (!this.ready) return;
    const s = this.settings;
    const t = this.ctx.currentTime;
    this.master.gain.setTargetAtTime(s.get('master'), t, 0.05);
    this.busMusic.gain.setTargetAtTime(s.get('music') * 0.55, t, 0.05);
    this.busSfx.gain.setTargetAtTime(s.get('sfx'), t, 0.05);
    this.busAmb.gain.setTargetAtTime(s.get('ambience') * 0.8, t, 0.05);
  }

  impulse(seconds, decay) {
    const c = this.ctx;
    const len = Math.floor(c.sampleRate * seconds);
    const buf = c.createBuffer(2, len, c.sampleRate);
    for (let ch = 0; ch < 2; ch++) {
      const d = buf.getChannelData(ch);
      for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, decay);
    }
    return buf;
  }

  makeNoise(seconds) {
    const c = this.ctx;
    const len = Math.floor(c.sampleRate * seconds);
    const buf = c.createBuffer(1, len, c.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    return buf;
  }

  // --- yapi taslari
  tone({ freq, type = 'sine', at = 0, dur = 0.2, vol = 0.3, attack = 0.005, slide = null, bus = 'sfx', reverb = 0.15, detune = 0 }) {
    if (!this.ready) return;
    const c = this.ctx;
    const t0 = c.currentTime + at;
    const o = c.createOscillator();
    o.type = type;
    o.frequency.setValueAtTime(freq, t0);
    o.detune.value = detune;
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(20, slide), t0 + dur);
    const gn = c.createGain();
    gn.gain.setValueAtTime(0.0001, t0);
    gn.gain.exponentialRampToValueAtTime(vol, t0 + attack);
    gn.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    o.connect(gn);
    const out = bus === 'music' ? this.busMusic : bus === 'amb' ? this.busAmb : this.busSfx;
    gn.connect(out);
    if (reverb > 0) {
      const send = c.createGain();
      send.gain.value = reverb;
      gn.connect(send);
      send.connect(this.reverb);
    }
    o.start(t0);
    o.stop(t0 + dur + 0.05);
  }

  noise({ at = 0, dur = 0.2, vol = 0.3, filter = 'bandpass', freq = 1000, q = 1, slide = null, bus = 'sfx', attack = 0.005, reverb = 0.05 }) {
    if (!this.ready) return;
    const c = this.ctx;
    const t0 = c.currentTime + at;
    const src = c.createBufferSource();
    src.buffer = this.noiseBuf;
    src.loop = true;
    const f = c.createBiquadFilter();
    f.type = filter;
    f.frequency.setValueAtTime(freq, t0);
    if (slide) f.frequency.exponentialRampToValueAtTime(slide, t0 + dur);
    f.Q.value = q;
    const gn = c.createGain();
    gn.gain.setValueAtTime(0.0001, t0);
    gn.gain.exponentialRampToValueAtTime(vol, t0 + attack);
    gn.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    src.connect(f);
    f.connect(gn);
    gn.connect(bus === 'amb' ? this.busAmb : this.busSfx);
    if (reverb > 0) {
      const s = c.createGain();
      s.gain.value = reverb;
      gn.connect(s);
      s.connect(this.reverb);
    }
    src.start(t0, Math.random());
    src.stop(t0 + dur + 0.05);
  }

  bell(freq, at = 0, vol = 0.2, dur = 1.2) {
    this.tone({ freq, type: 'sine', at, dur, vol, reverb: 0.4 });
    this.tone({ freq: freq * 2.01, type: 'sine', at, dur: dur * 0.6, vol: vol * 0.35, reverb: 0.4 });
    this.tone({ freq: freq * 3.02, type: 'sine', at, dur: dur * 0.3, vol: vol * 0.15, reverb: 0.4 });
  }

  // --- oyun sesleri
  sfx(name, opt = {}) {
    if (!this.ready) return;
    const r = (a, b) => a + Math.random() * (b - a);
    switch (name) {
      case 'jump':
        this.tone({ freq: r(330, 370), slide: 620, type: 'triangle', dur: 0.16, vol: 0.12 });
        break;
      case 'flap':
        this.noise({ dur: 0.25, vol: 0.18, freq: 900, slide: 2400, q: 0.8 });
        this.bell(880, 0, 0.08, 0.5);
        this.bell(1320, 0.06, 0.06, 0.5);
        break;
      case 'land':
        this.noise({ dur: 0.12, vol: Math.min(0.3, 0.08 + (opt.impact || 0) * 0.012), filter: 'lowpass', freq: 500 });
        break;
      case 'step': {
        const surf = opt.surface;
        const f = surf === 'wood' ? 1800 : surf === 'sand' ? 700 : surf === 'mushroom' ? 400 : 1200;
        this.noise({ dur: surf === 'wood' ? 0.05 : 0.07, vol: surf === 'wood' ? 0.07 : 0.045, freq: r(f * 0.85, f * 1.15), q: surf === 'wood' ? 4 : 1.2, reverb: 0 });
        break;
      }
      case 'shell':
        this.bell(r(1170, 1190), 0, 0.09, 0.5);
        this.bell(1567, 0.07, 0.08, 0.6);
        break;
      case 'shard': {
        const notes = [784, 988, 1175, 1568];
        notes.forEach((n, i) => this.bell(n, i * 0.07, 0.14, 1.4));
        this.noise({ at: 0, dur: 0.6, vol: 0.05, freq: 6000, q: 0.5, reverb: 0.4 });
        break;
      }
      case 'feather':
        [523, 659, 784, 1047, 1319].forEach((n, i) => this.tone({ freq: n, type: 'triangle', at: i * 0.05, dur: 0.9, vol: 0.08, reverb: 0.5 }));
        break;
      case 'item':
        this.bell(660, 0, 0.12, 0.6);
        this.bell(990, 0.09, 0.12, 0.8);
        break;
      case 'splash':
        this.noise({ dur: 0.5, vol: 0.12 + (opt.strength || 0) * 0.15, freq: 1400, slide: 400, q: 0.7 });
        this.noise({ at: 0.05, dur: 0.3, vol: 0.06, freq: 3000, q: 2 });
        break;
      case 'bounce':
        this.tone({ freq: 160, slide: 520, type: 'sine', dur: 0.35, vol: 0.25 });
        this.tone({ freq: 240, slide: 720, type: 'triangle', at: 0.02, dur: 0.3, vol: 0.08 });
        break;
      case 'uiMove':
        this.tone({ freq: 880, type: 'sine', dur: 0.06, vol: 0.05, reverb: 0 });
        break;
      case 'uiConfirm':
        this.tone({ freq: 660, type: 'triangle', dur: 0.1, vol: 0.08, reverb: 0.1 });
        this.tone({ freq: 990, type: 'triangle', at: 0.07, dur: 0.14, vol: 0.08, reverb: 0.1 });
        break;
      case 'uiBack':
        this.tone({ freq: 660, slide: 440, type: 'triangle', dur: 0.14, vol: 0.07, reverb: 0 });
        break;
      case 'error':
        this.tone({ freq: 220, type: 'square', dur: 0.12, vol: 0.04, reverb: 0 });
        this.tone({ freq: 196, type: 'square', at: 0.12, dur: 0.16, vol: 0.04, reverb: 0 });
        break;
      case 'buy':
        [1047, 1319, 1568].forEach((n, i) => this.bell(n, i * 0.06, 0.1, 0.6));
        break;
      case 'achievement':
        [523, 659, 784, 1047].forEach((n, i) => this.tone({ freq: n, type: 'triangle', at: i * 0.09, dur: 0.5, vol: 0.1, reverb: 0.35 }));
        this.bell(2093, 0.38, 0.08, 1.2);
        break;
      case 'fanfare':
        [[523, 0], [659, 0.1], [784, 0.2], [1047, 0.32], [784, 0.46], [1047, 0.56]].forEach(([n, at]) => this.tone({ freq: n, type: 'square', at, dur: 0.18, vol: 0.05, reverb: 0.3 }));
        break;
      case 'lose':
        [523, 494, 466].forEach((n, i) => this.tone({ freq: n, type: 'triangle', at: i * 0.16, dur: 0.2, vol: 0.07 }));
        break;
      case 'beep':
        this.tone({ freq: 660, type: 'square', dur: 0.15, vol: 0.05, reverb: 0 });
        break;
      case 'beepHigh':
        this.tone({ freq: 1320, type: 'square', dur: 0.3, vol: 0.05, reverb: 0 });
        break;
      case 'cast':
        this.noise({ dur: 0.3, vol: 0.08, freq: 2000, slide: 600, q: 1 });
        this.tone({ freq: 300, at: 0.35, slide: 200, dur: 0.1, vol: 0.06 });
        break;
      case 'bite':
        this.tone({ freq: 500, slide: 180, dur: 0.15, vol: 0.18 });
        this.noise({ at: 0.02, dur: 0.2, vol: 0.08, freq: 1200, q: 2 });
        break;
      case 'reel':
        this.tone({ freq: r(1800, 2200), type: 'square', dur: 0.02, vol: 0.015, reverb: 0 });
        break;
      case 'shutter':
        this.noise({ dur: 0.05, vol: 0.15, freq: 3000, q: 1, reverb: 0 });
        this.noise({ at: 0.08, dur: 0.06, vol: 0.12, freq: 2200, q: 1, reverb: 0 });
        break;
      case 'ignite':
        [262, 330, 392, 523, 659, 784].forEach((n, i) => this.tone({ freq: n, type: 'sine', at: i * 0.12, dur: 3.5, vol: 0.07, attack: 0.3, reverb: 0.6 }));
        this.noise({ dur: 2.5, vol: 0.06, freq: 500, slide: 4000, q: 0.5, attack: 0.5, reverb: 0.5 });
        break;
      case 'firework': {
        const d = opt.dist || 40;
        const v = Math.min(0.25, 6 / d);
        this.noise({ dur: 0.6, vol: v, filter: 'lowpass', freq: 900, slide: 200 });
        for (let i = 0; i < 6; i++) this.noise({ at: 0.15 + Math.random() * 0.6, dur: 0.05, vol: v * 0.4, freq: 4000, q: 2 });
        break;
      }
      case 'poof':
        this.noise({ dur: 0.35, vol: 0.12, freq: 1500, slide: 300, q: 0.6 });
        this.bell(1568, 0.05, 0.06, 0.6);
        break;
      case 'discover':
        [392, 523, 659, 784].forEach((n, i) => this.tone({ freq: n, type: 'triangle', at: i * 0.12, dur: 0.9, vol: 0.06, reverb: 0.5 }));
        break;
      default:
        break;
    }
  }

  // Hayvan Gecidi tarzi konusma "mirildanmasi": her harfte kisa bir ses.
  voiceBlip(pitch = 1, ch = 'a') {
    if (!this.ready) return;
    const now = this.ctx.currentTime;
    if (now - this.voiceT < 0.055) return;
    this.voiceT = now;
    const code = ch.toLowerCase().charCodeAt(0) || 97;
    const base = 260 * pitch * (1 + ((code * 7) % 12) / 24);
    this.tone({ freq: base, type: 'triangle', dur: 0.07, vol: 0.06, reverb: 0.05 });
    this.tone({ freq: base * 1.5, type: 'sine', dur: 0.05, vol: 0.025, reverb: 0 });
  }

  update(dt, game) {
    if (!this.ready) return;
    this.music.update(dt, game);
    this.ambience.update(dt, game);
  }
}
