// Adalilar. Her biri ayni sozlesmeye uyar: root (ayak hizasinda), head
// (oyuncuya bakmak icin pivot), eyes (goz kirpma), update(dt). Turlerin
// kendine has bir "huyu" var: baykus kanat cirpar, kurbagenin bogazi sisar,
// tavsanin kulagi seyirir, kunduz kuyrugunu vurur...

import * as THREE from 'three';
import { P } from './palette.js';
import { blob, sphere, cyl, cone, box, merge, mesh, vcMaterial, part, pivot } from './geom.js';
import { makeEyes } from './Fox.js';

const mat = () => vcMaterial({ flat: false, roughness: 0.7 });

function m(parts) {
  return mesh(merge(parts), { material: mat() });
}

function bigEyes(spacing, y, z, r, iris = P.eye) {
  const eyes = new THREE.Group();
  eyes.position.set(0, y, z);
  const parts = [];
  for (const s of [-1, 1]) {
    parts.push(sphere('#ffffff', r, { pos: [s * spacing, 0, 0], scale: [1, 1, 0.7] }, 12, 10));
    parts.push(sphere(iris, r * 0.62, { pos: [s * spacing, 0, r * 0.45], scale: [1, 1.1, 0.6] }, 10, 8));
    parts.push(sphere('#ffffff', r * 0.2, { pos: [s * spacing - r * 0.2, r * 0.25, r * 0.78] }, 6, 4));
  }
  eyes.add(m(parts));
  return eyes;
}

export class AnimalModel {
  constructor(kind) {
    this.kind = kind;
    this.root = new THREE.Group();
    this.body = new THREE.Group();
    this.root.add(this.body);
    this.head = new THREE.Group();
    this.eyes = null;
    this.extra = {};
    this.t = Math.random() * 10;
    this.blink = 1 + Math.random() * 3;
    this.quirk = 3 + Math.random() * 4;
    this.quirkT = 0;
    this.lookYaw = 0;
    this.lookTarget = null;
    this.talking = 0;
    BUILDERS[kind](this);
    this.root.traverse((o) => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  }

  update(dt) {
    this.t += dt;
    const t = this.t;
    this.blink -= dt;
    if (this.blink < 0) this.blink = 2 + Math.random() * 4;
    if (this.eyes) this.eyes.scale.y = this.blink < 0.12 ? 0.1 : 1;

    this.quirk -= dt;
    if (this.quirk < 0) {
      this.quirk = 4 + Math.random() * 5;
      this.quirkT = 1;
    }
    this.quirkT = Math.max(0, this.quirkT - dt * 1.2);

    const breathe = Math.sin(t * 2) * 0.015;
    this.body.scale.set(1 + breathe, 1 - breathe * 0.6 + this.talking * Math.abs(Math.sin(t * 14)) * 0.025, 1 + breathe);

    let target = 0;
    let pitch = 0;
    if (this.lookTarget) {
      const local = this.root.worldToLocal(this.lookTarget.clone());
      target = THREE.MathUtils.clamp(Math.atan2(local.x, local.z), -1.1, 1.1);
      pitch = THREE.MathUtils.clamp(-Math.atan2(local.y - 0.6, Math.hypot(local.x, local.z)) * 0.5, -0.3, 0.3);
    }
    this.lookYaw += (target - this.lookYaw) * Math.min(1, dt * 4);
    this.head.rotation.y = this.lookYaw;
    this.head.rotation.x += (pitch - this.head.rotation.x) * Math.min(1, dt * 4);

    QUIRKS[this.kind]?.(this, dt, t, this.quirkT);
  }
}

const BUILDERS = {
  owl(a) {
    a.body.add(m([
      blob(P.owl, 0.36, 0.44, 0.34, 2).translate(0, 0.5, 0),
      blob(P.owlLight, 0.26, 0.32, 0.2, 2).translate(0, 0.45, 0.16),
      // gogus tuyleri
      ...[-0.1, 0, 0.1].map((x, i) => cone(P.owlDark, 0.035, 0.06, { pos: [x, 0.5 - i % 2 * 0.08, 0.34], rot: [Math.PI, 0, 0] }, 4)),
      sphere(P.beak, 0.06, { pos: [-0.1, 0.06, 0.08], scale: [1, 0.5, 1.4] }, 8, 6),
      sphere(P.beak, 0.06, { pos: [0.1, 0.06, 0.08], scale: [1, 0.5, 1.4] }, 8, 6),
    ]));
    a.wings = [];
    for (const s of [-1, 1]) {
      const w = pivot(s * 0.32, 0.7, 0);
      w.add(m([blob(P.owlDark, 0.1, 0.32, 0.24, 1).translate(s * 0.04, -0.22, -0.02)]));
      a.body.add(w);
      a.wings.push(w);
    }
    a.head.position.set(0, 0.98, 0);
    a.head.add(m([
      sphere(P.owl, 0.3, { scale: [1.05, 0.9, 0.95] }, 16, 12),
      sphere(P.owlLight, 0.13, { pos: [-0.12, 0, 0.2], scale: [1, 1, 0.5] }, 12, 8),
      sphere(P.owlLight, 0.13, { pos: [0.12, 0, 0.2], scale: [1, 1, 0.5] }, 12, 8),
      cone(P.owl, 0.07, 0.18, { pos: [-0.2, 0.26, 0], rot: [0, 0, 0.4] }, 5),
      cone(P.owl, 0.07, 0.18, { pos: [0.2, 0.26, 0], rot: [0, 0, -0.4] }, 5),
      cone(P.beak, 0.045, 0.12, { pos: [0, -0.07, 0.28], rot: [Math.PI / 2 + 0.5, 0, 0] }, 6),
      // yuvarlak gozluk
      part(new THREE.TorusGeometry(0.1, 0.012, 6, 16), P.metal, { pos: [-0.12, 0.01, 0.29] }),
      part(new THREE.TorusGeometry(0.1, 0.012, 6, 16), P.metal, { pos: [0.12, 0.01, 0.29] }),
      box(P.metal, 0.05, 0.015, 0.015, { pos: [0, 0.03, 0.3] }),
    ]));
    a.eyes = bigEyes(0.12, 0.01, 0.24, 0.07, '#3a2a1a');
    a.head.add(a.eyes);
    a.body.add(a.head);
  },

  hedgehog(a) {
    const spikes = [];
    for (let i = 0; i < 46; i++) {
      const u = (i * 0.618) % 1;
      const v = (i / 46);
      const theta = u * Math.PI * 2;
      const phi = 0.15 + v * 1.25;
      const dir = new THREE.Vector3(Math.sin(phi) * Math.cos(theta), Math.cos(phi), Math.sin(phi) * Math.sin(theta));
      if (dir.z > 0.45) continue; // yuz tarafi acik kalsin
      const pos = dir.clone().multiplyScalar(0.36).add(new THREE.Vector3(0, 0.4, -0.03));
      const g = cone(P.hedgehogSpikes, 0.05, 0.2, {}, 4);
      const q = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir);
      g.applyQuaternion(q);
      g.translate(pos.x, pos.y, pos.z);
      spikes.push(g);
    }
    a.body.add(m([
      blob(P.hedgehog, 0.36, 0.38, 0.36, 2).translate(0, 0.4, 0),
      blob(P.hedgehogFace, 0.27, 0.27, 0.2, 2).translate(0, 0.36, 0.18),
      // onluk
      box(P.apron, 0.34, 0.26, 0.05, { pos: [0, 0.24, 0.3], rot: [-0.2, 0, 0] }),
      sphere('#2a1d1c', 0.06, { pos: [-0.12, 0.04, 0.12], scale: [1, 0.5, 1.4] }, 8, 6),
      sphere('#2a1d1c', 0.06, { pos: [0.12, 0.04, 0.12], scale: [1, 0.5, 1.4] }, 8, 6),
      ...spikes,
    ]));
    a.head.position.set(0, 0.5, 0.26);
    a.head.add(m([
      cone(P.hedgehogFace, 0.1, 0.2, { pos: [0, -0.04, 0.1], rot: [Math.PI / 2, 0, 0] }, 8),
      sphere('#2a1d1c', 0.04, { pos: [0, -0.04, 0.21] }, 8, 6),
      sphere(P.blush, 0.035, { pos: [-0.14, -0.06, 0.06], scale: [1, 0.6, 0.4] }, 6, 4),
      sphere(P.blush, 0.035, { pos: [0.14, -0.06, 0.06], scale: [1, 0.6, 0.4] }, 6, 4),
      sphere('#c2a38a', 0.06, { pos: [-0.17, 0.12, -0.05] }, 6, 4),
      sphere('#c2a38a', 0.06, { pos: [0.17, 0.12, -0.05] }, 6, 4),
    ]));
    a.eyes = makeEyes(0.085, 0.05, 0.05, 0.035);
    a.head.add(a.eyes);
    a.body.add(a.head);
  },

  frog(a) {
    a.body.add(m([
      blob(P.frog, 0.36, 0.26, 0.32, 2).translate(0, 0.27, 0),
      blob(P.frogLight, 0.26, 0.17, 0.2, 2).translate(0, 0.21, 0.14),
      blob(P.frogDark, 0.14, 0.1, 0.24, 1).translate(-0.28, 0.12, -0.04),
      blob(P.frogDark, 0.14, 0.1, 0.24, 1).translate(0.28, 0.12, -0.04),
      blob(P.frog, 0.07, 0.14, 0.07, 1).translate(-0.18, 0.12, 0.24),
      blob(P.frog, 0.07, 0.14, 0.07, 1).translate(0.18, 0.12, 0.24),
      ...[[-0.12, 0.42, -0.1], [0.15, 0.4, -0.18], [0.02, 0.46, -0.22]].map((p) => sphere(P.frogDark, 0.04, { pos: p, scale: [1, 0.4, 1] }, 6, 4)),
    ]));
    a.throat = new THREE.Group();
    a.throat.position.set(0, 0.2, 0.26);
    a.throat.add(m([sphere(P.frogLight, 0.1, { scale: [1, 0.7, 0.6] }, 10, 8)]));
    a.throat.scale.setScalar(0.4);
    a.body.add(a.throat);
    a.head.position.set(0, 0.4, 0.1);
    a.head.add(m([
      sphere(P.frog, 0.12, { pos: [-0.14, 0.06, 0], scale: [1, 1, 0.9] }, 10, 8),
      sphere(P.frog, 0.12, { pos: [0.14, 0.06, 0], scale: [1, 1, 0.9] }, 10, 8),
      part(new THREE.TorusGeometry(0.13, 0.012, 4, 14, Math.PI * 0.8), '#2e5e24', { pos: [0, -0.04, 0.2], rot: [0.2, 0, Math.PI + Math.PI * 0.1] }),
      // yaris bandi
      part(new THREE.TorusGeometry(0.21, 0.03, 6, 18), '#ff5a5a', { pos: [0, 0.02, -0.02], rot: [Math.PI / 2, 0, 0], scale: [1.25, 1, 0.8] }),
    ]));
    a.eyes = bigEyes(0.14, 0.1, 0.08, 0.075);
    a.head.add(a.eyes);
    a.body.add(a.head);
  },

  bear(a) {
    a.body.add(m([
      blob(P.bear, 0.55, 0.6, 0.5, 2).translate(0, 0.6, 0),
      blob(P.bearLight, 0.38, 0.42, 0.24, 2).translate(0, 0.55, 0.28),
      blob(P.bear, 0.16, 0.14, 0.24, 1).translate(-0.3, 0.13, 0.32),
      blob(P.bear, 0.16, 0.14, 0.24, 1).translate(0.3, 0.13, 0.32),
      blob(P.bear, 0.13, 0.3, 0.13, 1).rotateX(-0.5).translate(-0.48, 0.66, 0.2),
      blob(P.bear, 0.13, 0.3, 0.13, 1).rotateX(-0.5).translate(0.48, 0.66, 0.2),
    ]));
    a.head.position.set(0, 1.28, 0.06);
    a.head.add(m([
      sphere(P.bear, 0.36, { scale: [1, 0.9, 0.95] }, 16, 12),
      sphere(P.bear, 0.11, { pos: [-0.27, 0.27, -0.02] }, 8, 6),
      sphere(P.bear, 0.11, { pos: [0.27, 0.27, -0.02] }, 8, 6),
      sphere(P.bearLight, 0.06, { pos: [-0.27, 0.27, 0.05] }, 6, 4),
      sphere(P.bearLight, 0.06, { pos: [0.27, 0.27, 0.05] }, 6, 4),
      blob(P.bearLight, 0.16, 0.12, 0.14, 2).translate(0, -0.1, 0.3),
      sphere('#2a1d1c', 0.06, { pos: [0, -0.05, 0.43], scale: [1.2, 0.8, 0.8] }, 8, 6),
      // balikci sapkasi
      cyl('#7a8f4a', 0.42, 0.42, 0.03, { pos: [0, 0.24, -0.02] }, 18),
      cyl('#8ba35a', 0.27, 0.3, 0.16, { pos: [0, 0.33, -0.02] }, 16),
      box('#f2c94c', 0.05, 0.1, 0.02, { pos: [0.24, 0.32, 0.12], rot: [0, 0.6, 0.3] }),
    ]));
    a.eyes = makeEyes(0.13, 0.04, 0.3, 0.04);
    a.head.add(a.eyes);
    a.body.add(a.head);
  },

  rabbit(a) {
    a.body.add(m([
      blob(P.rabbit, 0.28, 0.32, 0.27, 2).translate(0, 0.33, 0),
      blob(P.rabbitGrey, 0.18, 0.2, 0.12, 2).translate(0, 0.3, 0.17),
      blob(P.rabbit, 0.12, 0.1, 0.2, 1).translate(-0.17, 0.08, 0.1),
      blob(P.rabbit, 0.12, 0.1, 0.2, 1).translate(0.17, 0.08, 0.1),
      sphere('#ffffff', 0.1, { pos: [0, 0.25, -0.27] }, 8, 6),
      // bahcivan atkisi
      part(new THREE.TorusGeometry(0.16, 0.04, 6, 14), '#f2c94c', { pos: [0, 0.58, 0.02], rot: [Math.PI / 2, 0, 0] }),
    ]));
    a.head.position.set(0, 0.78, 0.04);
    a.head.add(m([
      sphere(P.rabbit, 0.22, { scale: [1, 0.92, 0.95] }, 14, 10),
      blob(P.rabbit, 0.09, 0.07, 0.07, 2).translate(-0.07, -0.08, 0.17),
      blob(P.rabbit, 0.09, 0.07, 0.07, 2).translate(0.07, -0.08, 0.17),
      sphere(P.rabbitPink, 0.035, { pos: [0, -0.04, 0.21], scale: [1.2, 0.8, 0.8] }, 8, 6),
      sphere(P.blush, 0.035, { pos: [-0.14, -0.05, 0.13], scale: [1, 0.6, 0.4] }, 6, 4),
      sphere(P.blush, 0.035, { pos: [0.14, -0.05, 0.13], scale: [1, 0.6, 0.4] }, 6, 4),
    ]));
    a.ears = [];
    for (const s of [-1, 1]) {
      const e = pivot(s * 0.09, 0.15, -0.02);
      e.rotation.z = -s * 0.15;
      e.add(m([
        blob(P.rabbit, 0.06, 0.24, 0.035, 1).translate(0, 0.22, 0),
        blob(P.rabbitPink, 0.035, 0.18, 0.02, 1).translate(0, 0.22, 0.02),
      ]));
      a.head.add(e);
      a.ears.push(e);
    }
    a.eyes = makeEyes(0.085, 0.03, 0.17, 0.035);
    a.head.add(a.eyes);
    a.body.add(a.head);
  },

  beaver(a) {
    a.body.add(m([
      blob(P.beaver, 0.3, 0.36, 0.28, 2).translate(0, 0.38, 0),
      blob('#d8b08a', 0.2, 0.24, 0.13, 2).translate(0, 0.34, 0.18),
      blob(P.beaverDark, 0.12, 0.09, 0.18, 1).translate(-0.17, 0.08, 0.1),
      blob(P.beaverDark, 0.12, 0.09, 0.18, 1).translate(0.17, 0.08, 0.1),
      // alet kemeri
      part(new THREE.TorusGeometry(0.27, 0.035, 6, 16), '#6b4a2f', { pos: [0, 0.26, 0], rot: [Math.PI / 2, 0, 0], scale: [1.05, 1, 1] }),
      box('#c9c9c9', 0.05, 0.12, 0.03, { pos: [0.2, 0.2, 0.18], rot: [0, 0, 0.2] }),
    ]));
    a.tailPivot = pivot(0, 0.12, -0.25);
    a.tailPivot.add(m([
      blob(P.beaverTail, 0.16, 0.04, 0.26, 1).translate(0, 0, -0.22),
      box('#4a352a', 0.2, 0.01, 0.01, { pos: [0, 0.04, -0.15] }),
      box('#4a352a', 0.2, 0.01, 0.01, { pos: [0, 0.04, -0.25] }),
      box('#4a352a', 0.01, 0.01, 0.3, { pos: [0, 0.045, -0.2] }),
    ]));
    a.body.add(a.tailPivot);
    a.head.position.set(0, 0.84, 0.03);
    a.head.add(m([
      sphere(P.beaver, 0.23, { scale: [1, 0.92, 0.95] }, 14, 10),
      blob('#d8b08a', 0.11, 0.08, 0.08, 2).translate(0, -0.08, 0.17),
      sphere('#2a1d1c', 0.04, { pos: [0, -0.03, 0.24], scale: [1.3, 0.8, 0.8] }, 8, 6),
      box(P.tooth, 0.07, 0.07, 0.02, { pos: [0, -0.16, 0.22] }),
      box('#d9d0b0', 0.004, 0.07, 0.022, { pos: [0, -0.16, 0.222] }),
      sphere(P.beaverDark, 0.06, { pos: [-0.18, 0.15, -0.02] }, 6, 4),
      sphere(P.beaverDark, 0.06, { pos: [0.18, 0.15, -0.02] }, 6, 4),
      // baret
      sphere('#ffcc33', 0.24, { pos: [0, 0.07, -0.01], scale: [1, 0.75, 1] }, 14, 8),
      cyl('#ffcc33', 0.28, 0.28, 0.025, { pos: [0, 0.07, 0.03] }, 16),
      box('#e0a91f', 0.04, 0.05, 0.4, { pos: [0, 0.25, -0.01] }),
    ]));
    a.eyes = makeEyes(0.09, 0.03, 0.18, 0.035);
    a.head.add(a.eyes);
    a.body.add(a.head);
  },
};

const QUIRKS = {
  owl(a, dt, t, q) {
    const flap = q > 0 ? Math.sin(q * 30) * 0.6 * q : 0;
    a.wings[0].rotation.z = -0.1 - Math.abs(flap);
    a.wings[1].rotation.z = 0.1 + Math.abs(flap);
    a.head.rotation.z = Math.sin(t * 0.7) * 0.1;
  },
  hedgehog(a, dt, t, q) {
    a.body.position.y = Math.abs(Math.sin(q * 12)) * 0.06 * q;
  },
  frog(a, dt, t, q) {
    a.throat.scale.setScalar(0.4 + Math.max(0, Math.sin(t * 3)) * 0.5);
    a.body.position.y = q > 0 ? Math.abs(Math.sin(q * Math.PI)) * 0.25 : 0;
  },
  bear(a, dt, t) {
    a.body.rotation.z = Math.sin(t * 0.8) * 0.04;
  },
  rabbit(a, dt, t, q) {
    const tw = q > 0 ? Math.sin(q * 25) * 0.4 * q : 0;
    a.ears[0].rotation.z = 0.15 + tw;
    a.ears[1].rotation.z = -0.15 - Math.sin(t * 1.3) * 0.05;
    a.ears[0].rotation.x = a.ears[1].rotation.x = -0.1 + Math.sin(t * 1.1) * 0.06;
  },
  beaver(a, dt, t, q) {
    a.tailPivot.rotation.x = q > 0 ? -Math.abs(Math.sin(q * 18)) * 0.6 * q : Math.sin(t * 1.5) * 0.05;
  },
};

export const NPC_KINDS = Object.keys(BUILDERS);
