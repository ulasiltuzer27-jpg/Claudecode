using System.Numerics;
using Silk.NET.OpenGL;
using Starfall.Core;

namespace Starfall.Render;

public sealed class Camera
{
    public Vector3 Position = new(0, 10, 10);
    public Vector3 Target = Vector3.Zero;
    public float Fov = 60f;
    public float Near = 0.1f, Far = 2400f;
    public float Aspect = 16f / 9f;
    public Matrix4x4 View, Proj;

    public void Update()
    {
        View = Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY);
        Proj = MathX.PerspectiveGL(Fov * MathX.Pi / 180f, Aspect, Near, Far);
    }

    public Vector3 Forward => Vector3.Normalize(Target - Position);
}

public sealed class Quality
{
    public string Name = "medium";
    public float RenderScale = 1f;
    public int ShadowSize = 2048;
    public float ShadowRange = 40;
    public bool Bloom = true;
    public bool Fxaa = true;
    public float Grass = 0.65f;
    public float Flowers = 0.8f;
    public float Far = 520;

    public static Quality Get(string name) => name switch
    {
        "low" => new Quality { Name = "low", RenderScale = 0.75f, ShadowSize = 1024, ShadowRange = 30, Bloom = false, Fxaa = false, Grass = 0.3f, Flowers = 0.5f, Far = 380 },
        "high" => new Quality { Name = "high", RenderScale = 1f, ShadowSize = 4096, ShadowRange = 48, Bloom = true, Fxaa = true, Grass = 1f, Flowers = 1f, Far = 620 },
        _ => new Quality(),
    };
}

/// <summary>Su yuzeyi: deniz, gol veya donmus gol (uIce).</summary>
public sealed class WaterSurface
{
    public MeshData Mesh = null!;
    public Vector3 Position;
    public float Level;
    public float Amp = 0.12f;
    public Vector3 Shallow = MathX.Hex("#3fd0c8");
    public Vector3 Deep = MathX.Hex("#1b5fa8");
    public float Ice;
}

/// <summary>Su ve cimen shader'larinin okudugu kuresel yukseklik/maske haritasi.</summary>
public sealed class GroundMaps
{
    public float OriginX, OriginZ, Size;
    public int Res;
    public float[] Heights = Array.Empty<float>();
    public byte[] Mask = Array.Empty<byte>(); // RGBA: yogunluk, sarilik, koyuluk, "yesillik" (kar=0)
    internal Texture? HeightTex, MaskTex;
}

public sealed class GrassParams
{
    public bool Enabled = true;
    public int Count = 30000;
    public float Size = 64;
    public Vector3 Center;
    public Vector3 Player = new(0, -100, 0);
    public float DensityScale = 1;
}

public sealed class Billboard
{
    public Vector3 Position;
    public Vector2 Size = new(1, 1);
    public Vector4 Color = Vector4.One;
    public Texture? Tex;      // null = yumusak isilti
    public CpuImage? Image;   // Tex yoksa: CPU goruntusu (ilk cizimde yuklenir)
    public bool Additive = true;
    public bool Visible = true;
    public bool DepthTest = true;
}

public sealed class Beam
{
    public Matrix4x4 Transform;
    public float Intensity;
    public bool Visible;
}

public sealed class PostSettings
{
    public float Exposure = 1f;
    public float Vignette = 0.28f;
    public float Saturation = 1.08f;
    public float Contrast = 1.03f;
    public int Filter;
    public float Fade;
    public Vector3 FadeColor = new(0.043f, 0.063f, 0.125f);
    public float BloomStrength = 0.32f;
    public float BloomRadius = 0.55f;
    public float BloomThreshold = 0.88f;
}

/// <summary>Bir karede cizilecek her sey.</summary>
public sealed class RenderEnv
{
    public Scene Scene = new();
    public SkyState Sky = new();
    public Camera Camera = new();
    public Vector3 ShadowFocus;
    public readonly List<WaterSurface> Waters = new();
    public GroundMaps? Maps;
    public GrassParams Grass = new();
    public ParticlePool Additive = new(4000, true);
    public ParticlePool Normal = new(2000, false);
    public readonly List<Billboard> Billboards = new();
    public readonly List<Beam> Beams = new();
    public MeshData? BeamMesh;
    public PostSettings Post = new();
    public UI.UiDrawList? Ui;
    /// <summary>Kalite ayarindan gelen cicek yogunlugu (dunya kurulurken okunur).</summary>
    public float QualityFlowers = 0.8f;
}

public sealed unsafe class Renderer : IDisposable
{
    private readonly GL _gl;
    public Quality Q = Quality.Get("medium");
    private ShaderProgram _lit = null!, _litInst = null!, _shadow = null!, _shadowInst = null!;
    private ShaderProgram _sky = null!, _water = null!, _grass = null!, _particles = null!, _billboard = null!, _beam = null!;
    private ShaderProgram _bright = null!, _blur = null!, _composite = null!, _fxaa = null!, _ui = null!;
    private RenderTarget _hdr = null!, _ldr = null!, _shadowRt = null!;
    private RenderTarget[] _bloomA = null!, _bloomB = null!;
    private FullscreenTri _tri = null!;
    private GpuMesh _skyMesh = null!;
    private uint _grassVao, _grassVbo, _grassInstVbo;
    private int _grassBladeVerts;
    private uint _partVao, _partVbo;
    private uint _bbVao, _bbVbo;
    private uint _uiVao, _uiVbo;
    private int _uiCapacity;
    private Texture _white = null!;
    public int Width = 1280, Height = 720;
    public int DrawCalls;
    public bool ShadowsOn = true;
    public int Debug;
    private Matrix4x4 _lightVP;

    public Renderer(GL gl)
    {
        _gl = gl;
        Gfx.GL = gl;
        Init();
    }

    private void Init()
    {
        _lit = new ShaderProgram("lit", "lit.vert", "lit.frag");
        _litInst = new ShaderProgram("lit_inst", "lit.vert", "lit.frag", "INSTANCED");
        _shadow = new ShaderProgram("shadow", "shadow.vert", "shadow.frag");
        _shadowInst = new ShaderProgram("shadow_inst", "shadow.vert", "shadow.frag", "INSTANCED");
        _sky = new ShaderProgram("sky", "sky.vert", "sky.frag");
        _water = new ShaderProgram("water", "water.vert", "water.frag");
        _grass = new ShaderProgram("grass", "grass.vert", "grass.frag");
        _particles = new ShaderProgram("particles", "particles.vert", "particles.frag");
        _billboard = new ShaderProgram("billboard", "billboard.vert", "billboard.frag");
        _beam = new ShaderProgram("beam", "beam.vert", "beam.frag");
        _bright = new ShaderProgram("bright", "fullscreen.vert", "bright.frag");
        _blur = new ShaderProgram("blur", "fullscreen.vert", "blur.frag");
        _composite = new ShaderProgram("composite", "fullscreen.vert", "composite.frag");
        _fxaa = new ShaderProgram("fxaa", "fullscreen.vert", "fxaa.frag");
        _ui = new ShaderProgram("ui", "ui.vert", "ui.frag");
        _tri = new FullscreenTri();
        _skyMesh = new GpuMesh(MeshData.From(Geo.SphereGeo(1, 32, 16), false));
        _white = Texture.Rgba8(1, 1, new byte[] { 255, 255, 255, 255 });
        CreateTargets();

        // cimen yapragi + ornek tamponu
        float w = 0.07f;
        float[] blade =
        {
            -w, 0, 0, w, 0, 0, -w * 0.6f, 0.45f, 0,
            w, 0, 0, w * 0.6f, 0.45f, 0, -w * 0.6f, 0.45f, 0,
            -w * 0.6f, 0.45f, 0, w * 0.6f, 0.45f, 0, 0, 1, 0,
        };
        _grassBladeVerts = 9;
        _grassVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_grassVao);
        _grassVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _grassVbo);
        fixed (float* p = blade) _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(blade.Length * 4), p, BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, (void*)0);
        var offs = new float[60000 * 4];
        var rng = new Rng(4242);
        for (int i = 0; i < offs.Length; i++) offs[i] = rng.Next();
        _grassInstVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _grassInstVbo);
        fixed (float* p = offs) _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(offs.Length * 4), p, BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 16, (void*)0);
        _gl.VertexAttribDivisor(1, 1);

        // parcaciklar
        _partVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_partVao);
        _partVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _partVbo);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(6000 * 32), null, BufferUsageARB.DynamicDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 32, (void*)12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, 32, (void*)28);

        // billboard dortgeni
        float[] quad = { -1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1 };
        _bbVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_bbVao);
        _bbVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _bbVbo);
        fixed (float* p = quad) _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * 4), p, BufferUsageARB.StaticDraw);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, (void*)0);

        // arayuz
        _uiVao = _gl.GenVertexArray();
        _gl.BindVertexArray(_uiVao);
        _uiVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _uiVbo);
        _uiCapacity = 0;
        uint st = UI.UiDrawList.Stride * 4;
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, st, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, st, (void*)8);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, st, (void*)16);
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, st, (void*)32);
        _gl.BindVertexArray(0);
    }

    private void CreateTargets()
    {
        int w = Math.Max(1, (int)(Width * Q.RenderScale));
        int h = Math.Max(1, (int)(Height * Q.RenderScale));
        _hdr?.Dispose();
        _ldr?.Dispose();
        _hdr = new RenderTarget(w, h, InternalFormat.Rgba16f, true);
        _ldr = new RenderTarget(Width, Height, InternalFormat.Rgba8, false);
        if (_bloomA != null) foreach (var t in _bloomA.Concat(_bloomB)) t.Dispose();
        _bloomA = new RenderTarget[4];
        _bloomB = new RenderTarget[4];
        int bw = w / 2, bh = h / 2;
        for (int i = 0; i < 4; i++)
        {
            _bloomA[i] = new RenderTarget(bw, bh, InternalFormat.Rgba16f, false);
            _bloomB[i] = new RenderTarget(bw, bh, InternalFormat.Rgba16f, false);
            bw = Math.Max(1, bw / 2);
            bh = Math.Max(1, bh / 2);
        }
        _shadowRt?.Dispose();
        _shadowRt = new RenderTarget(Q.ShadowSize, Q.ShadowSize, InternalFormat.Rgba8, true, depthTexture: true, color: false);
    }

    public void SetQuality(Quality q)
    {
        Q = q;
        CreateTargets();
    }

    public void Resize(int w, int h)
    {
        if (w == Width && h == Height) return;
        Width = Math.Max(1, w);
        Height = Math.Max(1, h);
        CreateTargets();
    }

    // ------------------------------------------------------------------
    public void Render(RenderEnv env)
    {
        DrawCalls = 0;
        var cam = env.Camera;
        cam.Aspect = Width / (float)Height;
        cam.Update();
        env.Scene.Root.UpdateWorld(Matrix4x4.Identity);
        EnsureMaps(env.Maps);
        var sky = env.Sky;

        // ---- golge
        float range = Q.ShadowRange;
        float step = range * 2 / Q.ShadowSize;
        var f = env.ShadowFocus;
        var fx = MathF.Round(f.X / step) * step;
        var fz = MathF.Round(f.Z / step) * step;
        var target = new Vector3(fx, f.Y, fz);
        var lpos = target + sky.LightDir * 120f;
        var lview = Matrix4x4.CreateLookAt(lpos, target, MathF.Abs(sky.LightDir.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY);
        _lightVP = lview * MathX.OrthoGL(-range, range, -range, range, 1, 260);
        _shadowRt.Bind();
        _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.Enable(EnableCap.PolygonOffsetFill);
        _gl.PolygonOffset(1.5f, 2f);
        DrawShadowCasters(env, target, range * 1.6f);
        _gl.Disable(EnableCap.PolygonOffsetFill);

        // ---- sahne (HDR)
        _hdr.Bind();
        _gl.ClearColor(sky.Fog.X, sky.Fog.Y, sky.Fog.Z, 1);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        var frustum = new Frustum(cam.View * cam.Proj);
        SetupLit(_lit, env);
        SetupLit(_litInst, env);
        DrawNodes(env, frustum, transparent: false);
        DrawBatches(env, frustum);
        DrawGrass(env);
        DrawWaters(env);
        DrawSky(env);
        // saydamlar
        _gl.Enable(EnableCap.Blend);
        _gl.DepthMask(false);
        DrawNodes(env, frustum, transparent: true);
        DrawBeams(env);
        DrawBillboards(env);
        DrawParticles(env, env.Normal);
        DrawParticles(env, env.Additive);
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);

        // ---- post
        PostProcess(env.Post);
        // ---- arayuz
        if (env.Ui != null) DrawUi(env.Ui);
    }

    private void EnsureMaps(GroundMaps? m)
    {
        if (m == null || m.HeightTex != null) return;
        m.HeightTex = Texture.R32F(m.Res, m.Res, m.Heights);
        m.MaskTex = Texture.Rgba8(m.Res, m.Res, m.Mask);
    }

    private void SetupLit(ShaderProgram p, RenderEnv env)
    {
        var s = env.Sky;
        var cam = env.Camera;
        p.Use();
        p.Set("uView", cam.View);
        p.Set("uProj", cam.Proj);
        p.Set("uTime", s.Time);
        p.Set("uSunDir", s.LightDir);
        p.Set("uSunColor", s.SunColor * s.SunIntensity);
        p.Set("uHemiSky", s.HemiSky * s.HemiIntensity);
        p.Set("uHemiGround", s.HemiGround * s.HemiIntensity);
        p.Set("uFogColor", s.Fog);
        p.Set("uFogNear", Q.Far * 0.22f);
        p.Set("uFogFar", Q.Far);
        p.Set("uLightVP", _lightVP);
        p.Set("uShadowTexel", 1f / Q.ShadowSize);
        p.Set("uShadowsOn", ShadowsOn);
        p.Set("uDebug", Debug);
        _shadowRt.BindDepth(5);
        p.Set("uShadowMap", 5);
        // en yakin 4 nokta isigi
        var lights = env.Scene.Lights.Where(l => l.Intensity > 0.001f)
            .OrderBy(l => Vector3.DistanceSquared(l.Position, cam.Position)).Take(4).ToList();
        p.Set("uPointCount", lights.Count);
        for (int i = 0; i < lights.Count; i++)
        {
            p.Set($"uPointPos[{i}]", lights[i].Position);
            p.Set($"uPointColor[{i}]", lights[i].Color * lights[i].Intensity);
            p.Set($"uPointParams[{i}]", new Vector2(lights[i].Distance, lights[i].Decay));
        }
    }

    private static GpuMesh Gpu(MeshData m) => m.Gpu ??= new GpuMesh(m);

    private void ApplyMaterial(ShaderProgram p, Material m)
    {
        p.Set("uTint", m.Tint);
        p.Set("uEmissive", m.Emissive * m.EmissiveIntensity);
        p.Set("uOpacity", m.Opacity);
        p.Set("uReceiveShadow", m.ReceiveShadow);
        p.Set("uUnlit", m.Unlit);
        p.Set("uFogOn", true);
        p.Set("uSway", m.Sway);
        if (m.DoubleSided) _gl.Disable(EnableCap.CullFace); else _gl.Enable(EnableCap.CullFace);
        if (m.Blend == Blend.Additive) _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        else _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private void Walk(Node n, Action<Node> fn)
    {
        if (!n.Visible) return;
        if (n.Mesh != null) fn(n);
        foreach (var c in n.Children) Walk(c, fn);
    }

    private void DrawNodes(RenderEnv env, Frustum fr, bool transparent)
    {
        _lit.Use();
        Walk(env.Scene.Root, n =>
        {
            var m = n.Material!;
            bool tr = m.Blend != Blend.Opaque;
            if (tr != transparent) return;
            var c = Vector3.Transform(n.Mesh!.Center, n.World);
            float sc = MathF.Max(n.World.M11 * n.World.M11 + n.World.M12 * n.World.M12 + n.World.M13 * n.World.M13,
                MathF.Max(n.World.M21 * n.World.M21 + n.World.M22 * n.World.M22 + n.World.M23 * n.World.M23,
                          n.World.M31 * n.World.M31 + n.World.M32 * n.World.M32 + n.World.M33 * n.World.M33));
            float r = n.Mesh.Radius * MathF.Sqrt(sc);
            if (!fr.Sphere(c, r)) return;
            if (Vector3.Distance(c, env.Camera.Position) - r > Q.Far * 1.05f) return;
            ApplyMaterial(_lit, m);
            _gl.DepthMask(!tr && m.DepthWrite);
            _lit.Set("uModel", n.World);
            Gpu(n.Mesh).Draw();
            DrawCalls++;
        });
        _gl.DepthMask(true);
    }

    private void DrawBatches(RenderEnv env, Frustum fr)
    {
        _litInst.Use();
        foreach (var b in env.Scene.Batches)
        {
            if (!b.Visible || b.Transforms.Count == 0) continue;
            var g = Gpu(b.Mesh);
            if (b.Dirty)
            {
                var data = b.Pack();
                g.UploadInstances(data, b.Transforms.Count);
                b.Dirty = false;
            }
            if (!fr.Box(b.BoundsMin, b.BoundsMax)) continue;
            ApplyMaterial(_litInst, b.Material);
            g.DrawInstanced(b.Transforms.Count);
            DrawCalls++;
        }
    }

    private void DrawShadowCasters(RenderEnv env, Vector3 focus, float radius)
    {
        _shadow.Use();
        _shadow.Set("uLightVP", _lightVP);
        _shadow.Set("uTime", env.Sky.Time);
        Walk(env.Scene.Root, n =>
        {
            var m = n.Material!;
            if (!m.CastShadow || m.Blend != Blend.Opaque) return;
            var c = Vector3.Transform(n.Mesh!.Center, n.World);
            if (MathX.DistXZ(c, focus) - n.Mesh.Radius * 3 > radius && n.Mesh.Radius < 100) return;
            _shadow.Set("uModel", n.World);
            _shadow.Set("uSway", m.Sway);
            Gpu(n.Mesh).Draw();
        });
        _shadowInst.Use();
        _shadowInst.Set("uLightVP", _lightVP);
        _shadowInst.Set("uTime", env.Sky.Time);
        foreach (var b in env.Scene.Batches)
        {
            if (!b.Visible || !b.Material.CastShadow || b.Transforms.Count == 0) continue;
            var g = Gpu(b.Mesh);
            if (b.Dirty)
            {
                g.UploadInstances(b.Pack(), b.Transforms.Count);
                b.Dirty = false;
            }
            _shadowInst.Set("uSway", b.Material.Sway);
            g.DrawInstanced(b.Transforms.Count);
        }
    }

    private void DrawGrass(RenderEnv env)
    {
        var gp = env.Grass;
        var maps = env.Maps;
        if (!gp.Enabled || maps?.HeightTex == null || gp.Count <= 0) return;
        _grass.Use();
        SetupLit(_grass, env);
        _grass.Set("uSize", gp.Size);
        _grass.Set("uCenter", new Vector2(gp.Center.X, gp.Center.Z));
        _grass.Set("uPlayer", gp.Player);
        _grass.Set("uDensityScale", gp.DensityScale);
        _grass.Set("uMap", new Vector4(maps.OriginX, maps.OriginZ, maps.Size, 1f / maps.Res));
        maps.HeightTex.Bind(0);
        _grass.Set("uHeight", 0);
        maps.MaskTex!.Bind(1);
        _grass.Set("uMask", 1);
        _gl.Disable(EnableCap.CullFace);
        _gl.BindVertexArray(_grassVao);
        _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, (uint)_grassBladeVerts, (uint)Math.Min(60000, gp.Count));
        DrawCalls++;
    }

    private static readonly Vector3 SandShore = MathX.Hex("#e3cf98"), IceShore = MathX.Hex("#e6f0f8");

    private void DrawWaters(RenderEnv env)
    {
        var maps = env.Maps;
        if (maps?.HeightTex == null) return;
        var s = env.Sky;
        _water.Use();
        SetupLit(_water, env);
        _water.Set("uMap", new Vector4(maps.OriginX, maps.OriginZ, maps.Size, 1f / maps.Res));
        maps.HeightTex.Bind(0);
        _water.Set("uHeight", 0);
        _water.Set("uNight", s.Night);
        _water.Set("uSkyTop", s.Top);
        _water.Set("uSkyHor", s.Horizon);
        _water.Set("uSunDirW", s.Night > 0.5f ? s.MoonDir : s.SunDir);
        _water.Set("uSunColorW", s.SunColor);
        _water.Set("uCamPos", env.Camera.Position);
        _water.Set("uLight", MathX.Lerp(1f, 0.32f, s.Night) * (0.75f + 0.25f * MathF.Min(1, s.SunIntensity / 2.5f)));
        _gl.Enable(EnableCap.CullFace);
        foreach (var w in env.Waters)
        {
            _water.Set("uModel", Matrix4x4.CreateTranslation(w.Position.X, w.Level, w.Position.Z));
            _water.Set("uLevel", w.Level);
            _water.Set("uAmp", w.Amp);
            _water.Set("uShallow", w.Shallow);
            _water.Set("uDeep", w.Deep);
            _water.Set("uIce", w.Ice);
            _water.Set("uSand", w.Ice > 0.5f ? IceShore : SandShore);
            Gpu(w.Mesh).Draw();
            DrawCalls++;
        }
    }

    private void DrawSky(RenderEnv env)
    {
        var s = env.Sky;
        var cam = env.Camera;
        var rot = cam.View;
        rot.M41 = rot.M42 = rot.M43 = 0;
        _sky.Use();
        _sky.Set("uViewRot", rot);
        _sky.Set("uProj", cam.Proj);
        _sky.Set("uTop", s.Top);
        _sky.Set("uHorizon", s.Horizon);
        _sky.Set("uSunDirSky", s.SunDir);
        _sky.Set("uSunColorSky", s.SunColor);
        _sky.Set("uMoonDir", s.MoonDir);
        _sky.Set("uNight", s.Night);
        _sky.Set("uTime", s.Time);
        _sky.Set("uAurora", s.Aurora);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _skyMesh.Draw();
        _gl.DepthMask(true);
        _gl.DepthFunc(DepthFunction.Less);
        DrawCalls++;
    }

    private void DrawBeams(RenderEnv env)
    {
        if (env.BeamMesh == null) return;
        _beam.Use();
        _beam.Set("uView", env.Camera.View);
        _beam.Set("uProj", env.Camera.Proj);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _gl.Disable(EnableCap.CullFace);
        foreach (var b in env.Beams)
        {
            if (!b.Visible || b.Intensity <= 0) continue;
            _beam.Set("uModel", b.Transform);
            _beam.Set("uIntensity", b.Intensity);
            Gpu(env.BeamMesh).Draw();
        }
    }

    private void DrawBillboards(RenderEnv env)
    {
        _billboard.Use();
        _billboard.Set("uView", env.Camera.View);
        _billboard.Set("uProj", env.Camera.Proj);
        _billboard.Set("uFogColor", env.Sky.Fog);
        _billboard.Set("uFogNear", Q.Far * 0.22f);
        _billboard.Set("uFogFar", Q.Far);
        _gl.Disable(EnableCap.CullFace);
        _gl.BindVertexArray(_bbVao);
        foreach (var b in env.Billboards)
        {
            if (!b.Visible) continue;
            if (b.Additive) _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            else _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            if (b.DepthTest) _gl.Enable(EnableCap.DepthTest); else _gl.Disable(EnableCap.DepthTest);
            _billboard.Set("uCenter", b.Position);
            _billboard.Set("uSize", b.Size * 0.5f);
            _billboard.Set("uColor", b.Color);
            var tex = b.Tex ?? (b.Image != null ? b.Image.Gpu ??= Texture.Rgba8(b.Image.Width, b.Image.Height, b.Image.Rgba, true, true) : null);
            _billboard.Set("uMode", tex == null ? 1 : 0);
            (tex ?? _white).Bind(0);
            _billboard.Set("uTex", 0);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        _gl.Enable(EnableCap.DepthTest);
    }

    private void DrawParticles(RenderEnv env, ParticlePool pool)
    {
        if (pool.Count == 0) return;
        _particles.Use();
        _particles.Set("uView", env.Camera.View);
        _particles.Set("uProj", env.Camera.Proj);
        _particles.Set("uScale", _hdr.Height * 0.6f * (60f / env.Camera.Fov));
        _particles.Set("uFogColor", env.Sky.Fog);
        _particles.Set("uFogNear", Q.Far * 0.22f);
        _particles.Set("uFogFar", Q.Far);
        _particles.Set("uAdditive", pool.Additive);
        if (pool.Additive) _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        else _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Enable(EnableCap.ProgramPointSize);
        _gl.BindVertexArray(_partVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _partVbo);
        int n = Math.Min(pool.Count, 6000);
        fixed (float* p = pool.Packed) _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(n * 32), p);
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)n);
        DrawCalls++;
    }

    private void PostProcess(PostSettings post)
    {
        // bloom: parlak gecis + 4 seviyeli bulaniklik
        if (Q.Bloom)
        {
            _bloomA[0].Bind();
            _bright.Use();
            _hdr.BindColor(0);
            _bright.Set("uTex", 0);
            _bright.Set("uThreshold", post.BloomThreshold);
            _tri.Draw();
            int[] radii = { 3, 5, 7, 9 };
            for (int i = 0; i < 4; i++)
            {
                if (i > 0)
                {
                    // bir onceki seviyeyi kucult
                    _bloomA[i].Bind();
                    _blur.Use();
                    _bloomA[i - 1].BindColor(0);
                    _blur.Set("uTex", 0);
                    _blur.Set("uDir", Vector2.Zero);
                    _blur.Set("uRadius", 0);
                    _tri.Draw();
                }
                _blur.Use();
                _bloomB[i].Bind();
                _bloomA[i].BindColor(0);
                _blur.Set("uTex", 0);
                _blur.Set("uDir", new Vector2(1f / _bloomA[i].Width, 0));
                _blur.Set("uRadius", radii[i]);
                _tri.Draw();
                _bloomA[i].Bind();
                _bloomB[i].BindColor(0);
                _blur.Set("uDir", new Vector2(0, 1f / _bloomA[i].Height));
                _tri.Draw();
            }
        }
        _ldr.Bind();
        _composite.Use();
        _hdr.BindColor(0);
        _composite.Set("uScene", 0);
        for (int i = 0; i < 4; i++)
        {
            _bloomA[i].BindColor(1 + i);
            _composite.Set($"uBloom{i}", 1 + i);
        }
        _composite.Set("uBloomOn", Q.Bloom);
        _composite.Set("uBloomStrength", post.BloomStrength);
        _composite.Set("uBloomRadius", post.BloomRadius);
        _composite.Set("uExposure", post.Exposure);
        _composite.Set("uVignette", post.Vignette);
        _composite.Set("uSaturation", post.Saturation);
        _composite.Set("uContrast", post.Contrast);
        _composite.Set("uFilter", post.Filter);
        _composite.Set("uFade", post.Fade);
        _composite.Set("uFadeColor", post.FadeColor);
        _tri.Draw();

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        _fxaa.Use();
        _ldr.BindColor(0);
        _fxaa.Set("uTex", 0);
        _fxaa.Set("uInv", new Vector2(1f / Width, 1f / Height));
        _fxaa.Set("uOn", Q.Fxaa);
        _tri.Draw();
    }

    private void DrawUi(UI.UiDrawList list)
    {
        if (list.Count == 0) return;
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _ui.Use();
        _ui.Set("uScreen", new Vector2(Width, Height));
        _gl.BindVertexArray(_uiVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _uiVbo);
        int floats = list.Count * UI.UiDrawList.Stride;
        if (floats > _uiCapacity)
        {
            _uiCapacity = Math.Max(floats, _uiCapacity * 2);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_uiCapacity * 4), null, BufferUsageARB.DynamicDraw);
        }
        fixed (float* p = list.Data) _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(floats * 4), p);
        list.Font?.Bind(_gl, 1);
        _ui.Set("uFont", 1);
        _ui.Set("uTex", 0);
        foreach (var cmd in list.Commands)
        {
            (cmd.Texture ?? _white).Bind(0);
            if (cmd.Scissor is { } sc)
            {
                _gl.Enable(EnableCap.ScissorTest);
                _gl.Scissor(sc.X, Height - sc.Y - sc.H, (uint)Math.Max(0, sc.W), (uint)Math.Max(0, sc.H));
            }
            else _gl.Disable(EnableCap.ScissorTest);
            _gl.DrawArrays(PrimitiveType.Triangles, cmd.Start, (uint)cmd.Count);
            DrawCalls++;
        }
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.Blend);
    }

    /// <summary>Ekrandaki son kareyi RGBA olarak oku (ekran goruntusu).</summary>
    public byte[] ReadPixels(out int w, out int h)
    {
        w = Width; h = Height;
        var data = new byte[w * h * 4];
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        fixed (byte* p = data) _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        // GL alttan baslar: dikey cevir
        var flipped = new byte[data.Length];
        int row = w * 4;
        for (int y = 0; y < h; y++) System.Buffer.BlockCopy(data, y * row, flipped, (h - 1 - y) * row, row);
        for (int i = 3; i < flipped.Length; i += 4) flipped[i] = 255;
        return flipped;
    }

    public void Dispose()
    {
        _hdr.Dispose();
        _ldr.Dispose();
        _shadowRt.Dispose();
    }
}

/// <summary>Gorus konisi (frustum) kirpma.</summary>
public readonly struct Frustum
{
    private readonly Vector4[] _p;

    public Frustum(Matrix4x4 vp)
    {
        // satir-vektor kurali: duzlemler sutunlardan
        var c1 = new Vector4(vp.M11, vp.M21, vp.M31, vp.M41);
        var c2 = new Vector4(vp.M12, vp.M22, vp.M32, vp.M42);
        var c3 = new Vector4(vp.M13, vp.M23, vp.M33, vp.M43);
        var c4 = new Vector4(vp.M14, vp.M24, vp.M34, vp.M44);
        _p = new[] { c4 + c1, c4 - c1, c4 + c2, c4 - c2, c4 + c3, c4 - c3 };
        for (int i = 0; i < 6; i++)
        {
            float l = new Vector3(_p[i].X, _p[i].Y, _p[i].Z).Length();
            _p[i] /= l;
        }
    }

    public bool Sphere(Vector3 c, float r)
    {
        foreach (var p in _p)
            if (p.X * c.X + p.Y * c.Y + p.Z * c.Z + p.W < -r) return false;
        return true;
    }

    public bool Box(Vector3 mn, Vector3 mx)
    {
        foreach (var p in _p)
        {
            var v = new Vector3(p.X >= 0 ? mx.X : mn.X, p.Y >= 0 ? mx.Y : mn.Y, p.Z >= 0 ? mx.Z : mn.Z);
            if (p.X * v.X + p.Y * v.Y + p.Z * v.Z + p.W < 0) return false;
        }
        return true;
    }
}
