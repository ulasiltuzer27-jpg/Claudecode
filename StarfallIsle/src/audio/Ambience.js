// Ortam sesleri: dalga ugultusu (kiyiya yakinlikla), gunduz kus civiltisi,
// gece cirkir bocegi, yukseklerde ve suzulurken ruzgar.

export class Ambience {
  constructor(engine) {
    this.engine = engine;
    this.started = false;
    this.birdT = 2;
    this.cricketT = 1;
  }

  start() {
    const e = this.engine;
    const c = e.ctx;
    const loop = (freq, q, type = 'lowpass') => {
      const src = c.createBufferSource();
      src.buffer = e.noiseBuf;
      src.loop = true;
      const f = c.createBiquadFilter();
      f.type = type;
      f.frequency.value = freq;
      f.Q.value = q;
      const g = c.createGain();
      g.gain.value = 0;
      src.connect(f);
      f.connect(g);
      g.connect(e.busAmb);
      src.start();
      return { src, f, g };
    };
    this.waves = loop(420, 0.6);
    this.wind = loop(700, 0.9, 'bandpass');
    this.started = true;
    this.t = 0;
  }

  coastFactor(game) {
    const p = game.player?.pos;
    const T = game.world?.terrain;
    if (!p || !T) return 0.5;
    let wet = 0;
    const N = 12;
    for (let i = 0; i < N; i++) {
      const a = (i / N) * Math.PI * 2;
      const x = p.x + Math.cos(a) * 18;
      const z = p.z + Math.sin(a) * 18;
      if (T.height(x, z) < 0) wet++;
    }
    return wet / N;
  }

  update(dt, game) {
    if (!this.started) return;
    const e = this.engine;
    const c = e.ctx;
    this.t += dt;
    const night = game.sky ? game.sky.state.night : 0;
    const inMenu = game.state === 'menu';
    const coast = inMenu ? 0.6 : this.coastFactor(game);
    const swell = 0.6 + 0.4 * Math.sin(this.t * 0.45) * Math.sin(this.t * 0.17 + 1);
    this.waves.g.gain.setTargetAtTime(0.05 + coast * 0.22 * swell, c.currentTime, 0.4);
    this.waves.f.frequency.setTargetAtTime(300 + swell * 300, c.currentTime, 0.5);

    const p = game.player;
    const alt = p ? Math.max(0, p.pos.y - 15) / 30 : 0;
    const speed = p ? Math.hypot(p.vel.x, p.vel.y, p.vel.z) : 0;
    const windAmt = Math.min(0.3, alt * 0.08 + (p && p.gliding ? 0.06 + speed * 0.012 : 0));
    this.wind.g.gain.setTargetAtTime(windAmt, c.currentTime, 0.3);
    this.wind.f.frequency.setTargetAtTime(500 + speed * 60, c.currentTime, 0.3);

    if (inMenu) return;
    // kuslar
    this.birdT -= dt;
    if (this.birdT <= 0 && night < 0.4) {
      this.birdT = 1.5 + Math.random() * 4;
      const base = 2200 + Math.random() * 1600;
      const n = 2 + Math.floor(Math.random() * 4);
      for (let i = 0; i < n; i++) {
        e.tone({ freq: base, slide: base * (1.2 + Math.random() * 0.5), type: 'sine', at: i * 0.11, dur: 0.08, vol: 0.025 * (1 - coast * 0.5), bus: 'amb', reverb: 0.3 });
      }
    }
    // cirkir bocekleri
    this.cricketT -= dt;
    if (this.cricketT <= 0 && night > 0.5) {
      this.cricketT = 0.6 + Math.random() * 1.4;
      const f = 4200 + Math.random() * 600;
      for (let i = 0; i < 3; i++) e.tone({ freq: f, type: 'square', at: i * 0.045, dur: 0.03, vol: 0.006, bus: 'amb', reverb: 0.1 });
    }
  }
}
