using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Starfall.Render;

namespace Starfall.Core;

/// <summary>
/// Pencere + GL baglami + ana dongu. Oyun mantigi buraya bagli degil: ayni
/// Game sinifi testlerde pencere olmadan da calisir.
/// </summary>
public sealed class Host
{
    private IWindow _window = null!;
    private GL _gl = null!;
    private IInputContext _input = null!;
    public Renderer Renderer = null!;
    private readonly Game _game;
    private readonly HostOptions _opt;
    private int _frames;

    public Host(Game game, HostOptions opt)
    {
        _game = game;
        _opt = opt;
    }

    public void Run()
    {
        var o = WindowOptions.Default with
        {
            Size = new Vector2D<int>(_opt.Width, _opt.Height),
            Title = "Starfall Isle",
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
            VSync = !_opt.Capture,
            PreferredDepthBufferBits = 24,
            WindowState = _opt.Fullscreen ? WindowState.Fullscreen : WindowState.Normal,
        };
        _window = Window.Create(o);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += s => Renderer?.Resize(s.X, s.Y);
        _window.Closing += () => _game.OnQuit();
        _window.Run();
        _window.Dispose();
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window);
        var fb = _window.FramebufferSize;
        Renderer = new Renderer(_gl) { Width = fb.X, Height = fb.Y };
        Renderer.Resize(fb.X, fb.Y);
        _input = _window.CreateInput();
        _game.AttachHost(this, _input);
        SetIcon();
    }

    private void SetIcon()
    {
        try
        {
            var img = StbImageSharp.ImageResult.FromMemory(Gfx.ReadResourceBytes("icons/app128.png"), StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            var raw = new Silk.NET.Core.RawImage(img.Width, img.Height, img.Data);
            _window.SetWindowIcon(ref raw);
        }
        catch { /* bazi platformlar desteklemez */ }
    }

    private void OnRender(double dt)
    {
        _game.Frame((float)dt);
        Renderer.Render(_game.Env);
        _frames++;
        if (_game.PendingCapture is { } cap && _frames > 2)
        {
            _game.PendingCapture = null;
            SaveScreenshot(cap);
        }
        if (_game.QuitRequested) _window.Close();
    }

    public void SaveScreenshot(string path)
    {
        var px = Renderer.ReadPixels(out int w, out int h);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        new StbImageWriteSharp.ImageWriter().WritePng(px, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
    }

    public void SetFullscreen(bool on)
    {
        if (_window == null) return;
        _window.WindowState = on ? WindowState.Fullscreen : WindowState.Normal;
    }

    public void SetVSync(bool on) => _window.VSync = on;

    public void SetTitle(string t) => _window.Title = t;

    public Vector2 WindowSize => new(_window.FramebufferSize.X, _window.FramebufferSize.Y);
}

public sealed class HostOptions
{
    public int Width = 1280, Height = 720;
    public bool Fullscreen;
    public bool Capture;
}
