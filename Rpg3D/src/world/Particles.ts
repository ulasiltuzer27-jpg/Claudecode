import * as THREE from 'three';

/**
 * Hafif CPU parçacık sistemi: tek bir THREE.Points, sabit havuz.
 * Kıvılcım, kemik tozu, ateş, iyileşme ışıltısı, seviye halkası...
 * Hepsi aynı draw call. Doku yok; nokta shader'da yumuşak daire çiziliyor.
 */
interface P {
  alive: boolean;
  x: number; y: number; z: number;
  vx: number; vy: number; vz: number;
  life: number; maxLife: number;
  size: number; grow: number;
  r: number; g: number; b: number;
  gravity: number; drag: number;
}

export interface EmitOptions {
  count: number;
  color: THREE.ColorRepresentation;
  color2?: THREE.ColorRepresentation;
  speed?: [number, number];
  up?: number;
  spread?: number;
  life?: [number, number];
  size?: [number, number];
  grow?: number;
  gravity?: number;
  drag?: number;
  /** Başlangıç konumuna rastgele sapma yarıçapı. */
  radius?: number;
}

export class Particles {
  readonly points: THREE.Points;
  private pool: P[] = [];
  private pos: Float32Array;
  private col: Float32Array;
  private siz: Float32Array;
  private alpha: Float32Array;
  private cursor = 0;
  private c1 = new THREE.Color();
  private c2 = new THREE.Color();

  constructor(private max = 3000) {
    this.pos = new Float32Array(max * 3);
    this.col = new Float32Array(max * 3);
    this.siz = new Float32Array(max);
    this.alpha = new Float32Array(max);
    for (let i = 0; i < max; i++) {
      this.pool.push({ alive: false, x: 0, y: -999, z: 0, vx: 0, vy: 0, vz: 0, life: 0, maxLife: 1, size: 1, grow: 0, r: 1, g: 1, b: 1, gravity: 0, drag: 0 });
      this.pos[i * 3 + 1] = -999;
    }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage));
    geo.setAttribute('color', new THREE.BufferAttribute(this.col, 3).setUsage(THREE.DynamicDrawUsage));
    geo.setAttribute('size', new THREE.BufferAttribute(this.siz, 1).setUsage(THREE.DynamicDrawUsage));
    geo.setAttribute('alpha', new THREE.BufferAttribute(this.alpha, 1).setUsage(THREE.DynamicDrawUsage));
    geo.boundingSphere = new THREE.Sphere(new THREE.Vector3(), 1e5);
    const mat = new THREE.ShaderMaterial({
      uniforms: { uScale: { value: 400 } },
      vertexShader: /* glsl */ `
        attribute float size;
        attribute float alpha;
        attribute vec3 color;
        varying vec3 vColor;
        varying float vAlpha;
        uniform float uScale;
        void main() {
          vColor = color;
          vAlpha = alpha;
          vec4 mv = modelViewMatrix * vec4(position, 1.0);
          gl_PointSize = size * uScale / max(0.1, -mv.z);
          gl_Position = projectionMatrix * mv;
        }`,
      fragmentShader: /* glsl */ `
        varying vec3 vColor;
        varying float vAlpha;
        void main() {
          vec2 d = gl_PointCoord - 0.5;
          float r = dot(d, d) * 4.0;
          if (r > 1.0) discard;
          gl_FragColor = vec4(vColor, vAlpha * (1.0 - r));
        }`,
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    this.points = new THREE.Points(geo, mat);
    this.points.frustumCulled = false;
    this.points.renderOrder = 10;
  }

  setViewportHeight(h: number): void {
    (this.points.material as THREE.ShaderMaterial).uniforms.uScale.value = h * 0.6;
  }

  emit(at: THREE.Vector3, o: EmitOptions): void {
    this.c1.set(o.color);
    this.c2.set(o.color2 ?? o.color);
    const [s0, s1] = o.speed ?? [1, 3];
    const [l0, l1] = o.life ?? [0.4, 0.9];
    const [z0, z1] = o.size ?? [0.2, 0.4];
    const spread = o.spread ?? 1;
    for (let k = 0; k < o.count; k++) {
      const p = this.pool[this.cursor];
      this.cursor = (this.cursor + 1) % this.max;
      const theta = Math.random() * Math.PI * 2;
      const phi = Math.acos(1 - 2 * Math.random() * spread);
      const sp = s0 + Math.random() * (s1 - s0);
      const rad = (o.radius ?? 0) * Math.sqrt(Math.random());
      const ra = Math.random() * Math.PI * 2;
      p.alive = true;
      p.x = at.x + Math.cos(ra) * rad;
      p.y = at.y;
      p.z = at.z + Math.sin(ra) * rad;
      p.vx = Math.sin(phi) * Math.cos(theta) * sp;
      p.vy = Math.cos(phi) * sp + (o.up ?? 0);
      p.vz = Math.sin(phi) * Math.sin(theta) * sp;
      p.maxLife = p.life = l0 + Math.random() * (l1 - l0);
      p.size = z0 + Math.random() * (z1 - z0);
      p.grow = o.grow ?? 0;
      const t = Math.random();
      p.r = this.c1.r + (this.c2.r - this.c1.r) * t;
      p.g = this.c1.g + (this.c2.g - this.c1.g) * t;
      p.b = this.c1.b + (this.c2.b - this.c1.b) * t;
      p.gravity = o.gravity ?? 0;
      p.drag = o.drag ?? 0;
    }
  }

  update(dt: number): void {
    for (let i = 0; i < this.max; i++) {
      const p = this.pool[i];
      if (!p.alive) continue;
      p.life -= dt;
      if (p.life <= 0) {
        p.alive = false;
        this.pos[i * 3 + 1] = -999;
        this.alpha[i] = 0;
        continue;
      }
      const damp = Math.max(0, 1 - p.drag * dt);
      p.vx *= damp; p.vy = p.vy * damp - p.gravity * dt; p.vz *= damp;
      p.x += p.vx * dt; p.y += p.vy * dt; p.z += p.vz * dt;
      p.size = Math.max(0.01, p.size + p.grow * dt);
      const t = p.life / p.maxLife;
      this.pos[i * 3] = p.x; this.pos[i * 3 + 1] = p.y; this.pos[i * 3 + 2] = p.z;
      this.col[i * 3] = p.r; this.col[i * 3 + 1] = p.g; this.col[i * 3 + 2] = p.b;
      this.siz[i] = p.size;
      this.alpha[i] = Math.min(1, t * 2.2);
    }
    const g = this.points.geometry;
    g.attributes.position.needsUpdate = true;
    g.attributes.color.needsUpdate = true;
    g.attributes.size.needsUpdate = true;
    g.attributes.alpha.needsUpdate = true;
  }

  // --- Hazır efektler ------------------------------------------------------
  hitSpark(at: THREE.Vector3): void {
    this.emit(at, { count: 14, color: 0xffe2a0, color2: 0xff8a30, speed: [3, 7], life: [0.15, 0.35], size: [0.12, 0.25], gravity: 9, drag: 2 });
  }
  boneDust(at: THREE.Vector3): void {
    this.emit(at, { count: 30, color: 0xd8d2c0, color2: 0x8a8270, speed: [1, 4], up: 1.5, life: [0.5, 1.2], size: [0.25, 0.5], gravity: 3, drag: 2, radius: 0.6 });
  }
  heal(at: THREE.Vector3): void {
    this.emit(at, { count: 40, color: 0x80ff9a, color2: 0xe0ffd0, speed: [0.3, 1], up: 2.2, life: [0.8, 1.4], size: [0.15, 0.3], drag: 1, radius: 0.8 });
  }
  levelUp(at: THREE.Vector3): void {
    this.emit(at, { count: 120, color: 0xffe066, color2: 0xfff5c0, speed: [2, 5], up: 3, spread: 0.3, life: [0.8, 1.6], size: [0.2, 0.4], drag: 1.5, radius: 0.5 });
  }
  fireball(at: THREE.Vector3): void {
    this.emit(at, { count: 3, color: 0xffa030, color2: 0xff4010, speed: [0.2, 0.8], life: [0.2, 0.45], size: [0.35, 0.6], grow: -0.6 });
  }
  explosion(at: THREE.Vector3, color: THREE.ColorRepresentation = 0xff7a20): void {
    this.emit(at, { count: 70, color, color2: 0xffe080, speed: [3, 9], life: [0.3, 0.8], size: [0.4, 0.8], drag: 3, gravity: 2 });
  }
  magic(at: THREE.Vector3): void {
    this.emit(at, { count: 3, color: 0xb070ff, color2: 0x60e0ff, speed: [0.2, 0.8], life: [0.25, 0.5], size: [0.3, 0.5], grow: -0.5 });
  }
  flame(at: THREE.Vector3, scale = 1): void {
    this.emit(at, { count: 1, color: 0xffb040, color2: 0xff5010, speed: [0.1, 0.4], up: 1.6 * scale, spread: 0.2, life: [0.4, 0.8], size: [0.35 * scale, 0.6 * scale], grow: -0.5 * scale, radius: 0.25 * scale });
  }
  sparkle(at: THREE.Vector3, color: THREE.ColorRepresentation): void {
    this.emit(at, { count: 1, color, speed: [0.1, 0.4], up: 1, life: [0.5, 1], size: [0.15, 0.3], radius: 0.5 });
  }
}
