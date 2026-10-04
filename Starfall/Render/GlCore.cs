using System.Numerics;
using System.Reflection;
using System.Text;
using Silk.NET.OpenGL;

namespace Starfall.Render;

/// <summary>Tek GL baglami. Render disindaki kod GL'ye hic dokunmaz.</summary>
public static class Gfx
{
    public static GL GL = null!;

    public static string ReadResource(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(name) ?? throw new FileNotFoundException($"gomulu kaynak yok: {name}");
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    public static byte[] ReadResourceBytes(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(name) ?? throw new FileNotFoundException($"gomulu kaynak yok: {name}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static bool HasResource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceInfo(name) != null;
}

public sealed class ShaderProgram : IDisposable
{
    public readonly uint Handle;
    private readonly Dictionary<string, int> _loc = new();
    public readonly string Name;

    public ShaderProgram(string name, string vertFile, string fragFile, params string[] defines)
    {
        Name = name;
        var gl = Gfx.GL;
        string header = "#version 330 core\n" + string.Concat(defines.Select(d => $"#define {d}\n"));
        uint vs = Compile(ShaderType.VertexShader, header + Load(vertFile), vertFile);
        uint fs = Compile(ShaderType.FragmentShader, header + Load(fragFile), fragFile);
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, vs);
        gl.AttachShader(Handle, fs);
        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int ok);
        if (ok == 0) throw new Exception($"shader baglanamadi ({name}): {gl.GetProgramInfoLog(Handle)}");
        gl.DetachShader(Handle, vs);
        gl.DetachShader(Handle, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
    }

    private static string Load(string file)
    {
        string src = Gfx.ReadResource($"shaders/{file}");
        // basit #include "ad" destegi
        var sb = new StringBuilder();
        foreach (var line in src.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("#include \""))
            {
                string inc = t[10..^1];
                sb.AppendLine(Gfx.ReadResource($"shaders/{inc}.glsl"));
            }
            else sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static uint Compile(ShaderType type, string src, string file)
    {
        var gl = Gfx.GL;
        uint s = gl.CreateShader(type);
        gl.ShaderSource(s, src);
        gl.CompileShader(s);
        gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0) throw new Exception($"shader derlenemedi ({file}): {gl.GetShaderInfoLog(s)}");
        return s;
    }

    public void Use() => Gfx.GL.UseProgram(Handle);

    public int Loc(string name)
    {
        if (!_loc.TryGetValue(name, out int l))
        {
            l = Gfx.GL.GetUniformLocation(Handle, name);
            _loc[name] = l;
        }
        return l;
    }

    public void Set(string n, float v) { int l = Loc(n); if (l >= 0) Gfx.GL.Uniform1(l, v); }
    public void Set(string n, int v) { int l = Loc(n); if (l >= 0) Gfx.GL.Uniform1(l, v); }
    public void Set(string n, bool v) { int l = Loc(n); if (l >= 0) Gfx.GL.Uniform1(l, v ? 1 : 0); }
    public void Set(string n, Vector2 v) { int l = Loc(n); if (l >= 0) Gfx.GL.Uniform2(l, v.X, v.Y); }
    public void Set(string n, Vector3 v) { int l = Loc(n); if (l >= 0) Gfx.GL.Uniform3(l, v.X, v.Y, v.Z); }
    public void Set(string n, Vector4 v) { int l = Loc(n); if (l >= 0) Gfx.GL.Uniform4(l, v.X, v.Y, v.Z, v.W); }

    public unsafe void Set(string n, Matrix4x4 m)
    {
        int l = Loc(n);
        if (l >= 0) Gfx.GL.UniformMatrix4(l, 1, false, (float*)&m);
    }

    public void Dispose() => Gfx.GL.DeleteProgram(Handle);
}

/// <summary>GPU tarafinda bir mesh: konum + normal + renk (+ istege bagli ornek tamponu).</summary>
public sealed class GpuMesh : IDisposable
{
    public uint Vao, Vbo, InstanceVbo;
    public int VertexCount;
    public int InstanceCapacity;

    public unsafe GpuMesh(MeshData m)
    {
        var gl = Gfx.GL;
        Vao = gl.GenVertexArray();
        gl.BindVertexArray(Vao);
        Vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* p = m.Vertices)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(m.Vertices.Length * 4), p, BufferUsageARB.StaticDraw);
        uint stride = MeshData.Stride * 4;
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)24);
        VertexCount = m.VertexCount;
        gl.BindVertexArray(0);
    }

    /// <summary>Ornek (instance) tamponu: mat4 (3-6) + renk vec4 (7).</summary>
    public unsafe void EnsureInstances(int count)
    {
        var gl = Gfx.GL;
        if (InstanceVbo == 0)
        {
            gl.BindVertexArray(Vao);
            InstanceVbo = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVbo);
            uint stride = 20 * 4;
            for (uint i = 0; i < 4; i++)
            {
                gl.EnableVertexAttribArray(3 + i);
                gl.VertexAttribPointer(3 + i, 4, VertexAttribPointerType.Float, false, stride, (void*)(i * 16));
                gl.VertexAttribDivisor(3 + i, 1);
            }
            gl.EnableVertexAttribArray(7);
            gl.VertexAttribPointer(7, 4, VertexAttribPointerType.Float, false, stride, (void*)64);
            gl.VertexAttribDivisor(7, 1);
            gl.BindVertexArray(0);
        }
        if (count > InstanceCapacity)
        {
            InstanceCapacity = Math.Max(count, 16);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVbo);
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(InstanceCapacity * 80), null, BufferUsageARB.DynamicDraw);
        }
    }

    public unsafe void UploadInstances(float[] data, int count)
    {
        EnsureInstances(count);
        var gl = Gfx.GL;
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, InstanceVbo);
        fixed (float* p = data)
            gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(count * 80), p);
    }

    public void Draw()
    {
        var gl = Gfx.GL;
        gl.BindVertexArray(Vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)VertexCount);
    }

    public void DrawInstanced(int count)
    {
        var gl = Gfx.GL;
        gl.BindVertexArray(Vao);
        gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, (uint)VertexCount, (uint)count);
    }

    public void Dispose()
    {
        var gl = Gfx.GL;
        gl.DeleteBuffer(Vbo);
        if (InstanceVbo != 0) gl.DeleteBuffer(InstanceVbo);
        gl.DeleteVertexArray(Vao);
    }
}

public sealed class Texture : IDisposable
{
    public uint Handle;
    public int Width, Height;

    public static unsafe Texture Rgba8(int w, int h, byte[] data, bool linear = true, bool mips = false, bool repeat = false)
    {
        var gl = Gfx.GL;
        var t = new Texture { Handle = gl.GenTexture(), Width = w, Height = h };
        gl.BindTexture(TextureTarget.Texture2D, t.Handle);
        fixed (byte* p = data)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        t.Params(linear, mips, repeat);
        if (mips) gl.GenerateMipmap(TextureTarget.Texture2D);
        return t;
    }

    public static unsafe Texture R8(int w, int h, byte[] data)
    {
        var gl = Gfx.GL;
        var t = new Texture { Handle = gl.GenTexture(), Width = w, Height = h };
        gl.BindTexture(TextureTarget.Texture2D, t.Handle);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* p = data)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, (uint)w, (uint)h, 0, PixelFormat.Red, PixelType.UnsignedByte, p);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        t.Params(true, false, false);
        return t;
    }

    public static unsafe Texture R32F(int w, int h, float[] data)
    {
        var gl = Gfx.GL;
        var t = new Texture { Handle = gl.GenTexture(), Width = w, Height = h };
        gl.BindTexture(TextureTarget.Texture2D, t.Handle);
        fixed (float* p = data)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R16f, (uint)w, (uint)h, 0, PixelFormat.Red, PixelType.Float, p);
        t.Params(true, false, false);
        return t;
    }

    public void Params(bool linear, bool mips, bool repeat)
    {
        var gl = Gfx.GL;
        int min = linear ? (mips ? (int)TextureMinFilter.LinearMipmapLinear : (int)TextureMinFilter.Linear) : (int)TextureMinFilter.Nearest;
        int mag = linear ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest;
        int wrap = repeat ? (int)TextureWrapMode.Repeat : (int)TextureWrapMode.ClampToEdge;
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, min);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, mag);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, wrap);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, wrap);
    }

    public void Bind(int unit)
    {
        Gfx.GL.ActiveTexture(TextureUnit.Texture0 + unit);
        Gfx.GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public static Texture FromPngBytes(byte[] png, bool mips = true)
    {
        var img = StbImageSharp.ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return Rgba8(img.Width, img.Height, img.Data, true, mips);
    }

    public void Dispose() => Gfx.GL.DeleteTexture(Handle);
}

/// <summary>Cerceve tamponu: renk ekleri + istege bagli derinlik.</summary>
public sealed class RenderTarget : IDisposable
{
    public uint Fbo;
    public uint Color;
    public uint Depth;
    public int Width, Height;
    private readonly InternalFormat _fmt;
    private readonly bool _depthTex;
    private readonly bool _hasColor;
    private readonly bool _hasDepth;

    public RenderTarget(int w, int h, InternalFormat fmt, bool depth, bool depthTexture = false, bool color = true)
    {
        _fmt = fmt;
        _hasDepth = depth;
        _depthTex = depthTexture;
        _hasColor = color;
        Create(w, h);
    }

    private unsafe void Create(int w, int h)
    {
        var gl = Gfx.GL;
        Width = Math.Max(1, w);
        Height = Math.Max(1, h);
        Fbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        if (_hasColor)
        {
            Color = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, Color);
            var (pf, pt) = _fmt switch
            {
                InternalFormat.Rgba16f => (PixelFormat.Rgba, PixelType.HalfFloat),
                InternalFormat.R11fG11fB10f => (PixelFormat.Rgb, PixelType.HalfFloat),
                _ => (PixelFormat.Rgba, PixelType.UnsignedByte),
            };
            gl.TexImage2D(TextureTarget.Texture2D, 0, _fmt, (uint)Width, (uint)Height, 0, pf, pt, null);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Color, 0);
        }
        else
        {
            gl.DrawBuffer(DrawBufferMode.None);
            gl.ReadBuffer(ReadBufferMode.None);
        }
        if (_hasDepth)
        {
            if (_depthTex)
            {
                Depth = gl.GenTexture();
                gl.BindTexture(TextureTarget.Texture2D, Depth);
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.DepthComponent24, (uint)Width, (uint)Height, 0, PixelFormat.DepthComponent, PixelType.UnsignedInt, null);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
                float[] border = { 1, 1, 1, 1 };
                fixed (float* b = border) gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, b);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, Depth, 0);
            }
            else
            {
                Depth = gl.GenRenderbuffer();
                gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, Depth);
                gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)Width, (uint)Height);
                gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, Depth);
            }
        }
        var status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete) throw new Exception($"framebuffer eksik: {status}");
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Resize(int w, int h)
    {
        if (w == Width && h == Height) return;
        Release();
        Create(w, h);
    }

    public void Bind()
    {
        Gfx.GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        Gfx.GL.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    public void BindColor(int unit)
    {
        Gfx.GL.ActiveTexture(TextureUnit.Texture0 + unit);
        Gfx.GL.BindTexture(TextureTarget.Texture2D, Color);
    }

    public void BindDepth(int unit)
    {
        Gfx.GL.ActiveTexture(TextureUnit.Texture0 + unit);
        Gfx.GL.BindTexture(TextureTarget.Texture2D, Depth);
    }

    private void Release()
    {
        var gl = Gfx.GL;
        gl.DeleteFramebuffer(Fbo);
        if (_hasColor) gl.DeleteTexture(Color);
        if (_hasDepth)
        {
            if (_depthTex) gl.DeleteTexture(Depth); else gl.DeleteRenderbuffer(Depth);
        }
    }

    public void Dispose() => Release();
}

/// <summary>Tam ekran ucgen (post-process gecisleri icin).</summary>
public sealed class FullscreenTri
{
    private readonly uint _vao;

    public FullscreenTri()
    {
        _vao = Gfx.GL.GenVertexArray();
    }

    public void Draw()
    {
        Gfx.GL.BindVertexArray(_vao);
        Gfx.GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }
}
