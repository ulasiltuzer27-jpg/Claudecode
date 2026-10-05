using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim;

/// <summary>Deneyim, seviye, basarimlar ve egitim adimlari.</summary>
public static class Progression
{
    public static void AddXp(GameWorld w, int amount)
    {
        var before = w.Level;
        w.Progress.Xp += amount;
        w.Economy.Today.Xp += amount;
        var after = w.Level;
        w.GlobalsDirty = true;
        if (after > before)
        {
            w.Raise(new WorldEvent { Type = WorldEventType.LevelUp, Value = after, Key = "", Arg = "" });
            w.Toast("toast.levelup", $"{after}|level.{Math.Min(after, 12)}", 3);
            w.Progress.MaxStat("level", after);
            CheckAchievements(w);
        }
    }

    public static void CheckAchievements(GameWorld w)
    {
        w.Progress.MaxStat("level", w.Level);
        foreach (var a in w.Data.Achievements)
        {
            if (w.Progress.Achievements.Contains(a.Id))
            {
                continue;
            }

            if (w.Progress.Stat(a.Stat) >= a.Threshold)
            {
                w.Progress.Achievements.Add(a.Id);
                w.GlobalsDirty = true;
                w.Raise(new WorldEvent { Type = WorldEventType.Achievement, Key = a.Id, Arg = "" });
            }
        }
    }

    // ── Egitim ────────────────────────────────────────────────────────

    public const int TutorialSteps = 23;

    /// <summary>Etkilesim aninda hemen yeniden kontrol (gecikmesiz ilerleme).</summary>
    public static void Tutorial(GameWorld w, string signal)
    {
        _ = signal;
        UpdateTutorial(w);
    }

    /// <summary>Durum tabanli egitim: adim kosulu saglandiysa ilerle.</summary>
    public static void UpdateTutorial(GameWorld w)
    {
        if (!w.Progress.TutorialEnabled || w.Progress.TutorialStep >= TutorialSteps)
        {
            return;
        }

        var guard = 0;
        while (guard++ < 4 && w.Progress.TutorialStep < TutorialSteps && StepDone(w, w.Progress.TutorialStep))
        {
            w.Progress.TutorialStep++;
            w.GlobalsDirty = true;
            w.Raise(WorldEvent.Sound("tip", w.Layout.PlayerSpawn));
        }
    }

    public static ItemEntity? MainKazan(GameWorld w) => w.Items.FirstOrDefault(i => i.Type == ItemType.Kazan);

    private static bool StepDone(GameWorld w, int step)
    {
        var k = MainKazan(w);
        var pot = k?.Pot;
        var cart = w.Carts.FirstOrDefault();
        var suzgec = w.Items.FirstOrDefault(i => i.Type == ItemType.Suzgec);
        var riceIn = pot is not null && CookingModel.Grain(pot) > 0;
        var heat = 0;
        if (k is { Attach: Attach.Socket } && w.Get<StationEntity>(k.ParentId) is { Type: StationType.KazanOcagi } oc)
        {
            heat = oc.Heat[0];
        }

        var absorbed = pot is not null && riceIn && pot.WaterL <= 0.01f && pot.WaterAbsorbed > 0.1f;
        var pilavReady = pot is not null && CookingModel.IsPilavFood(pot.Food);
        return step switch
        {
            0 => w.Clock.Minute > w.Data.Balance.DayStartMinute + 4,
            1 => w.Players.Any(p => w.HeldBy(p)?.Type == ItemType.Suzgec) || riceIn,
            2 => (suzgec?.GrainKg ?? 0) >= 2 || riceIn,
            3 => (suzgec is { GrainKg: > 0 } && suzgec.Wash >= 0.85f) || riceIn,
            4 => k is { Attach: Attach.Socket } && w.Get<StationEntity>(k.ParentId)?.Type == StationType.KazanOcagi,
            5 => heat >= 1 || pilavReady,
            6 => (pot?.ButterG ?? 0) >= 100 || pilavReady,
            7 => riceIn,
            8 => (pot?.Toast ?? 0) >= 0.6f || (pot?.WaterL ?? 0) > 0 || pilavReady,
            9 => pot is not null && riceIn && pot.WaterAdded >= CookingModel.Grain(pot) * 1.35f || pilavReady,
            10 => pot is not null && riceIn && pot.SaltG >= CookingModel.Grain(pot) * 8 || pilavReady,
            11 => pot is { Lid: true } || absorbed || pilavReady,
            12 => absorbed || pilavReady,
            13 => (absorbed && heat == 0) || pilavReady,
            14 => pilavReady,
            15 => k is { Attach: Attach.Socket } && w.Get<StationEntity>(k.ParentId)?.Type == StationType.Cart,
            16 => cart is not null && !w.Layout.DepotArea.Contains(cart.Position, 0.5f),
            17 => cart?.Cart is { Spot.Length: > 0 },
            18 => cart?.Cart is { Open: true },
            19 => w.Progress.Stat("served") >= 1,
            20 => w.Progress.Stat("payments") >= 1,
            21 => w.Clock.Minute > _step21Start + 25,
            22 => w.Clock.Day >= 2,
            _ => true,
        };
    }

    private static float _step21Start = float.MaxValue;

    /// <summary>Adim 21 bilgi ekrani: belli bir sure gosterilir.</summary>
    public static void OnTutorialStepShown(GameWorld w)
    {
        if (w.Progress.TutorialStep == 21 && _step21Start > w.Clock.Minute + 1000)
        {
            _step21Start = w.Clock.Minute;
        }
        else if (w.Progress.TutorialStep != 21)
        {
            _step21Start = float.MaxValue;
        }
    }
}
