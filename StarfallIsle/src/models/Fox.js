// Mina: oyuncu karakteri. Hiyerarsik ilkel parcalardan kurulur, animasyonu
// tamamen prosedureldir (iskelet/kemik yok): bacaklar, kuyruk, kulaklar ve
// govde pivot gruplari uzerinden dondurulur.

import * as THREE from 'three';
import { P } from './palette.js';
import { blob, sphere, cyl, cone, box, merge, mesh, vcMaterial, part, pivot } from './geom.js';

const charMat = () => vcMaterial({ flat: false, roughness: 0.65 });

export function makeEyes(spacing, y, z, r = 0.045, color = P.eye) {
  const eyes = new THREE.Group();
  eyes.position.set(0, y, z);
  const parts = [];
  for (const s of [-1, 1]) {
    parts.push(sphere(color, r, { pos: [s * spacing, 0, 0], scale: [1, 1.3, 0.6] }, 10, 8));
    parts.push(sphere(P.eyeShine, r * 0.32, { pos: [s * spacing - r * 0.3, r * 0.45, r * 0.45] }, 6, 4));
  }
  eyes.add(mesh(merge(parts), { material: charMat(), castShadow: false }));
  return eyes;
}

export class FoxModel {
  constructor() {
    this.root = new THREE.Group();
    this.body = new THREE.Group();
    this.body.position.y = 0.36;
    this.root.add(this.body);
    const mat = charMat();

    // govde
    const torso = merge([
      blob(P.foxOrange, 0.24, 0.22, 0.32, 2),
      blob(P.foxCream, 0.18, 0.15, 0.24, 2).translate(0, -0.07, 0.05),
    ]);
    this.body.add(mesh(torso, { material: mat }));

    // atki
    const scarf = merge([
      part(new THREE.TorusGeometry(0.17, 0.05, 6, 14), P.scarf, { pos: [0, 0.12, 0.2], rot: [Math.PI / 2 - 0.35, 0, 0] }),
    ]);
    this.body.add(mesh(scarf, { material: mat }));
    this.scarfTail = pivot(0.1, 0.12, 0.12);
    this.scarfTail.add(mesh(merge([
      box(P.scarfDark, 0.07, 0.03, 0.2, { pos: [0, 0, -0.1] }),
    ]), { material: mat }));
    this.body.add(this.scarfTail);

    // bas
    this.head = pivot(0, 0.27, 0.27);
    const headGeo = merge([
      sphere(P.foxOrange, 0.24, { scale: [1, 0.92, 0.95] }, 16, 12),
      blob(P.foxCream, 0.12, 0.085, 0.1, 2).translate(-0.11, -0.08, 0.12),
      blob(P.foxCream, 0.12, 0.085, 0.1, 2).translate(0.11, -0.08, 0.12),
      cone(P.foxCream, 0.095, 0.2, { pos: [0, -0.06, 0.24], rot: [Math.PI / 2, 0, 0] }, 10),
      sphere(P.nose, 0.036, { pos: [0, -0.045, 0.335] }, 8, 6),
      sphere(P.blush, 0.035, { pos: [-0.155, -0.04, 0.14], scale: [1, 0.6, 0.4] }, 8, 6),
      sphere(P.blush, 0.035, { pos: [0.155, -0.04, 0.14], scale: [1, 0.6, 0.4] }, 8, 6),
      blob(P.foxOrangeDark, 0.08, 0.05, 0.08, 1).translate(0, 0.2, 0.06),
    ]);
    this.head.add(mesh(headGeo, { material: mat }));
    this.eyes = makeEyes(0.095, 0.035, 0.195);
    this.head.add(this.eyes);

    this.ears = [];
    for (const s of [-1, 1]) {
      const ear = pivot(s * 0.13, 0.16, -0.02);
      ear.rotation.z = -s * 0.32;
      ear.add(mesh(merge([
        cone(P.foxOrange, 0.09, 0.24, { pos: [0, 0.11, 0] }, 6),
        cone(P.foxCream, 0.05, 0.14, { pos: [0, 0.08, 0.035] }, 6),
        cone(P.foxDark, 0.045, 0.08, { pos: [0, 0.2, 0] }, 6),
      ]), { material: mat }));
      this.head.add(ear);
      this.ears.push(ear);
    }
    this.body.add(this.head);

    // kuyruk
    this.tail = pivot(0, 0.04, -0.28);
    this.tailInner = new THREE.Group();
    this.tail.add(this.tailInner);
    this.tailInner.add(mesh(merge([
      blob(P.foxOrange, 0.13, 0.13, 0.24, 2).rotateX(-0.7).translate(0, 0.1, -0.16),
      blob(P.foxCream, 0.1, 0.1, 0.13, 2).rotateX(-0.9).translate(0, 0.25, -0.33),
    ]), { material: mat }));
    this.body.add(this.tail);

    // bacaklar
    this.legs = [];
    const legGeo = () => merge([
      cyl(P.foxDark, 0.05, 0.045, 0.2, { pos: [0, -0.1, 0] }, 7),
      sphere(P.foxDark, 0.06, { pos: [0, -0.21, 0.02], scale: [1, 0.7, 1.2] }, 8, 6),
    ]);
    for (const [x, z, phase] of [[-0.12, 0.17, 0], [0.12, 0.17, Math.PI], [-0.12, -0.15, Math.PI], [0.12, -0.15, 0]]) {
      const leg = pivot(x, -0.12, z, mesh(legGeo(), { material: mat }));
      leg.userData.phase = phase;
      this.body.add(leg);
      this.legs.push(leg);
    }

    // yaprak planor
    this.glider = pivot(0, 0.2, 0.05);
    const leafShape = new THREE.Shape();
    leafShape.moveTo(0, -0.55);
    leafShape.quadraticCurveTo(0.55, -0.1, 0, 0.6);
    leafShape.quadraticCurveTo(-0.55, -0.1, 0, -0.55);
    const leafGeo = new THREE.ExtrudeGeometry(leafShape, { depth: 0.02, bevelEnabled: false });
    this.glider.add(mesh(merge([
      cyl(P.leafDark, 0.015, 0.015, 0.55, { pos: [0, 0.27, 0] }, 5),
      part(leafGeo, P.leaf, { pos: [0, 0.56, 0], rot: [-Math.PI / 2, 0, 0], scale: [1.1, 1, 1] }),
      box(P.leafDark, 0.03, 0.03, 1.05, { pos: [0, 0.585, 0] }),
    ]), { material: vcMaterial({ flat: true, roughness: 0.6 }) }));
    this.glider.visible = false;
    this.glider.scale.setScalar(0.01);
    this.body.add(this.glider);

    // hasir sapka (kozmetik)
    this.hat = pivot(0, 0.2, 0);
    this.hat.add(mesh(merge([
      cyl('#f2d58a', 0.3, 0.3, 0.025, {}, 16),
      cyl('#f2d58a', 0.15, 0.17, 0.13, { pos: [0, 0.07, 0] }, 14),
      cyl('#e2584b', 0.172, 0.172, 0.04, { pos: [0, 0.03, 0] }, 14),
      sphere('#fff3a8', 0.04, { pos: [0.16, 0.05, 0.06] }, 8, 6),
      sphere('#ff8fb1', 0.03, { pos: [0.19, 0.06, 0.02] }, 6, 4),
    ]), { material: mat }));
    this.hat.rotation.x = -0.12;
    this.hat.visible = false;
    this.head.add(this.hat);

    this.root.traverse((o) => { if (o.isMesh) o.castShadow = true; });

    this.t = 0;
    this.walkPhase = 0;
    this.blinkTimer = 2;
    this.earTimer = 3;
    this.squash = 0;       // + = ezilme, - = uzama
    this.squashVel = 0;
    this.flip = 0;         // kanat cirpma taklasi [0..1]
    this.gliderAmt = 0;
    this.lookYaw = 0;
    this.lookTarget = null;
  }

  setHat(on) {
    this.hat.visible = on;
  }

  land(impact) {
    this.squashVel -= Math.min(6, impact * 0.35);
  }

  jump() {
    this.squashVel += 3.5;
  }

  doFlip() {
    this.flip = 1;
  }

  // s: { speed (0..~1.5), grounded, vy, gliding, swimming, dt }
  update(dt, s) {
    this.t += dt;
    const t = this.t;
    const speed = s.speed;

    // esneme-ezilme yayi
    this.squashVel += (-this.squash * 90 - this.squashVel * 11) * dt;
    this.squash += this.squashVel * dt;
    const sq = this.squash;
    this.body.scale.set(1 + sq * 0.5, 1 - sq, 1 + sq * 0.5);

    // yuruyus dongusu
    const moving = s.grounded && speed > 0.05;
    this.walkPhase += dt * (6 + speed * 9) * (moving ? 1 : 0.0);
    const ph = this.walkPhase;
    const amp = moving ? Math.min(1, speed) * 0.9 : 0;

    let bodyY = 0.36;
    let bodyPitch = 0;
    let bodyRoll = 0;
    if (s.swimming) {
      bodyY = 0.2 + Math.sin(t * 3) * 0.02;
      bodyPitch = -0.25;
      for (const leg of this.legs) leg.rotation.x = Math.sin(t * 12 + leg.userData.phase) * 0.9;
    } else if (!s.grounded) {
      const tuck = s.gliding ? 0.2 : THREE.MathUtils.clamp(-s.vy * 0.05, -0.6, 0.6);
      for (const [i, leg] of this.legs.entries()) {
        const front = i < 2;
        leg.rotation.x = s.gliding ? (front ? -0.9 : 0.9) : front ? -0.6 - tuck : 0.6 + tuck;
      }
      bodyPitch = s.gliding ? 0.15 : THREE.MathUtils.clamp(-s.vy * 0.02, -0.25, 0.25);
    } else {
      for (const leg of this.legs) {
        leg.rotation.x = Math.sin(ph + leg.userData.phase) * amp;
      }
      bodyY += Math.abs(Math.sin(ph)) * 0.04 * amp;
      bodyPitch = 0.06 * Math.min(1.4, speed);
      bodyRoll = Math.sin(ph) * 0.04 * amp;
    }

    // takla (kanat cirpma)
    if (this.flip > 0) {
      this.flip = Math.max(0, this.flip - dt / 0.38);
      const k = 1 - this.flip;
      bodyPitch += -k * Math.PI * 2;
    }

    // nefes alma
    const breathe = Math.sin(t * 2.2) * 0.012;
    this.body.position.y = bodyY + breathe;
    this.body.rotation.x = bodyPitch;
    this.body.rotation.z = bodyRoll;

    // kuyruk: rahatken yavas sallanir, kosarken arkada savrulur
    const wag = moving ? Math.sin(ph * 0.5) * 0.25 : Math.sin(t * 2.4) * 0.45;
    this.tail.rotation.y = wag;
    this.tail.rotation.x = s.grounded ? -0.1 + speed * 0.35 : s.gliding ? 0.6 : 0.2;

    // atki ucu
    this.scarfTail.rotation.x = 0.3 + (s.grounded ? speed * 0.6 : 1.1) + Math.sin(t * 9) * 0.08 * (0.3 + speed);
    this.scarfTail.rotation.y = Math.sin(t * 5) * 0.2;

    // goz kirpma
    this.blinkTimer -= dt;
    if (this.blinkTimer < 0) this.blinkTimer = 2 + Math.random() * 3.5;
    this.eyes.scale.y = this.blinkTimer < 0.12 ? 0.12 : 1;

    // kulak oynatma
    this.earTimer -= dt;
    if (this.earTimer < 0) this.earTimer = 2 + Math.random() * 4;
    const twitch = this.earTimer < 0.2 ? Math.sin(this.earTimer * 40) * 0.3 : 0;
    this.ears[0].rotation.z = 0.32 + twitch + (s.grounded ? 0 : 0.35);
    this.ears[1].rotation.z = -0.32 - twitch * 0.5 - (s.grounded ? 0 : 0.35);
    this.ears[0].rotation.x = this.ears[1].rotation.x = s.grounded ? -speed * 0.25 : -0.5;

    // basla bakma (NPC ile konusurken)
    let targetYaw = 0;
    if (this.lookTarget) {
      const local = this.root.worldToLocal(this.lookTarget.clone());
      targetYaw = THREE.MathUtils.clamp(Math.atan2(local.x, local.z), -0.9, 0.9);
    }
    this.lookYaw += (targetYaw - this.lookYaw) * Math.min(1, dt * 6);
    this.head.rotation.y = this.lookYaw;
    this.head.rotation.x = s.swimming ? 0.25 : 0;

    // planor
    const gTarget = s.gliding ? 1 : 0;
    this.gliderAmt += (gTarget - this.gliderAmt) * Math.min(1, dt * 12);
    this.glider.visible = this.gliderAmt > 0.02;
    this.glider.scale.setScalar(Math.max(0.01, this.gliderAmt));
    this.glider.rotation.z = Math.sin(t * 3) * 0.08;
  }
}
