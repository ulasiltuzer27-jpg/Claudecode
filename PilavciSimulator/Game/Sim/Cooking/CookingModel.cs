namespace PilavciSimulator.Sim.Cooking;

/// <summary>Tencerenin bu dakikadaki isi kaynagi.</summary>
public readonly record struct HeatSource(int Level, float HoldTarget = 0f)
{
    /// <summary>Ocak yok, kendi haline birakildi.</summary>
    public static readonly HeatSource None = new(0);
}

/// <summary>
/// Pisirme kurallari. Saf fonksiyonlar: yalnizca <see cref="PotState"/>'i
/// degistirir, dunyaya dokunmaz; bu yuzden pencere acmadan test ediliyor
/// (bkz. Tests/CookingTests.cs).
///
/// Turk usulu pilav, kabaca:
///   1. Pirinci yika (nisasta gitsin), istersen ılık suda bekletip suz.
///   2. Tereyaginda kavur (taneler seffaflasana dek, karistirarak).
///   3. Pirincin bir bucuk kati sicak su ve tuz ekle, kapagi kapat.
///   4. Kaynayinca ateşi kis, su cekilene kadar pisir.
///   5. Ocagi kapat, kapagin altina bez koyup 10-20 dakika demlendir.
/// Model bu adimlarin her birini puanliyor; atlanan ya da abartilan adim
/// kaliteyi dusuruyor ve oyuncuya bir ipucu birakiyor.
/// </summary>
public static class CookingModel
{
    public const float Ambient = 21f;
    public const float RiceWaterRatio = 1.5f;
    public const float BulgurWaterRatio = 2.0f;
    /// <summary>1 kg kuru pirinc = 10 porsiyon = 20 kepce.</summary>
    public const float ScoopsPerKg = 20f;
    public const float NohutServingsPerKg = 16f;
    public const float FasulyeServingsPerKg = 10f;
    public const float ChickenServingsPerKg = 12f;
    /// <summary>Demleme bu kadar dakikayi gecince pilav "hazir" olur.</summary>
    public const float RestReady = 8f;

    public static float Grain(PotState p) => p.RiceKg + p.BulgurKg;

    public static bool IsPilavFood(Food f) => f is Food.Pilav or Food.BulgurPilav or Food.EtliPilav;

    /// <summary>Bir pisirme dakikasi (dt oyun dakikasi).</summary>
    public static void Step(PotState p, HeatSource heat, float dt, bool stirring)
    {
        if (dt <= 0 || p.Food == Food.Ruined && p.IsEmpty)
        {
            return;
        }

        var h = Math.Clamp(heat.Level, 0, 3);
        var grain = Grain(p);
        var hasWater = p.WaterL > 0.005f;
        var mass = p.WaterL + grain + p.ChickpeaKg + p.ChickenKg + p.BeansKg + p.MeatKg + 0.6f;

        // ── Sicaklik ─────────────────────────────────────────────────
        float target;
        float rate;
        if (h > 0)
        {
            target = hasWater ? 100f : 120f + 45f * h;
            rate = h * 22f / (1f + mass * 0.22f);
        }
        else if (heat.HoldTarget > 0)
        {
            // Araba isiticisi: sicakligi korur, kaynatmaz.
            target = heat.HoldTarget;
            rate = 3f;
        }
        else
        {
            target = Ambient;
            rate = (p.Lid ? 0.35f : 0.8f) * (1f + 4f / (mass + 1f));
        }

        if (p.Temp < target)
        {
            p.Temp = MathF.Min(target, p.Temp + rate * dt);
        }
        else
        {
            var cool = h > 0 ? rate : MathF.Max(rate, 0.3f);
            p.Temp = MathF.Max(target, p.Temp - cool * dt);
        }

        var boiling = hasWater && p.Temp >= 98f;
        var dry = !hasWater;

        // ── Islatma (ateşsiz, suda, ılık) ───────────────────────────
        if (hasWater && h == 0 && p.Temp < 50f)
        {
            if (p.ChickpeaKg > 0)
            {
                p.ChickpeaSoak = MathF.Min(1f, p.ChickpeaSoak + dt / 480f);
            }

            if (p.BeansKg > 0)
            {
                p.BeansSoak = MathF.Min(1f, p.BeansSoak + dt / 600f);
            }
        }

        // ── Et kizartma (etli pilav) ─────────────────────────────────
        if (p.MeatKg > 0 && dry && p.ButterG > 0 && p.Temp > 100f)
        {
            p.MeatCook = MathF.Min(1.5f, p.MeatCook + dt * 0.07f * h * (stirring ? 1.5f : 1f));
        }

        // ── Kavurma ──────────────────────────────────────────────────
        if (grain > 0 && dry && p.Temp > 105f && p.WaterAbsorbed < 0.01f)
        {
            if (p.ButterG > 0)
            {
                p.Toast += dt * 0.055f * h * (stirring ? 1f : 0.55f);
                if (!stirring)
                {
                    p.UnstirredHotMinutes += dt;
                    if (h >= 2)
                    {
                        p.Burn += dt * 0.012f * h;
                    }
                }
            }
            else
            {
                // Yagsiz kavurma: dibe yapisir.
                p.Toast += dt * 0.03f * h;
                p.Burn += dt * 0.03f * h;
            }

            if (p.Toast > 1.6f)
            {
                p.Burn += dt * 0.04f * h;
            }
        }

        // ── Su cekme (pirinc/bulgur) ─────────────────────────────────
        if (grain > 0 && boiling)
        {
            var absorb = grain * 0.11f * (0.5f + 0.35f * h) * (p.Lid ? 1.15f : 1f) * dt;
            absorb = MathF.Min(absorb, p.WaterL);
            p.WaterL -= absorb;
            p.WaterAbsorbed += absorb;
            if (h >= 3)
            {
                p.HighHeatMinutes += dt;
            }
        }
        else if (grain > 0 && hasWater && p.Temp > 60f)
        {
            var slow = MathF.Min(p.WaterL, grain * 0.01f * dt);
            p.WaterL -= slow;
            p.WaterAbsorbed += slow;
        }

        // ── Haslama (nohut/tavuk/fasulye) ────────────────────────────
        if (boiling && (p.ChickpeaKg > 0 || p.ChickenKg > 0 || p.BeansKg > 0))
        {
            var heatK = 0.6f + 0.25f * h;
            float r;
            if (p.ChickenKg > 0)
            {
                r = 1f / 35f;
            }
            else if (p.BeansKg > 0)
            {
                r = 1f / 75f * (0.15f + 0.85f * p.BeansSoak);
            }
            else
            {
                r = p.Canned ? 1f / 20f : 1f / 50f * (0.25f + 0.75f * p.ChickpeaSoak);
            }

            p.Boil += r * heatK * dt;
        }

        // ── Buharlasma ──────────────────────────────────────────────
        if (boiling)
        {
            var evap = (p.Lid ? 0.008f : 0.035f) * h * dt;
            p.WaterL = MathF.Max(0, p.WaterL - evap);
        }

        // ── Su bitti ama ates acik: dibi tutar ──────────────────────
        var cookedGrain = grain > 0 && p.WaterAbsorbed > 0.05f;
        if (dry && h > 0 && (cookedGrain || p.Boil > 0.05f))
        {
            p.Burn += dt * 0.05f * h;
        }

        // ── Demleme ─────────────────────────────────────────────────
        if (cookedGrain && dry && h == 0 && heat.HoldTarget <= 0)
        {
            p.Rest += dt * (p.Lid ? 1f : 0.4f);
        }
        else if (cookedGrain && dry && heat.HoldTarget > 0)
        {
            // Araba isiticisinda da demlenmeye devam eder (kapak kapaliysa).
            p.Rest += dt * (p.Lid ? 0.8f : 0.3f);
        }

        p.Burn = MathF.Min(p.Burn, 1.2f);
        UpdateResult(p);
    }

    /// <summary>Yemegin turunu ve (kilitli degilse) kalitesini gunceller.</summary>
    public static void UpdateResult(PotState p)
    {
        if (p.Burn >= 1f && p.Food != Food.Ruined)
        {
            p.Food = Food.Ruined;
            p.Scoops = 0;
            p.Quality = 0;
            p.Hint = "hint.burnt";
            return;
        }

        if (p.Food == Food.Ruined)
        {
            return;
        }

        var grain = Grain(p);
        var kinds = (grain > 0 ? 1 : 0) + (p.ChickpeaKg > 0 ? 1 : 0) + (p.ChickenKg > 0 ? 1 : 0) + (p.BeansKg > 0 ? 1 : 0);
        if (kinds > 1)
        {
            p.Food = Food.Ruined;
            p.Hint = "hint.mixed";
            p.Scoops = 0;
            return;
        }

        if (p.Locked)
        {
            return;
        }

        if (grain > 0)
        {
            var ready = p.WaterL <= 0.005f && p.WaterAbsorbed > 0.05f && p.Rest >= RestReady;
            var food = p.BulgurKg > p.RiceKg ? Food.BulgurPilav : p.MeatKg > 0.05f ? Food.EtliPilav : Food.Pilav;
            p.Food = ready ? food : Food.Raw;
            p.Quality = EvaluatePilav(p, out var hint);
            p.Hint = hint;
            if (ready)
            {
                p.Scoops = MathF.Floor(grain * ScoopsPerKg);
            }
        }
        else if (p.ChickpeaKg > 0)
        {
            p.Food = p.Boil >= 1f ? Food.Nohut : Food.Raw;
            p.Quality = EvaluateBoiled(p, out var hint);
            p.Hint = hint;
            if (p.Food == Food.Nohut)
            {
                p.Scoops = MathF.Floor(p.ChickpeaKg * NohutServingsPerKg);
            }
        }
        else if (p.BeansKg > 0)
        {
            p.Food = p.Boil >= 1f ? Food.Fasulye : Food.Raw;
            p.Quality = EvaluateBoiled(p, out var hint);
            p.Hint = hint;
            if (p.Food == Food.Fasulye)
            {
                p.Scoops = MathF.Floor(p.BeansKg * FasulyeServingsPerKg);
            }
        }
        else if (p.ChickenKg > 0)
        {
            p.Food = p.Boil >= 1f ? Food.TavukHaslama : Food.Raw;
            p.Quality = EvaluateBoiled(p, out var hint);
            p.Hint = hint;
        }
        else
        {
            p.Food = p.IsEmpty ? Food.None : Food.Raw;
            p.Quality = 0;
            p.Hint = null;
        }
    }

    /// <summary>Ilk kepce: kaliteyi dondurur.</summary>
    public static void Lock(PotState p)
    {
        if (!p.Locked)
        {
            UpdateResult(p);
            p.Locked = true;
        }
    }

    /// <summary>
    /// Pilav kalitesi 0..100 ve en buyuk kusurun ipucu anahtari.
    /// Mukemmel bir pilav ~95 alir; her hata belli bir cezayla dusurur.
    /// </summary>
    public static float EvaluatePilav(PotState p, out string? hint)
    {
        var grain = Grain(p);
        if (grain <= 0)
        {
            hint = null;
            return 0;
        }

        var bulgur = p.BulgurKg > p.RiceKg;
        var penalties = new List<(float Value, string Hint)>();
        var q = 96f;

        // Su orani
        var ideal = bulgur ? BulgurWaterRatio : RiceWaterRatio;
        var ratio = p.WaterAbsorbed / grain;
        var dev = MathF.Abs(ratio - ideal) / ideal;
        var waterPen = Math.Clamp((dev - 0.08f) / 0.25f, 0f, 1f) * 40f;
        penalties.Add((waterPen, ratio < ideal ? "hint.undercooked" : "hint.mushy"));

        // Yikama / islatma (yalniz pirinc)
        if (!bulgur)
        {
            penalties.Add(((1f - Math.Clamp(p.RiceWash, 0f, 1f)) * 12f, "hint.unwashed"));
            q += Math.Clamp(p.RiceSoak, 0f, 1f) * 4f;
        }

        // Tereyagi (g/kg)
        var butter = p.ButterG / grain;
        var butterPen = butter < 40 ? 15f : butter < 80 ? (80 - butter) / 40f * 12f : butter > 180 ? MathF.Min(10f, (butter - 180) / 15f) : 0f;
        penalties.Add((butterPen, butter < 80 ? "hint.nobutter" : "hint.oily"));

        // Kavurma
        var toastPen = p.Toast < 0.3f ? (bulgur ? 4f : 10f) : p.Toast < 0.6f ? (0.6f - p.Toast) / 0.3f * 5f : p.Toast > 1.5f ? 10f : 0f;
        penalties.Add((toastPen, p.Toast < 0.6f ? "hint.untoasted" : "hint.overtoasted"));

        // Tuz (g/kg)
        var salt = p.SaltG / grain;
        var saltPen = salt < 1 ? 18f : salt < 8 ? (8 - salt) / 7f * 10f : salt > 26 ? MathF.Min(18f, (salt - 26) * 1.5f) : 0f;
        penalties.Add((saltPen, salt < 8 ? "hint.nosalt" : "hint.salty"));

        // Demleme
        var restPen = p.Rest < 5 ? 12f : p.Rest < 10 ? (10 - p.Rest) / 5f * 6f : 0f;
        penalties.Add((restPen, "hint.norest"));

        // Ates yonetimi
        penalties.Add((MathF.Min(p.HighHeatMinutes * 1.2f, 12f), "hint.highheat"));
        penalties.Add((MathF.Min(p.UnstirredHotMinutes * 1.2f, 10f), "hint.unstirred"));
        penalties.Add((p.Burn * 60f, "hint.burning"));

        // Etli pilav
        if (p.MeatKg > 0.05f)
        {
            var meatPen = p.MeatCook < 1f ? (1f - p.MeatCook) * 18f : 0f;
            penalties.Add((meatPen, "hint.rawmeat"));
            var meatRatio = p.MeatKg / grain;
            if (meatRatio < 0.12f)
            {
                penalties.Add((6f, "hint.littlemeat"));
            }
        }

        if (p.Stale)
        {
            penalties.Add((20f, "hint.stale"));
        }

        var total = 0f;
        var worst = (0f, (string?)null);
        foreach (var (v, h) in penalties)
        {
            total += v;
            if (v > worst.Item1)
            {
                worst = (v, h);
            }
        }

        hint = worst.Item1 >= 3f ? worst.Item2 : null;
        return Math.Clamp(q - total, 0f, 100f);
    }

    /// <summary>Haslanan yemeklerin (nohut, tavuk, fasulye) kalitesi.</summary>
    public static float EvaluateBoiled(PotState p, out string? hint)
    {
        var penalties = new List<(float Value, string Hint)>();
        var q = 92f;
        var kg = p.ChickpeaKg + p.ChickenKg + p.BeansKg;
        if (kg <= 0)
        {
            hint = null;
            return 0;
        }

        if (p.ChickpeaKg > 0 && !p.Canned)
        {
            penalties.Add(((1f - p.ChickpeaSoak) * 16f, "hint.unsoaked"));
        }
        else if (p.Canned)
        {
            q = 76f;
        }

        if (p.BeansKg > 0)
        {
            penalties.Add(((1f - p.BeansSoak) * 18f, "hint.unsoaked"));
            q += MathF.Min(p.ButterG / kg / 20f, 5f);
        }

        if (p.Boil < 1f)
        {
            penalties.Add(((1f - p.Boil) * 40f, "hint.undercooked"));
        }
        else if (p.Boil > 1.6f)
        {
            penalties.Add((MathF.Min((p.Boil - 1.6f) * 25f, 20f), "hint.overboiled"));
        }

        var salt = p.SaltG / kg;
        penalties.Add((salt < 4 ? 10f : salt > 30 ? MathF.Min(15f, (salt - 30) * 1.2f) : 0f, salt < 4 ? "hint.nosalt" : "hint.salty"));
        penalties.Add((p.Burn * 60f, "hint.burning"));
        if (p.Stale)
        {
            penalties.Add((18f, "hint.stale"));
        }

        var total = 0f;
        var worst = (0f, (string?)null);
        foreach (var (v, h) in penalties)
        {
            total += v;
            if (v > worst.Item1)
            {
                worst = (v, h);
            }
        }

        hint = worst.Item1 >= 3f ? worst.Item2 : null;
        return Math.Clamp(q - total, 0f, 100f);
    }

    /// <summary>Gece gecince (ertesi gune devreden) yemek bayatlar, soguk kalir.</summary>
    public static void Overnight(PotState p, float minutes)
    {
        // Islatma gece boyunca devam eder (kapak kapali/acik fark etmez).
        if (p.WaterL > 0.05f)
        {
            if (p.ChickpeaKg > 0)
            {
                p.ChickpeaSoak = MathF.Min(1f, p.ChickpeaSoak + minutes / 480f);
            }

            if (p.BeansKg > 0)
            {
                p.BeansSoak = MathF.Min(1f, p.BeansSoak + minutes / 600f);
            }
        }

        if (p.Food is Food.Pilav or Food.BulgurPilav or Food.EtliPilav or Food.Nohut or Food.Fasulye or Food.TavukHaslama)
        {
            // Kalan kepce sayisi korunur: yeniden hesaplanirsa dun satilanlar geri gelirdi.
            var scoops = p.Scoops;
            var wasLocked = p.Locked;
            p.Stale = true;
            p.Locked = false;
            UpdateResult(p);
            if (wasLocked)
            {
                p.Scoops = scoops;
            }

            p.Locked = true;
        }

        p.Temp = Ambient;
    }
}
