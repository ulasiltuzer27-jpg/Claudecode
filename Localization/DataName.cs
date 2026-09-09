namespace PixelSurvival.Localization;

/// <summary>
/// Veri dosyalarındaki adların dile çevrilmesi.
///
/// ── Sorun ───────────────────────────────────────────────────────────────
/// Madde 23'te arayüz metinleri dil tablosuna taşındı, ama VERİ
/// dosyalarındaki adlar (item, mevsim, hava, ekin, düşman, NPC) tek
/// dilde kaldı. Sonuç tuhaftı: dil İngilizce'ye alındığında menüler
/// İngilizce, envanterdeki item adları Türkçe kalıyordu.
///
/// ── Neden dizinin tamamı dil tablosuna taşınmadı ────────────────────────
/// Adı tamamen `nameKey`'e çevirip `name` alanını silmek, her veri
/// dosyasını dil tablosuna BAĞIMLI hale getirirdi: modun eklediği bir
/// item, mod dil satırı da sağlamadıkça ekranda `[mod.item.x]` görünürdü.
/// Burada `name` YEDEK olarak kalıyor — anahtar yoksa (ya da tabloda
/// karşılığı yoksa) veri dosyasındaki ad kullanılıyor. Modlar böylece
/// tek dilli kalmayı seçebiliyor.
///
/// ── Neden ham alan `RawName` ────────────────────────────────────────────
/// Tanım sınıflarında JSON'dan gelen alan `RawName`, ekranda kullanılan
/// ise `Name`. Bu ayrım sayesinde MEVCUT bütün çağrı yerleri (`.Name`)
/// hiç değişmeden çevrilmiş adı almaya başladı — yüzlerce çağrıdan
/// birini atlamak, o adın sessizce çevrilmemesi demek olurdu.
/// `verify_content.py` `RawName`'in tanım dosyalarının dışında
/// kullanılmadığını denetliyor.
/// </summary>
public static class DataName
{
    /// <summary>
    /// Anahtar varsa dil tablosundan, yoksa veri dosyasındaki ham ad.
    ///
    /// Anahtar verilmiş ama tabloda karşılığı yoksa <see cref="Loc.T"/>
    /// <c>[anahtar]</c> döndürür; bu da ham ada düşülerek gizleniyor
    /// DEĞİL — çünkü eksik bir çevirinin ekranda görünmesi isteniyor.
    /// Eksik anahtarları <c>verify_content.py</c> derlemeden önce
    /// yakalıyor.
    /// </summary>
    public static string Of(string nameKey, string rawName) =>
        string.IsNullOrEmpty(nameKey) ? rawName : Loc.T(nameKey);
}
