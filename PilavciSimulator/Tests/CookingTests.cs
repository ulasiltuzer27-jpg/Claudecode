using PilavciSimulator.Sim.Cooking;
using Xunit;
using Xunit.Abstractions;

namespace PilavciSimulator.Tests;

public class CookingTests
{
    private readonly ITestOutputHelper _out;

    public CookingTests(ITestOutputHelper output) => _out = output;

    /// <summary>Tarif: verilen adimlari dakika dakika uygular.</summary>
    private static float Run(PotState p, int heat, float minutes, bool stir = false, Func<PotState, bool>? until = null)
    {
        var t = 0f;
        while (t < minutes)
        {
            if (until is not null && until(p))
            {
                break;
            }

            CookingModel.Step(p, new HeatSource(heat), 0.25f, stir);
            t += 0.25f;
        }

        return t;
    }

    private PotState PerfectPilav(float kg = 2f)
    {
        var p = new PotState { RiceKg = kg, RiceWash = 1f, RiceSoak = 0.6f, ButterG = 100 * kg };
        var heatUp = Run(p, 2, 10, stir: true, until: s => s.Temp > 106);
        var toast = Run(p, 2, 30, stir: true, until: s => s.Toast >= 0.9f);
        p.WaterL = kg * CookingModel.RiceWaterRatio * 1.05f;
        p.SaltG = 14 * kg;
        p.Lid = true;
        var cook = Run(p, 2, 60, until: s => s.WaterL <= 0.005f);
        var rest = Run(p, 0, 15);
        _out.WriteLine($"isinma {heatUp} dk, kavurma {toast} dk, pisme {cook} dk, demleme {rest} dk, su orani {p.WaterAbsorbed / kg:F2}");
        return p;
    }

    [Fact]
    public void Perfect_pilav_scores_high_and_is_ready()
    {
        var p = PerfectPilav();
        _out.WriteLine($"kalite {p.Quality:F1}, ipucu {p.Hint}, yanik {p.Burn:F2}, yemek {p.Food}, kepce {p.Scoops}");
        Assert.Equal(Food.Pilav, p.Food);
        Assert.True(p.Quality >= 90, $"kalite {p.Quality}");
        Assert.Equal(40, p.Scoops);
    }

    [Fact]
    public void Unwashed_and_unsalted_pilav_is_worse_with_hint()
    {
        var p = new PotState { RiceKg = 2, ButterG = 200 };
        Run(p, 2, 30, stir: true, until: s => s.Toast >= 0.9f);
        p.WaterL = 3.1f;
        p.Lid = true;
        Run(p, 2, 60, until: s => s.WaterL <= 0.005f);
        Run(p, 0, 15);
        _out.WriteLine($"kalite {p.Quality:F1} ipucu {p.Hint}");
        Assert.True(p.Quality < 75);
        Assert.Equal("hint.nosalt", p.Hint);
    }

    [Fact]
    public void Leaving_heat_on_after_absorption_burns_the_pot()
    {
        var p = new PotState { RiceKg = 1, RiceWash = 1, ButterG = 100, SaltG = 14 };
        Run(p, 2, 30, stir: true, until: s => s.Toast >= 0.9f);
        p.WaterL = 1.5f;
        p.Lid = true;
        Run(p, 3, 90);
        Assert.Equal(Food.Ruined, p.Food);
        Assert.Equal("hint.burnt", p.Hint);
    }

    [Fact]
    public void Too_much_water_makes_mush()
    {
        var p = new PotState { RiceKg = 1, RiceWash = 1, ButterG = 100, SaltG = 14 };
        Run(p, 2, 30, stir: true, until: s => s.Toast >= 0.9f);
        p.WaterL = 2.6f;
        p.Lid = true;
        Run(p, 2, 90, until: s => s.WaterL <= 0.005f);
        Run(p, 0, 15);
        _out.WriteLine($"kalite {p.Quality:F1} ipucu {p.Hint} oran {p.WaterAbsorbed:F2}");
        Assert.Equal("hint.mushy", p.Hint);
        Assert.True(p.Quality < 80);
    }

    [Fact]
    public void Soaked_chickpeas_cook_in_under_an_hour_and_unsoaked_take_long()
    {
        var soaked = new PotState { ChickpeaKg = 1, ChickpeaSoak = 1, WaterL = 3, SaltG = 12, Lid = true };
        var t1 = Run(soaked, 2, 300, until: s => s.Food == Food.Nohut);
        var raw = new PotState { ChickpeaKg = 1, WaterL = 6, SaltG = 12, Lid = true };
        var t2 = Run(raw, 2, 400, until: s => s.Food == Food.Nohut);
        _out.WriteLine($"islatilmis {t1} dk (q {soaked.Quality:F0}), islatilmamis {t2} dk (q {raw.Quality:F0})");
        Assert.True(t1 < 60);
        Assert.True(t2 > t1 * 2);
        Assert.Equal(16, soaked.Scoops);
        Assert.True(soaked.Quality > raw.Quality);
    }

    [Fact]
    public void Overnight_soaking_softens_chickpeas()
    {
        var p = new PotState { ChickpeaKg = 1, WaterL = 3 };
        CookingModel.Overnight(p, 600);
        Assert.Equal(1f, p.ChickpeaSoak, 3);
    }

    [Fact]
    public void Leftover_pilav_goes_stale_overnight_but_keeps_remaining_scoops()
    {
        var p = PerfectPilav(1);
        var fresh = p.Quality;
        CookingModel.Lock(p);
        p.Scoops -= 7;
        CookingModel.Overnight(p, 480);
        Assert.True(p.Stale);
        Assert.Equal(13, p.Scoops);
        Assert.True(p.Quality <= fresh - 15);
    }

    [Fact]
    public void Mixing_foods_ruins_the_pot()
    {
        var p = new PotState { RiceKg = 1, ChickpeaKg = 0.5f, WaterL = 2 };
        CookingModel.Step(p, HeatSource.None, 1, false);
        Assert.Equal(Food.Ruined, p.Food);
    }

    [Fact]
    public void Chicken_boils_in_about_half_an_hour()
    {
        var p = new PotState { ChickenKg = 1.5f, WaterL = 3, SaltG = 10, Lid = true };
        var t = Run(p, 2, 120, until: s => s.Food == Food.TavukHaslama);
        _out.WriteLine($"tavuk {t} dk, q {p.Quality:F0}");
        Assert.InRange(t, 20, 50);
    }

    [Fact]
    public void Cart_heater_keeps_pilav_warm()
    {
        var p = PerfectPilav(1);
        for (var i = 0; i < 120; i++)
        {
            CookingModel.Step(p, new HeatSource(0, 75f), 1, false);
        }

        Assert.InRange(p.Temp, 70f, 80f);
        Assert.Equal(Food.Pilav, p.Food);
    }
}
