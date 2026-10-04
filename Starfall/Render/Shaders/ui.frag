in vec2 vUv;
in vec4 vColor;
in vec4 vParams;
uniform sampler2D uTex;
uniform sampler2D uFont;
out vec4 FragColor;
float roundedBox(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}
void main() {
    int mode = int(vParams.x + 0.5);
    if (mode == 0) {
        // yuvarlatilmis dikdortgen: vUv = merkeze gore yerel piksel
        float d = roundedBox(vUv, vParams.yz, vParams.w);
        float a = 1.0 - smoothstep(-0.75, 0.75, d);
        FragColor = vec4(vColor.rgb, vColor.a * a);
    } else if (mode == 1) {
        FragColor = texture(uTex, vUv) * vColor;
    } else if (mode == 2) {
        float d = texture(uFont, vUv).r;
        float w = max(fwidth(d), 0.02) * 0.75;
        float a = smoothstep(0.5 - w, 0.5 + w, d);
        FragColor = vec4(vColor.rgb, vColor.a * a);
    } else {
        // yumusak golge
        float d = roundedBox(vUv, vParams.yz, vParams.w);
        float a = 1.0 - smoothstep(-18.0, 18.0, d);
        FragColor = vec4(vColor.rgb, vColor.a * a * a);
    }
}
