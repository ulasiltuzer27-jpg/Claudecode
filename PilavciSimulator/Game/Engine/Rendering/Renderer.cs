using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Core;

namespace PilavciSimulator.Engine.Rendering;

[Flags]
public enum DrawFlags : byte
{
    None = 0,
    NoShadow = 1,
    ViewModel = 2,
    Transparent = 4,
    ShadowOnly = 8,
}

/// <summary>
/// 3B cizim hatti. Bir kare su gecislerden olusur:
///
///  1. Golge gecisi: gunes yonunden ortografik derinlik (statik + dinamik).
///  2. Ana gecis (sahne RenderTexture'i): gokyuzu, opak, su, saydam,
///     parcaciklar.
///  3. El modeli gecisi: elde tutulan esya ayri bir RenderTexture'a kendi
///     derinligiyle cizilir ve sahnenin ustune bindirilir; boylece duvara
///     yaklasinca esya duvarin icine girmez.
///  4. Son islem: FXAA, vinyet, parlaklik, karartma -> ekran.
///
/// Arayuz (2B) bundan sonra ekran cozunurlugunde cizilir.
/// </summary>
public sealed class Renderer : IDisposable
{
    private struct DrawItem
    {
        public Mesh Mesh;
        public int Material;
        public Matrix4x4 World;
        public Color Tint;
        public Vector3 Emissive;
        public DrawFlags Flags;
        public float Distance;
        public Bounds Bounds;
    }

    public Shader LitShader { get; }
    public MaterialLib Materials { get; }
    public ParticleSystem Particles { get; }

    private readonly Shader _depthShader;
    private readonly Shader _skyShader;
    private readonly Shader _waterShader;
    private readonly Shader _postShader;
    private Material _depthMaterial;
    private Material _skyMaterial;
    private Material _waterMaterial;
    private readonly Mesh _skyMesh;

    private readonly List<DrawItem> _opaque = new(2048);
    private readonly List<DrawItem> _transparent = new(256);
    private readonly List<DrawItem> _viewModel = new(64);
    private readonly List<PointLight> _lights = new(64);
    private StaticScene? _static;
    private (Mesh Mesh, Matrix4x4 World)? _water;

    private ShadowMap? _shadow;
    private RenderTexture2D _sceneRt;
    private RenderTexture2D _vmRt;
    private int _rtWidth;
    private int _rtHeight;

    private CameraView _camera;
    private CameraView _vmCamera;
    private SceneEnvironment _env;

    // Uniform konumlari
    private readonly int _uViewPos, _uSunDir, _uSunColor, _uSkyAmb, _uGroundAmb, _uFogColor, _uFogDensity, _uExposure;
    private readonly int _uShadowsOn, _uShadowTexel, _uShadowBias, _uLightVP, _uNightGlow, _uLightCount, _uLightPos, _uLightColor;
    private readonly int _uEmissive, _uSpecular, _uUnlit, _uNoFog, _uTime, _uWind;
    private readonly int _sZenith, _sHorizon, _sGround, _sSunDir, _sSunDisc, _sMoonDir, _sCloudColor, _sCloudAmount, _sStars, _sTime;
    private readonly int _wViewPos, _wSunDir, _wSunColor, _wSkyAmb, _wHorizon, _wFogColor, _wFogDensity, _wTime, _wNightGlow;
    private readonly int _pResolution, _pFxaa, _pBrightness, _pVignette, _pFade, _pTired, _pSaturation;

    // Son yazilan cizim-basi degerler (gereksiz uniform yuklemesini onler)
    private Vector3 _lastEmissive = new(-1);
    private float _lastSpecular = -1;
    private int _lastUnlit = -1;
    private int _lastNoFog = -1;
    private float _lastWind = -1;

    public int ShadowQuality { get; private set; } = -1;
    public float RenderScale { get; set; } = 1f;
    public bool Fxaa { get; set; } = true;
    public float Brightness { get; set; } = 1f;
    public float Fade { get; set; }
    public float Tired { get; set; }
    public float Saturation { get; set; } = 1.05f;

    public int DrawCalls { get; private set; }
    public int ShadowCalls { get; private set; }

    public Renderer(Texture2D softDot, Texture2D waterRipples)
    {
        LitShader = LoadShader(ShaderSources.LitVertex, ShaderSources.LitFragment, "lit");
        _depthShader = LoadShader(ShaderSources.DepthVertex, ShaderSources.DepthFragment, "depth");
        _skyShader = LoadShader(ShaderSources.SkyVertex, ShaderSources.SkyFragment, "sky");
        _waterShader = LoadShader(ShaderSources.WaterVertex, ShaderSources.WaterFragment, "water");
        _postShader = LoadShader(ShaderSources.PostVertex, ShaderSources.PostFragment, "post");

        unsafe
        {
            LitShader.Locs[(int)ShaderLocationIndex.MapBrdf] = Raylib.GetShaderLocation(LitShader, "shadowMap");
        }

        Materials = new MaterialLib(LitShader);
        Particles = new ParticleSystem(softDot);

        int L(string n) => Raylib.GetShaderLocation(LitShader, n);
        _uViewPos = L("viewPos");
        _uSunDir = L("sunDir");
        _uSunColor = L("sunColor");
        _uSkyAmb = L("skyAmbient");
        _uGroundAmb = L("groundAmbient");
        _uFogColor = L("fogColor");
        _uFogDensity = L("fogDensity");
        _uExposure = L("exposure");
        _uShadowsOn = L("shadowsOn");
        _uShadowTexel = L("shadowTexel");
        _uShadowBias = L("shadowBias");
        _uLightVP = L("lightVP");
        _uNightGlow = L("nightGlow");
        _uLightCount = L("lightCount");
        _uLightPos = L("lightPosRange");
        _uLightColor = L("lightColor");
        _uEmissive = L("emissive");
        _uSpecular = L("specular");
        _uUnlit = L("unlit");
        _uNoFog = L("noFog");
        _uTime = L("time");
        _uWind = L("wind");

        int S(string n) => Raylib.GetShaderLocation(_skyShader, n);
        _sZenith = S("zenithColor");
        _sHorizon = S("horizonColor");
        _sGround = S("groundColor");
        _sSunDir = S("sunDir");
        _sSunDisc = S("sunDiscColor");
        _sMoonDir = S("moonDir");
        _sCloudColor = S("cloudColor");
        _sCloudAmount = S("cloudAmount");
        _sStars = S("starAmount");
        _sTime = S("time");

        int W(string n) => Raylib.GetShaderLocation(_waterShader, n);
        _wViewPos = W("viewPos");
        _wSunDir = W("sunDir");
        _wSunColor = W("sunColor");
        _wSkyAmb = W("skyAmbient");
        _wHorizon = W("horizonColor");
        _wFogColor = W("fogColor");
        _wFogDensity = W("fogDensity");
        _wTime = W("time");
        _wNightGlow = W("nightGlow");

        int P(string n) => Raylib.GetShaderLocation(_postShader, n);
        _pResolution = P("resolution");
        _pFxaa = P("fxaaOn");
        _pBrightness = P("brightness");
        _pVignette = P("vignette");
        _pFade = P("fade");
        _pTired = P("tired");
        _pSaturation = P("saturation");

        _depthMaterial = Raylib.LoadMaterialDefault();
        _depthMaterial.Shader = _depthShader;
        _skyMaterial = Raylib.LoadMaterialDefault();
        _skyMaterial.Shader = _skyShader;
        _waterMaterial = Raylib.LoadMaterialDefault();
        _waterMaterial.Shader = _waterShader;
        unsafe
        {
            _waterMaterial.Maps[(int)MaterialMapIndex.Albedo].Texture = waterRipples;
        }

        var sky = new MeshData();
        Shapes.Box(sky, Matrix4x4.Identity, new Vector3(2f), Color.White);
        _skyMesh = sky.Upload();
    }

    private static Shader LoadShader(string vs, string fs, string name)
    {
        var s = Raylib.LoadShaderFromMemory(vs, fs);
        if (s.Id == 0 || s.Id == Rlgl.GetShaderIdDefault())
        {
            throw new InvalidOperationException($"'{name}' shader'i derlenemedi (OpenGL 3.3 gerekli)");
        }

        return s;
    }

    /// <summary>Golge kalitesi: 0 kapali, 1=1024, 2=2048, 3=4096.</summary>
    public void SetShadowQuality(int quality)
    {
        if (quality == ShadowQuality)
        {
            return;
        }

        ShadowQuality = quality;
        _shadow?.Dispose();
        _shadow = null;
        if (quality > 0)
        {
            var size = quality switch { 1 => 1024, 2 => 2048, _ => 4096 };
            try
            {
                // Yari genislik: yakin golgeler keskin olsun diye dar tutuluyor (2048 doku / 56 m = 2,7 cm).
                _shadow = new ShadowMap(size, quality switch { 1 => 24f, 2 => 28f, _ => 36f });
            }
            catch (Exception ex)
            {
                Log.Warn($"golge haritasi olusturulamadi, golgeler kapali: {ex.Message}");
                _shadow = null;
            }
        }

        Materials.SetShadowTexture(_shadow?.Depth ?? default);
    }

    public void SetStaticScene(StaticScene? scene) => _static = scene;

    public void SetWater(Mesh mesh, Matrix4x4 world) => _water = (mesh, world);

    private readonly List<RenderModel> _release = new();

    /// <summary>
    /// Modeli GPU'dan bosaltmak uzere siraya koyar. Bu karede cizim kuyrugunda
    /// olabilecegi icin silme bir sonraki Begin'de yapilir.
    /// </summary>
    public void Release(RenderModel model) => _release.Add(model);

    public void Begin(in CameraView camera, in SceneEnvironment env)
    {
        foreach (var m in _release)
        {
            foreach (var part in m.Parts)
            {
                Raylib.UnloadMesh(part.Mesh);
            }

            m.Parts.Clear();
        }

        _release.Clear();
        _camera = camera;
        _vmCamera = camera;
        _vmCamera.Near = 0.01f;
        _vmCamera.Far = 10f;
        _vmCamera.FovDegrees = 62f;
        _env = env;
        _opaque.Clear();
        _transparent.Clear();
        _viewModel.Clear();
        _lights.Clear();
    }

    public void AddLight(in PointLight light) => _lights.Add(light);

    public void Submit(Mesh mesh, int material, in Matrix4x4 world, Bounds localBounds, Color tint,
        DrawFlags flags = DrawFlags.None, Vector3 emissive = default)
    {
        var item = new DrawItem
        {
            Mesh = mesh,
            Material = material,
            World = world,
            Tint = tint,
            Emissive = emissive,
            Flags = flags,
            Bounds = localBounds.Transform(world),
        };

        if ((flags & DrawFlags.ViewModel) != 0)
        {
            _viewModel.Add(item);
        }
        else if ((flags & DrawFlags.Transparent) != 0 || Materials[material].Transparent || tint.A < 255)
        {
            item.Distance = Vector3.DistanceSquared(item.Bounds.Center, _camera.Position);
            _transparent.Add(item);
        }
        else
        {
            _opaque.Add(item);
        }
    }

    public void Submit(RenderModel model, in Matrix4x4 world, Color tint, DrawFlags flags = DrawFlags.None,
        Vector3 emissive = default)
    {
        foreach (var part in model.Parts)
        {
            Submit(part.Mesh, part.Material, world, part.Bounds, tint, flags, emissive);
        }
    }

    /// <summary>Tum gecisleri calistirir; sonuc ekrana son islemle basilir.</summary>
    public void Render(int screenWidth, int screenHeight)
    {
        EnsureTargets(screenWidth, screenHeight);
        DrawCalls = 0;
        ShadowCalls = 0;

        var aspect = _rtWidth / (float)Math.Max(1, _rtHeight);
        _camera.Aspect = aspect;
        _vmCamera.Aspect = aspect;
        var frustum = Frustum.FromViewProjection(_camera.ViewProjection);

        if (_shadow is not null && _env.SunDirection.Y > 0.02f)
        {
            ShadowPass();
        }

        SetFrameUniforms();

        // ── Ana gecis ──────────────────────────────────────────────────
        Raylib.BeginTextureMode(_sceneRt);
        Raylib.ClearBackground(_env.HorizonColor.ToColor());
        Raylib.BeginMode3D(DummyCamera());
        Rlgl.SetMatrixProjection(Gfx.ToRay(_camera.Projection));
        Rlgl.SetMatrixModelView(Gfx.ToRay(_camera.View));

        DrawSky();

        if (_static is not null)
        {
            foreach (var chunk in _static.Chunks)
            {
                if (frustum.Intersects(chunk.Bounds))
                {
                    DrawOne(chunk.Mesh, chunk.Material, Matrix4x4.Identity, Materials[chunk.Material].Tint, default);
                }
            }
        }

        foreach (var d in _opaque)
        {
            if ((d.Flags & DrawFlags.ShadowOnly) == 0 && frustum.Intersects(d.Bounds))
            {
                DrawOne(d.Mesh, d.Material, d.World, d.Tint, d.Emissive);
            }
        }

        DrawWater();

        _transparent.Sort((a, b) => b.Distance.CompareTo(a.Distance));
        Rlgl.DisableDepthMask();
        foreach (var d in _transparent)
        {
            if (frustum.Intersects(d.Bounds))
            {
                DrawOne(d.Mesh, d.Material, d.World, d.Tint, d.Emissive);
            }
        }

        Rlgl.EnableDepthMask();

        Particles.Draw(_camera);
        Raylib.EndMode3D();
        Raylib.EndTextureMode();

        // ── El modeli ──────────────────────────────────────────────────
        if (_viewModel.Count > 0)
        {
            Raylib.BeginTextureMode(_vmRt);
            Raylib.ClearBackground(new Color(0, 0, 0, 0));
            Raylib.BeginMode3D(DummyCamera());
            Rlgl.SetMatrixProjection(Gfx.ToRay(_vmCamera.Projection));
            Rlgl.SetMatrixModelView(Gfx.ToRay(_vmCamera.View));
            SetInt(LitShader, _uNoFog, 1);
            _lastNoFog = 1;
            foreach (var d in _viewModel)
            {
                DrawOne(d.Mesh, d.Material, d.World, d.Tint, d.Emissive, forceNoFog: true);
            }

            Raylib.EndMode3D();
            Raylib.EndTextureMode();

            // El modelini sahnenin ustune bindir (ayni cozunurluk).
            Raylib.BeginTextureMode(_sceneRt);
            var src = new Rectangle(0, 0, _rtWidth, -_rtHeight);
            Raylib.DrawTexturePro(_vmRt.Texture, src, new Rectangle(0, 0, _rtWidth, _rtHeight), Vector2.Zero, 0, Color.White);
            Raylib.EndTextureMode();
        }

        // ── Son islem -> ekran ────────────────────────────────────────
        SetVec2(_postShader, _pResolution, new Vector2(_rtWidth, _rtHeight));
        SetInt(_postShader, _pFxaa, Fxaa ? 1 : 0);
        SetFloat(_postShader, _pBrightness, Brightness);
        SetFloat(_postShader, _pVignette, 0.55f);
        SetFloat(_postShader, _pFade, Math.Clamp(Fade, 0f, 1f));
        SetFloat(_postShader, _pTired, Math.Clamp(Tired, 0f, 1f));
        SetFloat(_postShader, _pSaturation, Saturation);
        Raylib.BeginShaderMode(_postShader);
        Raylib.DrawTexturePro(_sceneRt.Texture, new Rectangle(0, 0, _rtWidth, -_rtHeight),
            new Rectangle(0, 0, screenWidth, screenHeight), Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();
    }

    private static Camera3D DummyCamera() => new()
    {
        Position = new Vector3(0, 0, 1),
        Target = Vector3.Zero,
        Up = Vector3.UnitY,
        FovY = 60,
        Projection = CameraProjection.Perspective,
    };

    private void EnsureTargets(int screenW, int screenH)
    {
        var w = Math.Max(64, (int)(screenW * RenderScale));
        var h = Math.Max(64, (int)(screenH * RenderScale));
        if (w == _rtWidth && h == _rtHeight && _sceneRt.Id != 0)
        {
            return;
        }

        if (_sceneRt.Id != 0)
        {
            Raylib.UnloadRenderTexture(_sceneRt);
            Raylib.UnloadRenderTexture(_vmRt);
        }

        _sceneRt = Raylib.LoadRenderTexture(w, h);
        _vmRt = Raylib.LoadRenderTexture(w, h);
        Raylib.SetTextureFilter(_sceneRt.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(_vmRt.Texture, TextureFilter.Bilinear);
        _rtWidth = w;
        _rtHeight = h;
    }

    private void ShadowPass()
    {
        var shadow = _shadow!;
        var focus = _camera.Position + new Vector3(_camera.Forward.X, 0, _camera.Forward.Z) * (shadow.Extent * 0.35f);
        shadow.Fit(focus, _env.SunDirection);
        var lightFrustum = Frustum.FromViewProjection(shadow.LightViewProjection);

        Rlgl.EnableFramebuffer(shadow.FramebufferId);
        Rlgl.Viewport(0, 0, shadow.Size, shadow.Size);
        Rlgl.ClearScreenBuffers();
        Raylib.BeginMode3D(DummyCamera());
        Rlgl.SetMatrixProjection(Gfx.ToRay(shadow.LightProjection));
        Rlgl.SetMatrixModelView(Gfx.ToRay(shadow.LightView));
        Rlgl.DisableBackfaceCulling();

        if (_static is not null)
        {
            foreach (var chunk in _static.Chunks)
            {
                if (Materials[chunk.Material].CastShadow && lightFrustum.Intersects(chunk.Bounds))
                {
                    Raylib.DrawMesh(chunk.Mesh, _depthMaterial, Matrix4x4.Identity);
                    ShadowCalls++;
                }
            }
        }

        foreach (var d in _opaque)
        {
            if ((d.Flags & DrawFlags.NoShadow) == 0 && Materials[d.Material].CastShadow && lightFrustum.Intersects(d.Bounds))
            {
                Raylib.DrawMesh(d.Mesh, _depthMaterial, Gfx.ToRay(d.World));
                ShadowCalls++;
            }
        }

        Rlgl.EnableBackfaceCulling();
        Raylib.EndMode3D();
        Rlgl.DisableFramebuffer();
        Rlgl.Viewport(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    }

    private void SetFrameUniforms()
    {
        var lit = LitShader;
        SetVec3(lit, _uViewPos, _camera.Position);
        SetVec3(lit, _uSunDir, Vector3.Normalize(_env.SunDirection));
        SetVec3(lit, _uSunColor, _env.SunColor);
        SetVec3(lit, _uSkyAmb, _env.SkyAmbient);
        SetVec3(lit, _uGroundAmb, _env.GroundAmbient);
        SetVec3(lit, _uFogColor, _env.HorizonColor);
        SetFloat(lit, _uFogDensity, _env.FogDensity);
        SetFloat(lit, _uExposure, _env.Exposure);
        SetFloat(lit, _uNightGlow, _env.NightGlow);
        SetFloat(lit, _uTime, _env.Time);

        var shadowsOn = _shadow is not null && _env.SunDirection.Y > 0.02f;
        SetInt(lit, _uShadowsOn, shadowsOn ? 1 : 0);
        if (_shadow is not null)
        {
            SetFloat(lit, _uShadowTexel, 1f / _shadow.Size);
            SetFloat(lit, _uShadowBias, 0.0006f * (2048f / _shadow.Size) + 0.0004f);
            Raylib.SetShaderValueMatrix(lit, _uLightVP, Gfx.ToRay(_shadow.LightViewProjection));
        }

        // En yakin 8 isik (menzil hesaba katilarak).
        var camPos = _camera.Position;
        _lights.Sort((a, b) =>
            (Vector3.Distance(a.Position, camPos) - a.Range).CompareTo(Vector3.Distance(b.Position, camPos) - b.Range));
        var count = Math.Min(_lights.Count, ShaderSources.MaxLights);
        Span<float> pos = stackalloc float[ShaderSources.MaxLights * 4];
        Span<float> col = stackalloc float[ShaderSources.MaxLights * 3];
        for (var i = 0; i < count; i++)
        {
            var l = _lights[i];
            pos[i * 4 + 0] = l.Position.X;
            pos[i * 4 + 1] = l.Position.Y;
            pos[i * 4 + 2] = l.Position.Z;
            pos[i * 4 + 3] = l.Range;
            col[i * 3 + 0] = l.Color.X;
            col[i * 3 + 1] = l.Color.Y;
            col[i * 3 + 2] = l.Color.Z;
        }

        SetInt(lit, _uLightCount, count);
        if (count > 0)
        {
            Raylib.SetShaderValueV(lit, _uLightPos, pos, ShaderUniformDataType.Vec4, count);
            Raylib.SetShaderValueV(lit, _uLightColor, col, ShaderUniformDataType.Vec3, count);
        }

        // Cizim-basi onbellegi sifirla: baska bir shader araya girmis olabilir.
        _lastEmissive = new Vector3(-1);
        _lastSpecular = -1;
        _lastUnlit = -1;
        _lastNoFog = -1;
        _lastWind = -1;

        var sky = _skyShader;
        SetVec3(sky, _sZenith, _env.ZenithColor);
        SetVec3(sky, _sHorizon, _env.HorizonColor);
        SetVec3(sky, _sGround, _env.GroundColor);
        SetVec3(sky, _sSunDir, Vector3.Normalize(_env.SunDirection));
        SetVec3(sky, _sSunDisc, _env.SunDiscColor);
        SetVec3(sky, _sMoonDir, _env.MoonDirection);
        SetVec3(sky, _sCloudColor, _env.CloudColor);
        SetFloat(sky, _sCloudAmount, _env.CloudAmount);
        SetFloat(sky, _sStars, _env.StarAmount);
        SetFloat(sky, _sTime, _env.Time);

        var w = _waterShader;
        SetVec3(w, _wViewPos, _camera.Position);
        SetVec3(w, _wSunDir, Vector3.Normalize(_env.SunDirection));
        SetVec3(w, _wSunColor, _env.SunColor);
        SetVec3(w, _wSkyAmb, _env.SkyAmbient);
        SetVec3(w, _wHorizon, _env.HorizonColor);
        SetVec3(w, _wFogColor, _env.HorizonColor);
        SetFloat(w, _wFogDensity, _env.FogDensity);
        SetFloat(w, _wTime, _env.Time);
        SetFloat(w, _wNightGlow, _env.NightGlow);
    }

    private void DrawSky()
    {
        Rlgl.DisableDepthMask();
        Rlgl.DisableBackfaceCulling();
        Raylib.DrawMesh(_skyMesh, _skyMaterial, Matrix4x4.Identity);
        Rlgl.EnableBackfaceCulling();
        Rlgl.EnableDepthMask();
        DrawCalls++;
    }

    private void DrawWater()
    {
        if (_water is not { } w)
        {
            return;
        }

        Raylib.DrawMesh(w.Mesh, _waterMaterial, Gfx.ToRay(w.World));
        DrawCalls++;
    }

    private unsafe void DrawOne(Mesh mesh, int materialId, in Matrix4x4 world, Color tint, Vector3 emissive, bool forceNoFog = false)
    {
        var def = Materials[materialId];
        var lit = LitShader;
        var e = def.Emissive + emissive;
        if (e != _lastEmissive)
        {
            SetVec3(lit, _uEmissive, e);
            _lastEmissive = e;
        }

        if (def.Specular != _lastSpecular)
        {
            SetFloat(lit, _uSpecular, def.Specular);
            _lastSpecular = def.Specular;
        }

        // bit0: isiksiz, bit1: seffaf malzeme (kose alfasi opaklik olarak kullanilir)
        var unlit = (def.Unlit ? 1 : 0) | (def.Transparent ? 2 : 0);
        if (unlit != _lastUnlit)
        {
            SetInt(lit, _uUnlit, unlit);
            _lastUnlit = unlit;
        }

        var noFog = forceNoFog || def.NoFog ? 1 : 0;
        if (noFog != _lastNoFog)
        {
            SetInt(lit, _uNoFog, noFog);
            _lastNoFog = noFog;
        }

        if (def.Wind != _lastWind)
        {
            SetFloat(lit, _uWind, def.Wind);
            _lastWind = def.Wind;
        }

        var mat = def.Native;
        mat.Maps[(int)MaterialMapIndex.Albedo].Color = tint;
        if (def.DoubleSided)
        {
            Rlgl.DisableBackfaceCulling();
        }

        Raylib.DrawMesh(mesh, mat, Gfx.ToRay(world));

        if (def.DoubleSided)
        {
            Rlgl.EnableBackfaceCulling();
        }

        DrawCalls++;
    }

    private static void SetFloat(Shader s, int loc, float v) => Raylib.SetShaderValue(s, loc, v, ShaderUniformDataType.Float);
    private static void SetInt(Shader s, int loc, int v) => Raylib.SetShaderValue(s, loc, v, ShaderUniformDataType.Int);
    private static void SetVec2(Shader s, int loc, Vector2 v) => Raylib.SetShaderValue(s, loc, v, ShaderUniformDataType.Vec2);
    private static void SetVec3(Shader s, int loc, Vector3 v) => Raylib.SetShaderValue(s, loc, v, ShaderUniformDataType.Vec3);

    public void Dispose()
    {
        _shadow?.Dispose();
        if (_sceneRt.Id != 0)
        {
            Raylib.UnloadRenderTexture(_sceneRt);
            Raylib.UnloadRenderTexture(_vmRt);
        }
    }
}
