namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// GLSL 330 kaynaklari. Calisma aninda derlenir (Content Pipeline yok).
///
/// Renk uzayi: dokular ve kose renkleri sRGB kabul edilip dogrusala
/// cevriliyor, isik dogrusal uzayda hesaplaniyor, ACES ile tonlanip tekrar
/// sRGB'ye donuyor. Sis tonlamadan SONRA ekran uzayinda karisiyor; boylece
/// ufuktaki sis rengi gokyuzunun ufuk rengiyle birebir ayni.
/// </summary>
public static class ShaderSources
{
    public const int MaxLights = 8;

    public const string LitVertex = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec3 vertexNormal;
        in vec4 vertexColor;

        uniform mat4 matModel;
        uniform mat4 matView;
        uniform mat4 matProjection;
        uniform mat4 matNormal;
        uniform mat4 lightVP;
        uniform float time;
        uniform float wind;

        out vec3 vWorldPos;
        out vec2 vUv;
        out vec4 vColor;
        out vec3 vNormal;
        out vec4 vLightPos;

        void main()
        {
            vec4 wp = matModel * vec4(vertexPosition, 1.0);
            if (wind > 0.0)
            {
                float h = max(vertexPosition.y, 0.0);
                wp.x += sin(time * 1.7 + wp.x * 0.35 + wp.z * 0.21) * wind * h;
                wp.z += cos(time * 1.3 + wp.z * 0.31) * wind * h * 0.6;
            }
            vWorldPos = wp.xyz;
            vUv = vertexTexCoord;
            vColor = vertexColor;
            vNormal = normalize((matNormal * vec4(vertexNormal, 0.0)).xyz);
            vLightPos = lightVP * wp;
            gl_Position = matProjection * matView * wp;
        }
        """;

    public const string LitFragment = """
        #version 330
        in vec3 vWorldPos;
        in vec2 vUv;
        in vec4 vColor;
        in vec3 vNormal;
        in vec4 vLightPos;

        uniform sampler2D texture0;
        uniform sampler2D shadowMap;
        uniform vec4 colDiffuse;

        uniform vec3 viewPos;
        uniform vec3 sunDir;
        uniform vec3 sunColor;
        uniform vec3 skyAmbient;
        uniform vec3 groundAmbient;
        uniform vec3 fogColor;
        uniform float fogDensity;
        uniform float exposure;
        uniform int shadowsOn;
        uniform float shadowTexel;
        uniform float shadowBias;
        uniform float nightGlow;
        uniform int lightCount;
        uniform vec4 lightPosRange[8];
        uniform vec3 lightColor[8];

        uniform vec3 emissive;
        uniform float specular;
        uniform int unlit;
        uniform int noFog;

        out vec4 finalColor;

        vec3 toLinear(vec3 c) { return pow(max(c, 0.0), vec3(2.2)); }

        vec3 aces(vec3 x)
        {
            const float a = 2.51; const float b = 0.03; const float c = 2.43; const float d = 0.59; const float e = 0.14;
            return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
        }

        float shadowFactor(vec3 n)
        {
            if (shadowsOn == 0) return 1.0;
            vec3 p = vLightPos.xyz / vLightPos.w;
            p = p * 0.5 + 0.5;
            if (p.x <= 0.0 || p.x >= 1.0 || p.y <= 0.0 || p.y >= 1.0 || p.z >= 1.0) return 1.0;
            float ndl = clamp(dot(n, sunDir), 0.0, 1.0);
            float bias = shadowBias * (1.0 + 3.0 * (1.0 - ndl));
            float sum = 0.0;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    float d = texture(shadowMap, p.xy + vec2(x, y) * shadowTexel).r;
                    sum += (p.z - bias > d) ? 0.0 : 1.0;
                }
            }
            float s = sum / 9.0;
            vec2 edge = min(p.xy, 1.0 - p.xy);
            float fade = clamp(min(edge.x, edge.y) * 12.0, 0.0, 1.0);
            return mix(1.0, s, fade);
        }

        void main()
        {
            vec4 tex = texture(texture0, vUv);
            // Seffaf malzemede kose alfasi opakliktir; digerlerinde pencere isareti.
            bool vertexAlpha = (unlit & 2) != 0;
            vec4 base = tex * colDiffuse * vec4(vColor.rgb, vertexAlpha ? vColor.a : 1.0);
            if (base.a < 0.04) discard;

            vec3 albedo = toLinear(base.rgb);
            vec3 n = normalize(vNormal);
            if (!gl_FrontFacing) n = -n;
            vec3 V = normalize(viewPos - vWorldPos);

            vec3 color;
            if ((unlit & 1) != 0)
            {
                color = albedo;
            }
            else
            {
                float ndl = max(dot(n, sunDir), 0.0);
                float sh = ndl > 0.0 ? shadowFactor(n) : 1.0;
                vec3 amb = mix(groundAmbient, skyAmbient, n.y * 0.5 + 0.5);
                vec3 H = normalize(sunDir + V);
                float spec = pow(max(dot(n, H), 0.0), 40.0) * specular;
                color = albedo * (amb + sunColor * ndl * sh) + sunColor * spec * sh * 0.6;

                for (int i = 0; i < lightCount; i++)
                {
                    vec3 L = lightPosRange[i].xyz - vWorldPos;
                    float d = length(L);
                    float r = lightPosRange[i].w;
                    if (d < r)
                    {
                        L /= max(d, 0.001);
                        float att = pow(clamp(1.0 - pow(d / r, 4.0), 0.0, 1.0), 2.0) / (d * d * 0.35 + 1.0);
                        float nl = max(dot(n, L), 0.0) * 0.8 + 0.2;
                        float sp = pow(max(dot(n, normalize(L + V)), 0.0), 24.0) * specular;
                        color += (albedo * nl + sp) * lightColor[i] * att;
                    }
                }
            }

            color += emissive;

            // Pencere isareti: kose alfasi < 0.75 ise bu yuzey bir penceredir.
            // 0.25..0.75 arasi gece yanan pencere, < 0.25 sonuk pencere.
            if (!vertexAlpha && vColor.a < 0.75 && vColor.a > 0.25)
            {
                color += vec3(1.0, 0.72, 0.38) * nightGlow * 1.6;
            }

            vec3 display = pow(aces(color * exposure), vec3(1.0 / 2.2));
            if (noFog == 0)
            {
                float dist = length(viewPos - vWorldPos);
                float f = 1.0 - exp(-pow(dist * fogDensity, 2.0));
                display = mix(display, fogColor, clamp(f, 0.0, 1.0));
            }
            finalColor = vec4(display, base.a);
        }
        """;

    /// <summary>Golge haritasi icin yalnizca derinlik yazan shader.</summary>
    public const string DepthVertex = """
        #version 330
        in vec3 vertexPosition;
        uniform mat4 matModel;
        uniform mat4 matView;
        uniform mat4 matProjection;
        void main()
        {
            gl_Position = matProjection * matView * matModel * vec4(vertexPosition, 1.0);
        }
        """;

    public const string DepthFragment = """
        #version 330
        out vec4 finalColor;
        void main() { finalColor = vec4(1.0); }
        """;

    /// <summary>Gokyuzu: kamerayi saran kup, ufuk/zenit gecisi, gunes, bulut, yildiz.</summary>
    public const string SkyVertex = """
        #version 330
        in vec3 vertexPosition;
        uniform mat4 matView;
        uniform mat4 matProjection;
        out vec3 vDir;
        void main()
        {
            vDir = vertexPosition;
            mat4 rotView = mat4(mat3(matView));
            vec4 p = matProjection * rotView * vec4(vertexPosition, 1.0);
            gl_Position = p.xyww;
        }
        """;

    public const string SkyFragment = """
        #version 330
        in vec3 vDir;
        uniform vec3 zenithColor;
        uniform vec3 horizonColor;
        uniform vec3 groundColor;
        uniform vec3 sunDir;
        uniform vec3 sunDiscColor;
        uniform vec3 moonDir;
        uniform vec3 cloudColor;
        uniform float cloudAmount;
        uniform float starAmount;
        uniform float time;
        out vec4 finalColor;

        float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        float vnoise(vec2 p)
        {
            vec2 i = floor(p); vec2 f = fract(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = hash(i), b = hash(i + vec2(1, 0)), c = hash(i + vec2(0, 1)), d = hash(i + vec2(1, 1));
            return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
        }
        float fbm(vec2 p)
        {
            float s = 0.0, a = 0.5;
            for (int i = 0; i < 5; i++) { s += vnoise(p) * a; p *= 2.03; a *= 0.5; }
            return s;
        }

        void main()
        {
            vec3 dir = normalize(vDir);
            float h = dir.y;
            vec3 col;
            if (h >= 0.0) col = mix(horizonColor, zenithColor, pow(clamp(h, 0.0, 1.0), 0.55));
            else col = mix(horizonColor, groundColor, pow(clamp(-h, 0.0, 1.0), 0.35));

            float s = dot(dir, sunDir);
            col += sunDiscColor * (smoothstep(0.9992, 0.9996, s) * 3.0 + pow(max(s, 0.0), 80.0) * 0.45 + pow(max(s, 0.0), 6.0) * 0.12);

            float m = dot(dir, moonDir);
            col += vec3(0.85, 0.88, 1.0) * smoothstep(0.9994, 0.9997, m) * starAmount * 1.5;

            if (starAmount > 0.0 && h > 0.0)
            {
                vec2 g = floor(dir.xz / (h + 0.4) * 220.0);
                float st = hash(g);
                float tw = 0.6 + 0.4 * sin(time * 2.0 + st * 40.0);
                col += vec3(step(0.9975, st) * tw * starAmount * smoothstep(0.0, 0.25, h));
            }

            if (h > 0.0 && cloudAmount > 0.0)
            {
                vec2 uv = dir.xz / (h + 0.12) * 0.9 + vec2(time * 0.006, time * 0.002);
                float c = fbm(uv * 2.2);
                float cov = smoothstep(0.62 - cloudAmount * 0.35, 0.9 - cloudAmount * 0.2, c);
                cov *= smoothstep(0.0, 0.12, h);
                float lit = 0.75 + 0.25 * clamp(dot(sunDir, vec3(0, 1, 0)), 0.0, 1.0);
                col = mix(col, cloudColor * lit, cov * 0.9);
            }

            finalColor = vec4(col, 1.0);
        }
        """;

    /// <summary>Deniz: sinus dalgalari, fresnel, gunes parlamasi, sis.</summary>
    public const string WaterVertex = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        uniform mat4 matModel;
        uniform mat4 matView;
        uniform mat4 matProjection;
        uniform float time;
        out vec3 vWorldPos;
        out vec3 vNormal;
        out vec2 vUv;

        vec3 wave(vec2 p, vec2 d, float amp, float len, float speed, inout vec3 dn)
        {
            float k = 6.2831 / len;
            float f = k * dot(d, p) + time * speed;
            dn.x += -d.x * k * amp * cos(f);
            dn.z += -d.y * k * amp * cos(f);
            return vec3(0.0, amp * sin(f), 0.0);
        }

        void main()
        {
            vec4 wp = matModel * vec4(vertexPosition, 1.0);
            vec3 n = vec3(0.0, 1.0, 0.0);
            vec3 off = vec3(0.0);
            off += wave(wp.xz, normalize(vec2(1.0, 0.3)), 0.12, 9.0, 1.4, n);
            off += wave(wp.xz, normalize(vec2(-0.4, 1.0)), 0.07, 5.5, 1.9, n);
            off += wave(wp.xz, normalize(vec2(0.7, -0.8)), 0.04, 3.1, 2.6, n);
            wp.xyz += off;
            vWorldPos = wp.xyz;
            vNormal = normalize(n);
            vUv = wp.xz * 0.08;
            gl_Position = matProjection * matView * wp;
        }
        """;

    public const string WaterFragment = """
        #version 330
        in vec3 vWorldPos;
        in vec3 vNormal;
        in vec2 vUv;
        uniform sampler2D texture0;
        uniform vec3 viewPos;
        uniform vec3 sunDir;
        uniform vec3 sunColor;
        uniform vec3 skyAmbient;
        uniform vec3 horizonColor;
        uniform vec3 fogColor;
        uniform float fogDensity;
        uniform float time;
        uniform float nightGlow;
        out vec4 finalColor;

        void main()
        {
            float r1 = texture(texture0, vUv + vec2(time * 0.02, time * 0.013)).r;
            float r2 = texture(texture0, vUv * 1.7 - vec2(time * 0.017, -time * 0.01)).r;
            vec3 n = normalize(vNormal + vec3(r1 - 0.5, 0.0, r2 - 0.5) * 0.35);
            vec3 V = normalize(viewPos - vWorldPos);
            float fres = pow(1.0 - max(dot(n, V), 0.0), 4.0);
            vec3 deep = vec3(0.04, 0.18, 0.26) * (skyAmbient * 1.6 + sunColor * 0.25);
            vec3 refl = horizonColor;
            vec3 col = mix(deep, refl, clamp(fres * 0.9 + 0.08, 0.0, 1.0));
            vec3 H = normalize(sunDir + V);
            float sp = pow(max(dot(n, H), 0.0), 220.0) * 2.5;
            col += sunColor * sp * step(0.0, sunDir.y);
            col += vec3(1.0, 0.75, 0.45) * nightGlow * 0.03 * r1;
            float dist = length(viewPos - vWorldPos);
            float f = 1.0 - exp(-pow(dist * fogDensity, 2.0));
            col = mix(col, fogColor, clamp(f, 0.0, 1.0));
            finalColor = vec4(col, 0.92);
        }
        """;

    /// <summary>2B doku cizimleri icin (raylib varsayilaniyla ayni girdiler).</summary>
    public const string PostVertex = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        void main()
        {
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    /// <summary>Son islem: FXAA, vinyet, parlaklik, gecis karartmasi, yorgunluk.</summary>
    public const string PostFragment = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec2 resolution;
        uniform int fxaaOn;
        uniform float brightness;
        uniform float vignette;
        uniform float fade;
        uniform float tired;
        uniform float saturation;
        out vec4 finalColor;

        float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

        vec3 fxaa(vec2 uv)
        {
            vec2 px = 1.0 / resolution;
            vec3 rgbNW = texture(texture0, uv + vec2(-1.0, -1.0) * px).rgb;
            vec3 rgbNE = texture(texture0, uv + vec2(1.0, -1.0) * px).rgb;
            vec3 rgbSW = texture(texture0, uv + vec2(-1.0, 1.0) * px).rgb;
            vec3 rgbSE = texture(texture0, uv + vec2(1.0, 1.0) * px).rgb;
            vec3 rgbM = texture(texture0, uv).rgb;
            float lNW = luma(rgbNW), lNE = luma(rgbNE), lSW = luma(rgbSW), lSE = luma(rgbSE), lM = luma(rgbM);
            float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
            float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
            vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), ((lNW + lSW) - (lNE + lSE)));
            float dirReduce = max((lNW + lNE + lSW + lSE) * 0.03125, 1.0 / 128.0);
            float rcp = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
            dir = clamp(dir * rcp, vec2(-8.0), vec2(8.0)) * px;
            vec3 a = 0.5 * (texture(texture0, uv + dir * (1.0 / 3.0 - 0.5)).rgb + texture(texture0, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
            vec3 b = a * 0.5 + 0.25 * (texture(texture0, uv + dir * -0.5).rgb + texture(texture0, uv + dir * 0.5).rgb);
            float lB = luma(b);
            return (lB < lMin || lB > lMax) ? a : b;
        }

        void main()
        {
            vec2 uv = fragTexCoord;
            vec3 c = fxaaOn == 1 ? fxaa(uv) : texture(texture0, uv).rgb;
            float g = luma(c);
            c = mix(vec3(g), c, saturation);
            c *= brightness;
            vec2 d = uv - 0.5;
            float v = 1.0 - dot(d, d) * (vignette + tired * 2.5);
            c *= clamp(v, 0.0, 1.0);
            c = mix(c, vec3(g * 0.6), tired * 0.5);
            c *= (1.0 - fade);
            finalColor = vec4(c, 1.0);
        }
        """;

    /// <summary>Parcaciklar ve dunya ici isaretler: isiksiz, sisli, alfa.</summary>
    public const string ParticleVertex = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 vUv;
        out vec4 vColor;
        void main()
        {
            vUv = vertexTexCoord;
            vColor = vertexColor;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    public const string ParticleFragment = """
        #version 330
        in vec2 vUv;
        in vec4 vColor;
        uniform sampler2D texture0;
        out vec4 finalColor;
        void main()
        {
            vec4 t = texture(texture0, vUv);
            finalColor = t * vColor;
            if (finalColor.a < 0.01) discard;
        }
        """;
}
