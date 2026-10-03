// Stilize su: arazinin yukseklik dokusundan derinlik okunur, kiyida kopuk
// cizgileri, sigda kum rengi, derinde lacivert. Saydamlik kullanilmiyor
// (siralama sorunlari yok); "saydam" gorunumu derinlik renginden geliyor.
import * as THREE from 'three';

const vert = /* glsl */`
  uniform float uTime, uAmp;
  varying vec3 vWorld;
  varying vec2 vWaveGrad;
  #include <fog_pars_vertex>
  void main() {
    vec4 wp = modelMatrix * vec4(position, 1.0);
    float a1 = sin(wp.x * 0.12 + uTime * 0.9) ;
    float a2 = sin(wp.z * 0.09 - uTime * 0.7 + wp.x * 0.03);
    float a3 = sin((wp.x + wp.z) * 0.21 + uTime * 1.4);
    wp.y += (a1 * 0.5 + a2 * 0.4 + a3 * 0.15) * uAmp;
    vWaveGrad = vec2(cos(wp.x * 0.12 + uTime * 0.9) * 0.12 * 0.5 + cos((wp.x + wp.z) * 0.21 + uTime * 1.4) * 0.21 * 0.15 + cos(wp.z * 0.09 - uTime * 0.7 + wp.x * 0.03) * 0.03 * 0.4,
                     cos(wp.z * 0.09 - uTime * 0.7 + wp.x * 0.03) * 0.09 * 0.4 + cos((wp.x + wp.z) * 0.21 + uTime * 1.4) * 0.21 * 0.15) * uAmp;
    vWorld = wp.xyz;
    vec4 mvPosition = viewMatrix * wp;
    gl_Position = projectionMatrix * mvPosition;
    #include <fog_vertex>
  }
`;

const frag = /* glsl */`
  uniform sampler2D uHeight;
  uniform float uTime, uLevel, uWorld, uTexel, uNight, uLight;
  uniform vec3 uShallow, uDeep, uSand, uSkyTop, uSkyHor, uSunDir, uSunColor;
  varying vec3 vWorld;
  varying vec2 vWaveGrad;
  #include <fog_pars_fragment>

  float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
  float vnoise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
  }

  void main() {
    vec2 uv = (vWorld.xz + uWorld * 0.5) / uWorld;
    uv = uv * (1.0 - uTexel) + uTexel * 0.5;
    float th = texture2D(uHeight, clamp(uv, 0.0, 1.0)).r;
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) th = -9.0;
    float depth = uLevel - th;

    // kucuk dalgacik normalleri
    vec2 p = vWorld.xz * 0.35;
    float n1 = vnoise(p + vec2(uTime * 0.35, uTime * 0.2));
    float n2 = vnoise(p * 2.3 - vec2(uTime * 0.25, -uTime * 0.4));
    vec2 rip = (vec2(n1, n2) - 0.5) * 0.22;
    vec3 N = normalize(vec3(-vWaveGrad.x + rip.x, 1.0, -vWaveGrad.y + rip.y));
    vec3 V = normalize(cameraPosition - vWorld);

    vec3 water = mix(uShallow, uDeep, smoothstep(0.3, 7.0, depth));
    water = mix(uSand * 0.92, water, smoothstep(0.0, 1.1, depth));

    float fres = pow(1.0 - max(dot(N, V), 0.0), 4.0);
    vec3 sky = mix(uSkyHor, uSkyTop, 0.35);
    vec3 col = mix(water, sky, fres * 0.55);

    vec3 R = reflect(-V, N);
    float spec = pow(max(dot(R, uSunDir), 0.0), 140.0);
    col += uSunColor * spec * mix(1.4, 0.5, uNight);

    // kiyi kopugu: derinlikle kayan halkalar
    float foamN = vnoise(vWorld.xz * 0.8 + uTime * 0.3);
    float band = sin(depth * 9.0 - uTime * 1.8 + foamN * 3.0) * 0.5 + 0.5;
    float foam = smoothstep(0.9, 0.0, depth) * smoothstep(0.55, 0.85, band);
    foam = max(foam, smoothstep(0.18, 0.02, depth));
    foam *= smoothstep(-0.2, 0.05, depth);
    col = mix(col, vec3(1.0), foam * 0.85);

    // acik denizde seyrek kopuk lekeleri
    float caps = smoothstep(0.78, 0.86, vnoise(vWorld.xz * 0.07 + uTime * 0.05) * vnoise(vWorld.xz * 0.21 - uTime * 0.08) * 2.0);
    col = mix(col, vec3(0.95), caps * 0.25 * smoothstep(3.0, 10.0, depth));

    col *= uLight;
    gl_FragColor = vec4(col, 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
    #include <fog_fragment>
  }
`;

export class Water {
  constructor(scene, heightTex, opts) {
    this.uniforms = THREE.UniformsUtils.merge([THREE.UniformsLib.fog, {
      uHeight: { value: heightTex },
      uTime: { value: 0 },
      uAmp: { value: opts.amp ?? 0.12 },
      uLevel: { value: opts.level },
      uWorld: { value: opts.worldSize },
      uTexel: { value: 1 / opts.texSize },
      uNight: { value: 0 },
      uLight: { value: 1 },
      uShallow: { value: new THREE.Color(opts.shallow || '#3fd0c8') },
      uDeep: { value: new THREE.Color(opts.deep || '#1b5fa8') },
      uSand: { value: new THREE.Color('#e3cf98') },
      uSkyTop: { value: new THREE.Color() },
      uSkyHor: { value: new THREE.Color() },
      uSunDir: { value: new THREE.Vector3(0, 1, 0) },
      uSunColor: { value: new THREE.Color() },
    }]);
    this.uniforms.uHeight.value = heightTex;
    const mat = new THREE.ShaderMaterial({ uniforms: this.uniforms, vertexShader: vert, fragmentShader: frag, fog: true });
    this.mesh = new THREE.Mesh(opts.geometry, mat);
    this.mesh.position.y = opts.level;
    this.mesh.receiveShadow = false;
    scene.add(this.mesh);
  }

  update(dt, sky) {
    const u = this.uniforms;
    u.uTime.value += dt;
    u.uNight.value = sky.night;
    u.uSkyTop.value.copy(sky.top);
    u.uSkyHor.value.copy(sky.horizon);
    u.uSunDir.value.copy(sky.night > 0.5 ? new THREE.Vector3(-sky.sunDir.x, Math.abs(sky.sunDir.y) * 0.8 + 0.25, -sky.sunDir.z).normalize() : sky.sunDir);
    u.uSunColor.value.copy(sky.sunColor);
    u.uLight.value = THREE.MathUtils.lerp(1.0, 0.32, sky.night) * (0.75 + 0.25 * Math.min(1, sky.sunIntensity / 2.5));
  }
}

export function seaGeometry() {
  const g = new THREE.PlaneGeometry(1600, 1600, 200, 200);
  g.rotateX(-Math.PI / 2);
  return g;
}

export function lakeGeometry(r) {
  const g = new THREE.CircleGeometry(r, 64, 0, Math.PI * 2);
  g.rotateX(-Math.PI / 2);
  return g;
}
