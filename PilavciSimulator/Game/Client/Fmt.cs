using System.Globalization;
using System.Text;
using PilavciSimulator.Localization;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Client;

/// <summary>Ekrana yazilan sayilar ve yemek adlari.</summary>
public static class Fmt
{
    /// <summary>1250 -> "1.250 ₺" (TR) / "₺1,250" (EN).</summary>
    public static string Money(long v)
    {
        var neg = v < 0;
        var s = Math.Abs(v).ToString("N0", CultureInfo.InvariantCulture);
        if (Loc.CurrentCode == "tr")
        {
            s = s.Replace(',', '.');
            return (neg ? "-" : "") + s + " ₺";
        }

        return (neg ? "-" : "") + "₺" + s;
    }

    public static string Kg(float kg) => kg.ToString(kg < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + " kg";

    public static string Liters(float l) => l.ToString("0.0", CultureInfo.InvariantCulture) + " L";

    public static string Pct(float v) => ((int)MathF.Round(v * 100)).ToString(CultureInfo.InvariantCulture) + "%";

    public static string FoodName(Food f) => Loc.T("food." + f.ToString().ToLowerInvariant());

    /// <summary>Siparisin kisa metni: "Tam nohutlu pilav + ayran, paket".</summary>
    public static string Order(OrderSpec o)
    {
        var sb = new StringBuilder();
        sb.Append(Loc.T(Interactions_ScoopKey(o.Scoops)));
        sb.Append(' ');
        sb.Append(Loc.T("menu." + o.MenuId));
        if (o.Pepper)
        {
            sb.Append(", ").Append(Loc.T("order.pepper"));
        }

        if (o.Ayran)
        {
            sb.Append(" + ").Append(Loc.T("menu.ayran"));
        }

        if (o.Tursu)
        {
            sb.Append(" + ").Append(Loc.T("menu.tursu"));
        }

        if (o.Package)
        {
            sb.Append(" (").Append(Loc.T("order.package")).Append(')');
        }

        return sb.ToString();
    }

    public static IEnumerable<string> OrderLines(OrderSpec o)
    {
        yield return Loc.T(Interactions_ScoopKey(o.Scoops)) + " " + Loc.T("menu." + o.MenuId);
        if (o.Pepper)
        {
            yield return "+ " + Loc.T("order.pepper");
        }

        if (o.Ayran)
        {
            yield return "+ " + Loc.T("menu.ayran");
        }

        if (o.Tursu)
        {
            yield return "+ " + Loc.T("menu.tursu");
        }

        yield return o.Package ? Loc.T("order.package") : Loc.T("order.plate");
    }

    private static string Interactions_ScoopKey(int scoops) => scoops switch
    {
        1 => "size.yarim",
        2 => "size.tam",
        _ => "size.duble",
    };

    /// <summary>Tabagin icerigi: "Tam pilav, nohut, tavuk, karabiber".</summary>
    public static string Serving(ServingState s)
    {
        if (s.Dirty && s.IsEmpty)
        {
            return Loc.T("plate.dirty");
        }

        if (s.IsEmpty)
        {
            return Loc.T("plate.empty");
        }

        var parts = new List<string>();
        if (s.Scoops > 0)
        {
            parts.Add(Loc.T(Interactions_ScoopKey(s.Scoops)) + " " + FoodName(s.Base).ToLowerInvariant());
        }

        if (s.Nohut)
        {
            parts.Add(Loc.T("topping.nohut"));
        }

        if (s.Tavuk)
        {
            parts.Add(Loc.T("topping.tavuk"));
        }

        if (s.Fasulye)
        {
            parts.Add(Loc.T("topping.fasulye"));
        }

        if (s.Pepper)
        {
            parts.Add(Loc.T("order.pepper"));
        }

        if (s.Tursu)
        {
            parts.Add(Loc.T("menu.tursu"));
        }

        if (s.Ayran)
        {
            parts.Add(Loc.T("menu.ayran"));
        }

        return string.Join(", ", parts);
    }

    /// <summary>Kabin durumu (oyuncuya yol gosteren tek satir).</summary>
    public static string PotStatus(PotState p, int heat)
    {
        if (p.Food == Food.Ruined)
        {
            return Loc.T("pot.ruined");
        }

        if (p.Food is not (Food.None or Food.Raw))
        {
            return Loc.T("pot.ready", FoodName(p.Food), (int)p.Scoops);
        }

        var grain = CookingModel.Grain(p);
        if (grain > 0)
        {
            if (p.WaterAbsorbed < 0.05f && p.WaterL <= 0.005f)
            {
                return p.Temp > 105 ? Loc.T("pot.toasting", Pct(MathF.Min(1, p.Toast))) : Loc.T("pot.raw_grain");
            }

            if (p.WaterL > 0.005f)
            {
                return p.Temp >= 98 ? Loc.T("pot.absorbing", Pct(p.WaterAbsorbed / (p.WaterAbsorbed + p.WaterL))) : Loc.T("pot.heating", (int)p.Temp);
            }

            if (heat > 0)
            {
                return Loc.T("pot.turn_off");
            }

            return Loc.T("pot.resting", (int)p.Rest, (int)CookingModel.RestReady);
        }

        if (p.ChickpeaKg > 0 || p.ChickenKg > 0 || p.BeansKg > 0)
        {
            if (p.WaterL <= 0.005f)
            {
                return Loc.T("pot.need_water");
            }

            if (heat == 0 && p.Temp < 50)
            {
                return p.ChickpeaKg > 0 || p.BeansKg > 0 ? Loc.T("pot.soaking", Pct(p.ChickpeaKg > 0 ? p.ChickpeaSoak : p.BeansSoak)) : Loc.T("pot.need_heat");
            }

            return p.Temp >= 98 ? Loc.T("pot.boiling", Pct(MathF.Min(1, p.Boil))) : Loc.T("pot.heating", (int)p.Temp);
        }

        if (p.ButterG > 0)
        {
            return p.Temp > 60 ? Loc.T("pot.butter_melted") : Loc.T("pot.butter");
        }

        return p.WaterL > 0 ? Loc.T("pot.water_only") : Loc.T("pot.empty");
    }
}
