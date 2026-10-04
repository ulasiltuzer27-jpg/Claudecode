using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Yalnizca derinlik eki olan cerceve tamponu. raylib'in LoadRenderTexture'i
/// derinligi okunamaz bir renderbuffer'a koydugu icin elle kuruluyor
/// (raylib'in shadowmap ornegindeki yontem).
///
/// Golge kutusu oyuncuyu takip eder ve texel izgarasina kilitlenir;
/// kilitlenmeseydi kamera her kipirdadiginda golge kenarlari titrerdi.
/// </summary>
public sealed class ShadowMap : IDisposable
{
    public int Size { get; }
    public uint FramebufferId { get; }
    public Texture2D Depth { get; }
    public float Extent { get; }

    public Matrix4x4 LightView { get; private set; }
    public Matrix4x4 LightProjection { get; private set; }
    public Matrix4x4 LightViewProjection => LightView * LightProjection;

    public ShadowMap(int size, float extent)
    {
        Size = size;
        Extent = extent;
        FramebufferId = Rlgl.LoadFramebuffer();
        Rlgl.EnableFramebuffer(FramebufferId);
        var depthId = Rlgl.LoadTextureDepth(size, size, false);
        Rlgl.FramebufferAttach(FramebufferId, depthId, FramebufferAttachType.Depth, FramebufferAttachTextureType.Texture2D, 0);
        var ok = Rlgl.FramebufferComplete(FramebufferId);
        Rlgl.DisableFramebuffer();
        if (!ok)
        {
            throw new InvalidOperationException("golge cerceve tamponu tamamlanamadi");
        }

        Depth = new Texture2D
        {
            Id = depthId,
            Width = size,
            Height = size,
            Mipmaps = 1,
            Format = (PixelFormat)19,
        };
    }

    /// <summary>Gunes yonune ve takip noktasina gore isik matrislerini hesaplar.</summary>
    public void Fit(Vector3 focus, Vector3 sunDirection)
    {
        var dir = Vector3.Normalize(sunDirection);
        var up = MathF.Abs(dir.Y) > 0.98f ? Vector3.UnitZ : Vector3.UnitY;
        var distance = 160f;
        var view = Matrix4x4.CreateLookAt(focus + dir * distance, focus, up);

        // Texel'e kilitleme: odak noktasinin isik uzayindaki konumunu
        // texel boyutunun katina yuvarla, farki gorunume ekle.
        var texel = Extent * 2f / Size;
        var focusLs = Vector3.Transform(focus, view);
        var snapped = new Vector3(MathF.Round(focusLs.X / texel) * texel, MathF.Round(focusLs.Y / texel) * texel, focusLs.Z);
        var delta = snapped - focusLs;
        view *= Matrix4x4.CreateTranslation(-delta.X, -delta.Y, 0);

        LightView = view;
        LightProjection = Gfx.Ortho(-Extent, Extent, -Extent, Extent, 1f, distance * 2f);
    }

    public void Dispose()
    {
        // rlUnloadFramebuffer ekli derinlik dokusunu da siliyor.
        Rlgl.UnloadFramebuffer(FramebufferId);
    }
}
