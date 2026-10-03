// Adanin etrafinda yavasca suzulen low-poly bulutlar.
import * as THREE from 'three';
import { cloudGeo } from '../models/Nature.js';
import { mulberry32 } from '../world/noise.js';

export class Clouds {
  constructor(scene, count = 22) {
    this.group = new THREE.Group();
    this.material = new THREE.MeshLambertMaterial({ color: 0xffffff, emissive: 0xffffff, emissiveIntensity: 0.35, flatShading: true, transparent: true, opacity: 0.95 });
    const rng = mulberry32(777);
    this.items = [];
    for (let i = 0; i < count; i++) {
      const m = new THREE.Mesh(cloudGeo(i + 1), this.material);
      const s = 1 + rng() * 1.4;
      m.scale.set(s, s * (0.8 + rng() * 0.4), s);
      const item = { mesh: m, angle: rng() * Math.PI * 2, radius: 150 + rng() * 260, y: 75 + rng() * 45, speed: 0.004 + rng() * 0.006 };
      this.items.push(item);
      this.group.add(m);
    }
    scene.add(this.group);
    this._c = new THREE.Color();
  }

  update(dt, sky) {
    for (const it of this.items) {
      it.angle += it.speed * dt;
      it.mesh.position.set(Math.cos(it.angle) * it.radius, it.y, Math.sin(it.angle) * it.radius);
      it.mesh.rotation.y = -it.angle;
    }
    // bulut rengi gokyuzunu izler: gun batiminda pembe, gece lacivert
    this._c.copy(sky.horizon).lerp(new THREE.Color(1, 1, 1), 0.55 * (1 - sky.night));
    this.material.emissive.copy(this._c);
    this.material.emissiveIntensity = THREE.MathUtils.lerp(0.45, 0.15, sky.night);
    this.material.color.setScalar(THREE.MathUtils.lerp(1, 0.35, sky.night));
  }
}
