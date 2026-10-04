// Ortak aydinlatma: three.js MeshStandard/Lambert'in fiziksel formulleriyle
// ayni (BRDF_Lambert = albedo/pi, isinim = renk * siddet * NdotL).
const float RECIPROCAL_PI = 0.3183098862;
uniform vec3 uSunDir;
uniform vec3 uSunColor;     // siddetle carpilmis
uniform vec3 uHemiSky;      // siddetle carpilmis
uniform vec3 uHemiGround;
uniform vec3 uFogColor;
uniform float uFogNear;
uniform float uFogFar;
uniform mat4 uLightVP;
uniform sampler2DShadow uShadowMap;
uniform float uShadowTexel;
uniform bool uShadowsOn;
uniform int uPointCount;
uniform vec3 uPointPos[4];
uniform vec3 uPointColor[4]; // siddetle carpilmis
uniform vec2 uPointParams[4]; // (mesafe, dusus)

float shadowFactor(vec3 worldPos, vec3 n) {
    if (!uShadowsOn) return 1.0;
    vec4 sc = uLightVP * vec4(worldPos + n * 0.04, 1.0);
    vec3 p = sc.xyz / sc.w * 0.5 + 0.5;
    if (p.x <= 0.0 || p.x >= 1.0 || p.y <= 0.0 || p.y >= 1.0 || p.z >= 1.0) return 1.0;
    p.z -= 0.0006;
    float s = 0.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            s += texture(uShadowMap, vec3(p.xy + vec2(x, y) * uShadowTexel, p.z));
    return s / 9.0;
}

vec3 lighting(vec3 albedo, vec3 N, vec3 worldPos, float shadow) {
    float ndl = max(dot(N, uSunDir), 0.0);
    vec3 irr = uSunColor * ndl * shadow;
    float hw = 0.5 * N.y + 0.5;
    irr += mix(uHemiGround, uHemiSky, hw);
    for (int i = 0; i < 4; i++) {
        if (i >= uPointCount) break;
        vec3 L = uPointPos[i] - worldPos;
        float d = length(L);
        L /= max(d, 1e-4);
        float att = 1.0 / max(pow(d, uPointParams[i].y), 0.01);
        if (uPointParams[i].x > 0.0) {
            float r = clamp(1.0 - pow(d / uPointParams[i].x, 4.0), 0.0, 1.0);
            att *= r * r;
        }
        irr += uPointColor[i] * att * max(dot(N, L), 0.0);
    }
    return albedo * RECIPROCAL_PI * irr;
}

vec3 applyFog(vec3 c, float viewDepth) {
    return mix(c, uFogColor, smoothstep(uFogNear, uFogFar, viewDepth));
}
