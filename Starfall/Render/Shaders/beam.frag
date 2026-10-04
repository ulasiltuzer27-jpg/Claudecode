in float vV;
uniform float uIntensity;
out vec4 FragColor;
void main() {
    float a = pow(1.0 - vV, 1.6) * 0.32 * uIntensity;
    FragColor = vec4(vec3(1.0, 0.92, 0.7) * a, a);
}
