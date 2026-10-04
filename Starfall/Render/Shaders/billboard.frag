in vec2 vUv;
in float vDepth;
uniform sampler2D uTex;
uniform vec4 uColor;
uniform int uMode; // 0 doku, 1 yumusak isilti
uniform vec3 uFogColor;
uniform float uFogNear, uFogFar;
out vec4 FragColor;
void main() {
    vec4 c;
    if (uMode == 1) {
        float r = length(vUv - 0.5) * 2.0;
        float a = clamp(1.0 - r, 0.0, 1.0);
        a = a * a * (0.45 + 0.55 * (1.0 - smoothstep(0.0, 0.3, r)));
        a *= uColor.a * (1.0 - smoothstep(uFogNear, uFogFar, vDepth));
        c = vec4(uColor.rgb * a, a);
    } else {
        c = texture(uTex, vUv) * uColor;
        // doku sRGB; sahne dogrusal: kaba donusum
        c.rgb = pow(c.rgb, vec3(2.2)) * c.a;
    }
    // cikis on-carpimli (premultiplied): normal karisim One/OneMinusSrcAlpha
    FragColor = c;
}
