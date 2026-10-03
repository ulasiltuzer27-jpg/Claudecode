// WebGL renderer + post-processing zinciri + kalite on ayarlari.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { FXAAPass } from 'three/addons/postprocessing/FXAAPass.js';

export const QUALITY = {
  low: { pixelRatio: 0.75, shadows: 1024, bloom: false, fxaa: false, grass: 0.3, flowers: 0.5, shadowRange: 30, far: 380 },
  medium: { pixelRatio: 1, shadows: 2048, bloom: true, fxaa: true, grass: 0.65, flowers: 0.8, shadowRange: 40, far: 520 },
  high: { pixelRatio: 2, shadows: 4096, bloom: true, fxaa: true, grass: 1, flowers: 1, shadowRange: 48, far: 620 },
};

// Renk derecelendirme + vinyet + photo mode filtreleri. Ton eslemeden
// SONRA (sRGB uzayinda) calisir.
const GradeShader = {
  uniforms: {
    tDiffuse: { value: null },
    uVignette: { value: 0.28 },
    uSaturation: { value: 1.08 },
    uContrast: { value: 1.03 },
    uFilter: { value: 0 },
    uFade: { value: 0 },
    uFadeColor: { value: new THREE.Color('#0b1020') },
    uTime: { value: 0 },
  },
  vertexShader: /* glsl */`
    varying vec2 vUv;
    void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }
  `,
  fragmentShader: /* glsl */`
    uniform sampler2D tDiffuse;
    uniform float uVignette, uSaturation, uContrast, uFade, uTime;
    uniform int uFilter;
    uniform vec3 uFadeColor;
    varying vec2 vUv;
    void main() {
      vec4 c = texture2D(tDiffuse, vUv);
      vec3 col = c.rgb;
      float l = dot(col, vec3(0.299, 0.587, 0.114));
      col = mix(vec3(l), col, uSaturation);
      col = (col - 0.5) * uContrast + 0.5;
      if (uFilter == 1) { col *= vec3(1.08, 1.0, 0.86); }
      else if (uFilter == 2) { float g = dot(col, vec3(0.3, 0.59, 0.11)); col = vec3(g) * vec3(1.07, 0.92, 0.74); }
      else if (uFilter == 3) { float g = dot(col, vec3(0.3, 0.59, 0.11)); col = vec3(smoothstep(0.02, 0.98, g)); }
      else if (uFilter == 4) { col = mix(col, vec3(1.0, 0.85, 0.95), 0.12) + 0.04; }
      else if (uFilter == 5) { col = floor(col * 6.0 + 0.5) / 6.0; }
      vec2 d = vUv - 0.5;
      float vig = 1.0 - dot(d, d) * uVignette * 2.2;
      col *= vig;
      col = mix(col, uFadeColor, uFade);
      gl_FragColor = vec4(col, 1.0);
    }
  `,
};

export class Renderer {
  constructor(container, qualityName = 'medium') {
    this.container = container;
    this.renderer = new THREE.WebGLRenderer({ antialias: false, powerPreference: 'high-performance', preserveDrawingBuffer: false });
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.0;
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(this.renderer.domElement);
    this.canvas = this.renderer.domElement;

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(60, 16 / 9, 0.1, 2000);

    this.composer = new EffectComposer(this.renderer);
    this.renderPass = new RenderPass(this.scene, this.camera);
    this.bloom = new UnrealBloomPass(new THREE.Vector2(256, 256), 0.32, 0.55, 0.88);
    this.output = new OutputPass();
    this.grade = new ShaderPass(GradeShader);
    this.fxaa = new FXAAPass();
    this.composer.addPass(this.renderPass);
    this.composer.addPass(this.bloom);
    this.composer.addPass(this.output);
    this.composer.addPass(this.grade);
    this.composer.addPass(this.fxaa);

    this.setQuality(qualityName);
    this.resize();
    window.addEventListener('resize', () => this.resize());
  }

  setQuality(name) {
    this.qualityName = QUALITY[name] ? name : 'medium';
    this.q = QUALITY[this.qualityName];
    this.bloom.enabled = this.q.bloom;
    this.fxaa.enabled = this.q.fxaa;
    this.resize();
  }

  resize() {
    const w = this.container.clientWidth || window.innerWidth;
    const h = this.container.clientHeight || window.innerHeight;
    const pr = Math.min(window.devicePixelRatio || 1, this.q.pixelRatio);
    this.renderer.setPixelRatio(pr);
    this.renderer.setSize(w, h, false);
    this.canvas.style.width = '100%';
    this.canvas.style.height = '100%';
    this.composer.setPixelRatio(pr);
    this.composer.setSize(w, h);
    this.bloom.resolution.set(w * pr * 0.5, h * pr * 0.5);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  render(dt) {
    this.grade.uniforms.uTime.value += dt;
    this.composer.render(dt);
  }

  set fade(v) { this.grade.uniforms.uFade.value = v; }
  get fade() { return this.grade.uniforms.uFade.value; }
  set filter(v) { this.grade.uniforms.uFilter.value = v; }
  get filter() { return this.grade.uniforms.uFilter.value; }
}
