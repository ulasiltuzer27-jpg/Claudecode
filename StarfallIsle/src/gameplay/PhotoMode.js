// Photo mode: oyun durur, arayuz gizlenir, kamera serbest kalir.
import * as THREE from 'three';
import { Platform } from '../core/Platform.js';
import { t } from '../core/i18n.js';

const FILTERS = ['none', 'warm', 'sepia', 'mono', 'dream', 'poster'];

export class PhotoMode {
  constructor(game) {
    this.game = game;
    this.active = false;
    this.filter = 0;
    this.pos = new THREE.Vector3();
    this.yaw = 0;
    this.pitch = 0;
  }

  enter() {
    const g = this.game;
    this.active = true;
    this.prevState = g.state;
    g.setState('photo');
    const cam = g.camera;
    this.pos.copy(cam.position);
    const dir = new THREE.Vector3();
    cam.getWorldDirection(dir);
    this.yaw = Math.atan2(-dir.x, -dir.z);
    this.pitch = Math.asin(THREE.MathUtils.clamp(dir.y, -1, 1));
    this.hidePlayer = false;
    g.ui.hud.setVisible(false);
    g.ui.photo.show(true, this.filterName());
  }

  exit() {
    const g = this.game;
    this.active = false;
    g.renderer.filter = 0;
    g.player.model.root.visible = true;
    g.ui.photo.show(false);
    g.ui.hud.setVisible(true);
    g.setState('playing');
  }

  filterName() {
    return t(`photo.filter.${FILTERS[this.filter]}`);
  }

  async shoot() {
    const g = this.game;
    g.ui.photo.show(false);
    g.renderer.render(0);
    const url = g.renderer.canvas.toDataURL('image/png');
    g.ui.photo.show(true, this.filterName());
    g.ui.photo.flash();
    g.audio.sfx('shutter');
    g.stats.add('photos', 1);
    const where = await Platform.saveScreenshot(url);
    g.events.emit('toast', { key: 'photo.saved', params: { where: where || '' } });
  }

  update(dt, input) {
    if (!this.active) return;
    const g = this.game;
    if (input.pressed('back') || input.pressed('photo')) return this.exit();
    if (input.pressed('filter')) {
      this.filter = (this.filter + 1) % FILTERS.length;
      g.renderer.filter = this.filter;
      g.ui.photo.show(true, this.filterName());
      g.audio.sfx('uiMove');
    }
    if (input.pressed('hide')) {
      this.hidePlayer = !this.hidePlayer;
      g.player.model.root.visible = !this.hidePlayer;
    }
    if (input.pressed('shot')) {
      this.shoot();
      return;
    }
    const look = input.look();
    this.yaw -= look.x * g.settings.get('sensitivity');
    this.pitch -= look.y * g.settings.get('sensitivity');
    this.pitch = THREE.MathUtils.clamp(this.pitch, -1.4, 1.4);
    const mv = input.move();
    const speed = input.held('sprint') ? 14 : 5;
    const fwd = new THREE.Vector3(-Math.sin(this.yaw) * Math.cos(this.pitch), Math.sin(this.pitch), -Math.cos(this.yaw) * Math.cos(this.pitch));
    const right = new THREE.Vector3(Math.cos(this.yaw), 0, -Math.sin(this.yaw));
    this.pos.addScaledVector(fwd, mv.y * speed * dt).addScaledVector(right, mv.x * speed * dt);
    if (input.held('rise')) this.pos.y += speed * dt;
    if (input.held('sink')) this.pos.y -= speed * dt;
    // oyuncudan cok uzaklasmasin
    const p = g.player.pos;
    const off = this.pos.clone().sub(p);
    if (off.length() > 25) this.pos.copy(p).add(off.setLength(25));
    const T = g.world.terrain;
    this.pos.y = Math.max(this.pos.y, T.height(this.pos.x, this.pos.z) + 0.3, T.waterLevel(this.pos.x, this.pos.z) + 0.2);
    g.camera.position.copy(this.pos);
    g.camera.lookAt(this.pos.clone().add(fwd));
  }
}
