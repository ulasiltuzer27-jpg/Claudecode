import * as THREE from 'three';
import { Sky as SkyShader } from 'three/examples/jsm/objects/Sky.js';
import { clamp, lerp, smoothstep } from './Noise';

/**
 * Gökyüzü + güneş + gündüz/gece döngüsü.
 * timeOfDay: 0..1 (0.25 sabah, 0.5 öğle, 0.75 akşam, 0 gece yarısı).
 * Gece tam karanlık değil: oynanabilirlik için mavi bir ay ışığı kalıyor.
 */
export class SkySystem {
  readonly sky = new SkyShader();
  readonly sun = new THREE.DirectionalLight(0xffffff, 2.2);
  readonly hemi = new THREE.HemisphereLight(0xbfd9ff, 0x4a5a2a, 0.6);
  readonly ambient = new THREE.AmbientLight(0xffffff, 0.06);
  timeOfDay = 0.38;
  /** Gerçek saniye başına gün kesri: bir gün = 12 dakika. */
  daySpeed = 1 / 720;
  private sunDir = new THREE.Vector3();
  private fogDay = new THREE.Color(0xb8d4e8);
  private fogDusk = new THREE.Color(0xd8a080);
  private fogNight = new THREE.Color(0x1a2438);

  constructor(private scene: THREE.Scene, shadowSize: number, fogFar = 360) {
    this.sky.scale.setScalar(4000);
    const u = this.sky.material.uniforms;
    u.turbidity.value = 6;
    u.rayleigh.value = 1.6;
    u.mieCoefficient.value = 0.004;
    u.mieDirectionalG.value = 0.8;
    scene.add(this.sky, this.hemi, this.ambient, this.sun, this.sun.target);

    this.sun.castShadow = shadowSize > 0;
    if (shadowSize > 0) {
      this.sun.shadow.mapSize.set(shadowSize, shadowSize);
      const cam = this.sun.shadow.camera;
      cam.left = -45; cam.right = 45; cam.top = 45; cam.bottom = -45;
      cam.near = 1; cam.far = 260;
      this.sun.shadow.bias = -0.0004;
      this.sun.shadow.normalBias = 0.04;
    }
    scene.fog = new THREE.Fog(this.fogDay.clone(), fogFar * 0.25, fogFar);
  }

  /** 0 = gece, 1 = gündüz (güneş yüksekliğinden). */
  get daylight(): number {
    return smoothstep(-0.08, 0.25, this.sunDir.y);
  }

  update(dt: number, focus: THREE.Vector3): void {
    this.timeOfDay = (this.timeOfDay + dt * this.daySpeed) % 1;
    const ang = (this.timeOfDay - 0.25) * Math.PI * 2; // 0.25'te doğuda ufuk
    this.sunDir.set(Math.cos(ang) * 0.8, Math.sin(ang), 0.45).normalize();

    const day = this.daylight;
    const dusk = clamp(1 - Math.abs(this.sunDir.y - 0.08) / 0.22, 0, 1);
    this.sky.material.uniforms.sunPosition.value.copy(this.sunDir);
    // Gece: güneş ufkun altında, ışığı "ay" olarak ters yönden ver.
    const lightDir = this.sunDir.y > -0.05 ? this.sunDir : this.sunDir.clone().multiplyScalar(-1).setY(0.6).normalize();
    this.sun.position.copy(focus).addScaledVector(lightDir, 120);
    this.sun.target.position.copy(focus);
    this.sun.intensity = lerp(0.35, 2.3, day);
    this.sun.color.setRGB(lerp(0.55, 1, day), lerp(0.65, 0.96 - dusk * 0.15, day), lerp(1, 0.88 - dusk * 0.25, day));
    this.hemi.intensity = lerp(0.3, 0.65, day);
    this.hemi.color.setRGB(lerp(0.3, 0.75, day), lerp(0.38, 0.85, day), lerp(0.6, 1, day));

    const fog = this.scene.fog as THREE.Fog;
    fog.color.copy(this.fogNight).lerp(this.fogDay, day);
    if (dusk > 0) fog.color.lerp(this.fogDusk, dusk * 0.45);
    this.scene.environmentIntensity = lerp(0.12, 0.3, day);
    // Gece gökyüzü shader'ı simsiyah; sisle aynı renge yaklaştır.
    this.sky.material.uniforms.rayleigh.value = lerp(0.3, 1.6, day);
  }
}
