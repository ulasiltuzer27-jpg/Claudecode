using PilavciSimulator.Net;

namespace PilavciSimulator.Sim.Cooking;

/// <summary>Kazan ya da tencerede pisen yemegin sonuc turu.</summary>
public enum Food : byte
{
    None = 0,
    /// <summary>Henuz hazir degil (hammadde ya da pisiyor).</summary>
    Raw,
    Pilav,
    BulgurPilav,
    EtliPilav,
    Nohut,
    TavukHaslama,
    Fasulye,
    /// <summary>Yandi ya da cig kaldi: satilamaz.</summary>
    Ruined,
}

/// <summary>
/// Bir kabin icerigi ve pisirme durumu. Tum miktarlar gercek birimde
/// (kg, litre, gram, santigrat); zamanlar oyun dakikasinda.
/// Kurallar <see cref="CookingModel"/>'de; bu sinif yalnizca veri.
/// </summary>
public sealed class PotState
{
    // ── Icerik ──────────────────────────────────────────────────────
    public float RiceKg;
    public float BulgurKg;
    public float MeatKg;
    public float ChickpeaKg;
    public float ChickenKg;
    public float BeansKg;
    public float WaterL;
    public float ButterG;
    public float SaltG;
    /// <summary>Konserve nohut (hazir pismis) eklendi.</summary>
    public bool Canned;

    // ── Hazirlik kalitesi (agirlikli ortalama) ─────────────────────
    public float RiceWash;
    public float RiceSoak;
    public float ChickpeaSoak;
    public float BeansSoak;

    // ── Pisirme ilerlemesi ─────────────────────────────────────────
    public float Temp = 20f;
    public bool Lid;
    public float Toast;
    public float MeatCook;
    /// <summary>Tahilin cektigi toplam su (litre).</summary>
    public float WaterAbsorbed;
    /// <summary>Simdiye kadar eklenen toplam su (oyuncuya gosterim icin).</summary>
    public float WaterAdded;
    /// <summary>Su cekilme orani 0..1 (gosterim).</summary>
    public float Absorb;
    /// <summary>Haslama ilerlemesi (nohut/tavuk/fasulye), 1 = tam.</summary>
    public float Boil;
    /// <summary>Demleme dakikasi.</summary>
    public float Rest;
    public float Burn;
    /// <summary>Su cekerken yuksek ateste gecen dakika (dengesiz pisme).</summary>
    public float HighHeatMinutes;
    /// <summary>Su yokken (kavururken) karistirilmadan gecen sicak dakika.</summary>
    public float UnstirredHotMinutes;

    // ── Sonuc ──────────────────────────────────────────────────────
    public Food Food;
    /// <summary>Kalan servis (kepce). Pilavda 1 kepce = yarim porsiyon.</summary>
    public float Scoops;
    public float Quality;
    /// <summary>Onceki gunden kaldi: bayat.</summary>
    public bool Stale;
    /// <summary>Ilk kepce alindi: kalite artik degismez.</summary>
    public bool Locked;
    /// <summary>Son hesaplanan kusur (oyuncuya ipucu): Loc anahtari.</summary>
    public string? Hint;

    public bool IsEmpty =>
        RiceKg + BulgurKg + MeatKg + ChickpeaKg + ChickenKg + BeansKg + WaterL + ButterG + SaltG < 1e-3f && Scoops <= 0;

    /// <summary>Gorsel doluluk: litre esdegeri (pirinc ~1.2 L/kg pismis).</summary>
    public float FillLiters =>
        Food is Food.Pilav or Food.BulgurPilav or Food.EtliPilav or Food.Nohut or Food.Fasulye
            ? Scoops * 0.11f
            : RiceKg * (0.8f + 1.4f * Absorb) + BulgurKg * (0.7f + 1.6f * Absorb) + ChickpeaKg * (1f + Boil) + ChickenKg * 1.1f +
              BeansKg * (1f + Boil) + MeatKg + WaterL + ButterG * 0.001f;

    public void Clear()
    {
        RiceKg = BulgurKg = MeatKg = ChickpeaKg = ChickenKg = BeansKg = WaterL = ButterG = SaltG = 0;
        Canned = false;
        RiceWash = RiceSoak = ChickpeaSoak = BeansSoak = 0;
        Toast = MeatCook = WaterAbsorbed = WaterAdded = Absorb = Boil = Rest = Burn = HighHeatMinutes = UnstirredHotMinutes = 0;
        Food = Food.None;
        Scoops = 0;
        Quality = 0;
        Stale = false;
        Locked = false;
        Hint = null;
    }

    public void Write(NetWriter w)
    {
        w.Float(RiceKg);
        w.Float(BulgurKg);
        w.Float(MeatKg);
        w.Float(ChickpeaKg);
        w.Float(ChickenKg);
        w.Float(BeansKg);
        w.Float(WaterL);
        w.Float(ButterG);
        w.Float(SaltG);
        w.Bool(Canned);
        w.Float(RiceWash);
        w.Float(RiceSoak);
        w.Float(ChickpeaSoak);
        w.Float(BeansSoak);
        w.Float(Temp);
        w.Bool(Lid);
        w.Float(Toast);
        w.Float(MeatCook);
        w.Float(WaterAbsorbed);
        w.Float(WaterAdded);
        w.Float(Absorb);
        w.Float(Boil);
        w.Float(Rest);
        w.Float(Burn);
        w.Float(HighHeatMinutes);
        w.Float(UnstirredHotMinutes);
        w.Byte((byte)Food);
        w.Float(Scoops);
        w.Float(Quality);
        w.Bool(Stale);
        w.Bool(Locked);
        w.String(Hint);
    }

    public void Read(NetReader r)
    {
        RiceKg = r.Float();
        BulgurKg = r.Float();
        MeatKg = r.Float();
        ChickpeaKg = r.Float();
        ChickenKg = r.Float();
        BeansKg = r.Float();
        WaterL = r.Float();
        ButterG = r.Float();
        SaltG = r.Float();
        Canned = r.Bool();
        RiceWash = r.Float();
        RiceSoak = r.Float();
        ChickpeaSoak = r.Float();
        BeansSoak = r.Float();
        Temp = r.Float();
        Lid = r.Bool();
        Toast = r.Float();
        MeatCook = r.Float();
        WaterAbsorbed = r.Float();
        WaterAdded = r.Float();
        Absorb = r.Float();
        Boil = r.Float();
        Rest = r.Float();
        Burn = r.Float();
        HighHeatMinutes = r.Float();
        UnstirredHotMinutes = r.Float();
        Food = (Food)r.Byte();
        Scoops = r.Float();
        Quality = r.Float();
        Stale = r.Bool();
        Locked = r.Bool();
        Hint = r.String();
    }
}

/// <summary>Tabak ya da paket kabin icindeki servis.</summary>
public sealed class ServingState
{
    /// <summary>Pilav turu: Pilav / BulgurPilav / EtliPilav ya da None.</summary>
    public Food Base;
    public int Scoops;
    public bool Nohut;
    public bool Tavuk;
    public bool Fasulye;
    public bool Pepper;
    public bool Tursu;
    public bool Ayran;
    /// <summary>Bilesenlerin agirlikli kalitesi.</summary>
    public float Quality;
    public float Temp = 20f;
    public bool Stale;
    /// <summary>Kullanilmis (kirli) tabak.</summary>
    public bool Dirty;

    public bool IsEmpty => Scoops == 0 && !Nohut && !Tavuk && !Fasulye && !Tursu && !Ayran;

    public void Clear()
    {
        Base = Food.None;
        Scoops = 0;
        Nohut = Tavuk = Fasulye = Pepper = Tursu = Ayran = false;
        Quality = 0;
        Temp = 20;
        Stale = false;
    }

    public void Write(NetWriter w)
    {
        w.Byte((byte)Base);
        w.Byte((byte)Scoops);
        var flags = (Nohut ? 1 : 0) | (Tavuk ? 2 : 0) | (Fasulye ? 4 : 0) | (Pepper ? 8 : 0) | (Tursu ? 16 : 0) | (Ayran ? 32 : 0) |
                    (Stale ? 64 : 0) | (Dirty ? 128 : 0);
        w.Byte((byte)flags);
        w.Float(Quality);
        w.Float(Temp);
    }

    public void Read(NetReader r)
    {
        Base = (Food)r.Byte();
        Scoops = r.Byte();
        var f = r.Byte();
        Nohut = (f & 1) != 0;
        Tavuk = (f & 2) != 0;
        Fasulye = (f & 4) != 0;
        Pepper = (f & 8) != 0;
        Tursu = (f & 16) != 0;
        Ayran = (f & 32) != 0;
        Stale = (f & 64) != 0;
        Dirty = (f & 128) != 0;
        Quality = r.Float();
        Temp = r.Float();
    }
}
