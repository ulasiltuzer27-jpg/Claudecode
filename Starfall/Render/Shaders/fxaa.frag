in vec2 vUv;
uniform sampler2D uTex;
uniform vec2 uInv;
uniform bool uOn;
out vec4 FragColor;
void main() {
    if (!uOn) { FragColor = texture(uTex, vUv); return; }
    const float REDUCE_MIN = 1.0 / 128.0, REDUCE_MUL = 1.0 / 8.0, SPAN_MAX = 8.0;
    vec3 rgbNW = texture(uTex, vUv + vec2(-1.0, -1.0) * uInv).rgb;
    vec3 rgbNE = texture(uTex, vUv + vec2(1.0, -1.0) * uInv).rgb;
    vec3 rgbSW = texture(uTex, vUv + vec2(-1.0, 1.0) * uInv).rgb;
    vec3 rgbSE = texture(uTex, vUv + vec2(1.0, 1.0) * uInv).rgb;
    vec3 rgbM = texture(uTex, vUv).rgb;
    vec3 luma = vec3(0.299, 0.587, 0.114);
    float lNW = dot(rgbNW, luma), lNE = dot(rgbNE, luma), lSW = dot(rgbSW, luma), lSE = dot(rgbSE, luma), lM = dot(rgbM, luma);
    float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
    float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
    vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), (lNW + lSW) - (lNE + lSE));
    float dirReduce = max((lNW + lNE + lSW + lSE) * 0.25 * REDUCE_MUL, REDUCE_MIN);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = clamp(dir * rcpDirMin, vec2(-SPAN_MAX), vec2(SPAN_MAX)) * uInv;
    vec3 rgbA = 0.5 * (texture(uTex, vUv + dir * (1.0 / 3.0 - 0.5)).rgb + texture(uTex, vUv + dir * (2.0 / 3.0 - 0.5)).rgb);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (texture(uTex, vUv - dir * 0.5).rgb + texture(uTex, vUv + dir * 0.5).rgb);
    float lB = dot(rgbB, luma);
    FragColor = vec4((lB < lMin || lB > lMax) ? rgbA : rgbB, 1.0);
}
