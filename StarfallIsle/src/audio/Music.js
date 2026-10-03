// Uretken muzik: akor ilerlemesi + bas + arpej + pentatonik melodi.
// Her cubuk ileriye dogru (lookahead) zamanlanir; gece daha yavas ve
// yumusak, final daha parlak. Asla ayni sekilde tekrar etmez ama hep ayni
// "ses ailesinde" kalir.

const MOODS = {
  menu: { bpm: 76, prog: [[0, 'M'], [9, 'm'], [5, 'M'], [7, 'M']], octave: 0, melody: 0.45, arp: 0.6, bright: 0.8 },
  day: { bpm: 92, prog: [[0, 'M'], [7, 'M'], [9, 'm'], [5, 'M']], octave: 0, melody: 0.6, arp: 0.85, bright: 1 },
  night: { bpm: 66, prog: [[9, 'm'], [5, 'M'], [0, 'M'], [7, 'sus']], octave: -1, melody: 0.35, arp: 0.45, bright: 0.55 },
  finale: { bpm: 104, prog: [[5, 'M'], [7, 'M'], [4, 'm'], [9, 'm'], [5, 'M'], [7, 'M'], [0, 'M'], [0, 'M']], octave: 0, melody: 0.75, arp: 1, bright: 1.2 },
};
const PENTA = [0, 2, 4, 7, 9];

function midi(n) {
  return 440 * Math.pow(2, (n - 69) / 12);
}

function chordNotes(root, kind) {
  const third = kind === 'm' ? 3 : kind === 'sus' ? 5 : 4;
  return [root, root + third, root + 7];
}

export class Music {
  constructor(engine) {
    this.engine = engine;
    this.mood = 'menu';
    this.forced = 'menu';
    this.nextBar = 0;
    this.bar = 0;
    this.motif = null;
    this.melIdx = 7;
  }

  start() {
    this.nextBar = this.engine.ctx.currentTime + 0.3;
  }

  setMood(m) {
    this.forced = m;
  }

  update(dt, game) {
    const e = this.engine;
    if (!e.ready) return;
    let mood = this.forced;
    if (mood === 'auto') mood = game.sky && game.sky.state.night > 0.55 ? 'night' : 'day';
    if (game.state === 'menu' && this.forced !== 'finale') mood = 'menu';
    this.mood = mood;
    const now = e.ctx.currentTime;
    if (this.nextBar < now) this.nextBar = now + 0.05;
    while (this.nextBar < now + 0.25) {
      this.scheduleBar(this.nextBar);
      const M = MOODS[this.mood];
      this.nextBar += (60 / M.bpm) * 4;
      this.bar++;
    }
  }

  scheduleBar(t0) {
    const e = this.engine;
    const M = MOODS[this.mood];
    const beat = 60 / M.bpm;
    const [root, kind] = M.prog[this.bar % M.prog.length];
    const base = 48 + 12 * M.octave; // C3
    const at = (t) => Math.max(0, t - e.ctx.currentTime);
    const notes = chordNotes(root, kind);

    // pad
    for (const n of notes) {
      e.tone({ freq: midi(base + 12 + n), type: 'sine', at: at(t0), dur: beat * 4.2, vol: 0.05 * M.bright, attack: beat * 0.8, bus: 'music', reverb: 0.5 });
      e.tone({ freq: midi(base + 12 + n), type: 'triangle', at: at(t0), dur: beat * 4.0, vol: 0.018 * M.bright, attack: beat, bus: 'music', reverb: 0.5, detune: 7 });
    }
    // bas
    e.tone({ freq: midi(base - 12 + root), type: 'sine', at: at(t0), dur: beat * 1.8, vol: 0.12, attack: 0.02, bus: 'music', reverb: 0.05 });
    e.tone({ freq: midi(base - 12 + root + 7), type: 'sine', at: at(t0 + beat * 2), dur: beat * 1.6, vol: 0.09, attack: 0.02, bus: 'music', reverb: 0.05 });

    // arpej (sekizlik)
    const arpOrder = [0, 1, 2, 1, 2, 0, 1, 2];
    for (let i = 0; i < 8; i++) {
      if (Math.random() > M.arp) continue;
      const n = notes[arpOrder[i]] + (i >= 4 ? 12 : 0);
      e.tone({ freq: midi(base + 24 + n), type: 'triangle', at: at(t0 + i * beat * 0.5), dur: beat * 0.6, vol: 0.028 * M.bright, attack: 0.004, bus: 'music', reverb: 0.3 });
    }

    // melodi: iki cubukluk motif, kucuk degisikliklerle tekrar
    if (this.bar % 4 === 0 || !this.motif) this.motif = this.makeMotif(M);
    const variant = this.bar % 2;
    for (const step of this.motif) {
      if (Math.random() > M.melody + 0.25) continue;
      let deg = step.deg + (variant && Math.random() < 0.3 ? (Math.random() < 0.5 ? -1 : 1) : 0);
      const oct = Math.floor(deg / 5);
      const pn = PENTA[((deg % 5) + 5) % 5] + oct * 12;
      const freq = midi(base + 36 + pn);
      const tt = at(t0 + step.pos * beat);
      e.tone({ freq, type: 'sine', at: tt, dur: step.len * beat * 1.4, vol: 0.06 * M.bright, attack: 0.005, bus: 'music', reverb: 0.4 });
      e.tone({ freq: freq * 4, type: 'sine', at: tt, dur: 0.12, vol: 0.012 * M.bright, attack: 0.002, bus: 'music', reverb: 0.2 });
    }
  }

  makeMotif(M) {
    const steps = [];
    let pos = 0;
    let deg = 5 + Math.floor(Math.random() * 3);
    while (pos < 4) {
      const len = Math.random() < 0.6 ? 0.5 : 1;
      if (Math.random() < M.melody) steps.push({ pos, len, deg });
      deg += [-2, -1, -1, 0, 1, 1, 2][Math.floor(Math.random() * 7)];
      deg = Math.max(0, Math.min(10, deg));
      pos += len;
    }
    return steps;
  }
}
