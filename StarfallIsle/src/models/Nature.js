// Doga modelleri: agaclar, kayalar, calilar, cicekler, mantarlar.
// Hepsi tek geometri dondurur; sahnede InstancedMesh ile yuzlerce kez
// cizilir (tek draw call).

import * as THREE from 'three';
import { P } from './palette.js';
import { part, merge, jitter, faceTint, cyl, cone, sphere, box, prep, gradientY } from './geom.js';
import { mulberry32 } from '../world/noise.js';

function canopyBall(color, r, pos, seed, detail = 1) {
  const g = new THREE.IcosahedronGeometry(r, detail);
  const p = prep(g, color);
  jitter(p, r * 0.28, seed);
  p.translate(...pos);
  return p;
}

function trunk(color, rTop, rBottom, h, seed, lean = 0) {
  const g = part(new THREE.CylinderGeometry(rTop, rBottom, h, 7, 2), color, { pos: [0, h / 2, 0], rot: [0, 0, lean] });
  gradientY(g, P.barkDark, color, 0, h * 0.6);
  return g;
}

export function roundTree(seed = 1, blossom = false) {
  const c1 = blossom ? P.blossom : P.canopy;
  const c2 = blossom ? P.blossomLight : P.canopyLight;
  const g = merge([
    trunk(P.bark, 0.18, 0.3, 2.6, seed),
    part(new THREE.CylinderGeometry(0.06, 0.1, 1.1, 5), P.bark, { pos: [0.45, 2.2, 0], rot: [0, 0, -0.9] }),
    canopyBall(c1, 1.5, [0, 3.4, 0], seed),
    canopyBall(c2, 1.05, [0.9, 3.0, 0.4], seed + 1),
    canopyBall(c1, 1.1, [-0.8, 3.1, -0.4], seed + 2),
    canopyBall(c2, 0.95, [0.1, 4.3, 0.2], seed + 3),
  ]);
  return faceTint(g, 0.12, seed);
}

export function pineTree(seed = 1) {
  const parts = [trunk(P.barkDark, 0.12, 0.25, 1.6, seed)];
  const tiers = 4;
  for (let i = 0; i < tiers; i++) {
    const r = 1.55 - i * 0.32;
    const h = 1.5 - i * 0.12;
    const y = 1.2 + i * 0.95;
    const c = part(new THREE.ConeGeometry(r, h, 8, 1), i % 2 ? P.pineLight : P.pine, { pos: [0, y + h / 2, 0], rot: [0, i * 0.4, 0] });
    jitter(c, 0.18, seed + i);
    parts.push(c);
  }
  return faceTint(merge(parts), 0.12, seed);
}

export function birchTree(seed = 1) {
  const t = part(new THREE.CylinderGeometry(0.11, 0.17, 3.4, 7, 4), P.birch, { pos: [0, 1.7, 0] });
  // kayin gövdesindeki koyu lekeler
  const col = t.attributes.color;
  const pos = t.attributes.position;
  const rng = mulberry32(seed);
  const marks = Array.from({ length: 6 }, () => rng() * 3.2);
  for (let i = 0; i < pos.count; i += 3) {
    const y = (pos.getY(i) + pos.getY(i + 1) + pos.getY(i + 2)) / 3;
    if (marks.some((m) => Math.abs(m - y) < 0.07) && rng() > 0.3) {
      for (let j = 0; j < 3; j++) col.setXYZ(i + j, 0.2, 0.18, 0.17);
    }
  }
  const g = merge([
    t,
    canopyBall('#a6d65a', 1.0, [0, 3.7, 0], seed),
    canopyBall('#c3e36d', 0.8, [0.6, 3.2, 0.2], seed + 1),
    canopyBall('#a6d65a', 0.75, [-0.55, 3.3, -0.2], seed + 2),
    canopyBall('#c3e36d', 0.65, [0, 4.4, 0.3], seed + 3),
  ]);
  return faceTint(g, 0.1, seed);
}

export function palmTree(seed = 1) {
  const parts = [];
  const segs = 7;
  let x = 0;
  let y = 0;
  for (let i = 0; i < segs; i++) {
    const h = 0.6;
    const g = part(new THREE.CylinderGeometry(0.15 - i * 0.008, 0.18 - i * 0.008, h, 7), i % 2 ? '#b08a5a' : '#9c774a', {
      pos: [x, y + h / 2, 0], rot: [0, 0, -0.06 * i],
    });
    parts.push(g);
    x += Math.sin(0.06 * i) * h;
    y += Math.cos(0.06 * i) * h * 0.98;
  }
  for (let k = 0; k < 7; k++) {
    const a = (k / 7) * Math.PI * 2;
    const frond = part(new THREE.ConeGeometry(0.32, 2.1, 4, 1), k % 2 ? P.palm : '#3e9a48', {
      pos: [x + Math.cos(a) * 0.8, y - 0.05, Math.sin(a) * 0.8],
      rot: [0, -a, Math.PI / 2 + 0.35],
      scale: [1, 1, 0.25],
    });
    parts.push(frond);
  }
  parts.push(sphere('#7a5a3a', 0.13, { pos: [x + 0.12, y - 0.18, 0.08] }, 6, 4));
  parts.push(sphere('#7a5a3a', 0.13, { pos: [x - 0.1, y - 0.2, -0.1] }, 6, 4));
  return faceTint(merge(parts), 0.1, seed);
}

export function rockGeo(seed = 1, mossy = true) {
  const g = prep(new THREE.DodecahedronGeometry(1, 1), P.rock);
  jitter(g, 0.45, seed);
  g.scale(1, 0.72, 1);
  g.computeVertexNormals();
  const pos = g.attributes.position;
  const col = g.attributes.color;
  const nrm = g.attributes.normal;
  const moss = new THREE.Color(P.grassMoss);
  const base = new THREE.Color(P.rock);
  const dark = new THREE.Color(P.rockDark);
  const c = new THREE.Color();
  for (let i = 0; i < pos.count; i += 3) {
    const ny = (nrm.getY(i) + nrm.getY(i + 1) + nrm.getY(i + 2)) / 3;
    const y = (pos.getY(i) + pos.getY(i + 1) + pos.getY(i + 2)) / 3;
    if (mossy && ny > 0.75 && y > 0.2) c.copy(moss);
    else c.copy(base).lerp(dark, Math.max(0, -y * 0.8));
    for (let j = 0; j < 3; j++) col.setXYZ(i + j, c.r, c.g, c.b);
  }
  g.translate(0, 0.35, 0);
  return faceTint(g, 0.14, seed);
}

export function bushGeo(seed = 1) {
  const g = merge([
    canopyBall('#4fa64a', 0.6, [0, 0.45, 0], seed),
    canopyBall('#62b856', 0.45, [0.45, 0.35, 0.1], seed + 1),
    canopyBall('#4fa64a', 0.42, [-0.4, 0.33, -0.15], seed + 2),
  ]);
  return faceTint(g, 0.15, seed);
}

export function flowerGeo() {
  const parts = [cyl('#4f9e3a', 0.012, 0.012, 0.32, { pos: [0, 0.16, 0] }, 4)];
  for (let i = 0; i < 5; i++) {
    const a = (i / 5) * Math.PI * 2;
    parts.push(sphere('#ffffff', 0.05, { pos: [Math.cos(a) * 0.055, 0.33, Math.sin(a) * 0.055], scale: [1, 0.35, 1] }, 5, 3));
  }
  parts.push(sphere('#ffd84a', 0.035, { pos: [0, 0.345, 0] }, 5, 3));
  parts.push(part(new THREE.ConeGeometry(0.04, 0.12, 3), '#5fb04a', { pos: [0.04, 0.1, 0], rot: [0, 0, -0.9], scale: [1, 1, 0.3] }));
  const g = merge(parts);
  // Petal rengi instanceColor ile verilecek: petalleri beyaz birakip sap ve
  // gobegi "renk carpanindan etkilenmesin" diye isaretlemenin yolu yok, bu
  // yuzden sap/gobek de hafifce tonlanir; kucuk oldugu icin goze batmaz.
  return g;
}

export function mushroomGeo(color = P.mushroomRed, seed = 1) {
  const cap = part(new THREE.SphereGeometry(0.22, 10, 6, 0, Math.PI * 2, 0, Math.PI / 2), color, { pos: [0, 0.22, 0], scale: [1, 0.75, 1] });
  const parts = [cyl(P.mushroomStem, 0.07, 0.09, 0.24, { pos: [0, 0.12, 0] }, 7), cap];
  const rng = mulberry32(seed);
  for (let i = 0; i < 5; i++) {
    const a = rng() * Math.PI * 2;
    const r = 0.08 + rng() * 0.08;
    parts.push(sphere('#fffaf0', 0.025, { pos: [Math.cos(a) * r, 0.22 + 0.16 * Math.sqrt(1 - (r / 0.22) ** 2) * 0.75, Math.sin(a) * r], scale: [1, 0.4, 1] }, 5, 3));
  }
  return merge(parts);
}

// Ziplatan dev mantar (Mantar Vadisi): sapka ustu "boing".
export function giantMushroomGeo(color, capR, height, seed = 1) {
  const stem = part(new THREE.CylinderGeometry(capR * 0.22, capR * 0.3, height, 10, 3), P.mushroomStem, { pos: [0, height / 2, 0] });
  jitter(stem, 0.08, seed);
  const cap = part(new THREE.SphereGeometry(capR, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2), color, { pos: [0, height - 0.1, 0], scale: [1, 0.5, 1] });
  const under = part(new THREE.CircleGeometry(capR * 0.98, 16), '#f3e3c8', { pos: [0, height - 0.09, 0], rot: [Math.PI / 2, 0, 0] });
  const parts = [stem, cap, under];
  const rng = mulberry32(seed);
  for (let i = 0; i < 9; i++) {
    const a = rng() * Math.PI * 2;
    const rr = capR * (0.25 + rng() * 0.6);
    const yy = height - 0.1 + capR * 0.5 * Math.sqrt(Math.max(0, 1 - (rr / capR) ** 2));
    parts.push(sphere('#fffaf0', capR * 0.11, { pos: [Math.cos(a) * rr, yy, Math.sin(a) * rr], scale: [1, 0.35, 1] }, 6, 3));
  }
  return faceTint(merge(parts), 0.06, seed);
}

// Cimen yapragi (InstancedBufferGeometry tabani): y 0..1, genislik ~0.08
export function grassBladeGeo() {
  const g = new THREE.BufferGeometry();
  const w = 0.07;
  const verts = new Float32Array([
    -w, 0, 0, w, 0, 0, -w * 0.6, 0.45, 0,
    w, 0, 0, w * 0.6, 0.45, 0, -w * 0.6, 0.45, 0,
    -w * 0.6, 0.45, 0, w * 0.6, 0.45, 0, 0, 1, 0,
  ]);
  g.setAttribute('position', new THREE.BufferAttribute(verts, 3));
  g.computeVertexNormals();
  return g;
}

export function cloudGeo(seed = 1) {
  const rng = mulberry32(seed);
  const parts = [];
  const n = 4 + Math.floor(rng() * 4);
  for (let i = 0; i < n; i++) {
    const r = 3 + rng() * 4;
    const g = prep(new THREE.IcosahedronGeometry(r, 1), '#ffffff');
    jitter(g, r * 0.2, seed * 10 + i);
    g.scale(1, 0.6, 1);
    g.translate((i - n / 2) * 4.5 + rng() * 2, rng() * 2, (rng() - 0.5) * 6);
    parts.push(g);
  }
  return merge(parts);
}

export function logGeo(len = 1.6) {
  return merge([
    cyl(P.bark, 0.16, 0.16, len, { rot: [0, 0, Math.PI / 2] }, 7),
    cyl('#d9b98a', 0.13, 0.13, 0.01, { pos: [len / 2 + 0.005, 0, 0], rot: [0, 0, Math.PI / 2] }, 7),
    cyl('#d9b98a', 0.13, 0.13, 0.01, { pos: [-len / 2 - 0.005, 0, 0], rot: [0, 0, Math.PI / 2] }, 7),
  ]);
}

export function crystalGeo(seed = 1) {
  const rng = mulberry32(seed);
  const parts = [];
  for (let i = 0; i < 5; i++) {
    const h = 0.6 + rng() * 1.2;
    const g = part(new THREE.OctahedronGeometry(0.25, 0), i % 2 ? P.crystal : '#b9a6ff', {
      pos: [(rng() - 0.5) * 0.8, h * 0.45, (rng() - 0.5) * 0.8],
      rot: [(rng() - 0.5) * 0.6, rng() * 3, (rng() - 0.5) * 0.6],
      scale: [1, h * 2, 1],
    });
    parts.push(g);
  }
  return merge(parts);
}

export { box, cone };
