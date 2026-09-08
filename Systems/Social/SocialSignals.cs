using Microsoft.Xna.Framework;

namespace PixelSurvival.Systems.Social;

/// <summary>
/// Emote türü. Değerler KABLO ÜZERİNDE gider — değiştirilmez, yalnızca
/// sona eklenir. Eski bir istemci bilmediği bir emote alırsa
/// <see cref="EmoteSystem"/> onu sessizce yok sayar.
/// </summary>
public enum EmoteKind : byte
{
    Wave = 0,
    Yes = 1,
    No = 2,
    Thanks = 3,
    Help = 4,
    Laugh = 5
}

/// <summary>
/// Ping türü. Ping, sesli sohbeti olmayan oyuncuların TEK iletişim yolu;
/// bu yüzden anlamı türden gelir, yazıdan değil (madde 23: dil bağımsız).
/// </summary>
public enum PingKind : byte
{
    /// <summary>Genel dikkat çekme.</summary>
    Look = 0,

    /// <summary>Burada tehlike var.</summary>
    Danger = 1,

    /// <summary>Buraya gidelim.</summary>
    Go = 2,

    /// <summary>Burada kaynak var.</summary>
    Resource = 3
}

/// <summary>Bir oyuncunun üstünde duran emote.</summary>
public sealed class ActiveEmote(byte playerId, EmoteKind kind, float seconds)
{
    public byte PlayerId { get; } = playerId;
    public EmoteKind Kind { get; } = kind;
    public float SecondsLeft { get; set; } = seconds;
}

/// <summary>Dünyada duran bir ping işareti.</summary>
public sealed class WorldPing(byte playerId, PingKind kind, Vector2 position, float seconds)
{
    public byte PlayerId { get; } = playerId;
    public PingKind Kind { get; } = kind;
    public Vector2 Position { get; } = position;
    public float SecondsLeft { get; set; } = seconds;

    /// <summary>Toplam ömür — solma oranını hesaplamak için.</summary>
    public float TotalSeconds { get; } = seconds;

    /// <summary>1 = yeni, 0 = bitmek üzere.</summary>
    public float Freshness => TotalSeconds <= 0f ? 0f : SecondsLeft / TotalSeconds;
}

/// <summary>
/// AŞAMA 2 / MADDE 24 — emote ve ping.
///
/// ── İkisi neden aynı sınıfta ────────────────────────────────────────────
/// İkisi de aynı şeyi yapıyor: kısa ömürlü, ağdan yayılan, dil bağımsız
/// bir işaret. Ortak olan ömür sayacı ve spam koruması; ayrı sınıflar
/// bu iki mantığı kopyalardı.
///
/// ── Spam koruması ───────────────────────────────────────────────────────
/// Ping ve emote, sohbeti olmayan bir oyunda en kolay taciz aracıdır.
/// Oyuncu başına bekleme süresi bu yüzden burada, veri sınıfında —
/// arayüzde değil. Arayüzde olsaydı ağdan gelen mesajlar sınırı atlardı.
/// </summary>
public sealed class SocialSystem
{
    /// <summary>Emote ekranda ne kadar kalır.</summary>
    public const float EmoteSeconds = 2.5f;

    /// <summary>Ping dünyada ne kadar kalır.</summary>
    public const float PingSeconds = 6f;

    /// <summary>Aynı oyuncunun iki işareti arasındaki en kısa süre.</summary>
    private const float CooldownSeconds = 1.2f;

    private readonly List<ActiveEmote> _emotes = [];
    private readonly List<WorldPing> _pings = [];
    private readonly Dictionary<byte, float> _cooldowns = [];

    public IReadOnlyList<ActiveEmote> Emotes => _emotes;
    public IReadOnlyList<WorldPing> Pings => _pings;

    /// <summary>Bu oyuncu şu an işaret gönderebilir mi.</summary>
    public bool CanSignal(byte playerId) => _cooldowns.GetValueOrDefault(playerId) <= 0f;

    /// <summary>
    /// Emote gösterir. Bekleme süresi dolmadıysa <c>false</c> döner ve
    /// hiçbir şey olmaz.
    /// </summary>
    public bool TryEmote(byte playerId, EmoteKind kind)
    {
        if (!CanSignal(playerId)) return false;

        // Ayni oyuncunun onceki emote'u DUSER: iki balon ust uste
        // binmesin.
        _emotes.RemoveAll(e => e.PlayerId == playerId);
        _emotes.Add(new ActiveEmote(playerId, kind, EmoteSeconds));

        _cooldowns[playerId] = CooldownSeconds;
        return true;
    }

    /// <summary>Dünyaya ping koyar.</summary>
    public bool TryPing(byte playerId, PingKind kind, Vector2 position)
    {
        if (!CanSignal(playerId)) return false;

        // Oyuncu basina TEK ping: harita isaret cop luguna donmesin.
        _pings.RemoveAll(p => p.PlayerId == playerId);
        _pings.Add(new WorldPing(playerId, kind, position, PingSeconds));

        _cooldowns[playerId] = CooldownSeconds;
        return true;
    }

    /// <summary>Ömrü dolanları temizler ve bekleme sürelerini işletir.</summary>
    public void Update(float delta)
    {
        for (var i = _emotes.Count - 1; i >= 0; i--)
        {
            _emotes[i].SecondsLeft -= delta;
            if (_emotes[i].SecondsLeft <= 0f) _emotes.RemoveAt(i);
        }

        for (var i = _pings.Count - 1; i >= 0; i--)
        {
            _pings[i].SecondsLeft -= delta;
            if (_pings[i].SecondsLeft <= 0f) _pings.RemoveAt(i);
        }

        foreach (var (id, remaining) in _cooldowns.ToArray())
        {
            var left = remaining - delta;

            if (left <= 0f) _cooldowns.Remove(id);
            else _cooldowns[id] = left;
        }
    }

    /// <summary>Oyuncu ayrıldığında işaretlerini de temizler.</summary>
    public void Forget(byte playerId)
    {
        _emotes.RemoveAll(e => e.PlayerId == playerId);
        _pings.RemoveAll(p => p.PlayerId == playerId);
        _cooldowns.Remove(playerId);
    }

    public void Clear()
    {
        _emotes.Clear();
        _pings.Clear();
        _cooldowns.Clear();
    }

    /// <summary>
    /// Emote'un ekranda görünen sembolü.
    ///
    /// Bilerek METİN DEĞİL, sembol: emote'un tüm amacı dil bilmeden
    /// anlaşmak. Çevrilebilir bir emote, çeviriyi bilmeyen oyuncu için
    /// işe yaramaz.
    /// </summary>
    public static string Symbol(EmoteKind kind) => kind switch
    {
        EmoteKind.Wave => "o/",
        EmoteKind.Yes => "[+]",
        EmoteKind.No => "[-]",
        EmoteKind.Thanks => "<3",
        EmoteKind.Help => "[!]",
        EmoteKind.Laugh => ":D",
        _ => "?"
    };

    /// <summary>Ping'in dünyada çizilen sembolü ve rengi.</summary>
    public static (string Symbol, Color Color) PingStyle(PingKind kind) => kind switch
    {
        PingKind.Danger => ("[!]", new Color(236, 96, 96)),
        PingKind.Go => ("-->", new Color(120, 200, 240)),
        PingKind.Resource => ("[*]", new Color(240, 200, 96)),
        _ => ("[o]", new Color(226, 230, 240))
    };
}
