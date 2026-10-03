// Ruzgarda sallanan cimen. Binlerce yaprak oyuncunun etrafinda kayan bir
// karo icinde yasar: her yaprak dunyada sabit bir yere "oturur" (mod ile
// sarilir), yuksekligi ve yogunlugu dokulardan okunur. CPU hicbir yaprak
// icin is yapmaz; kamera hareket edince sadece bir uniform degisir.
import * as THREE from 'three';
import { grassBladeGeo } from '../models/Nature.js';
import { mulberry32 } from '../world/noise.js';

export class Grass {
  constructor(scene, heightTex, maskTex, worldSize, texSize, count = 30000, size = 64) {
    this.size = size;
    const blade = grassBladeGeo();
    const geo = new THREE.InstancedBufferGeometry();
    geo.index = blade.index;
    geo.setAttribute('position', blade.attributes.position);
    geo.setAttribute('normal', blade.attributes.normal);
    const max = 60000;
    const offsets = new Float32Array(max * 4);
    const rng = mulberry32(4242);
    for (let i = 0; i < max; i++) {
      offsets[i * 4] = rng();
      offsets[i * 4 + 1] = rng();
      offsets[i * 4 + 2] = rng();
      offsets[i * 4 + 3] = rng();
    }
    geo.setAttribute('aOffset', new THREE.InstancedBufferAttribute(offsets, 4));
    geo.instanceCount = count;
    this.geo = geo;

    this.uniforms = {
      uTime: { value: 0 },
      uCenter: { value: new THREE.Vector2() },
      uSize: { value: size },
      uHeight: { value: heightTex },
      uMask: { value: maskTex },
      uWorld: { value: worldSize },
      uTexel: { value: 1 / texSize },
      uPlayer: { value: new THREE.Vector3(0, -100, 0) },
      uDensityScale: { value: 1 },
    };
    const mat = new THREE.MeshLambertMaterial({ color: 0xffffff, side: THREE.DoubleSide });
    mat.onBeforeCompile = (shader) => {
      Object.assign(shader.uniforms, this.uniforms);
      shader.vertexShader = shader.vertexShader
        .replace('#include <common>', /* glsl */`#include <common>
          attribute vec4 aOffset;
          uniform float uTime, uSize, uWorld, uTexel, uDensityScale;
          uniform vec2 uCenter;
          uniform vec3 uPlayer;
          uniform sampler2D uHeight, uMask;
          varying vec3 vGrassColor;
        `)
        .replace('#include <beginnormal_vertex>', 'vec3 objectNormal = vec3(0.0, 1.0, 0.0);')
        .replace('#include <begin_vertex>', /* glsl */`
          vec2 tilePos = aOffset.xy * uSize;
          vec2 wxz = uCenter + mod(tilePos - uCenter + uSize * 0.5, uSize) - uSize * 0.5;
          vec2 tuv = (wxz + uWorld * 0.5) / uWorld;
          tuv = tuv * (1.0 - uTexel) + uTexel * 0.5;
          float ground = texture2D(uHeight, tuv).r;
          vec4 mask = texture2D(uMask, tuv);
          float density = mask.r * uDensityScale;
          float dist = length(wxz - uCenter);
          float fade = 1.0 - smoothstep(uSize * 0.3, uSize * 0.48, dist);
          float keep = step(aOffset.z, density) * fade;
          float h = (0.16 + aOffset.w * 0.22) * (0.6 + mask.r * 0.5) * keep;
          float ang = aOffset.z * 43.98;
          float c = cos(ang), s = sin(ang);
          vec3 bp = vec3(position.x * c, position.y * h, position.x * s);
          float wind = 0.7 * sin(uTime * 1.6 + wxz.x * 0.15 + wxz.y * 0.1) * 0.5 + 0.6 + sin(uTime * 3.3 + wxz.x * 0.6 + wxz.y * 0.4) * 0.15;
          float bend = position.y * position.y;
          bp.xz += vec2(0.92, 0.38) * wind * 0.25 * bend * h;
          vec2 toP = wxz - uPlayer.xz;
          float pd = length(toP);
          float push = (1.0 - smoothstep(0.15, 1.0, pd)) * (1.0 - step(1.6, abs(uPlayer.y - ground)));
          bp.xz += (toP / max(pd, 0.001)) * push * 0.5 * bend * h;
          bp.y -= push * 0.25 * bend * h;
          vec3 transformed = vec3(wxz.x, ground - 0.02, wxz.y) + bp;
          // renkler dogrusal uzayda (sRGB #3f8f3a -> #9ad46a)
          vec3 base = mix(vec3(0.05, 0.27, 0.04), vec3(0.12, 0.3, 0.03), mask.g);
          vec3 tip = mix(vec3(0.3, 0.64, 0.13), vec3(0.5, 0.7, 0.13), mask.g);
          base = mix(base, base * 0.7, mask.b);
          tip = mix(tip, vec3(0.16, 0.45, 0.12), mask.b);
          vGrassColor = mix(base, tip, position.y) * (0.88 + aOffset.w * 0.24);
        `);
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', '#include <common>\nvarying vec3 vGrassColor;')
        .replace('vec4 diffuseColor = vec4( diffuse, opacity );', 'vec4 diffuseColor = vec4( vGrassColor, opacity );')
        // Cift yuzlu malzemede arka yuzun normali ters cevrilir ve yapraklarin
        // yarisi simsiyah gorunur. Cimen icin normal hep "yukari" kalsin.
        .replace('#include <normal_fragment_begin>', 'float faceDirection = 1.0;\nvec3 normal = normalize( vNormal );\nvec3 nonPerturbedNormal = normal;');
    };
    this.mesh = new THREE.Mesh(geo, mat);
    this.mesh.frustumCulled = false;
    this.mesh.receiveShadow = true;
    this.mesh.castShadow = false;
    scene.add(this.mesh);
  }

  setCount(n) {
    this.geo.instanceCount = Math.min(60000, Math.floor(n));
  }

  update(dt, center, player) {
    this.uniforms.uTime.value += dt;
    this.uniforms.uCenter.value.set(center.x, center.z);
    if (player) this.uniforms.uPlayer.value.copy(player);
  }
}
