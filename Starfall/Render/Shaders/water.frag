#include "common"
in vec3 vWorld;
in vec2 vWaveGrad;
in float vDepth;
uniform sampler2D uHeight;
uniform vec4 uMap; // origin.x, origin.y(z), size, texel
uniform float uTime, uLevel, uNight, uLight, uIce;
uniform vec3 uShallow, uDeep, uSand, uSkyTop, uSkyHor, uSunDirW, uSunColorW, uCamPos;
out vec4 FragColor;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void main() {
    vec2 uv = (vWorld.xz - uMap.xy) / uMap.z;
    float th = -9.0;
    if (uv.x > 0.0 && uv.y > 0.0 && uv.x < 1.0 && uv.y < 1.0)
        th = texture(uHeight, uv * (1.0 - uMap.w) + uMap.w * 0.5).r;
    float depth = uLevel - th;
    vec2 p = vWorld.xz * 0.35;
    float n1 = vnoise(p + vec2(uTime * 0.35, uTime * 0.2));
    float n2 = vnoise(p * 2.3 - vec2(uTime * 0.25, -uTime * 0.4));
    vec2 rip = (vec2(n1, n2) - 0.5) * 0.22;
    vec3 N = normalize(vec3(-vWaveGrad.x + rip.x, 1.0, -vWaveGrad.y + rip.y));
    vec3 V = normalize(uCamPos - vWorld);
    vec3 water = mix(uShallow, uDeep, smoothstep(0.3, 7.0, depth));
    water = mix(uSand * 0.92, water, smoothstep(0.0, 1.1, depth));
    float fres = pow(1.0 - max(dot(N, V), 0.0), 4.0);
    vec3 sky = mix(uSkyHor, uSkyTop, 0.35);
    vec3 col = mix(water, sky, fres * 0.55);
    vec3 R = reflect(-V, N);
    float spec = pow(max(dot(R, uSunDirW), 0.0), 140.0);
    col += uSunColorW * spec * mix(1.4, 0.5, uNight);
    float foamN = vnoise(vWorld.xz * 0.8 + uTime * 0.3);
    float band = sin(depth * 9.0 - uTime * 1.8 + foamN * 3.0) * 0.5 + 0.5;
    float foam = smoothstep(0.9, 0.0, depth) * smoothstep(0.55, 0.85, band);
    foam = max(foam, smoothstep(0.18, 0.02, depth));
    foam *= smoothstep(-0.2, 0.05, depth);
    col = mix(col, vec3(1.0), foam * 0.85 * (1.0 - uIce));
    float caps = smoothstep(0.78, 0.86, vnoise(vWorld.xz * 0.07 + uTime * 0.05) * vnoise(vWorld.xz * 0.21 - uTime * 0.08) * 2.0);
    col = mix(col, vec3(0.95), caps * 0.25 * smoothstep(3.0, 10.0, depth) * (1.0 - uIce));
    col *= uLight;
    col = applyFog(col, vDepth);
    FragColor = vec4(col, 1.0);
}
