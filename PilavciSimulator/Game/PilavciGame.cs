using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Diagnostics;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Platform;
using PilavciSimulator.Screens;

namespace PilavciSimulator;

/// <summary>
/// Uygulamanin kendisi: pencere, ana dongu, ortak hizmetler ve ekran yigini.
///
/// Ana dongu: girdi -> ekran guncellemeleri -> 3B cizim (en alttaki opak
/// ekran) -> arayuz (alttan uste) -> bildirimler -> ekran goruntusu.
/// Platform (Steam) geri cagirimlari her karenin sonunda calisir; menuler
/// erken donse bile kacmaz.
/// </summary>
public sealed class PilavciGame : IDisposable
{
    public const string Title = "Pilavcı Simülatörü";

    public LaunchOptions Options { get; }
    public GameSettings Settings { get; private set; } = new();
    public InputSystem Input { get; private set; } = null!;
    public Fonts Fonts { get; private set; } = null!;
    public UiContext Ui { get; private set; } = null!;
    public Renderer Renderer { get; private set; } = null!;
    public GameAssets Assets { get; } = new();
    public ScreenStack Screens { get; }
    public IPlatform Platform { get; private set; } = null!;
    public CaptureHarness? Capture { get; }
    public Audio.AudioSystem Audio { get; private set; } = null!;
    public Toasts Toasts { get; } = new();
    /// <summary>Oynanan oturum (menudeyken null).</summary>
    public Net.GameSession? Session { get; set; }
    public Client.SignProvider Signs { get; private set; } = null!;
    /// <summary>Oyun verisi (Data/*.json); bir kez yuklenir, tum oturumlarda ortak.</summary>
    public Sim.Data.GameData Data { get; private set; } = null!;
    /// <summary>Mahalle yerlesimi (collider, yol grafigi, satis noktalari).</summary>
    public World.DistrictLayout Layout { get; private set; } = null!;
    public Client.ModelLibrary Models { get; private set; } = null!;
    /// <summary>Hazir CC0 modeller (Assets/Models/cc0). Bos olabilir; o zaman her sey prosedurel.</summary>
    public Content.Cc0Catalog Cc0 { get; private set; } = new();
    /// <summary>Dunya cizicisi: statik sahne GPU'ya bir kez yuklenir, menu ve oyun paylasir.</summary>
    public Client.WorldRenderer WorldView { get; private set; } = null!;
#if STEAM_BUILD
    /// <summary>Steam lobisi (Steam calisiyorsa). Davet kabulu buradan gelir.</summary>
    public Net.SteamLobby? Lobby { get; private set; }
#endif

    /// <summary>Oyuncunun gorunen adi: Steam'de Steam adi, degilse ayardaki ad.</summary>
    public string PlayerName
    {
        get
        {
            if (Platform.IsSteam)
            {
                return Platform.PlayerName;
            }

            var name = Options.PlayerName ?? (Settings.PlayerName.Trim().Length > 0 ? Settings.PlayerName.Trim() : Platform.PlayerName);
            return name.Length > 20 ? name[..20] : name;
        }
    }

    /// <summary>Tabela malzemesi (araba tabelasi gibi calisma aninda degisen yazilar).</summary>
    public int SignMaterial(string text, Raylib_cs.Color background) =>
        Signs.Sign(text, background, Raylib_cs.Color.White, 512, 112);

    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }
    public float Time { get; private set; }
    public bool QuitRequested { get; set; }

    private bool _cursorShown = true;
    private bool _disposed;

    public PilavciGame(LaunchOptions options)
    {
        Options = options;
        Screens = new ScreenStack(this);
        Capture = CaptureHarness.FromOptions(options);
    }

    public int Run()
    {
        Settings = GameSettings.Load();
        InitWindow();
        LoadContent();
        StartupRouter.Start(this);

        while (!QuitRequested && !Raylib.WindowShouldClose())
        {
            Frame();
            if (Capture is { Finished: true })
            {
                break;
            }
        }

        if (Capture is not null)
        {
            Console.WriteLine($"[capture] bitti: {Capture.Shots} goruntu, {Capture.Failures} hata");
            return Capture.Failures == 0 ? 0 : 3;
        }

        return 0;
    }

    private void InitWindow()
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        // MSAA: arayuzun yuvarlak kenarlari ve ikonlar yumusak
        var flags = ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint;
        if (Settings.VSync && Capture is null)
        {
            flags |= ConfigFlags.VSyncHint;
        }

        Raylib.SetConfigFlags(flags);
        var (w, h) = Options.Windowed ?? (Settings.Width, Settings.Height);
        Raylib.InitWindow(w, h, Title);
        Raylib.SetWindowMinSize(960, 540);
        Raylib.SetExitKey(KeyboardKey.Null);

        var iconPath = Path.Combine(Paths.Assets, "Icon", "icon.png");
        if (File.Exists(iconPath))
        {
            var icon = Raylib.LoadImage(iconPath);
            Raylib.SetWindowIcon(icon);
            Raylib.UnloadImage(icon);
        }

        if (Options.Windowed is null)
        {
            ApplyWindowMode();
        }

        Raylib.SetTargetFPS(Capture is not null ? 0 : Settings.FpsLimit);
        Log.Info($"pencere {Raylib.GetScreenWidth()}x{Raylib.GetScreenHeight()}, GL hazir");
    }

    public void ApplyWindowMode()
    {
        var monitor = Raylib.GetCurrentMonitor();
        var isFull = Raylib.IsWindowFullscreen();
        var isBorderless = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
        switch (Settings.WindowMode)
        {
            case WindowMode.Windowed:
                if (isFull) Raylib.ToggleFullscreen();
                if (isBorderless) Raylib.ToggleBorderlessWindowed();
                Raylib.SetWindowSize(Settings.Width, Settings.Height);
                break;
            case WindowMode.Borderless:
                if (isFull) Raylib.ToggleFullscreen();
                if (!isBorderless) Raylib.ToggleBorderlessWindowed();
                break;
            case WindowMode.Fullscreen:
                if (isBorderless) Raylib.ToggleBorderlessWindowed();
                if (!isFull)
                {
                    Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
                    Raylib.ToggleFullscreen();
                }

                break;
        }
    }

    /// <summary>Ayar ekranindan sonra: grafik/ses/kontrol ayarlarini uygula.</summary>
    public void ApplySettings()
    {
        Settings.Clamp();
        Renderer.SetShadowQuality(Settings.ShadowQuality);
        Renderer.RenderScale = Settings.RenderScale;
        Renderer.Fxaa = Settings.Fxaa;
        Renderer.Brightness = Settings.Brightness;
        Input.MouseSensitivity = Settings.MouseSensitivity;
        Input.InvertY = Settings.InvertY;
        Audio.ApplyVolumes(Settings);
        if (Capture is null)
        {
            Raylib.SetTargetFPS(Settings.VSync ? 0 : Settings.FpsLimit);
        }

        Loc.Use(Settings.Language);
    }

    private void LoadContent()
    {
        Loc.Load(Paths.Data, Settings.Language);
        IInputSource source = Capture is not null ? Capture.Input : new RaylibInputSource();
        Input = new InputSystem(source);
        Input.Load(Settings.Bindings);
        Fonts = new Fonts(Path.Combine(Paths.Assets, "Fonts"));
        Ui = new UiContext(Fonts, Input);

        Assets.LoadBaseTextures();
        Renderer = new Renderer(Assets.SoftDot, Assets.Ripples);
        Assets.Load(Renderer.Materials);
        Signs = new Client.SignProvider(Fonts, Renderer.Materials);

        Data = Sim.Data.GameData.Load(Paths.Data);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Cc0 = Content.Cc0Catalog.Load(Path.Combine(Paths.Assets, "Models", "cc0", "manifest.json"));
        Cc0.RegisterMaterials(Renderer.Materials, Assets.WhiteTex);
        Layout = World.DistrictBuilder.Build(withGeometry: true, Signs, prefabs: Cc0);
        Models = new Client.ModelLibrary(Renderer.Materials);
        WorldView = new Client.WorldRenderer(this, Models);
        WorldView.Upload(Layout);
        Log.Info($"mahalle kuruldu: {sw.ElapsedMilliseconds} ms");

        Audio = new Audio.AudioSystem(enabled: !Options.NoAudio && Capture is null);
        Ui.Sound = (id, volume) => Audio.Play(id, volume);
        Platform = PlatformFactory.Create(Options, Settings);
        if (Platform.GameLanguage is "turkish" && !File.Exists(GameSettings.FilePath))
        {
            Settings.Language = "tr";
        }
        else if (Platform.GameLanguage is "english" && !File.Exists(GameSettings.FilePath))
        {
            Settings.Language = "en";
        }

        ApplySettings();
#if STEAM_BUILD
        if (Platform.IsSteam)
        {
            Lobby = new Net.SteamLobby();
            Lobby.JoinRequested += hostId => GameFlow.JoinSteam(this, hostId);
        }
#endif
        Log.Info($"icerik yuklendi: {Renderer.Materials.Count} malzeme, platform={(Platform.IsSteam ? "Steam" : "yerel")}");
    }

    private void Frame()
    {
        var dt = Capture is not null ? CaptureHarness.FixedDt : MathF.Min(Raylib.GetFrameTime(), 0.1f);
        Time += dt;
        ScreenWidth = Raylib.GetScreenWidth();
        ScreenHeight = Raylib.GetScreenHeight();

        // Ekran degisiklikleri bir onceki kareden kalmis olabilir (ilk karede
        // acilis ekrani da burada kurulur; senaryo komutlari onu gormeli).
        Screens.ApplyPending();
        Capture?.BeforeFrame(this);
        Input.Update();
        Screens.ApplyPending();
        UpdateCursor();

        var top = Screens.Top;
        Ui.Begin(ScreenWidth, ScreenHeight, Settings.UiScale, interactive: true, dt);
        Screens.Update(dt);
        Toasts.Update(dt);
        Audio.Update(dt);

        if (Input.KeyPressed(KeyboardKey.F12))
        {
            SaveScreenshot();
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        Screens.Draw();
        Toasts.Draw(Ui);
        Ui.End();
        Capture?.AfterDraw();
        Raylib.EndDrawing();

        Input.EndFrame();
        Platform.RunCallbacks();
        Screens.ApplyPending();
        _ = top;
    }

    private void UpdateCursor()
    {
        var show = Screens.Top?.ShowCursor ?? true;
        if (show == _cursorShown)
        {
            return;
        }

        _cursorShown = show;
        if (Capture is not null)
        {
            return;
        }

        if (show)
        {
            Raylib.EnableCursor();
        }
        else
        {
            Raylib.DisableCursor();
        }
    }

    private void SaveScreenshot()
    {
        try
        {
            Rlgl.DrawRenderBatchActive();
            var img = Raylib.LoadImageFromScreen();
            var path = Path.Combine(Paths.Screenshots, $"pilav-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            Raylib.ExportImage(img, path);
            Raylib.UnloadImage(img);
            Toasts.Show(Loc.T("toast.screenshot"), Theme.Blue, icon: Icon.Paint);
        }
        catch (Exception ex)
        {
            Log.Warn($"ekran goruntusu alinamadi: {ex.Message}");
        }
    }

    public string Annotate() => Screens.Top?.Annotate() ?? "(bos)";

    /// <summary>Gelistirici/senaryo komutu: ustten asagi ilk taniyan ekran isler.</summary>
    public bool Command(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        if (args[0] == "screen-pop")
        {
            Screens.Pop();
            return true;
        }

        for (var i = Screens.All.Count - 1; i >= 0; i--)
        {
            if (Screens.All[i].Command(args))
            {
                return true;
            }
        }

        return false;
    }

    public void SaveSettings()
    {
        Settings.Bindings = Input.Save();
        Settings.Save();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var s in Screens.All.Reverse())
        {
            s.Exit();
        }

#if STEAM_BUILD
        Lobby?.Dispose();
#endif
        Platform?.Dispose();
        Audio?.Dispose();
        Renderer?.Dispose();
        Fonts?.Dispose();
        if (Raylib.IsWindowReady())
        {
            Raylib.CloseWindow();
        }
    }
}

/// <summary>Ekranin ust kosesinde kisa sureli bildirimler.</summary>
public sealed class Toasts
{
    private sealed record Item(string Text, Color Color, float Life, Icon Icon)
    {
        public float Age;
    }

    private readonly List<Item> _items = new();

    /// <summary>Bildirim; ikon verilmezse renkten secilir (kirmizi uyari, yesil onay...).</summary>
    public void Show(string text, Color color, float seconds = 3.5f, Icon icon = Icon.None)
    {
        if (icon == Icon.None)
        {
            icon = color.Equals(Theme.Red) ? Icon.Warning
                : color.Equals(Theme.Green) ? Icon.Check
                : color.Equals(Theme.Yellow) ? Icon.Star
                : Icon.Info;
        }

        _items.Add(new Item(text, color, seconds, icon));
        if (_items.Count > 6)
        {
            _items.RemoveAt(0);
        }
    }

    public IReadOnlyList<string> Recent => _items.Select(i => i.Text).ToList();

    public void Update(float dt)
    {
        foreach (var i in _items)
        {
            i.Age += dt;
        }

        _items.RemoveAll(i => i.Age >= i.Life);
    }

    public void Draw(UiContext ui)
    {
        // Sag alt koseden yukari dogru, sagdan kayarak girer: sag ustteki siparis fislerini ortmez.
        var y = ui.Height - ui.S(150);
        for (var n = _items.Count - 1; n >= 0; n--)
        {
            var i = _items[n];
            var a = MathF.Min(1f, MathF.Min(i.Age * 5f, (i.Life - i.Age) * 2f));
            var slide = 1f - MathF.Pow(1f - MathF.Min(1f, i.Age * 4f), 3f);
            var m = ui.Measure(i.Text, 24, true);
            var w = m.X + ui.S(90);
            var r = new Rectangle(ui.Width - (w + ui.S(24)) * slide, y, w, ui.S(56));
            ui.SoftShadow(r, 14, 8, (byte)(80 * a));
            ui.Panel(r, Theme.HudBgStrong.WithAlpha(0.9f * a), 14);
            Raylib.DrawCircleV(new Vector2(r.X + ui.S(30), r.Y + r.Height / 2), ui.S(19), i.Color.WithAlpha(a));
            Icons.Draw(i.Icon, new Vector2(r.X + ui.S(30), r.Y + r.Height / 2), ui.S(22), Theme.White.WithAlpha(a), i.Color.WithAlpha(a));
            ui.TextIn(r with { X = r.X + ui.S(52) }, i.Text, 24, Theme.Cream.WithAlpha(a), true);
            // Kalan sure ince cizgi
            Raylib.DrawRectangleRec(new Rectangle(r.X + ui.S(14), r.Y + r.Height - ui.S(5), (r.Width - ui.S(28)) * (1 - i.Age / i.Life), ui.S(2)), i.Color.WithAlpha(0.6f * a));
            y -= ui.S(66);
        }
    }
}
