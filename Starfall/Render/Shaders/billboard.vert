layout(location = 0) in vec2 aCorner;
uniform mat4 uView;
uniform mat4 uProj;
uniform vec3 uCenter;
uniform vec2 uSize;
out vec2 vUv;
out float vDepth;
void main() {
    vec4 vp = uView * vec4(uCenter, 1.0);
    vp.xy += aCorner * uSize;
    vDepth = -vp.z;
    vUv = aCorner * 0.5 + 0.5;
    vUv.y = 1.0 - vUv.y;
    gl_Position = uProj * vp;
}
