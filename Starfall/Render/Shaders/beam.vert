layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec3 aColor; // x: uv.y
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
out float vV;
void main() { vV = aColor.x; gl_Position = uProj * uView * uModel * vec4(aPos, 1.0); }
