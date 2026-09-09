using System.Text.Json;
using System.Text.Json.Serialization;
using PixelSurvival.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

using PixelSurvival.Workshop;

namespace PixelSurvival.Systems.Climate;

public sealed class DayPhase
{
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);

    /// <summary>Bu fazın bittiği gün kesri (0..1).</summary>
    [JsonPropertyName("until")] public float Until { get; init; }

    /// <summary>Bindirilecek ışık rengi (RGB).</summary>
    [JsonPropertyName("light")] public int[] Light { get; init; } = [255, 255, 255];

    /// <summary>0 = etkisiz, 1 = tamamen bu renk.</summary>
    [JsonPropertyName("intensity")] public float Intensity { get; init; }
}

public sealed class SeasonDefinition
{
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>
    /// Mevsimin MANTIKTA kullanılan sabit anahtarı.
    ///
    /// ── Neden ad yetmiyor ───────────────────────────────────────────────
    /// <c>crops.json</c> bir ekinin hangi mevsimlerde büyüdüğünü mevsim
    /// ADIYLA yazıyordu ve karşılaştırma <c>Season.Name</c> ile
    /// yapılıyordu. Ad dile çevrilir çevrilmez o karşılaştırma İngilizce
    /// oynayan oyuncuda hiçbir zaman tutmaz ve **ekinler hiç büyümezdi** —
    /// ekranda görünmeyen, ancak günler sonra fark edilecek bir hata.
    ///
    /// Anahtar verilmemişse ham ada düşülüyor: <c>key</c> alanı olmayan
    /// eski veri dosyalarında ve modlarda mevsimin ADI zaten anahtar
    /// görevi görüyordu ve onları kırmanın anlamı yok. Çevrilmemiş metnin
    /// mantıkta kullanıldığı TEK yer burası; satırdaki
    /// <c>ham ad kasten</c> işareti <c>verify_content.py</c>'nin bu tek
    /// istisnayı tanımasını sağlıyor.
    /// </summary>
    [JsonPropertyName("key")] public string RawKey { get; init; } = "";

    [JsonIgnore]
    public string Key => string.IsNullOrEmpty(RawKey) ? RawName : RawKey;  // ham ad kasten

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);
    [JsonPropertyName("tint")] public int[] Tint { get; init; } = [255, 255, 255];

    /// <summary>Hava anahtarı → ağırlık. Toplamları 1 olmalı.</summary>
    [JsonPropertyName("weather")] public Dictionary<string, float> Weather { get; init; } = [];
}

public sealed class WeatherType
{
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);

    /// <summary>Ek karartma (0..1). Yağmurlu hava güneşliden koyudur.</summary>
    [JsonPropertyName("darken")] public float Darken { get; init; }

    /// <summary>Ekrandaki parçacık sayısı. 0 = parçacık yok.</summary>
    [JsonPropertyName("particles")] public int Particles { get; init; }
}

/// <summary><c>Content/World/climate.json</c> dosyasının kod karşılığı.</summary>
public sealed class ClimateTable
{
    [JsonPropertyName("dayLengthSeconds")] public float DayLengthSeconds { get; init; } = 480f;

    /// <summary>
    /// Dünya kurulurken günün hangi noktasından başlanacağı (0 = gece yarısı,
    /// 0.5 = öğle).
    ///
    /// Varsayılan sabah: oyuncuyu yeni ürettiği dünyaya zifiri karanlıkta
    /// düşürmek, ilk izlenimi "hiçbir şey görünmüyor" yapıyordu. Değer
    /// veriden okunuyor, çünkü gece başlangıcı bilinçli bir tasarım tercihi
    /// olabilir (zorluk modu).
    /// </summary>
    [JsonPropertyName("startTimeOfDay")] public float StartTimeOfDay { get; init; } = 0.34f;
    [JsonPropertyName("daysPerSeason")] public int DaysPerSeason { get; init; } = 7;
    [JsonPropertyName("phases")] public List<DayPhase> Phases { get; init; } = [];
    [JsonPropertyName("seasons")] public List<SeasonDefinition> Seasons { get; init; } = [];
    [JsonPropertyName("weatherMinSeconds")] public float WeatherMinSeconds { get; init; } = 45f;
    [JsonPropertyName("weatherMaxSeconds")] public float WeatherMaxSeconds { get; init; } = 180f;
    [JsonPropertyName("weatherTypes")] public List<WeatherType> WeatherTypes { get; init; } = [];

    public static ClimateTable Load(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        // Mod bindirmesinden GECIYOR: bir mod bu tabloyu degistirebilir
        // ya da yeni satir ekleyebilir (bkz. ModdedContent).
        using var stream = ModdedContent.Open(content, assetName);

        var table = JsonSerializer.Deserialize<ClimateTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (table.DayLengthSeconds <= 0f || table.DaysPerSeason < 1)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': dayLengthSeconds ve daysPerSeason pozitif olmalı.");
        }

        if (table.Phases.Count == 0 || table.Seasons.Count == 0 || table.WeatherTypes.Count == 0)
        {
            throw new InvalidOperationException($"'{relativePath}': faz/mevsim/hava listesi boş.");
        }

        // Son faz günün sonunu kapsamalı, yoksa gün kesrinin son diliminde
        // hangi ışığın uygulanacağı belirsiz kalır.
        if (table.Phases[^1].Until < 1f)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': son fazın 'until' değeri en az 1.0 olmalı — " +
                $"aksi halde günün son diliminde ışık tanımsız kalır.");
        }

        var known = table.WeatherTypes.Select(w => w.Key).ToHashSet();
        foreach (var season in table.Seasons)
        {
            foreach (var key in season.Weather.Keys)
            {
                if (!known.Contains(key))
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{season.Name}' mevsimi tanımsız '{key}' " +
                        $"havasını kullanıyor.");
                }
            }
        }

        return table;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// AŞAMA 2 / MADDE 12 — gündüz-gece, mevsim ve hava döngüsü.
///
/// ════════════════════════════════════════════════════════════════════════
/// HOST OTORİTER
/// ════════════════════════════════════════════════════════════════════════
/// Dünya saati ve hava host'ta ilerler ve ağ üzerinden yayınlanır.
/// İstemci kendi saatini işletseydi iki taraf kayar ve "sende gece, bende
/// gündüz" durumu oluşurdu. Ekin büyümesi (madde 13) de bu saate bağlı;
/// saatin ayrışması ekonominin ayrışması demek.
/// ════════════════════════════════════════════════════════════════════════
///
/// Hava geçişleri, dünya tohumundan türeyen deterministik bir PRNG ile
/// seçilir — aynı tohum aynı hava sırasını verir. <c>System.Random</c>
/// KULLANILMAZ: .NET sürümleri arasında algoritması değişti.
/// </summary>
public sealed class ClimateSystem
{
    private readonly ClimateTable _table;
    private readonly Dictionary<string, WeatherType> _weatherByKey;

    private uint _randomState;
    private float _weatherSecondsLeft;

    /// <summary>Dünyanın kuruluşundan bu yana geçen saniye.</summary>
    public double WorldSeconds { get; private set; }

    public WeatherType Weather { get; private set; }

    public ClimateSystem(ClimateTable table, int seed)
    {
        _table = table;
        _weatherByKey = table.WeatherTypes.ToDictionary(w => w.Key);

        _randomState = (uint)seed ^ 0x5BF03635u;
        if (_randomState == 0)
        {
            _randomState = 0x9E3779B9u;
        }

        Weather = table.WeatherTypes[0];
        RollWeather();

        // Gun 1 gece yarisinda degil, veriden okunan saatte baslar.
        // Kesir 0..1 disina tasarsa gun sayaci bozulurdu; kirpiliyor.
        WorldSeconds = Math.Clamp(table.StartTimeOfDay, 0f, 0.999f) * table.DayLengthSeconds;
    }

    /// <summary>Kaçıncı gün (1'den başlar).</summary>
    public int Day => (int)(WorldSeconds / _table.DayLengthSeconds) + 1;

    /// <summary>Günün normalize edilmiş kesri: 0 = gece yarısı, 0.5 = öğle.</summary>
    public float DayFraction =>
        (float)(WorldSeconds % _table.DayLengthSeconds / _table.DayLengthSeconds);

    public SeasonDefinition Season =>
        _table.Seasons[(Day - 1) / _table.DaysPerSeason % _table.Seasons.Count];

    /// <summary>Mevsim içindeki gün (1'den başlar) — ekin büyümesi bunu kullanır.</summary>
    public int DayOfSeason => (Day - 1) % _table.DaysPerSeason + 1;

    public DayPhase Phase
    {
        get
        {
            var fraction = DayFraction;
            foreach (var phase in _table.Phases)
            {
                if (fraction < phase.Until)
                {
                    return phase;
                }
            }

            return _table.Phases[^1];
        }
    }

    /// <summary>Gündüz mü — balık türü ve ileride düşman spawn'ı için.</summary>
    public bool IsDaytime => Phase.Intensity < 0.2f;

    /// <summary>Host çağırır: saati ilerletir ve hava süresini işler.</summary>
    public void Update(float deltaSeconds)
    {
        WorldSeconds += deltaSeconds;
        _weatherSecondsLeft -= deltaSeconds;

        if (_weatherSecondsLeft <= 0f)
        {
            RollWeather();
        }
    }

    /// <summary>İstemci çağırır: host'tan gelen otoriter durumu uygular.</summary>
    public void ApplyNetworkState(double worldSeconds, string weatherKey)
    {
        WorldSeconds = worldSeconds;

        if (_weatherByKey.TryGetValue(weatherKey, out var weather))
        {
            Weather = weather;
        }
    }

    /// <summary>
    /// Dünyanın üzerine bindirilecek renk.
    ///
    /// Faz ışığı, mevsim tonu ve havanın karartması tek bir çarpımsal renkte
    /// birleşir. SpriteBatch.Draw'a tint olarak verilir; ayrı bir tam ekran
    /// geçişi çizmekten ucuz ve pixel art'ta daha temiz görünüyor.
    /// </summary>
    public Color AmbientTint
    {
        get
        {
            var phase = Phase;
            var season = Season;

            // Faz ışığı: beyazdan faz rengine doğru intensity kadar karıştır.
            var strength = Math.Clamp(phase.Intensity, 0f, 1f);
            var r = MathHelper.Lerp(255f, phase.Light[0], strength);
            var g = MathHelper.Lerp(255f, phase.Light[1], strength);
            var b = MathHelper.Lerp(255f, phase.Light[2], strength);

            // Mevsim tonu çarpımsal — ilkbahar yeşile, kış maviye çalar.
            r = r * season.Tint[0] / 255f;
            g = g * season.Tint[1] / 255f;
            b = b * season.Tint[2] / 255f;

            // Hava ek karartma.
            var darken = 1f - Math.Clamp(Weather.Darken, 0f, 1f);

            return new Color(
                (int)Math.Clamp(r * darken, 0f, 255f),
                (int)Math.Clamp(g * darken, 0f, 255f),
                (int)Math.Clamp(b * darken, 0f, 255f));
        }
    }

    /// <summary>
    /// Dünyanın üstüne çizilecek tam ekran bindirme rengi (alfalı).
    ///
    /// Çarpımsal tint yerine bindirme seçildi: tint kullanmak her çizim
    /// çağrısının imzasını değiştirmeyi gerektirirdi (tile, oyuncu, ekin,
    /// yaratık...). Tek bir dikdörtgen hem daha ucuz hem de arayüz katmanını
    /// karartmadan yalnızca dünyayı etkiliyor.
    /// </summary>
    public Color OverlayColor
    {
        get
        {
            var phase = Phase;
            var alpha = Math.Clamp(phase.Intensity + Weather.Darken, 0f, 0.85f);

            return new Color(phase.Light[0], phase.Light[1], phase.Light[2]) * alpha;
        }
    }

    /// <summary>Saati "Gun 3  14:20" biçiminde yazar.</summary>
    public string ClockText
    {
        get
        {
            var minutesOfDay = DayFraction * 24f * 60f;
            return $"{(int)(minutesOfDay / 60f):00}:{(int)(minutesOfDay % 60f):00}";
        }
    }

    private void RollWeather()
    {
        var season = Season;
        var roll = NextUnit();
        var cumulative = 0f;

        foreach (var (key, weight) in season.Weather)
        {
            cumulative += weight;
            if (roll <= cumulative && _weatherByKey.TryGetValue(key, out var picked))
            {
                Weather = picked;
                break;
            }
        }

        _weatherSecondsLeft = MathHelper.Lerp(
            _table.WeatherMinSeconds, _table.WeatherMaxSeconds, NextUnit());
    }

    /// <summary>xorshift32 — Noise.cs ile aynı, taşınabilir PRNG.</summary>
    private float NextUnit()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return _randomState / 4294967296f;
    }
}
