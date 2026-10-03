// Ucuncu sahis kamerasi: fare/sag cubukla doner, carpismayla yaklasir,
// istege bagli olarak hareket ederken yavasca karakterin arkasina gecer.
import * as THREE from 'three';

export class CameraRig {
  constructor(camera, game) {
    this.camera = camera;
    this.game = game;
    this.yaw = Math.PI;
    this.pitch = 0.32;
    this.distance = 7;
    this.targetDistance = 7;
    this.focus = new THREE.Vector3();
    this.idleLook = 0;
    this.shake = 0;
    this.fovBoost = 0;
    this.override = null; // ara sahneler icin { pos, look }
  }

  snap(target, yaw) {
    this.focus.copy(target).add(new THREE.Vector3(0, 0.8, 0));
    if (yaw !== undefined) this.yaw = yaw;
    this.update(0, null, null, true);
  }

  update(dt, input, player, instant = false) {
    const settings = this.game.settings;
    if (this.override) {
      const o = this.override;
      this.camera.position.lerp(o.pos, instant ? 1 : Math.min(1, dt * (o.speed || 2)));
      this.camera.lookAt(o.look);
      return;
    }
    if (input) {
      const look = input.look();
      const sens = settings.get('sensitivity');
      const inv = settings.get('invertY') ? -1 : 1;
      this.yaw -= look.x * sens;
      this.pitch += look.y * sens * inv;
      this.pitch = THREE.MathUtils.clamp(this.pitch, -0.5, 1.25);
      if (Math.abs(look.x) + Math.abs(look.y) > 0.0005) this.idleLook = 0;
      else this.idleLook += dt;
      if (input.mouse.wheel) this.targetDistance = THREE.MathUtils.clamp(this.targetDistance + input.mouse.wheel * 0.8, 3.5, 12);
    }

    if (player) {
      const target = player.pos.clone();
      target.y += 0.8;
      if (player.swimming) target.y = Math.max(target.y, player.waterLevelAt() + 0.5);
      const k = instant ? 1 : 1 - Math.exp(-dt * 10);
      this.focus.lerp(target, k);
      // otomatik kamera: oyuncu kamerayla oynamiyorsa arkaya gec
      const hs = Math.hypot(player.vel.x, player.vel.z);
      if (settings.get('autoCamera') && this.idleLook > 1.2 && hs > 2 && !instant) {
        const behind = player.yaw + Math.PI;
        let d = behind - this.yaw;
        d = Math.atan2(Math.sin(d), Math.cos(d));
        this.yaw += d * Math.min(1, dt * 0.9) * Math.min(1, hs / 8);
      }
      const speedFov = player.gliding ? 8 : player.vel.length() > 9 ? 4 : 0;
      this.fovBoost += (speedFov - this.fovBoost) * Math.min(1, dt * 2);
    }

    this.distance += (this.targetDistance - this.distance) * Math.min(1, dt * 6 || 1);
    const dir = new THREE.Vector3(
      Math.sin(this.yaw) * Math.cos(this.pitch),
      Math.sin(this.pitch),
      Math.cos(this.yaw) * Math.cos(this.pitch),
    );
    let dist = this.distance;
    const ph = this.game.physics;
    if (ph && player?.char) {
      const hit = ph.raycast(this.focus, dir, dist + 0.3, player.char.collider);
      if (hit !== null) dist = Math.max(1.2, hit - 0.35);
    }
    const pos = this.focus.clone().addScaledVector(dir, dist);
    const T = this.game.world?.terrain;
    if (T) {
      const gh = T.height(pos.x, pos.z) + 0.5;
      if (pos.y < gh) pos.y = gh;
      const wl = T.waterLevel(pos.x, pos.z) + 0.35;
      if (pos.y < wl) pos.y = wl;
    }
    if (this.shake > 0 && settings.get('cameraShake')) {
      this.shake = Math.max(0, this.shake - dt * 3);
      pos.x += (Math.random() - 0.5) * this.shake * 0.3;
      pos.y += (Math.random() - 0.5) * this.shake * 0.3;
    }
    this.camera.position.copy(pos);
    this.camera.lookAt(this.focus);
    const fov = 60 + this.fovBoost;
    if (Math.abs(this.camera.fov - fov) > 0.01) {
      this.camera.fov = fov;
      this.camera.updateProjectionMatrix();
    }
  }
}
