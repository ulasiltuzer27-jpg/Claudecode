layout(location = 0) in vec3 aPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
uniform float uTime, uAmp;
out vec3 vWorld;
out vec2 vWaveGrad;
out float vDepth;
void main() {
    vec4 wp = uModel * vec4(aPos, 1.0);
    float a1 = sin(wp.x * 0.12 + uTime * 0.9);
    float a2 = sin(wp.z * 0.09 - uTime * 0.7 + wp.x * 0.03);
    float a3 = sin((wp.x + wp.z) * 0.21 + uTime * 1.4);
    wp.y += (a1 * 0.5 + a2 * 0.4 + a3 * 0.15) * uAmp;
    vWaveGrad = vec2(cos(wp.x * 0.12 + uTime * 0.9) * 0.06 + cos((wp.x + wp.z) * 0.21 + uTime * 1.4) * 0.0315 + cos(wp.z * 0.09 - uTime * 0.7 + wp.x * 0.03) * 0.012,
                     cos(wp.z * 0.09 - uTime * 0.7 + wp.x * 0.03) * 0.036 + cos((wp.x + wp.z) * 0.21 + uTime * 1.4) * 0.0315) * uAmp;
    vWorld = wp.xyz;
    vec4 vp = uView * wp;
    vDepth = -vp.z;
    gl_Position = uProj * vp;
}
