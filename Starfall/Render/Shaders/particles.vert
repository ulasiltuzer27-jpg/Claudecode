layout(location = 0) in vec3 aPos;
layout(location = 1) in vec4 aColor; // rgb + alfa
layout(location = 2) in float aSize;
uniform mat4 uView;
uniform mat4 uProj;
uniform float uScale;
out vec4 vColor;
out float vDepth;
void main() {
    vec4 vp = uView * vec4(aPos, 1.0);
    vDepth = -vp.z;
    gl_PointSize = aSize * uScale / max(-vp.z, 0.1);
    gl_Position = uProj * vp;
    vColor = aColor;
}
