using System.Globalization;
using System.Numerics;
using Starfall.Gameplay;
using Starfall.UI;

namespace Starfall.Core;

/// <summary>
/// Komut satiri sahneleri (ekran goruntusu ve elle deneme icin):
///   --new N / --slot N      yeni oyun / yuva yukle
///   --pos x,y,z --yaw a     isinla
///   --hour h                saat
///   --cam x,y,z --look x,y,z  sabit kamera
///   --ui pause|journal|settings|achievements|slots|wardrobe|credits
///   --capture yol [--frames N]  N kare sonra PNG kaydet ve cik
/// </summary>
public sealed partial class Game
{
    private int _startupFrames = -1;

    public static Vector3 ParseVec(string s)
    {
        var p = s.Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(p[0], p[1], p[2]);
    }

    private float F(string k, float def) => Args.TryGetValue(k, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : def;

    private void RunStartupArgs()
    {
        if (Args.TryGetValue("new", out var ns)) NewGame(int.Parse(ns));
        else if (Args.TryGetValue("slot", out var ss)) LoadSlot(int.Parse(ss));
        ApplyScene();
        if (Args.TryGetValue("capture", out _)) _startupFrames = (int)F("frames", 8);
    }

    /// <summary>Oyun durumundan sonra (her yuklemede) sahne argumanlarini uygula.</summary>
    private void ApplyScene()
    {
        if (Save != null)
        {
            if (Args.TryGetValue("pos", out var ps))
            {
                var p = ParseVec(ps);
                Player.Spawn(p.X, p.Y, p.Z, F("yaw", Player.Yaw));
                CameraRig.Snap(Player.Pos, Player.Yaw + MathF.PI);
            }
            if (Args.ContainsKey("hour")) SetHour(F("hour", Hour));
            if (Args.TryGetValue("give", out var give))
                foreach (var id in give.Split(',')) Wardrobe.Give(id, false);
            if (Args.TryGetValue("wear", out var wear))
                foreach (var id in wear.Split(','))
                    if (Models.Outfits.Get(id) is { } o) { Wardrobe.Give(id, false); Wardrobe.Wear(o.Slot, id); }
        }
        else if (Args.ContainsKey("hour")) Hour = F("hour", Hour);
        if (Args.TryGetValue("cam", out var cs))
        {
            var look = Args.TryGetValue("look", out var ls) ? ParseVec(ls) : Player.Pos;
            CameraRig.Override = new CamOverride(ParseVec(cs), look, 1000);
            if (Args.ContainsKey("hideplayer")) Player.Model.Root.Visible = false;
        }
        if (Args.TryGetValue("ui", out var ui))
        {
            if (Save != null && ui != "credits") { SetState(GameState.Paused); Ui.Push(new PauseMenu(Ui)); }
            switch (ui)
            {
                case "journal": Ui.Push(new JournalScreen(Ui)); break;
                case "settings": Ui.Push(new SettingsScreen(Ui)); break;
                case "achievements": Ui.Push(new AchievementsScreen(Ui)); break;
                case "slots": Ui.Push(new SlotsScreen(Ui)); break;
                case "wardrobe": Ui.Push(new WardrobeScreen(Ui)); break;
                case "credits": Credits.Show(null); break;
            }
        }
        if (Args.TryGetValue("say", out var say)) Hud.Toast(say);
        if (Args.TryGetValue("talk", out var npc) && Npcs.ById.TryGetValue(npc, out var n)) Quests.Talk(n);
    }

    /// <summary>Host her karede cagirir: yakalama sayaci.</summary>
    public void StartupTick()
    {
        if (_startupFrames < 0) return;
        if (--_startupFrames == 0)
        {
            PendingCapture = Args["capture"];
            After(0.05f, () => QuitRequested = true);
        }
    }
}
