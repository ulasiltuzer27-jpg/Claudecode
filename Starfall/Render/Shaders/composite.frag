in vec2 vUv;
uniform sampler2D uScene;
uniform sampler2D uBloom0;
uniform sampler2D uBloom1;
uniform sampler2D uBloom2;
uniform sampler2D uBloom3;
uniform float uBloomStrength;
uniform float uBloomRadius;
uniform bool uBloomOn;
uniform float uExposure;
uniform float uVignette, uSaturation, uContrast, uFade;
uniform int uFilter;
uniform vec3 uFadeColor;
out vec4 FragColor;

vec3 RRTAndODTFit(vec3 v) {
    vec3 a = v * (v + 0.0245786) - 0.000090537;
    vec3 b = v * (0.983729 * v + 0.4329510) + 0.238081;
    return a / b;
}
vec3 aces(vec3 color) {
    const mat3 ACESInputMat = mat3(vec3(0.59719, 0.07600, 0.02840), vec3(0.35458, 0.90834, 0.13383), vec3(0.04823, 0.01566, 0.83777));
    const mat3 ACESOutputMat = mat3(vec3(1.60475, -0.10208, -0.00327), vec3(-0.53108, 1.10813, -0.07276), vec3(-0.07367, -0.00605, 1.07602));
    color *= uExposure / 0.6;
    color = ACESInputMat * color;
    color = RRTAndODTFit(color);
    color = ACESOutputMat * color;
    return clamp(color, 0.0, 1.0);
}
vec3 toSRGB(vec3 c) { return mix(pow(c, vec3(0.41666)) * 1.055 - vec3(0.055), c * 12.92, vec3(lessThanEqual(c, vec3(0.0031308)))); }
float lerpBloom(float f) { return mix(f, 1.2 - f, uBloomRadius); }

void main() {
    vec3 hdr = texture(uScene, vUv).rgb;
    if (uBloomOn) {
        vec3 b = lerpBloom(1.0) * texture(uBloom0, vUv).rgb + lerpBloom(0.8) * texture(uBloom1, vUv).rgb
               + lerpBloom(0.6) * texture(uBloom2, vUv).rgb + lerpBloom(0.4) * texture(uBloom3, vUv).rgb;
        hdr += b * uBloomStrength;
    }
    vec3 col = toSRGB(aces(hdr));
    float l = dot(col, vec3(0.299, 0.587, 0.114));
    col = mix(vec3(l), col, uSaturation);
    col = (col - 0.5) * uContrast + 0.5;
    if (uFilter == 1) { col *= vec3(1.08, 1.0, 0.86); }
    else if (uFilter == 2) { float g = dot(col, vec3(0.3, 0.59, 0.11)); col = vec3(g) * vec3(1.07, 0.92, 0.74); }
    else if (uFilter == 3) { float g = dot(col, vec3(0.3, 0.59, 0.11)); col = vec3(smoothstep(0.02, 0.98, g)); }
    else if (uFilter == 4) { col = mix(col, vec3(1.0, 0.85, 0.95), 0.12) + 0.04; }
    else if (uFilter == 5) { col = floor(col * 6.0 + 0.5) / 6.0; }
    vec2 d = vUv - 0.5;
    col *= 1.0 - dot(d, d) * uVignette * 2.2;
    col = mix(col, uFadeColor, uFade);
    FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
