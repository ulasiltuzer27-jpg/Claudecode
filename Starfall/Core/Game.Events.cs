using System.Numerics;
using Starfall.Gameplay;
using Starfall.Render;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.Core;

public sealed partial class Game
{
    private void WireEvents()
    {
        var E = Events;
        E.On("jump", e =>
        {
            Audio.Sfx("jump");
            Stats.Add("jumps", 1);
            Env.Normal.Emit(Emit.At(e.Pos, 6, "#f2e6cf").Sp(0.25f).V(0, 0.8f, 0, 1.2f).L(0.5f).S(0.35f, 0.7f).A(0.6f).D(3));
        });
        E.On("flap", e =>
        {
            Audio.Sfx("flap");
            Input.Rumble(0.25f, 80);
            Env.Additive.Emit(Emit.At(e.Pos + new Vector3(0, 0.5f, 0), 18, "#ffd76a", "#fff2a8", "#ffb627").R(3.5f).G(-3).D(2).L(0.8f).S(0.28f, 0.05f));
            int used = (int)e.A, total = (int)e.B;
            if (used == total && total > 0 && !Flag("hintFlapsOut"))
            {
                SetFlag("hintFlapsOut");
                Hint(T("hint.flapsOut"));
            }
        });
        E.On("land", e =>
        {
            float impact = e.A;
            if (impact < 3) return;
            Audio.Sfx("land", impact);
            var col = World.IsSnow(e.Pos.X, e.Pos.Z) ? "#f4f8ff" : "#efe2c8";
            Env.Normal.Emit(Emit.At(e.Pos, (int)MathF.Min(16, 4 + impact), col).Sp(0.3f).V(0, 0.5f, 0, 2).L(0.6f).S(0.4f, 0.9f).A(0.55f).D(4));
            if (impact > 22)
            {
                CameraRig.Shake = MathF.Min(1, impact / 40);
                Input.Rumble(0.4f, 120);
            }
        });
        E.On("splash", e =>
        {
            Audio.Sfx("splash", e.A);
            float wl = World.WaterLevel(e.Pos.X, e.Pos.Z);
            Env.Normal.Emit(Emit.At(new Vector3(e.Pos.X, wl, e.Pos.Z), 24, "#ffffff").R(3 + e.A * 4).G(-12).L(0.8f).S(0.2f).A(0.85f));
        });
        E.On("step", e => Audio.Sfx("step:" + e.Key));
        E.On("bounce", e =>
        {
            Audio.Sfx("bounce");
            if (e.Data is Physics.Shape sh && World.Mushrooms.TryGetValue(sh, out var m)) m.Squish = 1;
            Env.Additive.Emit(Emit.At(e.Pos, 12, "#ff9df0", "#ffffff", "#b98aff").R(3).G(-4).L(0.7f).S(0.25f));
        });
        E.On("glideStart", _ =>
        {
            if (!Flag("hintGlide"))
            {
                SetFlag("hintGlide");
                Hint(T("hint.glide"));
            }
        });
        E.On("toast", e => Hud.Toast(e.Data is (string, object)[] p ? T(e.Key!, p) : T(e.Key!)));
        E.On("achievement", e =>
        {
            Hud.Achievement((Achievements.AchievementDef)e.Data!);
            Audio.Sfx("achievement");
            SaveProfile();
        });
        E.On("fishCaught", _ => Progress.RefreshStats());
        WireIsle2Events();
    }

    public void OnCollect(Item item)
    {
        var s = Save;
        if (s == null) return;
        if (!s.Collected.Contains(item.Id)) s.Collected.Add(item.Id);
        var at = item.Pos;
        switch (item.Kind)
        {
            case "shell":
                s.Shells++;
                s.ShellsTotal++;
                Audio.Sfx("shell");
                Env.Additive.Emit(Emit.At(at, 8, "#ffd0c0", "#ffffff").R(2).G(-3).L(0.6f).S(0.18f));
                if (!Flag("hintShell")) { SetFlag("hintShell"); Hint(T("hint.shell")); }
                break;
            case "shard":
                Audio.Sfx("shard");
                Input.Rumble(0.3f, 120);
                Env.Additive.Emit(Emit.At(at, 40, "#fff2a8", "#ffd76a", "#ffffff").R(5).G(-2).D(1.5f).L(1.2f).S(0.35f, 0.05f));
                Hud.Toast(T("toast.shard", ("n", Progress.ShardCount()), ("total", WD.ShardTotal)));
                ShardMilestones();
                break;
            case "feather":
                Audio.Sfx("feather");
                Env.Additive.Emit(Emit.At(at, 30, "#ffd76a", "#ffb627").R(4).G(-2).D(1.5f).L(1.1f).S(0.3f));
                Hud.Toast(T("toast.feather", ("n", Progress.FeatherCount())));
                if (!Flag("hintFeather2")) { SetFlag("hintFeather2"); Hint(T("hint.feather", ("jump", Input.Glyph("jump")))); }
                break;
            case "carrot":
            case "tool":
                Audio.Sfx("item");
                Env.Additive.Emit(Emit.At(at, 14, "#ffffff", "#ffe28a").R(2.5f).G(-3).L(0.8f).S(0.22f));
                Hud.Toast(T($"toast.{item.Id}"));
                break;
            default:
                OnCollectIsle2(item);
                break;
        }
        Events.Emit("collect", at, key: item.Kind, data: item);
        Progress.RefreshStats();
        Autosave();
    }

    public void GrantReward(string id, string kind)
    {
        var s = Save!;
        if (s.Rewards.Contains(id)) return;
        s.Rewards.Add(id);
        var pos = Player.Pos + new Vector3(0, 1.2f, 0);
        if (kind == "feather")
        {
            Audio.Sfx("feather");
            Env.Additive.Emit(Emit.At(pos, 30, "#ffd76a", "#ffb627").R(4).G(-2).L(1.1f).S(0.3f));
            Hud.Toast(T("toast.feather", ("n", Progress.FeatherCount())));
        }
        else if (kind == "shard")
        {
            Audio.Sfx("shard");
            Env.Additive.Emit(Emit.At(pos, 40, "#fff2a8", "#ffd76a").R(5).G(-2).L(1.2f).S(0.35f));
            Hud.Toast(T("toast.shard", ("n", Progress.ShardCount()), ("total", WD.ShardTotal)));
            ShardMilestones();
        }
        else GrantRewardIsle2(id, kind, pos);
        Progress.RefreshStats();
        Autosave();
    }

    private void ShardMilestones()
    {
        int n = Progress.ShardCount();
        if (n >= WD.ShardsForFinale && !Flag("hintReady") && !Flag("finale"))
        {
            SetFlag("hintReady");
            After(1.5f, () => Hint(T("hint.ready", ("n", WD.ShardsForFinale)), 8));
        }
    }
}
