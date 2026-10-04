layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec3 aColor;
#ifdef INSTANCED
layout(location = 3) in vec4 iM0;
layout(location = 4) in vec4 iM1;
layout(location = 5) in vec4 iM2;
layout(location = 6) in vec4 iM3;
layout(location = 7) in vec4 iColor;
#endif
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
uniform float uTime;
uniform float uSway;
out vec3 vWorld;
out vec3 vNormal;
out vec3 vColor;
out float vDepth;

void main() {
#ifdef INSTANCED
    mat4 model = mat4(iM0, iM1, iM2, iM3);
    vec3 col = aColor * iColor.rgb;
#else
    mat4 model = uModel;
    vec3 col = aColor;
#endif
    vec3 p = aPos;
    if (uSway > 0.0) {
        float ph = model[3].x * 0.13 + model[3].z * 0.11;
        float s = max(p.y - 1.2, 0.0);
        p.x += sin(uTime * 1.3 + ph) * 0.03 * s * uSway;
        p.z += cos(uTime * 1.05 + ph) * 0.022 * s * uSway;
    }
    vec4 wp = model * vec4(p, 1.0);
    vWorld = wp.xyz;
    vNormal = mat3(model) * aNormal;
    vColor = col;
    vec4 vp = uView * wp;
    vDepth = -vp.z;
    gl_Position = uProj * vp;
}
