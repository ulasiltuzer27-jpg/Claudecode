using System.Text.Json;

namespace PixelSurvival.Persistence;

/// <summary>Yükleme denemesinin sonucu.</summary>
public enum LoadOutcome
{
    Success,

    /// <summary>Kayıt dosyası yok — ilk açılış.</summary>
    NotFound,

    /// <summary>Dosya var ama okunamadı (bozuk JSON, disk hatası).</summary>
    Unreadable,

    /// <summary>Kayıt başka bir sürümle yazılmış.</summary>
    VersionMismatch
}

/// <summary>
/// Kaydı diske yazar ve okur.
///
/// ── Atomik yazım ────────────────────────────────────────────────────────
/// Kayıt önce geçici bir dosyaya yazılır, sonra yerine TAŞINIR. Doğrudan
/// üstüne yazmak, yazımın ortasında oyun kapanırsa (çökme, elektrik) yarım
/// bir dosya bırakır — ve o yarım dosya oyuncunun TEK kaydıdır. Taşıma
/// işletim sistemi düzeyinde atomik olduğu için ya eski kayıt ya yeni
/// kayıt kalır, ikisinin karışımı asla.
///
/// ── Yedek ───────────────────────────────────────────────────────────────
/// Başarılı bir yazımdan önce mevcut kayıt <c>.bak</c> olarak saklanır.
/// Atomik yazım yarım dosyaya karşı korur ama MANTIK hatasına karşı
/// korumaz: kaydedilen veri hatalıysa oyuncunun geri döneceği bir nokta
/// olmalı.
/// </summary>
public static class SaveGame
{
    /// <summary>Kayıt klasörü (çalışma dizinine göreli).</summary>
    public const string Directory = "saves";

    public const string FileName = "world.json";

    public static string Path => System.IO.Path.Combine(Directory, FileName);

    private static string BackupPath => Path + ".bak";
    private static string TempPath => Path + ".tmp";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Diskte okunabilir bir kayıt var mı — menü bunu sorar.</summary>
    public static bool Exists() => File.Exists(Path);

    /// <summary>
    /// Kaydı diske yazar.
    /// </summary>
    /// <returns>Başarılıysa <c>null</c>, değilse hata açıklaması.</returns>
    public static string? Save(SaveData data)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            // 1) Gecici dosyaya yaz.
            using (var stream = File.Create(TempPath))
            {
                JsonSerializer.Serialize(stream, data, Options);
            }

            // 2) Mevcut kaydi yedekle. Atomik yazim yarim dosyaya karsi
            //    korur; yedek MANTIK hatasina karsi.
            if (File.Exists(Path))
            {
                File.Copy(Path, BackupPath, overwrite: true);
            }

            // 3) Yerine tasi — bu adim atomik.
            File.Move(TempPath, Path, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            // Gecici dosya ortada kalmasin: bir sonraki yazim onu
            // gormemeli.
            try { if (File.Exists(TempPath)) File.Delete(TempPath); }
            catch (IOException) { /* temizlik basarisizligi asil hatayi gizlemesin */ }

            return ex.Message;
        }
    }

    /// <summary>Kaydı okur.</summary>
    public static LoadOutcome Load(out SaveData? data, out string message)
    {
        data = null;
        message = "";

        if (!File.Exists(Path)) return LoadOutcome.NotFound;

        try
        {
            using var stream = File.OpenRead(Path);
            data = JsonSerializer.Deserialize<SaveData>(stream, Options);
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return LoadOutcome.Unreadable;
        }

        if (data is null)
        {
            message = "kayit bos";
            return LoadOutcome.Unreadable;
        }

        if (data.Version != SaveData.CurrentVersion)
        {
            // Eski bir kaydi yeni alan duzeniyle okumak, alanlarin yanlis
            // yerlere oturmasi demek. Acikca reddetmek daha iyi.
            message = $"kayit surumu {data.Version}, beklenen {SaveData.CurrentVersion}";
            data = null;
            return LoadOutcome.VersionMismatch;
        }

        return LoadOutcome.Success;
    }

    /// <summary>Kaydı siler (yeni oyun). Yedek KORUNUR.</summary>
    public static void Delete()
    {
        try
        {
            if (File.Exists(Path)) File.Move(Path, BackupPath, overwrite: true);
        }
        catch (IOException)
        {
            // Silinemeyen kayit yeni oyunu engellemez: uzerine yazilacak.
        }
    }
}
