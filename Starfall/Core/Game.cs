using System.Numerics;
using Starfall.Achievements;
using Starfall.Audio;
using Starfall.Gameplay;
using Starfall.Render;
using Starfall.UI;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.Core;

public enum GameState { Loading, Menu, Playing, Paused, Dialogue, Cutscene, Photo }

/// <summary>Oturum boyunca tutulan (kayda girmeyen) sayaclar.</summary>
public sealed class SessionCounters
{
    public float IceSlide;
    public bool ReachedIsle2ByBoat;
    public float RegionTimer, SaveTimer = 60, FpsAcc;
    public int FpsN;
}

/// <summary>
/// Oyunun kalbi: sistemleri kurar, durum makinesini ve ana dongunun sirasini yonetir.
/// Diger moduller birbirini buradan (game.*) bulur. Pencere olmadan da calisir
/// (--verify / --playtest): o zaman Host null'dir ve hicbir GL cagrisi yapilmaz.
/// </summary>
public sealed partial class Game
{
    public const float DaySeconds = 720; // 24 oyun saati = 12 gercek dakika
    public static readonly string Version = typeof(Game).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";

    public readonly Events Events = new();
    public readonly Settings Settings = new();
    public readonly SaveManager Saves = new();
    public readonly RenderEnv Env = new();
    public readonly Input Input = new();
    public readonly SessionCounters Session = new();
    public readonly Dictionary<string, string> Args;
    public readonly bool TestMode, Headless;

    public GameWorld World = null!;
    public Player Player = null!;
    public CameraRig CameraRig = null!;
    public Collectibles Collectibles = null!;
    public Npcs Npcs = null!;
    public Quests Quests = null!;
    public Race Race = null!;
    public Fishing Fishing = null!;
    public Finale Finale = null!;
    public PhotoMode Photo = null!;
    public Dialogue Dialogue = null!;
    public Progress Progress = null!;
    public Wardrobe Wardrobe = null!;
    public Hud Hud = null!;
    public Credits Credits = null!;
    public UiManager Ui = null!;
    public AudioEngine Audio = null!;
    public Stats Stats = null!;
    public AchievementTracker Achievements = null!;
    public ProfileData Profile = null!;
    public SaveData? Save;
    public int? Slot;
    public Host? Host;

    public GameState State = GameState.Loading;
    public float Hour = 17.6f;
    public double Time;
    public float RealDt, Fps;
    public float MenuAngle = 0.4f;
    public bool QuitRequested;
    public string? PendingCapture;
    public Interaction? CurrentInteraction;
    public string? CurrentRegion;
    private readonly List<(float T, Action A)> _timers = new();
    private Action<string>? _shotCallback;
    public string? PendingShot;
    private float _fadeInT = -1, _fadeInDur = 1;

    public sealed record Interaction(string Label, Action Run, string Action = "interact");

    public Game(Dictionary<string, string> args, bool headless = false)
    {
        Args = args;
        TestMode = args.ContainsKey("test") || headless;
        Headless = headless;
    }

    public bool Arg(string k) => Args.ContainsKey(k);

    // ------------------------------------------------------------------ kurulum
    public void Build()
    {
        if (Args.TryGetValue("data", out var dir)) Storage.SetDir(dir);
        if (Arg("fresh")) foreach (var n in new[] { "settings", "profile", "slot_1", "slot_2", "slot_3" }) Storage.Remove(n);
        Settings.Load();
        if (Args.TryGetValue("lang", out var lang)) Settings.V.Lang = lang;
        if (Args.TryGetValue("quality", out var q)) Settings.V.Quality = q;
        SetLang(Settings.V.Lang);
        Env.QualityFlowers = Quality.Get(Settings.V.Quality).Flowers;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        World = new GameWorld(Env);
        World.BuildAll(TestMode);
        Log($"dunya {sw.ElapsedMilliseconds} ms (agac {World.TreeCount}, sekil {World.Physics.Shapes.Count})");

        Audio = new AudioEngine(this);
        Dialogue = new Dialogue(this);
        Progress = new Progress(this);
        Player = new Player(this);
        Wardrobe = new Wardrobe(this);
        CameraRig = new CameraRig(this);
        Collectibles = new Collectibles(this);
        Collectibles.Build();
        Npcs = new Npcs(this);
        Npcs.Build(World.NpcDefs);
        Quests = new Quests(this);
        Race = new Race(this);
        Fishing = new Fishing(this);
        Finale = new Finale(this);
        Photo = new PhotoMode(this);
        Hud = new Hud(this);
        Credits = new Credits(this);
        Ui = new UiManager(this);
        BuildIsle2Systems();

        Profile = Saves.LoadProfile();
        Stats = new Stats(Profile.Stats, Events);
        Achievements = new AchievementTracker(Stats, Profile.Unlocked, Events);
        Steam.Init(!Headless && !Arg("nosteam"));
        Achievements.SyncToSteam();

        WireEvents();
        Player.Spawn(WD.Start.X, WD.DockDef.Y + 0.1f, WD.Start.Z, WD.Start.Yaw);
        ToMainMenu(true);
    }

    public static void Log(string s)
    {
        if (Environment.GetEnvironmentVariable("STARFALL_QUIET") == null) Console.WriteLine(s);
    }

    /// <summary>Pencere hazir: renderer ayarlarini uygula.</summary>
    public void AttachHost(Host host, Silk.NET.Input.IInputContext input)
    {
        Host = host;
        Input.Attach(input);
        Audio.Start();
        ApplySettings(null);
        Settings.Changed += k => ApplySettings(k);
        if (Arg("nobloom")) host.Renderer.Q.Bloom = false;
        if (Arg("noshadow")) host.Renderer.ShadowsOn = false;
        if (Args.TryGetValue("debug", out var dbg)) host.Renderer.Debug = int.Parse(dbg);
        Ui.OnHostReady();
        RunStartupArgs();
    }

    public void ApplySettings(string? key)
    {
        var S = Settings.V;
        if (key is null or "quality")
        {
            var q = Quality.Get(S.Quality);
            Host?.Renderer.SetQuality(q);
            Env.Grass.Count = (int)(50000 * q.Grass);
        }
        if (key is null or "lang")
        {
            SetLang(S.Lang);
            Ui?.Refresh();
        }
        if (key is null or "fullscreen") Host?.SetFullscreen(S.Fullscreen);
        if (key is null or "vsync") Host?.SetVSync(S.VSync && !Arg("capture"));
        if (key is null or "master" or "music" or "sfx" or "ambience") Audio?.ApplyVolumes();
    }

    // ------------------------------------------------------------------ durum
    public void SetState(GameState s)
    {
        State = s;
        bool gameplay = s == GameState.Playing;
        if (gameplay && !TestMode) Input.SetLocked(true);
        if (!gameplay && s != GameState.Photo) Input.SetLocked(false);
    }

    public void ToMainMenu(bool first = false)
    {
        if (!first && Save != null) Autosave(true);
        Save = null;
        Ui.Clear();
        if (Dialogue.Active) Dialogue.Close(false);
        if (Race.State != "idle") Race.Cancel();
        if (Fishing.State != "idle") Fishing.Stop();
        LeaveSpecialStates();
        Hud.Visible = false;
        SetState(GameState.Menu);
        Hour = 17.6f;
        MenuAngle = 0.4f;
        Ui.Push(new MainMenu(Ui));
        Audio.Music.SetMood("menu");
        Steam.RichPresence("status", "Menu");
    }

    public void NewGame(int slot)
    {
        Slot = slot;
        Save = SaveManager.NewSave();
        StartSession();
        Stats.Add("games_started", 1);
        Autosave(true);
        After(1.2f, () => Hint(T("hint.move", ("jump", Input.Glyph("jump")))));
    }

    public void LoadSlot(int slot)
    {
        var data = Saves.LoadSlot(slot);
        if (data == null) { NewGame(slot); return; }
        Slot = slot;
        Save = data;
        StartSession();
    }

    private void StartSession()
    {
        var s = Save!;
        Ui.Clear();
        Profile.LastSlot = Slot;
        Collectibles.ApplySave(s);
        // dunya durumunu kayda gore kur
        var owl = Npcs.ById["owl"];
        if (s.Flag("owlIntro")) Npcs.MoveOwlToPeak();
        else
        {
            var d = WD.Npc("owl");
            Npcs.PlaceAt(owl, d.X, d.Z, d.Yaw);
        }
        if (s.Flag("bridge")) World.BuildBridge(false);
        Finale.SetLit(s.Flag("finale"));
        Wardrobe.ApplySave(s);
        ApplyIsle2Save(s);
        Hour = s.Hour;
        var p = s.Pos ?? new[] { WD.Start.X, WD.DockDef.Y + 0.1f, WD.Start.Z };
        Player.Spawn(p[0], p[1] + 0.05f, p[2], s.Yaw);
        CameraRig.Snap(Player.Pos, Player.Yaw + MathX.Pi);
        CameraRig.Override = null;
        Hud.Visible = true;
        SetState(GameState.Playing);
        Audio.Music.SetMood("auto");
        Progress.RefreshStats();
        Session.RegionTimer = 0;
        CurrentRegion = null;
        Steam.RichPresence("status", T("presence.exploring"));
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        SetState(GameState.Paused);
        Ui.Push(new PauseMenu(Ui));
    }

    public void Resume()
    {
        Ui.Clear();
        SetState(GameState.Playing);
    }

    public bool Flag(string k) => Save?.Flag(k) == true;

    public void SetFlag(string k, bool v = true)
    {
        if (Save != null) Save.Flags[k] = v;
    }

    public void Hint(string text, float sec = 6) => Hud.Hint(text, sec);

    // ------------------------------------------------------------------ zamanlayicilar
    /// <summary>setTimeout karsiligi: gercek zamanla (menude de) isler.</summary>
    public void After(float sec, Action a) => _timers.Add((sec, a));

    private void TickTimers(float dt)
    {
        for (int i = 0; i < _timers.Count; i++)
        {
            var (t, a) = _timers[i];
            t -= dt;
            if (t <= 0)
            {
                _timers.RemoveAt(i--);
                try { a(); } catch (Exception ex) { Console.Error.WriteLine(ex); }
            }
            else _timers[i] = (t, a);
        }
    }

    public void FadeIn(float sec)
    {
        _fadeInT = 0;
        _fadeInDur = MathF.Max(0.05f, sec);
        Env.Post.Fade = 1;
    }

    public void SetHour(float h)
    {
        Hour = h;
        if (Save != null) Save.Hour = h;
    }

    /// <summary>Kamp atesinde dinlen: kararip saati degistir.</summary>
    public void Rest(float toHour)
    {
        SetState(GameState.Cutscene);
        Player.Frozen = true;
        float k = 0;
        bool set = false;
        void Step(float dt)
        {
            k += dt * 1.8f;
            if (k < 1) Env.Post.Fade = k;
            else if (k < 1.05f) { Env.Post.Fade = 1; if (!set) { set = true; SetHour(toHour); } }
            else Env.Post.Fade = MathF.Max(0, 2.05f - k);
            if (k >= 2.05f)
            {
                Env.Post.Fade = 0;
                Player.Frozen = false;
                SetState(GameState.Playing);
                Autosave();
                _tickers.Remove(Step);
            }
        }
        _tickers.Add(Step);
    }

    private readonly List<Action<float>> _tickers = new();

    // ------------------------------------------------------------------ kayit
    private float _saveDelay = -1;

    public void Autosave(bool now = false)
    {
        if (Save == null || Slot == null) return;
        if (now) DoSave();
        else _saveDelay = 0.8f;
    }

    private void DoSave()
    {
        _saveDelay = -1;
        if (Save == null || Slot == null) return;
        var p = Player;
        var safe = p.Grounded && !p.Swimming && !p.Boating && !p.Climbing ? p.Pos : p.LastSafe;
        Save.Pos = new[] { safe.X, safe.Y, safe.Z };
        Save.Yaw = p.Yaw;
        Save.Hour = Hour;
        Save.Island = World.IslandAt(safe.X, safe.Z);
        Saves.SaveSlot(Slot.Value, Save);
        SaveProfile();
    }

    public void SaveProfile()
    {
        Profile.Stats = Stats.ToDict();
        Profile.Unlocked = new Dictionary<string, long>(Achievements.Unlocked);
        Saves.SaveProfile(Profile);
    }

    public void OnQuit()
    {
        if (Save != null) Autosave(true);
        else SaveProfile();
        Settings.Save();
        Audio.Dispose();
        Steam.Shutdown();
    }

    public void RequestScreenshot(Action<string> done)
    {
        if (Host == null) { done("(headless)"); return; }
        var name = $"starfall-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        PendingShot = Path.Combine(Storage.PhotosDir(), name);
        _shotCallback = done;
    }

    public void ShotSaved(string path)
    {
        var cb = _shotCallback;
        _shotCallback = null;
        PendingShot = null;
        cb?.Invoke(path);
    }

    // ------------------------------------------------------------------ dongu
    /// <summary>
    /// Yavas karelerde simulasyon 1/60 sn'lik alt adimlara bolunur: oyun zamani gercek
    /// zamanla ayni hizda akar, fizik kararli kalir. Tek seferlik tus olaylari (ziplama
    /// gibi) yalnizca ilk alt adimda islenir.
    /// </summary>
    public void Frame(float raw)
    {
        RealDt = MathF.Max(0, raw);
        int maxSteps = TestMode ? 30 : 6;
        float total = MathF.Min(RealDt, maxSteps / 60f);
        int steps = Math.Max(1, (int)MathF.Ceiling(total * 60 - 1e-4f));
        float dt = total / steps;
        for (int i = 0; i < steps; i++)
        {
            try { Update(dt, i == 0); }
            catch (Exception ex) { Console.Error.WriteLine(ex); if (TestMode) throw; }
            if (i == 0) Input.ConsumeEdges();
        }
        Input.EndFrame();
    }

    public void Update(float dt, bool firstStep = true)
    {
        var input = Input;
        if (firstStep)
        {
            input.Update();
            Session.FpsAcc += RealDt > 0 ? RealDt : dt;
            Session.FpsN++;
            if (Session.FpsAcc > 0.5f)
            {
                Fps = Session.FpsN / Session.FpsAcc;
                Session.FpsAcc = 0;
                Session.FpsN = 0;
            }
            Steam.RunCallbacks();
        }
        Time += dt;
        var st = State;
        bool playing = st == GameState.Playing;
        TickTimers(dt);
        foreach (var tk in _tickers.ToArray()) tk(dt);
        if (_fadeInT >= 0)
        {
            _fadeInT += dt;
            Env.Post.Fade = MathF.Max(0, 1 - _fadeInT / _fadeInDur);
            if (_fadeInT >= _fadeInDur) _fadeInT = -1;
        }
        if (_saveDelay >= 0)
        {
            _saveDelay -= dt;
            if (_saveDelay < 0) DoSave();
        }
        Settings.Update(dt);
        Achievements.Update(dt);
        input.Tick(dt);

        // saat
        if (st != GameState.Menu && st != GameState.Paused && st != GameState.Loading && st != GameState.Photo)
            Hour = (Hour + 24 / DaySeconds * dt) % 24;
        if (Save != null && (playing || st == GameState.Dialogue)) Save.PlayTime += dt;

        // girdi yonlendirme
        if (st is GameState.Menu or GameState.Paused) Ui.HandleInput(input);
        else if (Credits.Active) Credits.Handle(input);
        else if (st == GameState.Dialogue) Dialogue.Handle(input);
        else if (st == GameState.Photo) Photo.Update(dt, input);
        else if (playing) HandleGameplayInput(input);

        // dunya
        var focus = st == GameState.Menu ? MenuCamera(dt) : Player.Pos;
        Env.Sky.Update(Hour, dt, World.Snowiness(focus));
        World.Update(dt, (float)Time, Env.Sky, Player.Pos);
        Env.Grass.Center = st == GameState.Menu ? Env.Camera.Position : Player.Pos;
        Env.Grass.Player = Player.Pos;

        if (st is GameState.Playing or GameState.Dialogue or GameState.Cutscene)
        {
            bool frozen = st != GameState.Playing || Fishing.State != "idle";
            bool prevFrozen = Player.Frozen;
            if (st != GameState.Playing) Player.Frozen = true;
            Player.Update(dt, input, CameraRig.Yaw);
            if (st != GameState.Playing) Player.Frozen = prevFrozen;
            if (!frozen || st == GameState.Dialogue) CameraRig.Update(dt, st == GameState.Playing ? input : null, Player);
            else CameraRig.Update(dt, null, Player);
            Collectibles.Update(dt, Player);
            Race.Update(dt);
            Fishing.Update(dt, input);
            Dialogue.Update(dt);
            TickGameplay(dt);
        }
        else if (st == GameState.Photo) Collectibles.Update(0, Player);
        else if (st == GameState.Menu) Player.Model.Update(dt, new Models.FoxPose { Grounded = true });
        Npcs.Update(dt, Player);
        Finale.Update(dt);
        UpdateIsle2(dt);
        Env.Additive.Update(dt);
        Env.Normal.Update(dt);
        Audio.Update(dt);
        Hud.Update(dt);
        Credits.Update(dt);
        Ui.Update(dt);
        Env.ShadowFocus = st == GameState.Menu ? Env.Camera.Target : Player.Pos;
    }

    private Vector3 MenuCamera(float dt)
    {
        MenuAngle += dt * 0.025f;
        float a = MenuAngle;
        const float r = 120;
        var target = new Vector3(10, 12, 10);
        var cam = Env.Camera;
        cam.Position = new Vector3(MathF.Sin(a) * r + 20, 46 + MathF.Sin(a * 0.7f) * 6, MathF.Cos(a) * r + 30);
        cam.Target = target;
        cam.Fov = 55;
        return target;
    }

    private void HandleGameplayInput(Input input)
    {
        if (input.Pressed("pause"))
        {
            if (Fishing.State != "idle") return; // balik tutarken Esc oltayi toplar
            if (Race.State == "countdown") return;
            Pause();
            return;
        }
        if (input.Pressed("journal"))
        {
            SetState(GameState.Paused);
            Ui.Push(new PauseMenu(Ui));
            Ui.Push(new JournalScreen(Ui));
            return;
        }
        if (input.Pressed("photo") && Fishing.State == "idle" && !Player.Climbing) { Photo.Enter(); return; }
        if (Fishing.State != "idle" || Race.State == "countdown") return;
        var act = CurrentInteraction;
        if (act != null && input.Pressed(act.Action)) act.Run();
    }

    /// <summary>Bolgeler, etkilesim ipuclari, ozel istatistikler.</summary>
    private void TickGameplay(float dt)
    {
        var p = Player;
        var s = Save;
        if (s == null) return;

        // etkilesim
        CurrentInteraction = null;
        if (State == GameState.Playing && Fishing.State == "idle" && Race.State == "idle" && !p.Climbing)
            CurrentInteraction = FindInteraction();
        Hud.Prompt(CurrentInteraction?.Action ?? "interact", CurrentInteraction?.Label);

        // bolgeler (yarim saniyede bir)
        Session.RegionTimer -= dt;
        if (Session.RegionTimer <= 0)
        {
            Session.RegionTimer = 0.5f;
            var r = RegionAt(p.Pos);
            if (r != null && !s.Regions.Contains(r.Id))
            {
                s.Regions.Add(r.Id);
                Hud.RegionBanner(T($"region.{r.Id}"), T($"region.{r.Id}.sub"));
                Audio.Sfx("discover");
                if (r.Id == "cave") Stats.Max("cave", 1);
                OnRegionDiscovered(r);
                Progress.RefreshStats();
                Autosave();
            }
            CurrentRegion = r?.Id;
            if (r != null && r.Id == "peak" && p.Pos.Y > 36 && p.Grounded) Stats.Max("peak", 1);
            if (Hour < 1 && Hour >= 0) Stats.Max("night", 1);
        }
        // periyodik kayit
        Session.SaveTimer -= dt;
        if (Session.SaveTimer <= 0)
        {
            Session.SaveTimer = 60;
            Autosave(true);
        }
    }

    private Interaction? FindInteraction()
    {
        var p = Player;
        if (IsleInteraction() is { } special) return special;
        var npc = Npcs.Nearest(p.Pos);
        if (npc != null) return new Interaction(T("prompt.talk", ("name", T($"npc.{npc.Id}"))), () => Quests.Talk(npc));
        if (Vector3.Distance(World.CampfirePos, p.Pos) < 3.2f) return new Interaction(T("prompt.rest"), CampfireDialogue);
        var sign = NearSign();
        if (sign != null) return new Interaction(T("prompt.read"), () => Dialogue.Start(name: T("sign.name"), lines: Lines(sign)));
        if (Fishing.CanFish()) return new Interaction(T("prompt.fish"), () => Fishing.Start());
        return null;
    }

    public RegionDef? RegionAt(Vector3 pos)
    {
        foreach (var r in World.Regions)
        {
            float d = MathX.Hypot(pos.X - r.X, pos.Z - r.Z);
            if (d < r.R && pos.Y > r.MinY) return r;
        }
        return null;
    }

    private string? NearSign()
    {
        var pp = Player.Pos;
        var signs = WD.Village.Signs.Select((s, i) => (s.X, s.Z, Key: $"sign.{i}")).Append((WD.HintSign.X, WD.HintSign.Z, "sign.hint")).Concat(World.ExtraSigns);
        foreach (var (x, z, key) in signs)
            if (MathX.Hypot(pp.X - x, pp.Z - z) < 2.2f) return key;
        return null;
    }

    private void CampfireDialogue()
    {
        Dialogue.Start(name: T("campfire.name"), lines: Lines("campfire.text"), choices: new List<Choice>
        {
            new(T("campfire.morning"), () => Rest(7)),
            new(T("campfire.evening"), () => Rest(19)),
            new(T("campfire.midnight"), () => Rest(23.5f)),
            new(T("choice.cancel")),
        });
    }

    public void FlyOwlToPeak()
    {
        var owl = Npcs.ById["owl"];
        var at = owl.Pos + new Vector3(0, 0.8f, 0);
        Env.Normal.Emit(Emit.At(at, 30, "#ffffff", "#e8d6b8").R(3).L(0.9f).S(0.6f, 1.2f).A(0.8f).D(3));
        Env.Additive.Emit(Emit.At(at, 20, "#ffd76a").R(4).L(1).S(0.3f));
        Audio.Sfx("poof");
        Npcs.MoveOwlToPeak();
        SetFlag("owlMoved");
        After(0.6f, () => Hint(T("hint.owlFlew"), 7));
        Autosave();
    }

    public IEnumerable<string> AchievementsMissing() => Achievements.Missing;
}
