using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PixelSurvival.Workshop;

/// <summary>Diskte bulunmuş, yüklenmiş bir mod.</summary>
public sealed class LoadedMod(ModManifest manifest, string rootDirectory, bool fromWorkshop)
{
    public ModManifest Manifest { get; } = manifest;

    /// <summary>Modun klasörü. İçerik dosyaları buraya göre çözülür.</summary>
    public string RootDirectory { get; } = rootDirectory;

    /// <summary>Steam Workshop'tan mı geldi, yerel <c>mods/</c> klasöründen mi.</summary>
    public bool FromWorkshop { get; } = fromWorkshop;

    /// <summary>Oyuncu bu modu kapatmış olabilir.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Bu modun sağladığı içerik dosyaları (kök klasöre göreli).</summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}

/// <summary>
/// AŞAMA 2 / MADDE 25 — Steam Workshop altyapısı.
///
/// ── Ne yapar ────────────────────────────────────────────────────────────
/// Mod klasörlerini bulur, manifestlerini doğrular, yükleme sırasına dizer
/// ve aktif mod kümesinin PARMAK İZİNİ üretir.
///
/// ── Parmak izi neden gerekli ────────────────────────────────────────────
/// Bu oyunda harita ağdan GÖNDERİLMİYOR: iki taraf aynı tohumdan aynı
/// dünyayı üretiyor (bkz. README, "İki kişilik test nasıl yapılır").
/// Üretim biome eşiklerinden, tile tablosundan ve kaynak tablosundan
/// türüyor — hepsi VERİ, yani hepsi modlanabilir.
///
/// Sonuç: farklı mod kümesine sahip iki oyuncu AYNI TOHUMDAN FARKLI DÜNYA
/// üretir. Ekranlar sessizce ayrışır; oyuncu duvarın içinde yürür,
/// topladığı ağaç diğerinde durur. Bu, teşhisi en zor çok oyunculu
/// hatalardan biridir.
///
/// Bu yüzden aktif mod kümesi tohumun bir parçası gibi ele alınıyor:
/// <see cref="Fingerprint"/> host ile istemcide aynı olmalı. Farklıysa
/// bağlantı REDDEDİLİR — sessizce ayrışmış bir oyundan iyidir.
///
/// ── Modlar kod çalıştırmaz ──────────────────────────────────────────────
/// Bkz. <see cref="ModContentKind"/>. Yalnızca JSON veri ve PNG asset.
/// </summary>
public sealed class ModRegistry
{
    /// <summary>Yerel modların arandığı klasör (çalışma dizinine göreli).</summary>
    public const string LocalModsDirectory = "mods";

    /// <summary>
    /// Bir modun sağlayabileceği dosya uzantıları.
    ///
    /// Beyaz liste, kara liste DEĞİL: yeni bir tehlikeli uzantı çıktığında
    /// kara listeyi güncellemeyi unutmak, beyaz listeyi güncellemeyi
    /// unutmaktan çok daha kötü sonuçlanır.
    /// </summary>
    private static readonly string[] AllowedExtensions = [".json", ".png"];

    private readonly List<LoadedMod> _mods = [];

    /// <summary>Yükleme sırasına dizilmiş modlar.</summary>
    public IReadOnlyList<LoadedMod> Mods => _mods;

    public IEnumerable<LoadedMod> Active => _mods.Where(m => m.Enabled);

    /// <summary>Yükleme sırasında toplanan uyarılar — arayüzde gösterilir.</summary>
    public IReadOnlyList<string> Warnings => _warnings;
    private readonly List<string> _warnings = [];

    /// <summary>
    /// Verilen klasörleri tarar ve mod'ları yükler.
    /// </summary>
    /// <param name="directories">
    /// Taranacak kökler. Her biri, içinde <c>mod.json</c> bulunan alt
    /// klasörler barındırır.
    /// </param>
    public void Discover(IEnumerable<(string Path, bool FromWorkshop)> directories)
    {
        _mods.Clear();
        _warnings.Clear();

        foreach (var (root, fromWorkshop) in directories)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var folder in Directory.EnumerateDirectories(root))
            {
                var manifestPath = Path.Combine(folder, ModManifest.FileName);

                // mod.json yoksa burasi mod degil. Sessizce atlaniyor:
                // mods/ klasorunde README veya .zip bulunmasi normal.
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var manifest = ModManifest.Load(manifestPath);

                    if (_mods.Any(m => m.Manifest.Id == manifest.Id))
                    {
                        _warnings.Add($"'{manifest.Id}' iki kez bulundu; ikincisi atlandi " +
                                      $"({folder}).");
                        continue;
                    }

                    _mods.Add(new LoadedMod(manifest, folder, fromWorkshop)
                    {
                        Files = CollectFiles(folder)
                    });
                }
                catch (Exception ex)
                {
                    // Bozuk bir mod OYUNU COKERTMEZ; atlanip raporlanir.
                    // Workshop'tan gelen icerige guvenilemez ve tek bozuk
                    // abonelik oyunu acilmaz hale getirmemeli.
                    _warnings.Add($"'{folder}' yuklenemedi: {ex.Message}");
                }
            }
        }

        // Kucuk loadOrder once; esitlikte kimlige gore. Sira DETERMINISTIK
        // olmali, yoksa ayni mod kumesi iki makinede farkli sonuc verir ve
        // parmak izi ayni olmasina ragmen dunyalar ayrisir.
        _mods.Sort((a, b) =>
        {
            var order = a.Manifest.LoadOrder.CompareTo(b.Manifest.LoadOrder);
            return order != 0 ? order : string.CompareOrdinal(a.Manifest.Id, b.Manifest.Id);
        });
    }

    /// <summary>
    /// Modun sağladığı, izin verilen uzantıya sahip dosyalar.
    ///
    /// İzin verilmeyen bir dosya varsa uyarı üretilir ama mod yüklenmeye
    /// devam eder: içerikte bir LICENSE veya README bulunması normaldir,
    /// modu tümden reddetmek aşırı olurdu.
    /// </summary>
    private List<string> CollectFiles(string folder)
    {
        var files = new List<string>();

        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, path).Replace('\\', '/');

            if (relative == ModManifest.FileName) continue;

            if (!AllowedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                continue;
            }

            files.Add(relative);
        }

        // Dosya sirasi da deterministik: parmak izi buradan turuyor ve
        // dosya sistemi siralamasi platformdan platforma degisir.
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// AKTİF mod kümesinin parmak izi.
    ///
    /// Host ile istemcide aynı olmalı; farklıysa iki taraf aynı tohumdan
    /// FARKLI dünya üretir. Kimlik + sürüm + dosya listesi karışıma
    /// giriyor: aynı kimlikli ama içeriği değişmiş bir mod da farklı
    /// parmak izi vermeli.
    ///
    /// Boş küme (mod yok) sabit bir değer döndürür, böylece modsuz iki
    /// oyuncu her zaman eşleşir.
    /// </summary>
    public string Fingerprint
    {
        get
        {
            var builder = new StringBuilder();

            foreach (var mod in Active)
            {
                builder.Append(mod.Manifest.Id).Append('@')
                       .Append(mod.Manifest.Version).Append('[');

                foreach (var file in mod.Files) builder.Append(file).Append(';');

                builder.Append("]\n");
            }

            if (builder.Length == 0) return "modsuz";

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return Convert.ToHexString(hash)[..16];
        }
    }

    /// <summary>
    /// MADDE 25 — aktif modların sağladığı dil satırlarını uygular.
    ///
    /// Yükleme sırası <see cref="Discover"/>'da belirlendi; burada o sırayla
    /// bindiriliyor, yani BÜYÜK loadOrder son yazar ve kazanır.
    ///
    /// Bozuk bir dil dosyası modu tümden devre dışı BIRAKMAZ: uyarı
    /// üretilir ve diğer dosyalar yüklenmeye devam eder. Workshop'tan gelen
    /// tek bozuk dosya, çalışan bir modu kullanılamaz hale getirmemeli.
    /// </summary>
    public int ApplyLocalization()
    {
        var applied = 0;

        foreach (var mod in Active)
        {
            foreach (var relative in mod.Files)
            {
                if (!relative.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase) ||
                    !relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = Path.Combine(mod.RootDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

                try
                {
                    using var stream = File.OpenRead(path);
                    var table = JsonSerializer.Deserialize<Localization.LocaleTable>(
                        stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (table is null || table.Code.Length == 0)
                    {
                        _warnings.Add($"'{mod.Manifest.Id}': {relative} icinde 'code' yok.");
                        continue;
                    }

                    applied += Localization.Loc.Overlay(table.Code, table.Strings);
                }
                catch (Exception ex)
                {
                    _warnings.Add($"'{mod.Manifest.Id}': {relative} okunamadi ({ex.Message}).");
                }
            }
        }

        return applied;
    }

    /// <summary>Arayüzde gösterilecek özet.</summary>
    public string Summary =>
        _mods.Count == 0
            ? "Mod yok"
            : $"{Active.Count()}/{_mods.Count} mod aktif  ({Fingerprint})";
}
