// Gok kubbe + gunes/ay isigi + sis. Gun dongusu 0..24 saat.
import * as THREE from 'three';

const KEYS = [
  { h: 0.0, top: '#0a1230', hor: '#1b2a57', sun: '#9fb4ff', si: 0.5, sky: '#5060a8', gnd: '#262a40', hi: 0.85, fog: '#1b2650' },
  { h: 4.5, top: '#101a42', hor: '#2c3466', sun: '#9fb4ff', si: 0.45, sky: '#5060a8', gnd: '#262a40', hi: 0.8, fog: '#252c5c' },
  { h: 5.6, top: '#2d3d7a', hor: '#d98c8f', sun: '#ffb58a', si: 0.9, sky: '#8c8cc8', gnd: '#4a4050', hi: 0.85, fog: '#b98a94' },
  { h: 6.8, top: '#5f95dc', hor: '#ffcfa0', sun: '#ffd2a0', si: 1.8, sky: '#b4d0f4', gnd: '#6a6650', hi: 0.9, fog: '#f2d2b6' },
  { h: 9.0, top: '#3d93f2', hor: '#c4e6ff', sun: '#fff3df', si: 2.6, sky: '#b9dcff', gnd: '#6b6f4f', hi: 0.9, fog: '#cfe7fb' },
  { h: 15.5, top: '#3b8eef', hor: '#cfe9ff', sun: '#fff0d8', si: 2.5, sky: '#b9dcff', gnd: '#6b6f4f', hi: 0.9, fog: '#d3e9fb' },
  { h: 18.0, top: '#4d72c2', hor: '#ffb07a', sun: '#ffb070', si: 1.7, sky: '#c9b3c8', gnd: '#5c4a40', hi: 0.75, fog: '#f4c09c' },
  { h: 19.3, top: '#2e3a7a', hor: '#e8807a', sun: '#ff8a6a', si: 0.75, sky: '#8a78a8', gnd: '#3d3040', hi: 0.6, fog: '#b9788a' },
  { h: 20.6, top: '#141f4c', hor: '#4a3f7a', sun: '#9fb4ff', si: 0.48, sky: '#5060a8', gnd: '#262a40', hi: 0.82, fog: '#2e2e60' },
  { h: 24.0, top: '#0a1230', hor: '#1b2a57', sun: '#9fb4ff', si: 0.5, sky: '#5060a8', gnd: '#262a40', hi: 0.85, fog: '#1b2650' },
];
for (const k of KEYS) for (const p of ['top', 'hor', 'sun', 'sky', 'gnd', 'fog']) k[p] = new THREE.Color(k[p]);

const skyVert = /* glsl */`
  varying vec3 vDir;
  void main() {
    vDir = normalize(position);
    vec4 p = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
    gl_Position = p.xyww;
  }
`;

const skyFrag = /* glsl */`
  uniform vec3 uTop, uHorizon, uSunDir, uSunColor, uMoonDir;
  uniform float uNight, uTime;
  varying vec3 vDir;
  float hash(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
  void main() {
    vec3 d = normalize(vDir);
    float h = clamp(d.y, -1.0, 1.0);
    float t = pow(clamp(h, 0.0, 1.0), 0.55);
    vec3 col = mix(uHorizon, uTop, t);
    col = mix(col, uHorizon * 0.85, smoothstep(0.0, -0.25, h));
    // gunes parlamasi
    float sd = max(dot(d, uSunDir), 0.0);
    col += uSunColor * pow(sd, 8.0) * 0.35 * (1.0 - uNight);
    col += uSunColor * smoothstep(0.9993, 0.9997, sd) * 2.5 * (1.0 - uNight);
    // ay
    float md = max(dot(d, uMoonDir), 0.0);
    col += vec3(0.85, 0.9, 1.0) * smoothstep(0.9990, 0.9994, md) * 1.6 * uNight;
    col += vec3(0.5, 0.6, 1.0) * pow(md, 30.0) * 0.25 * uNight;
    // yildizlar
    if (uNight > 0.01 && h > 0.0) {
      vec3 cell = floor(d * 180.0);
      float s = hash(cell);
      float star = step(0.9975, s) * (0.6 + 0.4 * sin(uTime * 2.0 + s * 100.0));
      col += vec3(star) * uNight * smoothstep(0.0, 0.2, h);
    }
    gl_FragColor = vec4(col, 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
  }
`;

export class Sky {
  constructor(scene, renderer) {
    this.scene = scene;
    this.uniforms = {
      uTop: { value: new THREE.Color() },
      uHorizon: { value: new THREE.Color() },
      uSunDir: { value: new THREE.Vector3(0, 1, 0) },
      uMoonDir: { value: new THREE.Vector3(0, -1, 0) },
      uSunColor: { value: new THREE.Color() },
      uNight: { value: 0 },
      uTime: { value: 0 },
    };
    const mat = new THREE.ShaderMaterial({
      uniforms: this.uniforms, vertexShader: skyVert, fragmentShader: skyFrag,
      side: THREE.BackSide, depthWrite: false, fog: false,
    });
    this.dome = new THREE.Mesh(new THREE.SphereGeometry(1500, 32, 16), mat);
    this.dome.frustumCulled = false;
    this.dome.renderOrder = -1;
    scene.add(this.dome);

    this.sun = new THREE.DirectionalLight(0xffffff, 2.5);
    this.sun.castShadow = true;
    this.sun.shadow.bias = -0.0004;
    this.sun.shadow.normalBias = 0.04;
    scene.add(this.sun);
    scene.add(this.sun.target);

    this.hemi = new THREE.HemisphereLight(0xb9dcff, 0x6b6f4f, 0.9);
    scene.add(this.hemi);

    scene.fog = new THREE.Fog(0xcfe7fb, 120, 520);

    this.state = {
      top: new THREE.Color(), horizon: new THREE.Color(), sunColor: new THREE.Color(),
      sunDir: new THREE.Vector3(), night: 0, fog: new THREE.Color(), sunIntensity: 1,
    };
    this._tmp = new THREE.Color();
    this.setShadowQuality(2048, 40);
  }

  setShadowQuality(size, range) {
    this.sun.shadow.mapSize.set(size, size);
    if (this.sun.shadow.map) {
      this.sun.shadow.map.dispose();
      this.sun.shadow.map = null;
    }
    const c = this.sun.shadow.camera;
    c.left = -range; c.right = range; c.top = range; c.bottom = -range;
    c.near = 1; c.far = 260;
    c.updateProjectionMatrix();
    this.shadowRange = range;
  }

  setFar(far) {
    this.scene.fog.far = far;
    this.scene.fog.near = far * 0.22;
  }

  update(hour, focus, dt) {
    this.uniforms.uTime.value += dt;
    let i = 0;
    while (i < KEYS.length - 2 && KEYS[i + 1].h <= hour) i++;
    const a = KEYS[i];
    const b = KEYS[i + 1];
    const t = THREE.MathUtils.clamp((hour - a.h) / (b.h - a.h), 0, 1);
    const s = this.state;
    s.top.copy(a.top).lerp(b.top, t);
    s.horizon.copy(a.hor).lerp(b.hor, t);
    s.sunColor.copy(a.sun).lerp(b.sun, t);
    s.fog.copy(a.fog).lerp(b.fog, t);
    const si = THREE.MathUtils.lerp(a.si, b.si, t);
    s.sunIntensity = si;

    // gunes yorungesi: 6'da dogu, 12'de tepe, 18'de bati
    const ang = ((hour - 6) / 12) * Math.PI;
    const sunDir = new THREE.Vector3(Math.cos(ang), Math.sin(ang), 0.35).normalize();
    const moonDir = sunDir.clone().negate();
    moonDir.y = Math.abs(moonDir.y) * 0.8 + 0.25;
    moonDir.normalize();
    const night = THREE.MathUtils.clamp(1 - (sunDir.y + 0.12) / 0.3, 0, 1);
    s.night = night;
    s.sunDir.copy(sunDir);

    this.uniforms.uTop.value.copy(s.top);
    this.uniforms.uHorizon.value.copy(s.horizon);
    this.uniforms.uSunDir.value.copy(sunDir);
    this.uniforms.uMoonDir.value.copy(moonDir);
    this.uniforms.uSunColor.value.copy(s.sunColor);
    this.uniforms.uNight.value = night;

    // golge veren isik: gunduz gunes, gece ay
    const lightDir = night > 0.5 ? moonDir : sunDir;
    const lightDirClamped = lightDir.clone();
    lightDirClamped.y = Math.max(lightDirClamped.y, 0.18);
    lightDirClamped.normalize();
    this.sun.color.copy(s.sunColor);
    this.sun.intensity = si;
    const r = this.shadowRange;
    // golge haritasi titremesin diye merkez texel izgarasina oturtuluyor
    const step = (r * 2) / this.sun.shadow.mapSize.x;
    const fx = Math.round(focus.x / step) * step;
    const fz = Math.round(focus.z / step) * step;
    this.sun.position.set(fx + lightDirClamped.x * 120, focus.y + lightDirClamped.y * 120, fz + lightDirClamped.z * 120);
    this.sun.target.position.set(fx, focus.y, fz);

    this.hemi.color.copy(a.sky).lerp(b.sky, t);
    this.hemi.groundColor.copy(a.gnd).lerp(b.gnd, t);
    this.hemi.intensity = THREE.MathUtils.lerp(a.hi, b.hi, t);

    this.scene.fog.color.copy(s.fog);
    this.dome.position.copy(focus);
  }
}
