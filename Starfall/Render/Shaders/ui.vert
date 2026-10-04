layout(location = 0) in vec2 aPos;    // piksel
layout(location = 1) in vec2 aUv;
layout(location = 2) in vec4 aColor;
layout(location = 3) in vec4 aParams; // mod, yari genislik, yari yukseklik, yaricap
uniform vec2 uScreen;
out vec2 vUv;
out vec4 vColor;
out vec4 vParams;
void main() {
    vUv = aUv;
    vColor = aColor;
    vParams = aParams;
    vec2 p = aPos / uScreen * 2.0 - 1.0;
    gl_Position = vec4(p.x, -p.y, 0.0, 1.0);
}
