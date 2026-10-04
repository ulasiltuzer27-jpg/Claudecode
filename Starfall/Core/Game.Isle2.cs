using System.Numerics;
using Starfall.Achievements;
using Starfall.Gameplay;
using Starfall.Models;
using Starfall.Render;
using Starfall.UI;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.Core;

/// <summary>
/// Asama B: Kar Adasi, tekne, tirmanma, kizak, buzda balik, kazi + hazine, fotograf gorevi,
/// ev ve ikinci final. Ada 1 akisi (Game.cs) bu kancalari cagirir.
/// </summary>
public sealed partial class Game
{
    public Boats Boats = null!;
    public Digging Digging = null!;
    public PhotoQuest PhotoQuest = null!;
    public Finale2 Finale2 = null!;
    public House House = null!;
    public bool InsideHouse;

    private void BuildIsle2Systems()
    {
        Boats = new Boats(this);
        Digging = new Digging(this);
        PhotoQuest = new PhotoQuest(this);
        Finale2 = new Finale2(this);
        House = new House(this);
        // Kar Adasi esyalari: aurora kristalleri, kabuklar, yelken, eldiven
        foreach (var c in WD2.Crystals) Collectibles.Add("aurora", c.Id, Collectibles.Resolve(c));
        Collectibles.Add("tool", "tool_sail", SailPos(), "sail");
        Collectibles.Add("tool", "mitten", Collectibles.Resolve(WD2.Mitten), "mitten");
        Collectibles.AddShells(WD2Shells.Compute(World), 100);
    }

    /// <summary>Yelken bezi: batik geminin egik guvertesinin ustu (carpismadan olculur).</summary>
    private Vector3 SailPos()
    {
        var sw = WD.Landmarks.Shipwreck;
        var d = World.Anchor("deck");
        var l = WD2.SailLocal;
        float c = MathF.Cos(sw.Yaw), si = MathF.Sin(sw.Yaw);
        var p = new Vector3(sw.X + l.X * c + l.Z * si, d.Y + 2, sw.Z - l.X * si + l.Z * c);
        float y = World.Physics.GroundAt(p.X, p.Z, p.Y, out var gy, out _) ? gy : d.Y - 1.2f;
        return new Vector3(p.X, y + 0.06f, p.Z);
    }

    private void ApplyIsle2Save(SaveData s)
    {
        InsideHouse = false;
        Boats.ApplySave(s);
        Digging.ApplySave(s);
        House.ApplySave(s);
        if (s.Flag("owlIsle2")) MoveOwlToObservatory(false);
        Env.Sky.AuroraBoost = 0;
        // kayit adadaysa ama tekne diger iskeledeyse, oyuncu yine de oldugu yerde baslar
    }

    private void UpdateIsle2(float dt)
    {
        World.UpdateIsle2Fx(dt, Env.Camera.Position, State == GameState.Menu ? Env.Camera.Target : Player.Pos, Env.Sky.Night);
        Boats.Update(dt);
        if (State is GameState.Playing or GameState.Dialogue or GameState.Cutscene)
        {
            Digging.Update(dt);
            Finale2.Update(dt);
            TickIsle2(dt);
        }
    }

    private void TickIsle2(float dt)
    {
        var s = Save;
        if (s == null) return;
        var p = Player;
        // Kar Adasi'na ilk varis
        bool onIsle2 = World.TerrainAt(p.Pos.X, p.Pos.Z)?.Id == "isle2";
        if (onIsle2 && !s.Flag("isle2Visited") && (p.Grounded || p.Boating))
        {
            s.Flags["isle2Visited"] = true;
            Stats.Max("isle2", 1);
            Hud.RegionBanner(T("island.isle2"), T("island.isle2.sub"));
            Audio.Sfx("discover");
            After(2.5f, () => Hint(T("hint.isle2"), 8));
            if (!s.Flag("owlIsle2")) MoveOwlToObservatory(true);
            Autosave();
        }
        // sicak su
        if (p.Swimming && World.InHotSpring(p.Pos.X, p.Pos.Z))
        {
            if (Stats.Get("hotspring") < 1) { Stats.Max("hotspring", 1); Hud.Toast(T("toast.hotspring")); }
            if (Session.FpsN % 20 == 0) Env.Normal.Emit(Emit.At(p.Pos + new Vector3(0, 0.6f, 0), 1, "#ffffff").V(0, 0.6f, 0, 0.2f).L(2).S(0.6f, 1.6f).A(0.3f));
        }
        // buz kaymasi istatistigi
        if (Session.IceSlide >= 1)
        {
            int m = (int)Session.IceSlide;
            Session.IceSlide -= m;
            Stats.Add("ice_m", m);
        }
        // tum kabuklar (Ada 1) -> kabuk canta
        if (s.ShellsTotal >= 100) Wardrobe.Give("back_shell", true);
    }

    private void LeaveSpecialStates()
    {
        Player.ExitSpecialStates();
        InsideHouse = false;
        if (Finale2.Active) Finale2.Active = false;
        Env.Sky.AuroraBoost = 0;
    }

    /// <summary>Oncelikli etkilesimler: tekne, ev, kizak, kazi.</summary>
    private Interaction? IsleInteraction()
    {
        if (Boats.Interaction() is { } b) return b;
        if (Player.Boating) return new Interaction("", () => { }, "none");
        if (House.Interaction() is { } h) return h;
        if (InsideHouse) return null;
        var pp = Player.Pos;
        if (Flag("sledOk") && !Player.Sledding && MathX.Hypot(pp.X - L2.SledTop.X, pp.Z - L2.SledTop.Y) < 3.2f && Race.State == "idle")
        {
            var dir = Vector2.Normalize(L2.SledBottom - L2.SledTop);
            return new Interaction(T("prompt.sled"), () =>
            {
                Player.Spawn(L2.SledTop.X, World.Height(L2.SledTop.X, L2.SledTop.Y) + 0.1f, L2.SledTop.Y, MathF.Atan2(dir.X, dir.Y));
                Player.StartSled(MathF.Atan2(dir.X, dir.Y));
            });
        }
        if (Digging.Interaction() is { } d) return d;
        return null;
    }

    private void OnRegionDiscovered(RegionDef r)
    {
        if (r.Id == "icecave") Stats.Max("icecave", 1);
    }

    private void WireIsle2Events()
    {
        var E = Events;
        E.On("photo", _ => PhotoQuest.OnPhoto());
        E.On("climbTop", e =>
        {
            // egitim duvarinin tepesine ciktiysa
            var w = World.ClimbWalls.FirstOrDefault(c => c.Id == "train");
            if (w.Res == null) return;
            var b = w.Res.Group.Position;
            float horiz = Vector2.Distance(new Vector2(e.Pos.X, e.Pos.Z), new Vector2(b.X, b.Z));
            if (horiz < w.Def.W && e.Pos.Y > w.Def.BaseY + w.Def.H * 0.7f) SetFlag("trainTop");
        });
        E.On("boatIn", _ => Steam.RichPresence("status", T("presence.sailing")));
        E.On("fishCaught", e =>
        {
            if (e.Data as string == "ice") Stats.Max("ice_species", IceSpecies());
        });
    }

    public int IceSpecies() => WD2.Fish.Count(f => Save?.Fish.TryGetValue(f.Id, out var n) == true && n > 0);

    private void OnCollectIsle2(Item item)
    {
        var at = item.Pos;
        if (item.Kind == "aurora")
        {
            Audio.Sfx("aurora");
            Input.Rumble(0.3f, 120);
            Env.Additive.Emit(Emit.At(at, 40, "#7dffd8", "#b18cff", "#ffffff").R(5).G(-2).D(1.5f).L(1.3f).S(0.35f, 0.05f));
            Hud.Toast(T("toast.aurora", ("n", Progress.AuroraCount()), ("total", GameWorld.AuroraTotal)));
            AuroraMilestones();
        }
        else if (item.Kind == "tool")
        {
            Audio.Sfx("item");
            Env.Additive.Emit(Emit.At(at, 14, "#ffffff", "#ffe28a").R(2.5f).G(-3).L(0.8f).S(0.22f));
            Hud.Toast(T($"toast.{item.Id}"));
        }
    }

    private void GrantRewardIsle2(string id, string kind, Vector3 pos)
    {
        if (kind == "aurora")
        {
            Audio.Sfx("aurora");
            Env.Additive.Emit(Emit.At(pos, 40, "#7dffd8", "#b18cff").R(5).G(-2).L(1.2f).S(0.35f));
            Hud.Toast(T("toast.aurora", ("n", Progress.AuroraCount()), ("total", GameWorld.AuroraTotal)));
            AuroraMilestones();
        }
    }

    private void AuroraMilestones()
    {
        int n = Progress.AuroraCount();
        Stats.Max("auroras_max", n);
        if (n >= GameWorld.AuroraTotal && !Flag("hintAuroraReady") && !Flag("finale2"))
        {
            SetFlag("hintAuroraReady");
            After(1.5f, () => Hint(T("hint.auroraReady"), 8));
        }
    }

    /// <summary>Odul belirteci: "map2", "outfit:x", "furn:x", "shells:n", "q_au_x".</summary>
    public void GiveItem(string token)
    {
        var s = Save!;
        if (token.StartsWith("outfit:")) { Wardrobe.Give(token[7..], true); return; }
        if (token.StartsWith("furn:"))
        {
            var id = token[5..];
            if (Furniture.Get(id) == null) return;
            s.Furniture.Add(id);
            Hud.Toast(T("toast.furniture", ("name", T($"furn.{id}"))));
            return;
        }
        if (token.StartsWith("shells:"))
        {
            int n = int.Parse(token[7..]);
            s.Shells += n;
            Hud.Toast(T("toast.digShells", ("n", n)));
            return;
        }
        if (token.StartsWith("q_au_")) { GrantReward(token, "aurora"); return; }
        if (token.StartsWith("map"))
        {
            if (!s.Rewards.Contains(token)) s.Rewards.Add(token);
            Hud.Toast(T("toast.map", ("n", token[3..])));
            Audio.Sfx("item");
        }
    }

    public void MoveOwlToObservatory(bool animate)
    {
        var owl = Npcs.ById["owl"];
        if (animate)
        {
            Env.Normal.Emit(Emit.At(owl.Pos + new Vector3(0, 0.8f, 0), 30, "#ffffff", "#e8d6b8").R(3).L(0.9f).S(0.6f, 1.2f).A(0.8f).D(3));
            Audio.Sfx("poof");
        }
        var p = WD2.OwlIsle2;
        Npcs.PlaceAt(owl, p.X, p.Y, MathF.Atan2(L2.Harbor.X - p.X, L2.Harbor.Y - p.Y));
        SetFlag("owlIsle2");
    }

    /// <summary>Kararip bir is yap, sonra aydinlan (kapilar, isinlanmalar).</summary>
    public void Transition(Action mid, float half = 0.35f)
    {
        var prev = State;
        SetState(GameState.Cutscene);
        Player.Frozen = true;
        float k = 0;
        bool done = false;
        void Step(float dt)
        {
            k += dt / half;
            if (k < 1) Env.Post.Fade = k;
            else if (!done) { done = true; Env.Post.Fade = 1; mid(); }
            else Env.Post.Fade = MathF.Max(0, 2 - k);
            if (k >= 2)
            {
                Env.Post.Fade = 0;
                Player.Frozen = false;
                if (State == GameState.Cutscene) SetState(prev == GameState.Cutscene ? GameState.Playing : prev);
                _tickers.Remove(Step);
            }
        }
        _tickers.Add(Step);
    }

    /// <summary>Oyun icinden bir ekran ac (mola durumunda).</summary>
    public void OpenScreen(UiScreen s)
    {
        SetState(GameState.Paused);
        Ui.Push(s);
    }

    /// <summary>Muzigin "auto" ruh hali: ada + gece/gunduz (+ ev ici).</summary>
    public string Ambience2Mood()
    {
        bool night = Env.Sky.Night > 0.55f;
        if (InsideHouse) return "house";
        bool snow = World.IsSnow(Player.Pos.X, Player.Pos.Z);
        if (snow) return night ? "aurora" : "snow";
        return night ? "night" : "day";
    }
}

/// <summary>
/// Kar Adasi cakil kumsali kabuklari (8 grup x 5 = 40; kimlikler sh_100..). Grup yerleri ada
/// merkezinden verilen acilarda kiyi cizgisi aranarak bulunur (kiyi gurultuyle sekillendigi icin).
/// </summary>
public static class WD2Shells
{
    public static readonly float[] Angles = { 200, 235, 250, 160, 120, 60, 20, 300 };

    public static (float X, float Z, float Deg)[] Compute(GameWorld w)
    {
        var list = new List<(float, float, float)>();
        foreach (var deg in Angles)
        {
            float a = deg * MathX.Pi / 180;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            float best = 140;
            for (float r = 90; r < 200; r += 0.5f)
            {
                var p = L2.C + dir * r;
                if (w.Height(p.X, p.Y) < 1.5f) { best = r - 2.5f; break; }
            }
            var c = L2.C + dir * best;
            // kiyiya teget dizilsin
            list.Add((c.X, c.Y, deg + 90));
        }
        return list.ToArray();
    }
}
