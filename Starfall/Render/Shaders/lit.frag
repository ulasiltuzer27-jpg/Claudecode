#include "common"
in vec3 vWorld;
in vec3 vNormal;
in vec3 vColor;
in float vDepth;
uniform vec3 uTint;
uniform vec3 uEmissive;
uniform float uOpacity;
uniform bool uReceiveShadow;
uniform bool uUnlit;
uniform bool uFogOn;
uniform int uDebug;
out vec4 FragColor;

void main() {
    vec3 N = normalize(vNormal);
    if (!gl_FrontFacing) N = -N;
    vec3 albedo = vColor * uTint;
    vec3 c;
    if (uUnlit) {
        c = albedo;
    } else {
        float sh = uReceiveShadow ? shadowFactor(vWorld, N) : 1.0;
        c = lighting(albedo, N, vWorld, sh);
    }
    if (uDebug == 1) { FragColor = vec4(vec3(shadowFactor(vWorld, N)), 1.0); return; }
    if (uDebug == 2) { vec4 sc = uLightVP * vec4(vWorld, 1.0); FragColor = vec4(sc.xyz / sc.w * 0.5 + 0.5, 1.0); return; }
    c += uEmissive;
    if (uFogOn) c = applyFog(c, vDepth);
    FragColor = vec4(c, uOpacity);
}
