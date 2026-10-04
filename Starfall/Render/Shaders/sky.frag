in vec3 vDir;
uniform vec3 uTop, uHorizon, uSunDirSky, uSunColorSky, uMoonDir;
uniform float uNight, uTime, uAurora;
out vec4 FragColor;
float hash(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float hash2(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash2(i), hash2(i + vec2(1, 0)), u.x), mix(hash2(i + vec2(0, 1)), hash2(i + vec2(1, 1)), u.x), u.y); }
void main() {
    vec3 d = normalize(vDir);
    float h = clamp(d.y, -1.0, 1.0);
    float t = pow(clamp(h, 0.0, 1.0), 0.55);
    vec3 col = mix(uHorizon, uTop, t);
    col = mix(col, uHorizon * 0.85, smoothstep(0.0, -0.25, h));
    float sd = max(dot(d, uSunDirSky), 0.0);
    col += uSunColorSky * pow(sd, 8.0) * 0.35 * (1.0 - uNight);
    col += uSunColorSky * smoothstep(0.9993, 0.9997, sd) * 2.5 * (1.0 - uNight);
    float md = max(dot(d, uMoonDir), 0.0);
    col += vec3(0.85, 0.9, 1.0) * smoothstep(0.9990, 0.9994, md) * 1.6 * uNight;
    col += vec3(0.5, 0.6, 1.0) * pow(md, 30.0) * 0.25 * uNight;
    if (uNight > 0.01 && h > 0.0) {
        vec3 cell = floor(d * 180.0);
        float s = hash(cell);
        float star = step(0.9975, s) * (0.6 + 0.4 * sin(uTime * 2.0 + s * 100.0));
        col += vec3(star) * uNight * smoothstep(0.0, 0.2, h);
    }
    // kuzey isiklari (Kar Adasi): perde perde dalgalanan seritler
    if (uAurora > 0.001 && h > 0.02) {
        vec2 q = d.xz / max(d.y, 0.05);
        float band = 0.0;
        for (int i = 0; i < 3; i++) {
            float fi = float(i);
            float w = vnoise(vec2(q.x * 0.35 + fi * 3.1, uTime * 0.05 + fi)) * 2.0;
            float y = q.y * 0.6 + w + fi * 0.8 - 1.2;
            band += exp(-y * y * 6.0) * (0.6 + 0.4 * vnoise(vec2(q.x * 2.0, uTime * 0.3 + fi)));
        }
        vec3 ac = mix(vec3(0.15, 1.0, 0.55), vec3(0.55, 0.3, 1.0), smoothstep(0.2, 0.9, h));
        col += ac * band * uAurora * smoothstep(0.02, 0.25, h) * 0.55;
    }
    FragColor = vec4(col, 1.0);
}
