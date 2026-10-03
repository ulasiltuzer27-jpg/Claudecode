// Havuzlu parcacik sistemi (THREE.Points). Iki ornek kullaniliyor: biri
// toplamali karisim (parilti, ates, ates bocegi), biri normal (toz, duman).
import * as THREE from 'three';

const vert = /* glsl */`
  attribute float aSize;
  attribute float aAlpha;
  attribute vec3 aColor;
  varying float vAlpha;
  varying vec3 vColor;
  uniform float uScale;
  #include <fog_pars_vertex>
  void main() {
    vec4 mvPosition = modelViewMatrix * vec4(position, 1.0);
    gl_PointSize = aSize * uScale / max(-mvPosition.z, 0.1);
    gl_Position = projectionMatrix * mvPosition;
    vAlpha = aAlpha;
    vColor = aColor;
    #include <fog_vertex>
  }
`;

const frag = /* glsl */`
  varying float vAlpha;
  varying vec3 vColor;
  uniform float uSoft;
  #include <fog_pars_fragment>
  void main() {
    vec2 d = gl_PointCoord - 0.5;
    float r = length(d) * 2.0;
    if (r > 1.0) discard;
    float a = mix(1.0 - step(0.85, r), 1.0 - r * r, uSoft) * vAlpha;
    gl_FragColor = vec4(vColor, a);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
    #include <fog_fragment>
  }
`;

export class Particles {
  constructor(scene, max = 2500, additive = false) {
    this.max = max;
    this.count = 0;
    this.pos = new Float32Array(max * 3);
    this.vel = new Float32Array(max * 3);
    this.col = new Float32Array(max * 3);
    this.size = new Float32Array(max);
    this.alpha = new Float32Array(max);
    this.life = new Float32Array(max);
    this.maxLife = new Float32Array(max);
    this.s0 = new Float32Array(max);
    this.s1 = new Float32Array(max);
    this.grav = new Float32Array(max);
    this.drag = new Float32Array(max);
    this.a0 = new Float32Array(max);
    this.wobble = new Float32Array(max);

    const geo = new THREE.BufferGeometry();
    this.posAttr = new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage);
    this.colAttr = new THREE.BufferAttribute(this.col, 3).setUsage(THREE.DynamicDrawUsage);
    this.sizeAttr = new THREE.BufferAttribute(this.size, 1).setUsage(THREE.DynamicDrawUsage);
    this.alphaAttr = new THREE.BufferAttribute(this.alpha, 1).setUsage(THREE.DynamicDrawUsage);
    geo.setAttribute('position', this.posAttr);
    geo.setAttribute('aColor', this.colAttr);
    geo.setAttribute('aSize', this.sizeAttr);
    geo.setAttribute('aAlpha', this.alphaAttr);
    geo.setDrawRange(0, 0);
    this.uniforms = THREE.UniformsUtils.merge([THREE.UniformsLib.fog, { uScale: { value: 400 }, uSoft: { value: 1 } }]);
    const mat = new THREE.ShaderMaterial({
      uniforms: this.uniforms, vertexShader: vert, fragmentShader: frag,
      transparent: true, depthWrite: false, fog: true,
      blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending,
    });
    this.points = new THREE.Points(geo, mat);
    this.points.frustumCulled = false;
    this.points.renderOrder = 5;
    this.geo = geo;
    scene.add(this.points);
    this._c = new THREE.Color();
  }

  setViewport(heightPx) {
    this.uniforms.uScale.value = heightPx * 0.6;
  }

  // o: { pos:Vector3, count, spread, vel:Vector3, velSpread, life, size, sizeEnd, color(s), gravity, drag, alpha, wobble }
  emit(o) {
    const n = o.count ?? 1;
    const colors = Array.isArray(o.color) ? o.color : [o.color ?? '#ffffff'];
    for (let k = 0; k < n; k++) {
      if (this.count >= this.max) return;
      const i = this.count++;
      const sp = o.spread ?? 0;
      const sx = typeof sp === 'number' ? sp : sp.x;
      const sy = typeof sp === 'number' ? sp : sp.y;
      const sz = typeof sp === 'number' ? sp : sp.z;
      this.pos[i * 3] = o.pos.x + (Math.random() - 0.5) * 2 * sx;
      this.pos[i * 3 + 1] = o.pos.y + (Math.random() - 0.5) * 2 * sy;
      this.pos[i * 3 + 2] = o.pos.z + (Math.random() - 0.5) * 2 * sz;
      const vs = o.velSpread ?? 0;
      const v = o.vel || { x: 0, y: 0, z: 0 };
      this.vel[i * 3] = v.x + (Math.random() - 0.5) * 2 * vs;
      this.vel[i * 3 + 1] = v.y + (Math.random() - 0.5) * 2 * vs;
      this.vel[i * 3 + 2] = v.z + (Math.random() - 0.5) * 2 * vs;
      if (o.radial) {
        const a = Math.random() * Math.PI * 2;
        const b = Math.acos(2 * Math.random() - 1);
        const s = o.radial * (0.6 + Math.random() * 0.4);
        this.vel[i * 3] += Math.sin(b) * Math.cos(a) * s;
        this.vel[i * 3 + 1] += Math.cos(b) * s;
        this.vel[i * 3 + 2] += Math.sin(b) * Math.sin(a) * s;
      }
      this._c.set(colors[Math.floor(Math.random() * colors.length)]);
      this.col[i * 3] = this._c.r;
      this.col[i * 3 + 1] = this._c.g;
      this.col[i * 3 + 2] = this._c.b;
      const life = (o.life ?? 1) * (0.7 + Math.random() * 0.6);
      this.life[i] = life;
      this.maxLife[i] = life;
      this.s0[i] = (o.size ?? 0.3) * (0.7 + Math.random() * 0.6);
      this.s1[i] = o.sizeEnd ?? this.s0[i];
      this.grav[i] = o.gravity ?? 0;
      this.drag[i] = o.drag ?? 0;
      this.a0[i] = o.alpha ?? 1;
      this.wobble[i] = o.wobble ?? 0;
    }
  }

  update(dt) {
    let i = 0;
    while (i < this.count) {
      this.life[i] -= dt;
      if (this.life[i] <= 0) {
        this.kill(i);
        continue;
      }
      const t = 1 - this.life[i] / this.maxLife[i];
      const d = Math.max(0, 1 - this.drag[i] * dt);
      this.vel[i * 3] *= d;
      this.vel[i * 3 + 1] = this.vel[i * 3 + 1] * d + this.grav[i] * dt;
      this.vel[i * 3 + 2] *= d;
      if (this.wobble[i]) {
        const w = this.wobble[i];
        this.vel[i * 3] += Math.sin(this.life[i] * 5 + i) * w * dt;
        this.vel[i * 3 + 2] += Math.cos(this.life[i] * 4 + i * 1.3) * w * dt;
      }
      this.pos[i * 3] += this.vel[i * 3] * dt;
      this.pos[i * 3 + 1] += this.vel[i * 3 + 1] * dt;
      this.pos[i * 3 + 2] += this.vel[i * 3 + 2] * dt;
      this.size[i] = this.s0[i] + (this.s1[i] - this.s0[i]) * t;
      const fadeIn = Math.min(1, t * 8);
      this.alpha[i] = this.a0[i] * fadeIn * (1 - t * t);
      i++;
    }
    this.geo.setDrawRange(0, this.count);
    this.posAttr.needsUpdate = true;
    this.colAttr.needsUpdate = true;
    this.sizeAttr.needsUpdate = true;
    this.alphaAttr.needsUpdate = true;
  }

  kill(i) {
    const last = --this.count;
    if (i === last) return;
    for (const arr of [this.pos, this.vel, this.col]) {
      arr[i * 3] = arr[last * 3];
      arr[i * 3 + 1] = arr[last * 3 + 1];
      arr[i * 3 + 2] = arr[last * 3 + 2];
    }
    for (const arr of [this.size, this.alpha, this.life, this.maxLife, this.s0, this.s1, this.grav, this.drag, this.a0, this.wobble]) {
      arr[i] = arr[last];
    }
  }
}
