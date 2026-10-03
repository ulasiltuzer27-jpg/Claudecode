// Final: yildiz parcalari fenere akar, lamba yanar, havai fisekler,
// jenerik. Sonrasinda fener her gece donen isigiyla adayi aydinlatir.
import * as THREE from 'three';
import { t } from '../core/i18n.js';

const beamVert = /* glsl */`
  varying float vV;
  void main() { vV = uv.y; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }
`;
const beamFrag = /* glsl */`
  uniform float uIntensity;
  varying float vV;
  void main() {
    float a = pow(1.0 - vV, 1.6) * 0.32 * uIntensity;
    gl_FragColor = vec4(vec3(1.0, 0.92, 0.7) * a, a);
  }
`;

export class Finale {
  constructor(game) {
    this.game = game;
    this.active = false;
    this.lit = false;
    this.beam = null;
  }

  ensureBeam() {
    if (this.beam) return;
    const lh = this.game.world.lighthouse;
    const lamp = this.game.world.anchors.get('lighthouse.lamp');
    this.beamUniforms = { uIntensity: { value: 0 } };
    const mat = new THREE.ShaderMaterial({
      uniforms: this.beamUniforms, vertexShader: beamVert, fragmentShader: beamFrag,
      transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide, fog: false,
    });
    // Silindirin ust yaricapi genis, alti dar: kaydirinca dar uc lambaya
    // (y=0) oturur. Dar uctaki uv.y=0, parlaklik orada en yuksek.
    const g = new THREE.CylinderGeometry(9, 0.5, 90, 24, 1, true);
    g.translate(0, 45, 0);
    g.rotateZ(-Math.PI / 2);
    const g2 = g.clone().rotateY(Math.PI);
    this.beam = new THREE.Group();
    this.beam.add(new THREE.Mesh(g, mat), new THREE.Mesh(g2, mat));
    this.beam.position.copy(lamp);
    this.beam.visible = false;
    this.game.renderer.scene.add(this.beam);
    this.lampLight = new THREE.PointLight('#ffd27a', 0, 40, 1.4);
    this.lampLight.position.copy(lamp);
    this.game.renderer.scene.add(this.lampLight);
    this.lampMat = lh.lampMat;
  }

  setLit(on) {
    this.ensureBeam();
    this.lit = on;
    this.beam.visible = on;
    this.lampMat.emissiveIntensity = on ? 3.5 : 0;
    this.lampLight.intensity = on ? 30 : 0;
  }

  start() {
    const g = this.game;
    this.ensureBeam();
    this.active = true;
    this.t = 0;
    this.stage = 0;
    g.setState('cutscene');
    g.player.frozen = true;
    g.audio.music.setMood('finale');
    this.lamp = g.world.anchors.get('lighthouse.lamp');
  }

  update(dt) {
    const g = this.game;
    if (this.lit && this.beam) {
      this.beam.rotation.y += dt * 0.5;
      const night = g.sky.state.night;
      this.beamUniforms.uIntensity.value = 0.25 + night * 0.9;
    }
    if (!this.active) return;
    this.t += dt;
    const time = this.t;
    const lamp = this.lamp;
    const R = g.renderer;

    // 0-0.8: kararma, saati aksama al
    if (this.stage === 0) {
      R.fade = Math.min(1, time / 0.8);
      if (time >= 0.8) {
        this.stage = 1;
        g.setHour(20.4);
        g.ui.hud.setVisible(false);
      }
      return;
    }
    // kamera fenerin etrafinda doner
    const ang = 0.6 + (time - 0.8) * 0.18;
    const camPos = new THREE.Vector3(lamp.x + Math.sin(ang) * 26, lamp.y + 3, lamp.z + Math.cos(ang) * 26);
    g.cameraRig.override = { pos: camPos, look: lamp.clone().add(new THREE.Vector3(0, -2, 0)), speed: 6 };

    if (this.stage === 1) {
      R.fade = Math.max(0, 1 - (time - 0.8) / 0.8);
      // parcalar oyuncudan lambaya akar
      if (Math.random() < dt * 30) {
        const from = g.player.pos.clone().add(new THREE.Vector3(0, 1, 0));
        const v = lamp.clone().sub(from).multiplyScalar(1 / 1.6);
        g.particles.additive.emit({ pos: from, count: 1, spread: 0.3, vel: v, life: 1.6, size: 0.45, sizeEnd: 0.2, color: ['#fff2a8', '#ffd76a'], alpha: 1 });
      }
      if (time > 4.2) {
        this.stage = 2;
        this.ignite = 0;
        g.audio.sfx('ignite');
      }
      return;
    }
    if (this.stage === 2) {
      this.ignite = Math.min(1, this.ignite + dt);
      this.lampMat.emissiveIntensity = this.ignite * 3.5;
      this.lampLight.intensity = this.ignite * 30;
      this.beam.visible = true;
      this.lit = true;
      this.beamUniforms.uIntensity.value = this.ignite * 1.15;
      if (time > 5.4 && !this.titleShown) {
        this.titleShown = true;
        g.ui.hud.titleCard(t('finale.title'), t('finale.subtitle'));
      }
      // havai fisekler
      if (Math.random() < dt * 2.2 && time < 14) this.firework();
      if (time > 15) {
        this.stage = 3;
        this.t3 = 0;
      }
      return;
    }
    if (this.stage === 3) {
      this.t3 += dt;
      R.fade = Math.min(1, this.t3 / 1.0);
      if (this.t3 >= 1.0) {
        this.stage = 4;
        g.ui.hud.titleCard(null);
        g.ui.credits.show(() => this.end());
      }
    }
  }

  firework() {
    const g = this.game;
    const lamp = this.lamp || g.world.anchors.get('lighthouse.lamp');
    const a = Math.random() * Math.PI * 2;
    const r = 18 + Math.random() * 30;
    const pos = new THREE.Vector3(lamp.x + Math.cos(a) * r, lamp.y + 18 + Math.random() * 25, lamp.z + Math.sin(a) * r);
    const palettes = [['#ff6b6b', '#ffd0d0'], ['#ffd84a', '#fff4b0'], ['#7fd0ff', '#d0f0ff'], ['#b98aff', '#efe0ff'], ['#9be36a', '#e0ffd0']];
    const col = palettes[Math.floor(Math.random() * palettes.length)];
    g.particles.additive.emit({ pos, count: 90, radial: 13, gravity: -4, drag: 1.2, life: 1.8, size: 0.7, sizeEnd: 0.1, color: col, alpha: 1 });
    g.audio.sfx('firework', { dist: pos.distanceTo(g.camera.position) });
  }

  end() {
    const g = this.game;
    this.active = false;
    g.cameraRig.override = null;
    g.save.flags.finale = true;
    if (g.save.finaleTime === null) g.save.finaleTime = g.save.playTime;
    g.stats.max('finale', 1);
    if (g.save.playTime < 30 * 60) g.stats.max('speedrun', 1);
    g.player.frozen = false;
    g.ui.hud.setVisible(true);
    g.audio.music.setMood('auto');
    g.setState('playing');
    g.progress.updateCompletion();
    g.autosave();
    let k = 1;
    const fadeIn = () => {
      k -= 0.04;
      g.renderer.fade = Math.max(0, k);
      if (k > 0) requestAnimationFrame(fadeIn);
    };
    fadeIn();
    g.cameraRig.snap(g.player.pos, g.player.yaw + Math.PI);
  }
}
