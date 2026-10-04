in vec2 vUv;
uniform sampler2D uTex;
uniform vec2 uDir;     // texel adimi * yon
uniform int uRadius;
out vec4 FragColor;
void main() {
    if (uRadius == 0) { FragColor = vec4(texture(uTex, vUv).rgb, 1.0); return; }
    float sigma = float(uRadius);
    float wsum = 0.0;
    vec3 sum = vec3(0.0);
    for (int i = -uRadius; i <= uRadius; i++) {
        float x = float(i);
        float w = exp(-0.5 * x * x / (sigma * sigma)) / sigma;
        sum += texture(uTex, vUv + uDir * x).rgb * w;
        wsum += w;
    }
    FragColor = vec4(sum / wsum, 1.0);
}
