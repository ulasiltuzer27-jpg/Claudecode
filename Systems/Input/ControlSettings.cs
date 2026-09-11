using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;

namespace PixelSurvival.Systems.Input;

/// <summary>
/// Oyuncunun kontrol tercihleri: tuş atamaları ve fare hassasiyeti.
///
/// ── Neden dünya kaydından AYRI bir dosya ────────────────────────────────
/// <c>saves/world.json</c> dünyayı tutuyor. Tuş atamaları oraya konsaydı
/// "yeni oyun" başlatmak ya da kaydı silmek oyuncunun kontrollerini de
/// sıfırlardı — oysa ikisinin birbiriyle ilgisi yok. Ayrıca doğrulama
/// script'leri her koşumda <c>saves/</c> klasörünü siliyor.
///
/// ── Neden hiçbir ayar diske yazılmıyordu ────────────────────────────────
/// Dil, yazı ölçeği ve renk körlüğü paleti şu an her açılışta sıfırlanıyor
/// (bilinen eksik). Bu dosya yalnızca kontrol ayarlarını kalıcı yapıyor;
/// diğerlerini de buraya taşımak ayrı bir iş.
/// </summary>
public sealed class ControlSettings
{
    public const string Directory = "config";
    public const string FileName = "controls.json";

    public static string Path => System.IO.Path.Combine(Directory, FileName);

    /// <summary>
    /// Fare hassasiyetinin alt ve üst sınırı.
    ///
    /// Sınır yoksa oyuncu hassasiyeti 0'a çekip nişan imlecini tamamen
    /// kilitleyebilir ya da öyle yükseltebilir ki imleç tek karede
    /// ekranın bir ucundan diğerine sıçrar. İkisi de "ayarı bozdum, geri
    /// getiremiyorum" ile biter.
    /// </summary>
    public const float MinSensitivity = 0.25f;
    public const float MaxSensitivity = 4.0f;

    /// <summary>Bir tuşa basışta hassasiyetin değişme miktarı.</summary>
    public const float SensitivityStep = 0.25f;

    /// <summary>
    /// Varsayılan atamalar — mevcut davranışın BİREBİR aynısı.
    ///
    /// Her eyleme iki tuş: bu sistem eklenmeden önce de WASD ile ok
    /// tuşları birlikte çalışıyordu. Tek tuşa indirmek, kimsenin istemediği
    /// bir davranış değişikliği olurdu.
    /// </summary>
    private static readonly Dictionary<GameAction, Keys[]> Defaults = new()
    {
        [GameAction.MoveUp] = [Keys.W, Keys.Up],
        [GameAction.MoveDown] = [Keys.S, Keys.Down],
        [GameAction.MoveLeft] = [Keys.A, Keys.Left],
        [GameAction.MoveRight] = [Keys.D, Keys.Right],
        [GameAction.Gather] = [Keys.Space, Keys.E],
        [GameAction.Attack] = [Keys.F, Keys.LeftControl],
        [GameAction.Build] = [Keys.R, Keys.None]
    };

    /// <summary>Bir eyleme bağlı tuş yuvası sayısı (birincil + ikincil).</summary>
    public const int SlotCount = 2;

    private readonly Dictionary<GameAction, Keys[]> _bindings = [];

    private float _sensitivity = 1.0f;

    /// <summary>
    /// Nişan imlecinin fare hareketine tepki çarpanı.
    ///
    /// Ayarlanabilir olması ancak fare GERÇEKTEN nişan alıyorsa anlamlı;
    /// bkz. <see cref="MouseAim"/>.
    /// </summary>
    public float MouseSensitivity
    {
        get => _sensitivity;
        set => _sensitivity = Math.Clamp(value, MinSensitivity, MaxSensitivity);
    }

    /// <summary>Fare ile nişan alma açık mı.</summary>
    public bool MouseAimEnabled { get; set; } = true;

    public ControlSettings() => ResetAll();

    /// <summary>Bir eyleme bağlı tuşlar. Dizi <see cref="SlotCount"/> uzunluğunda.</summary>
    public Keys[] KeysFor(GameAction action) => _bindings[action];

    /// <summary>Tuşu bir yuvaya yazar.</summary>
    /// <remarks>
    /// ÇAKIŞMA TEMİZLENİR: aynı tuş başka bir eyleme bağlıysa oradan
    /// silinir. Aksi halde tek tuş iki iş birden yapardı ve oyuncu bunu
    /// ancak oyunun içinde, karakterini kaynağa vururken fark ederdi.
    /// Sessizce reddetmek de kötü olurdu: oyuncu tuşa basar, hiçbir şey
    /// olmaz, sebebini bilmez. Çalmak, en az şaşırtan davranış.
    /// </remarks>
    /// <returns>Tuşun çalındığı eylem, yoksa <c>null</c>.</returns>
    public GameAction? Assign(GameAction action, int slot, Keys key)
    {
        if (slot < 0 || slot >= SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot,
                $"Yuva 0..{SlotCount - 1} arasinda olmali.");
        }

        GameAction? stolenFrom = null;

        if (key != Keys.None)
        {
            foreach (var other in GameActions.All)
            {
                var slots = _bindings[other];
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != key || (other == action && i == slot)) continue;

                    slots[i] = Keys.None;
                    stolenFrom ??= other;
                }
            }
        }

        _bindings[action][slot] = key;
        return stolenFrom;
    }

    /// <summary>Tek bir eylemi varsayılana döndürür.</summary>
    public void Reset(GameAction action)
    {
        var defaults = Defaults[action];
        var slots = new Keys[SlotCount];

        for (var i = 0; i < SlotCount; i++)
        {
            slots[i] = i < defaults.Length ? defaults[i] : Keys.None;
        }

        _bindings[action] = slots;

        // Varsayilana donen tus baska bir eylemde duruyor olabilir; orada
        // birakmak iki eylemi ayni tusa baglardi.
        foreach (var key in slots)
        {
            if (key == Keys.None) continue;

            foreach (var other in GameActions.All)
            {
                if (other == action) continue;

                var otherSlots = _bindings[other];
                for (var i = 0; i < otherSlots.Length; i++)
                {
                    if (otherSlots[i] == key) otherSlots[i] = Keys.None;
                }
            }
        }
    }

    /// <summary>Bütün atamaları ve hassasiyeti varsayılana döndürür.</summary>
    public void ResetAll()
    {
        _bindings.Clear();

        foreach (var action in GameActions.All)
        {
            var defaults = Defaults[action];
            var slots = new Keys[SlotCount];

            for (var i = 0; i < SlotCount; i++)
            {
                slots[i] = i < defaults.Length ? defaults[i] : Keys.None;
            }

            _bindings[action] = slots;
        }

        _sensitivity = 1.0f;
        MouseAimEnabled = true;
    }

    /// <summary>Eylemin herhangi bir tuşu basılı mı.</summary>
    public bool IsDown(KeyboardState keyboard, GameAction action)
    {
        foreach (var key in _bindings[action])
        {
            if (key != Keys.None && keyboard.IsKeyDown(key)) return true;
        }

        return false;
    }

    /// <summary>
    /// Hiçbir eyleme bağlı olmayan bir eylem var mı.
    ///
    /// Hareket eylemlerinden birinin tuşsuz kalması oyuncuyu yürüyemez
    /// hale getirir; ayar ekranı bunu görünür kılıyor.
    /// </summary>
    public bool IsUnbound(GameAction action) =>
        _bindings[action].All(k => k == Keys.None);

    // ---------------- diske yazma / okuma ----------------

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);

        var data = new PersistedControls
        {
            MouseSensitivity = _sensitivity,
            MouseAimEnabled = MouseAimEnabled,
            Bindings = GameActions.All.ToDictionary(
                a => a.ToString(),
                a => _bindings[a].Select(k => k.ToString()).ToArray())
        };

        File.WriteAllText(Path, JsonSerializer.Serialize(data, JsonOptions));
    }

    /// <summary>
    /// Diskten okur. Dosya yoksa ya da bozuksa VARSAYILANA döner.
    ///
    /// Bozuk ayar dosyası yüzünden oyunun açılmaması, kaybedilen bir tuş
    /// atamasından çok daha kötü. Elle düzenlenmiş bir dosyada bilinmeyen
    /// bir tuş adı olması beklenen bir durum.
    /// </summary>
    public static ControlSettings Load()
    {
        var settings = new ControlSettings();

        if (!File.Exists(Path)) return settings;

        try
        {
            var data = JsonSerializer.Deserialize<PersistedControls>(
                File.ReadAllText(Path), JsonOptions);

            if (data is null) return settings;

            settings.MouseSensitivity = data.MouseSensitivity;
            settings.MouseAimEnabled = data.MouseAimEnabled;

            foreach (var action in GameActions.All)
            {
                if (!data.Bindings.TryGetValue(action.ToString(), out var names)) continue;

                for (var i = 0; i < SlotCount && i < names.Length; i++)
                {
                    // Taninmayan tus adi: o yuva bos kalir, digerleri
                    // etkilenmez. Butun dosyayi atmaktan iyi.
                    settings._bindings[action][i] =
                        Enum.TryParse<Keys>(names[i], out var key) ? key : Keys.None;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException
                                      or UnauthorizedAccessException)
        {
            Console.WriteLine($"[kontrol] ayar dosyasi okunamadi, varsayilana donuldu: {ex.Message}");
            settings.ResetAll();
        }

        return settings;
    }

    /// <summary>Diskteki şemanın kod karşılığı.</summary>
    private sealed class PersistedControls
    {
        [JsonPropertyName("mouseSensitivity")] public float MouseSensitivity { get; set; } = 1.0f;
        [JsonPropertyName("mouseAimEnabled")] public bool MouseAimEnabled { get; set; } = true;

        // Tuslar SAYI degil AD olarak yaziliyor: Keys enum degerleri
        // sayisal olarak yazilsaydi dosya elle okunamaz olurdu ve
        // MonoGame surumu arasinda deger kaymasi sessiz bir hataya
        // donusurdu.
        [JsonPropertyName("bindings")]
        public Dictionary<string, string[]> Bindings { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
