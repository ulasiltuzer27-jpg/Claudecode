// Kodla modelleme icin kucuk arac seti. Butun modeller ayni kurala uyuyor:
// her parca position + normal + color niteligi tasiyan, indekssiz bir
// BufferGeometry. Boylece hepsi tek cagrida birlestirilebiliyor ve tek bir
// vertex-color malzemesiyle ciziliyor (az draw call, tutarli stil).

import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { mulberry32 } from '../world/noise.js';

const _c = new THREE.Color();

export function prep(geometry, color) {
  let g = geometry.index ? geometry.toNonIndexed() : geometry;
  if (g !== geometry) geometry.dispose();
  for (const name of Object.keys(g.attributes)) {
    if (name !== 'position' && name !== 'normal') g.deleteAttribute(name);
  }
  if (!g.attributes.normal) g.computeVertexNormals();
  const count = g.attributes.position.count;
  const colors = new Float32Array(count * 3);
  _c.set(color);
  for (let i = 0; i < count; i++) {
    colors[i * 3] = _c.r;
    colors[i * 3 + 1] = _c.g;
    colors[i * 3 + 2] = _c.b;
  }
  g.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  return g;
}

// Parcayi renklendir + donustur. opts: { pos:[x,y,z], rot:[x,y,z], scale:[x,y,z]|n }
export function part(geometry, color, opts = {}) {
  const g = prep(geometry, color);
  const m = new THREE.Matrix4();
  const q = new THREE.Quaternion();
  const e = new THREE.Euler(...(opts.rot || [0, 0, 0]));
  q.setFromEuler(e);
  const s = opts.scale === undefined ? [1, 1, 1] : Array.isArray(opts.scale) ? opts.scale : [opts.scale, opts.scale, opts.scale];
  m.compose(new THREE.Vector3(...(opts.pos || [0, 0, 0])), q, new THREE.Vector3(...s));
  g.applyMatrix4(m);
  return g;
}

export function merge(parts) {
  const merged = mergeGeometries(parts, false);
  for (const p of parts) p.dispose();
  return merged;
}

// Rastgele kucuk sapmalar: ilkel sekiller "elde yapilmis" gorunsun.
// Ayni konumdaki koseler ayni miktarda kayar, yani yuzeyde catlak olusmaz.
export function jitter(geometry, amount, seed = 1) {
  const rng = mulberry32(seed);
  const pos = geometry.attributes.position;
  const map = new Map();
  for (let i = 0; i < pos.count; i++) {
    const key = `${pos.getX(i).toFixed(3)},${pos.getY(i).toFixed(3)},${pos.getZ(i).toFixed(3)}`;
    let d = map.get(key);
    if (!d) {
      d = [(rng() - 0.5) * amount, (rng() - 0.5) * amount, (rng() - 0.5) * amount];
      map.set(key, d);
    }
    pos.setXYZ(i, pos.getX(i) + d[0], pos.getY(i) + d[1], pos.getZ(i) + d[2]);
  }
  pos.needsUpdate = true;
  geometry.computeVertexNormals();
  return geometry;
}

// Dikey renk gecisi (or. agac govdesinin dibi koyu, tepesi acik).
export function gradientY(geometry, bottomColor, topColor, y0, y1) {
  const pos = geometry.attributes.position;
  const col = geometry.attributes.color;
  const a = new THREE.Color(bottomColor);
  const b = new THREE.Color(topColor);
  for (let i = 0; i < pos.count; i++) {
    const t = Math.min(1, Math.max(0, (pos.getY(i) - y0) / (y1 - y0)));
    _c.copy(a).lerp(b, t);
    col.setXYZ(i, _c.r, _c.g, _c.b);
  }
  col.needsUpdate = true;
  return geometry;
}

// Ucgen basina hafif renk sapmasi: low-poly yuzeylerde "firca darbesi" etkisi.
export function faceTint(geometry, amount, seed = 3) {
  const rng = mulberry32(seed);
  const col = geometry.attributes.color;
  for (let i = 0; i < col.count; i += 3) {
    const k = 1 + (rng() - 0.5) * amount;
    for (let j = 0; j < 3; j++) {
      col.setXYZ(i + j, col.getX(i + j) * k, col.getY(i + j) * k, col.getZ(i + j) * k);
    }
  }
  col.needsUpdate = true;
  return geometry;
}

export const MATERIALS = {};

export function vcMaterial(opts = {}) {
  const key = JSON.stringify(opts);
  if (!MATERIALS[key]) {
    MATERIALS[key] = new THREE.MeshStandardMaterial({
      vertexColors: true,
      flatShading: opts.flat !== false,
      roughness: opts.roughness ?? 0.85,
      metalness: opts.metalness ?? 0,
      emissive: opts.emissive ?? 0x000000,
      emissiveIntensity: opts.emissiveIntensity ?? 1,
      transparent: !!opts.transparent,
      opacity: opts.opacity ?? 1,
    });
  }
  return MATERIALS[key];
}

export function mesh(geometry, opts = {}) {
  const m = new THREE.Mesh(geometry, opts.material || vcMaterial(opts));
  m.castShadow = opts.castShadow !== false;
  m.receiveShadow = opts.receiveShadow !== false;
  return m;
}

// Yuvarlatilmis kapsul benzeri govde: kure olceklenip hafifce ezilir.
export function blob(color, sx, sy, sz, detail = 1) {
  return part(new THREE.IcosahedronGeometry(1, detail), color, { scale: [sx, sy, sz] });
}

export function sphere(color, r, opts = {}, w = 12, h = 8) {
  return part(new THREE.SphereGeometry(r, w, h), color, opts);
}

export function box(color, w, h, d, opts = {}) {
  return part(new THREE.BoxGeometry(w, h, d), color, opts);
}

export function cyl(color, rTop, rBottom, h, opts = {}, seg = 8) {
  return part(new THREE.CylinderGeometry(rTop, rBottom, h, seg), color, opts);
}

export function cone(color, r, h, opts = {}, seg = 8) {
  return part(new THREE.ConeGeometry(r, h, seg), color, opts);
}

// Ucgen prizma (cati): genislik x, yukseklik y, derinlik z; tabani y=0.
export function prism(color, w, h, d, opts = {}) {
  const shape = new THREE.Shape();
  shape.moveTo(-w / 2, 0);
  shape.lineTo(w / 2, 0);
  shape.lineTo(0, h);
  shape.lineTo(-w / 2, 0);
  const g = new THREE.ExtrudeGeometry(shape, { depth: d, bevelEnabled: false });
  g.translate(0, 0, -d / 2);
  return part(g, color, opts);
}

export function starShape(outer, inner, points = 5) {
  const shape = new THREE.Shape();
  for (let i = 0; i < points * 2; i++) {
    const r = i % 2 === 0 ? outer : inner;
    const a = (i / (points * 2)) * Math.PI * 2 + Math.PI / 2;
    const x = Math.cos(a) * r;
    const y = Math.sin(a) * r;
    if (i === 0) shape.moveTo(x, y); else shape.lineTo(x, y);
  }
  shape.closePath();
  return shape;
}

// Pivot'lu grup: animasyonlu parcalar (bacak, kulak, kuyruk) icin.
export function pivot(x, y, z, child) {
  const g = new THREE.Group();
  g.position.set(x, y, z);
  if (child) g.add(child);
  return g;
}
