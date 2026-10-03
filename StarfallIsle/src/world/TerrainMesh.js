// Arazi meshi + yukseklik/maske dokulari (Three.js tarafi).
import * as THREE from 'three';
import { VERTS, CELLS, CELL, WORLD_SIZE, LAYOUT } from './Terrain.js';
import { smoothstep, lerp } from './noise.js';
import { P } from '../models/palette.js';

const C = (hex) => new THREE.Color(hex);
const COL = {
  sand: C(P.sand), sandWet: C(P.sandWet), grass: C(P.grass), grassDark: C(P.grassDark),
  meadow: C(P.grassMeadow), forest: C(P.grassForest), moss: C(P.grassMoss), rock: C(P.rock),
  rockDark: C(P.rockDark), path: C(P.path), deep: C('#8f7d5a'), windy: C('#a9c96a'), peak: C('#8fcf6a'),
};

function gauss(x, z, cx, cz, r) {
  const d2 = ((x - cx) ** 2 + (z - cz) ** 2) / (r * r);
  return Math.exp(-d2);
}

export function regionTints(x, z) {
  const L = LAYOUT;
  return {
    meadow: gauss(x, z, L.meadow.x, L.meadow.z, 48),
    forest: gauss(x, z, L.forest.x, L.forest.z, 52),
    hollow: gauss(x, z, L.hollow.x, L.hollow.z, 36),
    windy: gauss(x, z, L.windy.x, L.windy.z, 40),
    peak: gauss(x, z, L.peak.x, L.peak.z, 40),
  };
}

export function buildTerrain(terrain, exclusions = []) {
  const N = VERTS;
  const positions = new Float32Array(N * N * 3);
  const colors = new Float32Array(N * N * 3);
  const mask = new Uint8Array(N * N * 4);
  const n = { x: 0, y: 1, z: 0 };
  const c = new THREE.Color();
  const noise = terrain.n3;

  for (let iz = 0; iz < N; iz++) {
    for (let ix = 0; ix < N; ix++) {
      const i = iz * N + ix;
      const x = -WORLD_SIZE / 2 + ix * CELL;
      const z = -WORLD_SIZE / 2 + iz * CELL;
      const h = terrain.heights[i];
      positions[i * 3] = x;
      positions[i * 3 + 1] = h;
      positions[i * 3 + 2] = z;

      terrain.normal(x, z, n);
      const wl = terrain.waterLevel(x, z);
      const lake = terrain.isLake(x, z) || Math.hypot(x - LAYOUT.lake.x, z - LAYOUT.lake.z) < LAYOUT.lake.r + 5;
      const tint = regionTints(x, z);
      const vary = noise(x * 0.08, z * 0.08) * 0.5 + noise(x * 0.3, z * 0.3) * 0.25;

      // cimen tabani
      c.copy(COL.grass).lerp(COL.grassDark, smoothstep(-0.3, 0.6, vary));
      c.lerp(COL.meadow, tint.meadow * 0.8);
      c.lerp(COL.forest, tint.forest * 0.85);
      c.lerp(COL.moss, tint.hollow * 0.75);
      c.lerp(COL.windy, tint.windy * 0.6);
      c.lerp(COL.peak, tint.peak * 0.4);

      // kumsal
      const beachTop = lake ? wl + 0.55 : 1.8;
      const beach = 1 - smoothstep(beachTop, beachTop + 0.9, h);
      c.lerp(COL.sand, beach);
      if (h < wl) c.copy(COL.sandWet).lerp(COL.deep, smoothstep(0, 6, wl - h));

      // kaya (dik yamaclar)
      const rock = smoothstep(0.82, 0.7, n.y);
      c.lerp(COL.rock, rock);
      c.lerp(COL.rockDark, rock * smoothstep(0, 1, vary + 0.3) * 0.5);

      // patika
      const pd = terrain.pathDist[i];
      const path = (1 - smoothstep(1.1, 2.1, pd)) * (h > wl + 0.3 ? 1 : 0) * (1 - rock);
      c.lerp(COL.path, path * 0.92);

      const bright = 1 + vary * 0.08;
      colors[i * 3] = c.r * bright;
      colors[i * 3 + 1] = c.g * bright;
      colors[i * 3 + 2] = c.b * bright;

      // cimen maskesi
      let dens = (1 - beach) * (1 - rock) * (1 - smoothstep(0.9, 2.6, 3 - pd)) * (h > wl + 0.25 ? 1 : 0);
      dens *= 0.55 + 0.45 * smoothstep(-0.4, 0.4, vary);
      dens = Math.min(1, dens * (1 + tint.meadow * 0.4));
      for (const e of exclusions) {
        const d = Math.hypot(x - e.x, z - e.z);
        if (d < e.r + 2) dens *= smoothstep(e.r, e.r + 2, d);
      }
      mask[i * 4] = Math.round(Math.max(0, Math.min(1, dens)) * 255);
      mask[i * 4 + 1] = Math.round(Math.min(1, tint.meadow * 0.9 + tint.windy * 0.8) * 255);
      mask[i * 4 + 2] = Math.round(Math.min(1, tint.forest + tint.hollow * 0.7) * 255);
      mask[i * 4 + 3] = 255;
    }
  }

  const indices = new Uint32Array(CELLS * CELLS * 6);
  let k = 0;
  for (let iz = 0; iz < CELLS; iz++) {
    for (let ix = 0; ix < CELLS; ix++) {
      const a = iz * N + ix;
      const b = a + 1;
      const d = a + N;
      const e = d + 1;
      indices[k++] = a; indices[k++] = d; indices[k++] = b;
      indices[k++] = b; indices[k++] = d; indices[k++] = e;
    }
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  geo.setIndex(new THREE.BufferAttribute(indices, 1));
  geo.computeVertexNormals();
  geo.computeBoundingSphere();

  const mat = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.95, metalness: 0 });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.receiveShadow = true;
  mesh.castShadow = true;

  const half = new Uint16Array(N * N);
  for (let i = 0; i < N * N; i++) half[i] = THREE.DataUtils.toHalfFloat(terrain.heights[i]);
  const heightTex = new THREE.DataTexture(half, N, N, THREE.RedFormat, THREE.HalfFloatType);
  heightTex.magFilter = THREE.LinearFilter;
  heightTex.minFilter = THREE.LinearFilter;
  heightTex.needsUpdate = true;

  const maskTex = new THREE.DataTexture(mask, N, N, THREE.RGBAFormat, THREE.UnsignedByteType);
  maskTex.magFilter = THREE.LinearFilter;
  maskTex.minFilter = THREE.LinearFilter;
  maskTex.needsUpdate = true;

  return { mesh, heightTex, maskTex, mask, colors };
}

// Vertex renkleri dogrusal uzayda; canvas sRGB bekler.
function toSRGB(v) {
  const c = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.pow(v, 1 / 2.4) - 0.055;
  return Math.max(0, Math.min(255, c * 255));
}

// Gunlukteki harita icin kus bakisi gorunum (canvas).
export function renderMapCanvas(terrain, size = 512, colors) {
  const cv = document.createElement('canvas');
  cv.width = cv.height = size;
  const g = cv.getContext('2d');
  const img = g.createImageData(size, size);
  const n = { x: 0, y: 1, z: 0 };
  for (let py = 0; py < size; py++) {
    for (let px = 0; px < size; px++) {
      const x = -WORLD_SIZE / 2 + (px / size) * WORLD_SIZE;
      const z = -WORLD_SIZE / 2 + (py / size) * WORLD_SIZE;
      const h = terrain.height(x, z);
      const wl = terrain.waterLevel(x, z);
      let r, gg, b;
      if (h < wl) {
        const d = Math.min(1, (wl - h) / 7);
        r = lerp(120, 40, d); gg = lerp(214, 110, d); b = lerp(214, 170, d);
      } else {
        const ix = Math.min(VERTS - 1, Math.round((x + WORLD_SIZE / 2) / CELL));
        const iz = Math.min(VERTS - 1, Math.round((z + WORLD_SIZE / 2) / CELL));
        const i = iz * VERTS + ix;
        r = toSRGB(colors[i * 3]); gg = toSRGB(colors[i * 3 + 1]); b = toSRGB(colors[i * 3 + 2]);
        terrain.normal(x, z, n);
        const shade = 0.75 + 0.35 * Math.max(0, -n.x * 0.6 + n.y * 0.6 - n.z * 0.5);
        r *= shade; gg *= shade; b *= shade;
      }
      const o = (py * size + px) * 4;
      img.data[o] = r; img.data[o + 1] = gg; img.data[o + 2] = b; img.data[o + 3] = 255;
    }
  }
  g.putImageData(img, 0, 0);
  return cv;
}
