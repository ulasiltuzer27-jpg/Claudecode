in vec2 vUv;
uniform sampler2D uTex;
uniform float uThreshold;
out vec4 FragColor;
void main() {
    vec4 c = texture(uTex, vUv);
    float v = dot(c.rgb, vec3(0.299, 0.587, 0.114));
    float a = smoothstep(uThreshold, uThreshold + 0.01, v);
    FragColor = vec4(c.rgb * a, 1.0);
}
