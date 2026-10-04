in vec4 vColor;
in float vDepth;
uniform vec3 uFogColor;
uniform float uFogNear, uFogFar;
uniform bool uAdditive;
out vec4 FragColor;
void main() {
    vec2 d = gl_PointCoord - 0.5;
    float r = length(d) * 2.0;
    if (r > 1.0) discard;
    float a = (1.0 - r * r) * vColor.a;
    float fog = smoothstep(uFogNear, uFogFar, vDepth);
    vec3 c = uAdditive ? vColor.rgb * (1.0 - fog) : mix(vColor.rgb, uFogColor, fog);
    FragColor = vec4(c, a);
}
