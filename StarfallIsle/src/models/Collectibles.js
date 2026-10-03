// Toplanabilir esya modelleri.
import * as THREE from 'three';
import { P } from './palette.js';
import { part, merge, cyl, cone, sphere, box, starShape, prep } from './geom.js';

let _starGeo = null;
export function starGeo() {
  if (!_starGeo) {
    const g = new THREE.ExtrudeGeometry(starShape(0.42, 0.19), {
      depth: 0.12, bevelEnabled: true, bevelThickness: 0.08, bevelSize: 0.06, bevelSegments: 1,
    });
    g.center();
    _starGeo = prep(g, P.star);
  }
  return _starGeo;
}

export const starMaterial = new THREE.MeshStandardMaterial({
  color: '#ffe680', emissive: '#ffc93d', emissiveIntensity: 1.1, roughness: 0.35, metalness: 0.1, flatShading: true,
});

let _featherGeo = null;
export function featherGeo() {
  if (!_featherGeo) {
    const shape = new THREE.Shape();
    shape.moveTo(0, -0.45);
    shape.quadraticCurveTo(0.24, -0.1, 0.06, 0.5);
    shape.quadraticCurveTo(0, 0.56, -0.06, 0.5);
    shape.quadraticCurveTo(-0.24, -0.1, 0, -0.45);
    const blade = new THREE.ExtrudeGeometry(shape, { depth: 0.03, bevelEnabled: true, bevelThickness: 0.015, bevelSize: 0.015, bevelSegments: 1 });
    blade.translate(0, 0.05, -0.015);
    _featherGeo = merge([
      prep(blade, '#ffd54a'),
      cyl('#fff1b0', 0.015, 0.012, 1.1, { pos: [0, 0.0, 0.03] }, 4),
    ]);
  }
  return _featherGeo;
}

export const featherMaterial = new THREE.MeshStandardMaterial({
  vertexColors: true, emissive: '#ffb020', emissiveIntensity: 0.6, roughness: 0.3, metalness: 0.5, flatShading: true,
});

let _shellGeo = null;
export function shellGeo() {
  if (!_shellGeo) {
    // tarak kabugu: yelpaze + dalgali kaburgalar
    const g = new THREE.CircleGeometry(0.22, 18, 0, Math.PI);
    const pos = g.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i);
      const y = pos.getY(i);
      const a = Math.atan2(y, x);
      const r = Math.hypot(x, y);
      const ridge = Math.cos(a * 18) * 0.012 * (r / 0.22);
      pos.setZ(i, ridge + Math.sqrt(Math.max(0, 0.22 * 0.22 - r * r)) * 0.4);
    }
    g.computeVertexNormals();
    const shell = prep(g, P.shell);
    const col = shell.attributes.color;
    const sp = shell.attributes.position;
    const a = new THREE.Color(P.shell);
    const b = new THREE.Color(P.shellDark);
    const c = new THREE.Color();
    for (let i = 0; i < sp.count; i++) {
      const r = Math.hypot(sp.getX(i), sp.getY(i)) / 0.22;
      c.copy(a).lerp(b, r * 0.8);
      col.setXYZ(i, c.r, c.g, c.b);
    }
    shell.rotateX(-Math.PI / 2 + 0.5);
    _shellGeo = merge([shell, box(P.shellDark, 0.1, 0.05, 0.06, { pos: [0, 0.02, 0.02] })]);
    _shellGeo.translate(0, 0.08, 0);
  }
  return _shellGeo;
}

export function carrotGeo() {
  return merge([
    cone(P.carrot, 0.09, 0.42, { pos: [0, 0.21, 0], rot: [Math.PI, 0, 0] }, 7),
    cone('#5fbf4a', 0.04, 0.22, { pos: [0.03, 0.5, 0], rot: [0, 0, -0.3] }, 4),
    cone('#5fbf4a', 0.04, 0.24, { pos: [-0.03, 0.51, 0], rot: [0, 0, 0.3] }, 4),
    cone('#4fa63a', 0.04, 0.2, { pos: [0, 0.5, 0.03], rot: [0.3, 0, 0] }, 4),
  ]);
}

export function toolGeo(kind) {
  if (kind === 'hammer') {
    return merge([
      cyl('#b07a4f', 0.035, 0.035, 0.6, { pos: [0, 0.3, 0] }, 6),
      box(P.metal, 0.3, 0.12, 0.12, { pos: [0, 0.62, 0] }),
    ]);
  }
  if (kind === 'saw') {
    return merge([
      box('#cfd6dc', 0.6, 0.18, 0.02, { pos: [0.1, 0.3, 0] }),
      box('#b07a4f', 0.16, 0.2, 0.05, { pos: [-0.28, 0.32, 0] }),
      ...Array.from({ length: 8 }, (_, i) => cone('#aeb6bd', 0.03, 0.05, { pos: [-0.15 + i * 0.07, 0.2, 0], rot: [0, 0, Math.PI] }, 3)),
    ]);
  }
  return merge([ // shovel
    cyl('#b07a4f', 0.03, 0.03, 0.7, { pos: [0, 0.45, 0] }, 6),
    box(P.metal, 0.22, 0.26, 0.03, { pos: [0, 0.08, 0] }),
    box('#b07a4f', 0.16, 0.04, 0.04, { pos: [0, 0.82, 0] }),
  ]);
}

export function questMarkerTexture(symbol, color) {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d');
  g.fillStyle = color;
  g.strokeStyle = '#ffffff';
  g.lineWidth = 8;
  g.beginPath();
  g.arc(64, 64, 52, 0, Math.PI * 2);
  g.fill();
  g.stroke();
  g.fillStyle = '#ffffff';
  g.font = 'bold 80px "Baloo 2", sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.fillText(symbol, 64, 70);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  return tex;
}
