#include "common"
in vec3 vWorld;
in vec3 vColor;
in float vDepth;
out vec4 FragColor;
void main() {
    vec3 N = vec3(0.0, 1.0, 0.0);
    float sh = shadowFactor(vWorld, N);
    vec3 c = lighting(vColor, N, vWorld, sh);
    c = applyFog(c, vDepth);
    FragColor = vec4(c, 1.0);
}
