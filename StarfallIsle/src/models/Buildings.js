// Yapilar ve dekor. Her kurucu { group, colliders, glow } dondurur:
//  - group: sahneye eklenecek meshler (yerel uzayda, kapi +Z'ye bakar)
//  - colliders: fizik tanimlari (yerel uzayda); Props bunlari dunyaya tasir
//  - glow: gece yanan pencere/fener geometrileri; tum ada icin tek bir
//    "gece isigi" meshinde birlestirilir (tek draw call, tek parlaklik ayari)

import * as THREE from 'three';
import { P } from './palette.js';
import { part, merge, box, cyl, cone, sphere, prism, mesh, vcMaterial, jitter, prep, faceTint, gradientY } from './geom.js';
import { rockGeo, crystalGeo } from './Nature.js';
import { mulberry32 } from '../world/noise.js';

// Ucgen sarimini ters cevir (ic yuzey) ve tek renge boya.
function flipFaces(g, color) {
  const pos = g.attributes.position;
  const nrm = g.attributes.normal;
  const col = g.attributes.color;
  const c = new THREE.Color(color);
  for (let i = 0; i < pos.count; i += 3) {
    for (const attr of [pos, nrm]) {
      const ax = attr.getX(i + 1), ay = attr.getY(i + 1), az = attr.getZ(i + 1);
      attr.setXYZ(i + 1, attr.getX(i + 2), attr.getY(i + 2), attr.getZ(i + 2));
      attr.setXYZ(i + 2, ax, ay, az);
    }
  }
  for (let i = 0; i < nrm.count; i++) {
    nrm.setXYZ(i, -nrm.getX(i), -nrm.getY(i), -nrm.getZ(i));
    col.setXYZ(i, c.r, c.g, c.b);
  }
  return g;
}

function result() {
  return { group: new THREE.Group(), colliders: [], glow: [], anchors: {} };
}

function add(res, parts, opts = {}) {
  const m = mesh(merge(parts), opts);
  res.group.add(m);
  return m;
}

export function cottage({ w = 4.2, d = 3.6, wallH = 2.5, wall = P.wall, roof = P.roofRed, seed = 1, chimney = true } = {}) {
  const res = result();
  const base = 0.3;
  const top = base + wallH;
  const roofH = 1.7;
  const ang = Math.atan2(roofH, d / 2 + 0.1);
  const slabLen = (d / 2 + 0.5) / Math.cos(ang);
  const parts = [
    box(P.stone, w + 0.3, 1.3, d + 0.3, { pos: [0, base - 0.65, 0] }),
    box(wall, w, wallH, d, { pos: [0, base + wallH / 2, 0] }),
    // gable duvarlari
    prism(wall, d, roofH, w, { pos: [0, top, 0], rot: [0, Math.PI / 2, 0] }),
  ];
  // ahsap iskelet
  for (const sx of [-1, 1]) {
    for (const sz of [-1, 1]) {
      parts.push(box(P.timber, 0.18, wallH, 0.18, { pos: [sx * (w / 2 - 0.02), base + wallH / 2, sz * (d / 2 - 0.02)] }));
    }
  }
  parts.push(box(P.timber, w + 0.1, 0.16, 0.2, { pos: [0, top - 0.05, d / 2] }));
  parts.push(box(P.timber, w + 0.1, 0.16, 0.2, { pos: [0, top - 0.05, -d / 2] }));
  // cati kiremit levhalari
  for (const s of [-1, 1]) {
    const slab = part(new THREE.BoxGeometry(w + 0.8, 0.16, slabLen, 1, 1, 4), roof, {
      pos: [0, top + roofH / 2 + 0.05, s * (d / 4 + 0.1)],
      rot: [s * ang, 0, 0],
    });
    parts.push(faceTint(slab, 0.18, seed + (s > 0 ? 1 : 2)));
  }
  parts.push(box(P.timber, w + 0.9, 0.18, 0.22, { pos: [0, top + roofH + 0.06, 0] }));
  // kapi
  parts.push(box(P.door, 0.95, 1.65, 0.1, { pos: [0, base + 0.82, d / 2 + 0.04] }));
  parts.push(box(P.timber, 1.15, 0.12, 0.14, { pos: [0, base + 1.7, d / 2 + 0.05] }));
  parts.push(sphere(P.gold, 0.05, { pos: [0.3, base + 0.85, d / 2 + 0.11] }, 6, 4));
  parts.push(box(P.stone, 1.2, 0.18, 0.5, { pos: [0, base - 0.05, d / 2 + 0.3] }));
  // pencereler
  const winPos = [
    [-w / 2 + 0.85, d / 2, 0], [w / 2 - 0.85, d / 2, 0],
    [-w / 2, 0, Math.PI / 2], [w / 2, 0, Math.PI / 2],
  ];
  for (const [x, z, ry] of winPos) {
    const side = ry !== 0;
    const ox = side ? x + Math.sign(x) * 0.04 : x;
    const oz = side ? 0 : z + 0.04;
    const y = base + 1.45;
    const fw = side ? 0.12 : 0.85;
    const fd = side ? 0.85 : 0.12;
    parts.push(box(P.timber, fw, 0.85, fd, { pos: [ox, y, oz] }));
    res.glow.push(box(P.windowGlow, side ? 0.14 : 0.62, 0.62, side ? 0.62 : 0.14, { pos: [side ? ox + Math.sign(x) * 0.01 : ox, y, side ? oz : oz + 0.01] }));
    if (!side) {
      parts.push(box(roof, 0.22, 0.75, 0.06, { pos: [ox - 0.55, y, oz + 0.04] }));
      parts.push(box(roof, 0.22, 0.75, 0.06, { pos: [ox + 0.55, y, oz + 0.04] }));
      parts.push(box(P.woodDark, 0.8, 0.18, 0.22, { pos: [ox, y - 0.52, oz + 0.12] }));
      const rng = mulberry32(seed + x * 10);
      for (let i = 0; i < 4; i++) {
        parts.push(sphere(['#ff7aa2', '#ffd84a', '#ffffff', '#c58bff'][Math.floor(rng() * 4)], 0.08, { pos: [ox - 0.28 + i * 0.19, y - 0.38, oz + 0.12] }, 5, 3));
      }
    }
  }
  if (chimney) {
    parts.push(box(P.stone, 0.55, 1.6, 0.55, { pos: [w * 0.28, top + roofH * 0.55, -d * 0.18] }));
    parts.push(box(P.rockDark, 0.68, 0.14, 0.68, { pos: [w * 0.28, top + roofH * 0.55 + 0.85, -d * 0.18] }));
    res.anchors.smoke = new THREE.Vector3(w * 0.28, top + roofH * 0.55 + 1.0, -d * 0.18);
  }
  add(res, parts);

  res.colliders.push({ type: 'box', hx: w / 2 + 0.15, hy: (wallH + 1.3) / 2, hz: d / 2 + 0.15, pos: [0, (wallH + base - 1) / 2, 0] });
  const hw = w / 2 + 0.4;
  const hd = d / 2 + 0.5;
  const ry = top - Math.tan(ang) * 0.4;
  res.colliders.push({
    type: 'hull',
    points: [-hw, ry, -hd, hw, ry, -hd, -hw, ry, hd, hw, ry, hd, -hw, top + roofH + 0.1, 0, hw, top + roofH + 0.1, 0],
  });
  res.anchors.roofTop = new THREE.Vector3(0, top + roofH + 0.15, 0);
  res.anchors.door = new THREE.Vector3(0, 0, d / 2 + 0.8);
  return res;
}

export function shopStall() {
  const res = result();
  const parts = [
    box(P.wood, 2.8, 1.0, 1.0, { pos: [0, 0.5, 0] }),
    box(P.woodDark, 2.9, 0.1, 1.1, { pos: [0, 1.02, 0] }),
    box(P.woodDark, 2.8, 0.12, 0.05, { pos: [0, 0.3, 0.51] }),
    box(P.woodDark, 2.8, 0.12, 0.05, { pos: [0, 0.7, 0.51] }),
  ];
  for (const sx of [-1.35, 1.35]) {
    for (const sz of [-0.45, 0.45]) parts.push(cyl(P.woodDark, 0.06, 0.06, 2.6, { pos: [sx, 1.3, sz] }, 6));
  }
  // cizgili tente
  const stripes = 9;
  for (let i = 0; i < stripes; i++) {
    parts.push(box(i % 2 ? '#ffffff' : '#ff6b6b', 3.2 / stripes, 0.06, 1.6, {
      pos: [-1.6 + (i + 0.5) * (3.2 / stripes), 2.55, 0.15], rot: [0.28, 0, 0],
    }));
    parts.push(cone(i % 2 ? '#ffffff' : '#ff6b6b', 0.18, 0.25, { pos: [-1.6 + (i + 0.5) * (3.2 / stripes), 2.27, 0.95], rot: [Math.PI, 0, 0] }, 3));
  }
  // tezgahtaki mallar
  const fruit = ['#ff5a4f', '#ffd84a', '#7fd060', '#ff9a3c'];
  for (let i = 0; i < 8; i++) parts.push(sphere(fruit[i % 4], 0.1, { pos: [-1.1 + i * 0.3, 1.16, 0.05 + (i % 2) * 0.15] }, 6, 4));
  parts.push(box(P.wood, 0.6, 0.45, 0.5, { pos: [1.8, 0.23, 0.4], rot: [0, 0.3, 0] }));
  parts.push(box(P.woodDark, 0.5, 0.4, 0.45, { pos: [-1.85, 0.2, 0.5], rot: [0, -0.2, 0] }));
  // tabela: deniz kabugu
  parts.push(box(P.woodDark, 1.2, 0.5, 0.08, { pos: [0, 3.05, 0.2] }));
  parts.push(sphere(P.shell, 0.18, { pos: [0, 3.05, 0.27], scale: [1, 0.9, 0.3] }, 8, 6));
  add(res, parts);
  res.colliders.push({ type: 'box', hx: 1.45, hy: 0.55, hz: 0.55, pos: [0, 0.5, 0] });
  return res;
}

export function lighthouse() {
  const res = result();
  const H = 14;
  const tower = prep(new THREE.CylinderGeometry(1.8, 2.6, H, 18, 10), P.lhWhite);
  tower.translate(0, H / 2 + 0.9, 0);
  const pos = tower.attributes.position;
  const col = tower.attributes.color;
  const red = new THREE.Color(P.lhRed);
  const white = new THREE.Color(P.lhWhite);
  for (let i = 0; i < pos.count; i += 3) {
    const y = (pos.getY(i) + pos.getY(i + 1) + pos.getY(i + 2)) / 3;
    const band = Math.floor((y - 0.9) / 2.8) % 2;
    const c = band ? red : white;
    for (let j = 0; j < 3; j++) col.setXYZ(i + j, c.r, c.g, c.b);
  }
  const parts = [
    cyl(P.stone, 3.5, 3.8, 1.9, { pos: [0, -0.05, 0] }, 14),
    tower,
    cyl(P.metal, 2.75, 2.75, 0.25, { pos: [0, H + 1.0, 0] }, 20),
    cyl(P.metal, 1.4, 1.4, 0.2, { pos: [0, H + 2.95, 0] }, 16),
    cone(P.lhRed, 1.75, 1.5, { pos: [0, H + 3.8, 0] }, 16),
    sphere(P.gold, 0.22, { pos: [0, H + 4.65, 0] }, 8, 6),
    box(P.door, 1.1, 1.9, 0.3, { pos: [0, 1.85, 2.42], rot: [-0.06, 0, 0] }),
    box(P.timber, 1.35, 0.15, 0.35, { pos: [0, 2.85, 2.38] }),
  ];
  for (let i = 0; i < 24; i++) {
    const a = (i / 24) * Math.PI * 2;
    parts.push(cyl(P.metal, 0.03, 0.03, 0.8, { pos: [Math.cos(a) * 2.65, H + 1.5, Math.sin(a) * 2.65] }, 4));
  }
  parts.push(part(new THREE.TorusGeometry(2.65, 0.05, 4, 32), P.metal, { pos: [0, H + 1.9, 0], rot: [Math.PI / 2, 0, 0] }));
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * Math.PI * 2;
    parts.push(box(P.metal, 0.1, 1.8, 0.1, { pos: [Math.cos(a) * 1.3, H + 2.0, Math.sin(a) * 1.3] }));
  }
  for (const y of [5, 9.5]) parts.push(box('#3b4b5c', 0.6, 0.8, 0.2, { pos: [0, y, 2.2 - (y - 1) * 0.055] }));
  add(res, parts);

  // lamba odasi: ayri malzeme, final aninda parlakligi canlandiriliyor
  const lampMat = new THREE.MeshStandardMaterial({ color: '#fff6d8', emissive: '#ffd27a', emissiveIntensity: 0.0, roughness: 0.2, transparent: true, opacity: 0.85 });
  const lamp = new THREE.Mesh(new THREE.CylinderGeometry(1.2, 1.2, 1.75, 16), lampMat);
  lamp.position.set(0, H + 2.0, 0);
  res.group.add(lamp);
  res.lamp = lamp;
  res.lampMat = lampMat;
  res.anchors.lamp = new THREE.Vector3(0, H + 2.0, 0);
  res.anchors.door = new THREE.Vector3(0, 0.9, 3.4);

  res.colliders.push({ type: 'cyl', r: 3.7, hh: 0.95, pos: [0, -0.05, 0] });
  res.colliders.push({ type: 'cyl', r: 2.35, hh: H / 2, pos: [0, H / 2 + 0.9, 0] });
  return res;
}

export function windmill() {
  const res = result();
  const parts = [
    cyl(P.stone, 2.6, 2.9, 1.2, { pos: [0, -0.2, 0] }, 8),
    cyl('#f6efe2', 1.5, 2.3, 7.5, { pos: [0, 4.0, 0] }, 8),
    cone(P.roofBlue, 2.0, 1.9, { pos: [0, 8.7, 0] }, 8),
    box(P.door, 0.9, 1.6, 0.2, { pos: [0, 1.2, 2.2], rot: [-0.1, 0, 0] }),
    box('#3b4b5c', 0.5, 0.6, 0.15, { pos: [0, 4.5, 1.92], rot: [-0.1, 0, 0] }),
    box('#3b4b5c', 0.5, 0.6, 0.15, { pos: [1.65, 3.2, 0], rot: [0, 0, 0.1] }),
    cyl(P.woodDark, 0.22, 0.22, 0.9, { pos: [0, 7.6, 1.85], rot: [Math.PI / 2, 0, 0] }, 8),
  ];
  add(res, parts);
  const sails = new THREE.Group();
  sails.position.set(0, 7.6, 2.35);
  const sp = [sphere(P.woodDark, 0.3, {}, 8, 6)];
  for (let i = 0; i < 4; i++) {
    const a = (i / 4) * Math.PI * 2;
    const arm = new THREE.Group();
    const armParts = [box(P.woodDark, 0.15, 4.4, 0.1, { pos: [0, 2.3, 0] })];
    for (let k = 0; k < 5; k++) armParts.push(box(P.sail, 0.9, 0.7, 0.04, { pos: [0.5, 1.0 + k * 0.75, 0] }));
    const g = merge(armParts);
    g.rotateZ(a);
    sp.push(g);
  }
  sails.add(mesh(merge(sp)));
  res.group.add(sails);
  res.sails = sails;
  res.colliders.push({ type: 'cyl', r: 2.2, hh: 4.2, pos: [0, 3.8, 0] });
  return res;
}

export function dock(length = 26, width = 2.6) {
  const res = result();
  const parts = [];
  const planks = Math.floor(length / 0.5);
  for (let i = 0; i < planks; i++) {
    parts.push(box(i % 3 === 0 ? P.woodDark : P.wood, width, 0.12, 0.44, { pos: [0, 0, i * 0.5 + 0.25] }));
  }
  for (let i = 0; i <= length; i += 3) {
    for (const s of [-1, 1]) parts.push(cyl(P.woodDark, 0.12, 0.12, 4, { pos: [s * (width / 2 - 0.1), -1.9, i] }, 6));
    for (const s of [-1, 1]) parts.push(cyl(P.woodDark, 0.09, 0.09, 0.8, { pos: [s * (width / 2 - 0.1), 0.4, i] }, 6));
  }
  add(res, parts);
  res.colliders.push({ type: 'box', hx: width / 2, hy: 0.1, hz: length / 2, pos: [0, -0.02, length / 2] });
  return res;
}

// A ve B arasinda halat kopru (yerel = dunya, donusum yok).
export function bridge(a, b, width = 2.2) {
  const res = result();
  const dir = new THREE.Vector3().subVectors(b, a);
  const len = dir.length();
  const yaw = Math.atan2(dir.x, dir.z);
  const pitch = -Math.asin(dir.y / len);
  const n = Math.floor(len / 0.55);
  const planks = [];
  for (let i = 0; i < n; i++) {
    const t = (i + 0.5) / n;
    const sag = Math.sin(t * Math.PI) * 0.35;
    const p = new THREE.Vector3().lerpVectors(a, b, t);
    p.y -= sag;
    const g = box(i % 2 ? P.wood : '#c08a5c', width, 0.1, 0.46, { pos: [0, 0, 0], rot: [pitch, 0, 0] });
    g.rotateY(yaw);
    g.translate(p.x, p.y, p.z);
    planks.push(g);
  }
  const ropes = [];
  for (const s of [-1, 1]) {
    const side = new THREE.Vector3(Math.cos(yaw), 0, -Math.sin(yaw)).multiplyScalar(s * (width / 2));
    const pts = [];
    for (let i = 0; i <= 12; i++) {
      const t = i / 12;
      const p = new THREE.Vector3().lerpVectors(a, b, t).add(side);
      p.y += 0.85 - Math.sin(t * Math.PI) * 0.45;
      pts.push(p);
    }
    const curve = new THREE.CatmullRomCurve3(pts);
    ropes.push(prep(new THREE.TubeGeometry(curve, 24, 0.04, 4), '#d8c49a'));
    for (const t of [0, 1]) {
      const p = new THREE.Vector3().lerpVectors(a, b, t).add(side);
      ropes.push(cyl(P.woodDark, 0.1, 0.12, 1.6, { pos: [p.x, p.y + 0.2, p.z] }, 6));
    }
  }
  add(res, [...planks, ...ropes]);
  // fizik: sarkmayi takip eden 4 egimli kutu
  const segs = 4;
  for (let i = 0; i < segs; i++) {
    const t0 = i / segs;
    const t1 = (i + 1) / segs;
    const p0 = new THREE.Vector3().lerpVectors(a, b, t0);
    p0.y -= Math.sin(t0 * Math.PI) * 0.35;
    const p1 = new THREE.Vector3().lerpVectors(a, b, t1);
    p1.y -= Math.sin(t1 * Math.PI) * 0.35;
    const mid = new THREE.Vector3().addVectors(p0, p1).multiplyScalar(0.5);
    const d = new THREE.Vector3().subVectors(p1, p0);
    const l = d.length();
    res.colliders.push({ type: 'box', hx: width / 2, hy: 0.08, hz: l / 2 + 0.05, pos: [mid.x, mid.y - 0.03, mid.z], rot: [-Math.asin(d.y / l), yaw, 0], order: 'YXZ' });
  }
  return res;
}

export function shipwreck() {
  const res = result();
  const hull = prep(new THREE.SphereGeometry(1, 14, 8, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2), P.woodDark);
  hull.scale(2.3, 1.7, 6.2);
  jitter(hull, 0.12, 9);
  const pos = hull.attributes.position;
  const col = hull.attributes.color;
  for (let i = 0; i < pos.count; i += 3) {
    const y = (pos.getY(i) + pos.getY(i + 1) + pos.getY(i + 2)) / 3;
    const band = Math.floor((y + 2) * 2.2) % 2;
    const c = new THREE.Color(band ? P.woodDark : '#6e4a32');
    for (let j = 0; j < 3; j++) col.setXYZ(i + j, c.r, c.g, c.b);
  }
  const parts = [
    hull,
    box(P.wood, 4.2, 0.15, 11, { pos: [0, -0.05, 0] }),
    box('#222', 0.8, 0.6, 0.1, { pos: [2.1, -0.7, 1.5], rot: [0, Math.PI / 2, 0.2] }),
    box('#222', 0.6, 0.4, 0.1, { pos: [-2.0, -0.9, -2.0], rot: [0, Math.PI / 2, -0.1] }),
    box(P.woodDark, 4.4, 0.9, 0.2, { pos: [0, 0.35, -5.2] }),
    box(P.wood, 1.1, 0.8, 0.9, { pos: [-1.0, 0.45, 2.5], rot: [0, 0.4, 0] }),
    box(P.wood, 0.9, 0.7, 0.8, { pos: [1.1, 0.4, 3.4], rot: [0, -0.3, 0] }),
    cyl('#8c6a4a', 0.4, 0.4, 0.9, { pos: [0.6, 0.45, -2.2] }, 8),
    cyl(P.woodDark, 0.18, 0.22, 8, { pos: [0, 4.0, 0.5] }, 8),
    box(P.woodDark, 4.2, 0.16, 0.16, { pos: [0, 5.6, 0.5] }),
    cyl(P.woodDark, 0.85, 0.75, 0.6, { pos: [0, 7.4, 0.5] }, 10),
  ];
  const sail = new THREE.Shape();
  sail.moveTo(-1.9, 0);
  sail.lineTo(1.9, 0);
  sail.lineTo(1.7, -1.4);
  sail.lineTo(1.1, -1.9);
  sail.lineTo(0.6, -1.5);
  sail.lineTo(0.0, -2.4);
  sail.lineTo(-0.7, -1.7);
  sail.lineTo(-1.3, -2.1);
  sail.lineTo(-1.8, -1.2);
  sail.closePath();
  parts.push(part(new THREE.ExtrudeGeometry(sail, { depth: 0.03, bevelEnabled: false }), P.sail, { pos: [0, 5.5, 0.62] }));
  const g = merge(parts);
  g.rotateZ(0.22);
  g.rotateX(-0.06);
  res.group.add(mesh(g));
  // carpisma: govde + gozcu yuvasi; geometriyle ayni egimde
  const tilt = (x, y, z) => new THREE.Vector3(x, y, z).applyEuler(new THREE.Euler(-0.06, 0, 0.22, 'XYZ'));
  const deck = tilt(0, -0.05, 0);
  res.colliders.push({ type: 'box', hx: 2.1, hy: 0.6, hz: 5.6, pos: [deck.x, deck.y - 0.5, deck.z], rot: [-0.06, 0, 0.22] });
  const mastP = tilt(0, 4.0, 0.5);
  res.colliders.push({ type: 'cyl', r: 0.22, hh: 4, pos: [mastP.x, mastP.y, mastP.z], rot: [-0.06, 0, 0.22] });
  const nest = tilt(0, 7.15, 0.5);
  res.colliders.push({ type: 'cyl', r: 0.85, hh: 0.3, pos: [nest.x, nest.y, nest.z], rot: [-0.06, 0, 0.22] });
  res.anchors.nest = tilt(0, 7.9, 0.5);
  return res;
}

export function campfire() {
  const res = result();
  const parts = [];
  for (let i = 0; i < 9; i++) {
    const a = (i / 9) * Math.PI * 2;
    const r = rockGeo(i + 40, false);
    r.scale(0.22, 0.22, 0.22);
    r.translate(Math.cos(a) * 0.75, 0, Math.sin(a) * 0.75);
    parts.push(r);
  }
  for (let i = 0; i < 4; i++) {
    parts.push(cyl(P.bark, 0.08, 0.08, 0.9, { pos: [0, 0.18, 0], rot: [0.5, (i / 4) * Math.PI * 2, Math.PI / 2 - 0.4] }, 6));
  }
  // kutuk oturaklar
  for (const [x, z, ry] of [[2.2, 0, 0], [-2.2, 0.2, 0.2], [0.2, -2.3, Math.PI / 2]]) {
    parts.push(cyl(P.bark, 0.25, 0.25, 1.6, { pos: [x, 0.25, z], rot: [0, ry, Math.PI / 2] }, 8));
  }
  add(res, parts);
  res.anchors.fire = new THREE.Vector3(0, 0.25, 0);
  return res;
}

export function lantern() {
  const res = result();
  add(res, [
    cyl(P.metal, 0.06, 0.08, 2.4, { pos: [0, 1.2, 0] }, 6),
    box(P.metal, 0.5, 0.06, 0.06, { pos: [0.2, 2.35, 0] }),
    box(P.metal, 0.34, 0.06, 0.34, { pos: [0.4, 2.2, 0] }),
    cone(P.metal, 0.26, 0.2, { pos: [0.4, 2.0 + 0.32, 0] }, 4),
  ]);
  res.glow.push(box(P.windowGlow, 0.26, 0.34, 0.26, { pos: [0.4, 2.0, 0] }));
  res.colliders.push({ type: 'cyl', r: 0.1, hh: 1.2, pos: [0, 1.2, 0] });
  return res;
}

export function signpost(colors = ['#f2c94c', '#7fd0ff']) {
  const res = result();
  const parts = [cyl(P.woodDark, 0.08, 0.1, 2.2, { pos: [0, 1.1, 0] }, 6)];
  colors.forEach((c, i) => {
    const dir = i % 2 ? -1 : 1;
    parts.push(box(c, 1.1, 0.3, 0.06, { pos: [dir * 0.45, 1.8 - i * 0.42, 0.06], rot: [0, 0, dir * 0.05] }));
    parts.push(cone(c, 0.15, 0.22, { pos: [dir * 1.1, 1.8 - i * 0.42, 0.06], rot: [0, 0, -dir * Math.PI / 2], scale: [1, 1, 0.3] }, 3));
  });
  add(res, parts);
  res.colliders.push({ type: 'cyl', r: 0.12, hh: 1.1, pos: [0, 1.1, 0] });
  return res;
}

export function rowboat() {
  const res = result();
  const hull = prep(new THREE.SphereGeometry(1, 12, 6, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2), '#e8f1f7');
  hull.scale(0.9, 0.5, 1.9);
  const pos = hull.attributes.position;
  const col = hull.attributes.color;
  for (let i = 0; i < pos.count; i += 3) {
    const y = (pos.getY(i) + pos.getY(i + 1) + pos.getY(i + 2)) / 3;
    const c = new THREE.Color(y > -0.12 ? '#3f7fc4' : '#e8f1f7');
    for (let j = 0; j < 3; j++) col.setXYZ(i + j, c.r, c.g, c.b);
  }
  // Kase yukaridan bakilinca ic yuzunden gorunur: ters sarimli bir ic kabuk ekle.
  const inner = hull.clone();
  inner.scale(0.94, 0.94, 0.96);
  flipFaces(inner, P.wood);
  add(res, [
    hull,
    inner,
    box(P.wood, 1.5, 0.06, 0.3, { pos: [0, -0.1, 0.3] }),
    box(P.wood, 1.3, 0.06, 0.3, { pos: [0, -0.1, -0.9] }),
    cyl(P.wood, 0.03, 0.03, 2.0, { pos: [0.75, 0.05, 0], rot: [0.2, 0, Math.PI / 2 - 0.3] }, 5),
    box(P.wood, 0.14, 0.02, 0.4, { pos: [1.55, -0.2, 0.2] }),
  ]);
  res.colliders.push({ type: 'box', hx: 0.8, hy: 0.25, hz: 1.7, pos: [0, -0.3, 0] });
  return res;
}

export function crate() {
  const res = result();
  add(res, [
    box(P.wood, 0.8, 0.8, 0.8, { pos: [0, 0.4, 0] }),
    box(P.woodDark, 0.84, 0.1, 0.84, { pos: [0, 0.75, 0] }),
    box(P.woodDark, 0.84, 0.1, 0.84, { pos: [0, 0.05, 0] }),
    box(P.woodDark, 0.1, 0.8, 0.84, { pos: [0, 0.4, 0], rot: [0.78, 0, 0] }),
  ]);
  res.colliders.push({ type: 'box', hx: 0.42, hy: 0.4, hz: 0.42, pos: [0, 0.4, 0] });
  return res;
}

export function barrel() {
  const res = result();
  add(res, [
    cyl('#a8744a', 0.35, 0.35, 0.9, { pos: [0, 0.45, 0] }, 10),
    cyl(P.metal, 0.37, 0.37, 0.06, { pos: [0, 0.2, 0] }, 10),
    cyl(P.metal, 0.37, 0.37, 0.06, { pos: [0, 0.7, 0] }, 10),
  ]);
  res.colliders.push({ type: 'cyl', r: 0.37, hh: 0.45, pos: [0, 0.45, 0] });
  return res;
}

export function bench() {
  const res = result();
  add(res, [
    box(P.wood, 1.6, 0.08, 0.45, { pos: [0, 0.45, 0] }),
    box(P.wood, 1.6, 0.35, 0.06, { pos: [0, 0.75, -0.2], rot: [-0.15, 0, 0] }),
    box(P.woodDark, 0.08, 0.45, 0.4, { pos: [-0.7, 0.22, 0] }),
    box(P.woodDark, 0.08, 0.45, 0.4, { pos: [0.7, 0.22, 0] }),
  ]);
  res.colliders.push({ type: 'box', hx: 0.8, hy: 0.25, hz: 0.25, pos: [0, 0.25, 0] });
  return res;
}

export function fence(points, terrain) {
  const res = result();
  const parts = [];
  for (let i = 0; i < points.length - 1; i++) {
    const [ax, az] = points[i];
    const [bx, bz] = points[i + 1];
    const len = Math.hypot(bx - ax, bz - az);
    const n = Math.max(1, Math.round(len / 1.6));
    for (let k = 0; k <= n; k++) {
      if (k === n && i < points.length - 2) continue;
      const x = ax + (bx - ax) * (k / n);
      const z = az + (bz - az) * (k / n);
      const y = terrain.height(x, z);
      parts.push(box('#f3e6cf', 0.14, 1.0, 0.14, { pos: [x, y + 0.4, z] }));
      parts.push(cone('#f3e6cf', 0.1, 0.16, { pos: [x, y + 0.98, z] }, 4));
    }
    const yaw = Math.atan2(bx - ax, bz - az);
    const ym = terrain.height((ax + bx) / 2, (az + bz) / 2);
    for (const h of [0.35, 0.7]) {
      const g = box('#f3e6cf', 0.06, 0.1, len, { rot: [0, yaw, 0] });
      g.translate((ax + bx) / 2, ym + h, (az + bz) / 2);
      parts.push(g);
    }
    res.colliders.push({ type: 'box', hx: 0.08, hy: 0.5, hz: len / 2, pos: [(ax + bx) / 2, ym + 0.5, (az + bz) / 2], rot: [0, yaw, 0] });
  }
  add(res, parts);
  return res;
}

export function flagPole(color = '#ff5a5a') {
  const res = result();
  add(res, [
    cyl('#e8e8e8', 0.05, 0.06, 3.2, { pos: [0, 1.6, 0] }, 6),
    sphere(P.gold, 0.09, { pos: [0, 3.25, 0] }, 6, 4),
  ]);
  const flag = new THREE.Group();
  flag.position.set(0, 2.8, 0);
  const shape = new THREE.Shape();
  shape.moveTo(0, 0);
  shape.lineTo(1.1, -0.3);
  shape.lineTo(0, -0.65);
  shape.closePath();
  flag.add(mesh(merge([part(new THREE.ExtrudeGeometry(shape, { depth: 0.02, bevelEnabled: false }), color)])));
  res.group.add(flag);
  res.flag = flag;
  res.colliders.push({ type: 'cyl', r: 0.08, hh: 1.6, pos: [0, 1.6, 0] });
  return res;
}

// Gizli magara: halka seklinde dev kayalar + cati, bir yanda giris.
export function rockDome() {
  const res = result();
  const parts = [];
  const n = 9;
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    if (i === 0) continue; // giris (+X yonu)
    const r = rockGeo(70 + i, true);
    const s = 2.6 + (i % 3) * 0.4;
    r.scale(s, s * 1.5, s);
    r.translate(Math.cos(a) * 5.2, -0.8, Math.sin(a) * 5.2);
    parts.push(r);
    const pts = [];
    const p = r.attributes.position;
    for (let k = 0; k < p.count; k += 6) pts.push(p.getX(k), p.getY(k), p.getZ(k));
    res.colliders.push({ type: 'hull', points: pts });
  }
  const roof = rockGeo(99, true);
  roof.scale(7.2, 2.2, 7.2);
  roof.translate(0, 5.6, 0);
  parts.push(roof);
  const rp = [];
  const p = roof.attributes.position;
  for (let k = 0; k < p.count; k += 6) rp.push(p.getX(k), p.getY(k), p.getZ(k));
  res.colliders.push({ type: 'hull', points: rp });
  add(res, parts);
  const crystals = [];
  for (let i = 0; i < 5; i++) {
    const a = (i / 5) * Math.PI * 2 + 0.6;
    const c = crystalGeo(i + 5);
    c.translate(Math.cos(a) * 3.0, 0, Math.sin(a) * 3.0);
    crystals.push(c);
  }
  const cm = new THREE.Mesh(merge(crystals), new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, emissive: '#5fdfff', emissiveIntensity: 0.9, roughness: 0.3 }));
  res.group.add(cm);
  res.anchors.inside = new THREE.Vector3(0, 0.6, 0);
  return res;
}
