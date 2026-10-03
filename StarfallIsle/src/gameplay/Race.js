// Kurbaga ile yaris: geri sayim, kurbaga rotada ziplayarak ilerler,
// bayraga ilk varan kazanir.
import * as THREE from 'three';
import { RACE_PATH, RACE_FROG_SPEED } from '../world/WorldData.js';
import { t } from '../core/i18n.js';

export class Race {
  constructor(game) {
    this.game = game;
    this.state = 'idle';
    this.timer = 0;
    this.curve = null;
  }

  buildCurve() {
    const T = this.game.world.terrain;
    const pts = RACE_PATH.map(([x, z]) => new THREE.Vector3(x, T.height(x, z), z));
    this.curve = new THREE.CatmullRomCurve3(pts);
    this.length = this.curve.getLength();
    this.goal = this.game.world.raceFlagPos.clone();
  }

  start(npc) {
    if (!this.curve) this.buildCurve();
    this.npc = npc;
    npc.racing = true;
    this.state = 'countdown';
    this.timer = 3.4;
    this.elapsed = 0;
    this.frogDist = 0;
    this.lastCount = 4;
    this.game.ui.hud.raceBanner(t('race.ready'));
    this.game.audio.sfx('uiConfirm');
  }

  cancel() {
    if (this.npc) this.resetFrog();
    this.state = 'idle';
    this.game.ui.hud.raceBanner(null);
    this.game.ui.hud.raceTimer(null);
  }

  resetFrog() {
    const npc = this.npc;
    npc.racing = false;
    const d = npc.def;
    this.game.npcs.placeAt(npc, d.x, d.z, d.yaw);
  }

  update(dt) {
    if (this.state === 'idle') return;
    const g = this.game;
    const hud = g.ui.hud;
    if (this.state === 'countdown') {
      this.timer -= dt;
      const n = Math.ceil(this.timer - 0.4);
      if (n !== this.lastCount && n >= 1 && n <= 3) {
        this.lastCount = n;
        hud.raceBanner(String(n));
        g.audio.sfx('beep');
      }
      if (this.timer <= 0.4 && this.lastCount !== 0) {
        this.lastCount = 0;
        hud.raceBanner(t('race.go'));
        g.audio.sfx('beepHigh');
      }
      if (this.timer <= 0) {
        this.state = 'running';
        setTimeout(() => { if (this.state === 'running') hud.raceBanner(null); }, 700);
      }
      return;
    }
    if (this.state === 'running') {
      this.elapsed += dt;
      hud.raceTimer(this.elapsed);
      this.frogDist += RACE_FROG_SPEED * dt;
      const u = Math.min(1, this.frogDist / this.length);
      const p = this.curve.getPointAt(u);
      const ahead = this.curve.getPointAt(Math.min(1, u + 0.01));
      const T = g.world.terrain;
      const hop = Math.abs(Math.sin(this.elapsed * 7)) * 0.7;
      const npc = this.npc;
      npc.pos.set(p.x, T.height(p.x, p.z) + hop, p.z);
      npc.model.root.position.copy(npc.pos);
      npc.yaw = Math.atan2(ahead.x - p.x, ahead.z - p.z);
      npc.model.root.rotation.y = npc.yaw;
      if (npc.collider) npc.collider.setTranslation({ x: p.x, y: -50, z: p.z });

      const pl = g.player.pos;
      const playerAtGoal = Math.hypot(pl.x - this.goal.x, pl.z - this.goal.z) < 3.2 && Math.abs(pl.y - this.goal.y) < 4;
      if (playerAtGoal) return this.finish(true);
      if (u >= 1) return this.finish(false);
      if (this.elapsed > 70) return this.finish(false);
    }
  }

  finish(won) {
    const g = this.game;
    this.state = 'idle';
    g.ui.hud.raceTimer(null);
    g.ui.hud.raceBanner(won ? t('race.win') : t('race.lose'));
    setTimeout(() => g.ui.hud.raceBanner(null), 2200);
    const npc = this.npc;
    if (won) {
      g.audio.sfx('fanfare');
      g.particles.additive.emit({ pos: g.player.pos.clone().add(new THREE.Vector3(0, 1.5, 0)), count: 60, radial: 6, gravity: -6, life: 1.6, size: 0.25, color: ['#ff6b6b', '#ffd84a', '#7fd0ff', '#9be36a', '#ff9df0'] });
      g.stats.max('race', 1);
    } else {
      g.audio.sfx('lose');
    }
    const firstWin = won && g.save.quests.frog !== 'done';
    setTimeout(() => {
      // kurbaga bayragin yaninda dursun, konusma bitince evine donsun
      g.npcs.placeAt(npc, this.goal.x + 1.6, this.goal.z + 1.2, 0);
      npc.racing = false;
      const done = () => {
        this.resetFrog();
        if (firstWin) g.quests.finishQuest('frog', 'q_frog', 'shard');
      };
      const key = won ? (firstWin ? 'dlg.frog.win' : 'dlg.frog.winAgain') : 'dlg.frog.lose';
      g.dialogue.start({ npc, lines: t(key, { time: this.elapsed.toFixed(1) }), onEnd: done });
    }, 1400);
  }
}
