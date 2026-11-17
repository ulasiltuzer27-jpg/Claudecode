import * as THREE from 'three';
import { clamp } from '../world/Noise';

/**
 * Omuz üstü 3. şahıs kamera. Fareyle döner, tekerlekle yaklaşır.
 * Arazinin içine girmez; titreme (shake) vuruş hissi için.
 */
export class ThirdPersonCamera {
  yaw = 0;
  pitch = 0.32;
  distance = 7.5;
  sensitivity = 1;
  invertY = false;
  private target = new THREE.Vector3();
  private shakeAmt = 0;
  private shakeT = 0;
  /** Kamera başlangıçta hedefe "ışınlansın" (ilk kare yumuşatma olmasın). */
  private snapped = false;

  constructor(readonly camera: THREE.PerspectiveCamera) {}

  addMouse(dx: number, dy: number, wheel: number): void {
    const k = 0.0024 * this.sensitivity;
    this.yaw -= dx * k;
    this.pitch = clamp(this.pitch + dy * k * (this.invertY ? -1 : 1), -0.35, 1.2);
    this.distance = clamp(this.distance + wheel * 0.8, 3.5, 15);
  }

  shake(a: number): void {
    this.shakeAmt = Math.min(0.6, this.shakeAmt + a);
  }

  snap(): void {
    this.snapped = false;
  }

  update(dt: number, focus: THREE.Vector3, groundY: (x: number, z: number) => number): void {
    const want = focus.clone().add(new THREE.Vector3(0, 2.1, 0));
    if (!this.snapped) { this.target.copy(want); this.snapped = true; }
    this.target.lerp(want, Math.min(1, dt * 12));

    const cp = Math.cos(this.pitch);
    const off = new THREE.Vector3(Math.sin(this.yaw) * cp, Math.sin(this.pitch), Math.cos(this.yaw) * cp);
    // Omuz kayması: karakter ekranın biraz solunda, nişangah açık kalsın.
    const right = new THREE.Vector3(Math.cos(this.yaw), 0, -Math.sin(this.yaw));
    let dist = this.distance;
    const pos = new THREE.Vector3();
    // Kamera araziye gömülecekse mesafeyi kısalt.
    for (let i = 0; i < 6; i++) {
      pos.copy(this.target).addScaledVector(off, dist).addScaledVector(right, 0.7);
      if (pos.y > groundY(pos.x, pos.z) + 0.5) break;
      dist *= 0.75;
    }
    pos.y = Math.max(pos.y, groundY(pos.x, pos.z) + 0.5);

    this.shakeT += dt * 40;
    if (this.shakeAmt > 0.001) {
      pos.x += Math.sin(this.shakeT * 1.3) * this.shakeAmt * 0.3;
      pos.y += Math.sin(this.shakeT * 1.7) * this.shakeAmt * 0.3;
      this.shakeAmt *= Math.max(0, 1 - dt * 6);
    }
    this.camera.position.copy(pos);
    this.camera.lookAt(this.target.clone().addScaledVector(right, 0.7));
  }
}
