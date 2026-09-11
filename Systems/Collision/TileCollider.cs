using Microsoft.Xna.Framework;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Collision;

/// <summary>
/// Float hassasiyetli eksen hizalı kutu. MonoGame'in <see cref="Rectangle"/>'ı
/// int tabanlı; hareket alt-pixel hızlarla yapıldığı için burada float gerekiyor.
/// </summary>
public readonly struct Aabb(float x, float y, float width, float height)
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Width = width;
    public readonly float Height = height;

    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;

    public Aabb Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);
}

/// <summary>
/// Tile ızgarasına karşı çarpışma çözümü.
///
/// ── 1. Eksen ayrık hareket (kayma) ──────────────────────────────────────
/// Önce X ekseninde ilerlenir ve çakışma varsa duvara hizalanır, sonra Y
/// ekseninde aynısı yapılır.
///
/// Tek adımda çözülseydi duvara çapraz basan karakter takılıp kalırdı.
/// Eksen ayrık çözümde X bloklanır ama Y serbest kalır — karakter duvar
/// boyunca KAYAR. Top-down oyunlarda beklenen his budur.
///
/// ── 2. Köşe düzeltmesi ──────────────────────────────────────────────────
/// Kayma tek başına bir şeyi çözmüyor: oyuncu tek eksende, düz koşarken
/// bir köşeye "sürtünürse" hareket TAMAMEN duruyor.
///
/// Somut hâli: çarpışma kutusu 8 pixel yüksekliğinde ama tile'lar 16.
/// Yani kutu neredeyse her zaman İKİ tile sırasına birden yayılır. Oyuncu
/// bir koridorda sağa koşarken kutusunun üst 2 pixel'i bir üstteki katı
/// tile'a değiyorsa, alttaki sıra tamamen açık olmasına rağmen duvara
/// toslar. Oyuncunun gördüğü şey "önümde boşluk var ama geçemiyorum"
/// olur ve geçmek için elle 2 pixel aşağı hizalanması gerekir.
///
/// Çözüm: tek eksende hareket bloklandığında dik eksende
/// <see cref="MaxCornerNudge"/> pixel'e kadar küçük bir itme denenir.
/// İtilmiş konumda hareket açılıyorsa hem itme hem hareket uygulanır.
/// Oyuncu köşeden "yağ gibi" kayar.
///
/// Neden yalnızca TEK eksende hareket varken: oyuncu çapraz basıyorsa
/// dik eksende zaten bir yön İSTİYOR demektir. Onu ters yöne itmek
/// girdisini ezmek olurdu. Çapraz durumda eksen ayrık kayma zaten
/// yeterli — o yüzden düzeltme devreye girmez.
/// </summary>
public static class TileCollider
{
    /// <summary>
    /// Çakışmayı çözmek için uygulanan minik pay. Kutu duvara tam yapışırsa
    /// bir sonraki karede yuvarlama hatası yüzünden içeri sızabilir.
    /// </summary>
    private const float Skin = 0.001f;

    /// <summary>
    /// Köşe düzeltmesinin itebileceği en fazla pixel.
    ///
    /// 4, tile boyutunun (16) dörtte biri. Üst sınır keyfi değil, iki
    /// taraflı bir denge:
    ///
    ///   • Çok küçük (1-2 px) olursa düzeltme çoğu köşede devreye girmez
    ///     ve takılma hissi kalır.
    ///   • Çok büyük (8+ px) olursa oyuncu duvara her sürttüğünde gözle
    ///     görülür biçimde yana ZIPLAR; nişan alırken karakterin kendi
    ///     kafasına göre kayması, takılmaktan daha sinir bozucudur.
    ///
    /// Kutu yüksekliği zaten 8 pixel; 4'ten fazla itmek kutuyu tamamen
    /// başka bir tile sırasına taşımak demek olurdu.
    /// </summary>
    private const float MaxCornerNudge = 4f;

    /// <summary>
    /// <paramref name="box"/>'ı <paramref name="delta"/> kadar hareket ettirmeye
    /// çalışır, katı tile'lara çarpınca durdurur.
    /// </summary>
    /// <returns>
    /// Gerçekte uygulanabilen hareket vektörü. Köşe düzeltmesi devreye
    /// girdiyse basılmayan eksende de sıfırdan farklı bir bileşen içerebilir.
    /// </returns>
    public static Vector2 Move(TileMap map, Aabb box, Vector2 delta)
    {
        // ── Duvarın içinde başlayan kutu: çarpışma UYGULANMAZ ──────────
        // Geri tepme, ışınlanma ya da üstüne inşa edilen bir yapı oyuncuyu
        // katı bir tile'ın içinde bırakabilir. Orada normal çözüm
        // çalıştırılırsa her yön "duvara giriyor" diye reddedilir ve
        // oyuncu KALICI olarak kilitlenir — oyunu bırakmaktan başka çaresi
        // kalmaz.
        //
        // Bilinçli takas: kutu duvarın içindeyken birkaç kare çarpışmasız
        // kalıyor. İstismar edilebilmesi için oyuncunun önce duvarın içine
        // girmesi gerekir ki bunun tek yolu yukarıdaki üç durum. Kalıcı
        // kilitlenme ise kesin ve geri dönüşsüz.
        if (Overlaps(map, box))
        {
            return delta;
        }

        var applied = Vector2.Zero;

        // --- X ekseni ---
        if (delta.X != 0f)
        {
            applied.X = SweepX(map, box, delta.X);

            // Tek eksende koşarken tıkandıysak köşe düzeltmesi dene.
            if (applied.X != delta.X && delta.Y == 0f &&
                TryNudge(map, box, delta.X, horizontal: true, out var nudgeY))
            {
                // İtme dik eksende; hareketin tamamı artık geçiyor.
                return new Vector2(delta.X, nudgeY);
            }
        }

        box = box.Offset(applied.X, 0f);

        // --- Y ekseni ---
        if (delta.Y != 0f)
        {
            applied.Y = SweepY(map, box, delta.Y);

            if (applied.Y != delta.Y && delta.X == 0f &&
                TryNudge(map, box, delta.Y, horizontal: false, out var nudgeX))
            {
                return new Vector2(nudgeX, delta.Y);
            }
        }

        return applied;
    }

    /// <summary>
    /// X ekseninde süpürme: gidilebilen mesafeyi döndürür.
    /// </summary>
    private static float SweepX(TileMap map, Aabb box, float dx)
    {
        if (!Overlaps(map, box.Offset(dx, 0f)))
        {
            return dx;
        }

        // Duvarın kenarına tam hizala.
        var tileSize = map.TileSize;
        var moved = box.Offset(dx, 0f);

        var aligned = dx > 0f
            ? MathF.Floor(moved.Right / tileSize) * tileSize - box.Right - Skin
            : (MathF.Floor(moved.Left / tileSize) + 1) * tileSize - box.Left + Skin;

        return Validate(map, box, aligned, dx, horizontal: true);
    }

    /// <summary>Y ekseninde süpürme.</summary>
    private static float SweepY(TileMap map, Aabb box, float dy)
    {
        if (!Overlaps(map, box.Offset(0f, dy)))
        {
            return dy;
        }

        var tileSize = map.TileSize;
        var moved = box.Offset(0f, dy);

        var aligned = dy > 0f
            ? MathF.Floor(moved.Bottom / tileSize) * tileSize - box.Bottom - Skin
            : (MathF.Floor(moved.Top / tileSize) + 1) * tileSize - box.Top + Skin;

        return Validate(map, box, aligned, dy, horizontal: false);
    }

    /// <summary>
    /// Hizalama sonucunu denetler.
    ///
    /// İki ayrı tuzak var:
    ///
    /// 1. Hizalama TERS yöne itiyorsa hiç kımıldama. Bu, hesabın
    ///    varsaydığı duvarın aslında başka bir yerde olduğu anlamına gelir.
    ///
    /// 2. Hizalanmış konum HÂLÂ doluysa hiç kımıldama. Hesap "engel,
    ///    kutunun yeni kenarını içeren tile sütunudur" varsayımına
    ///    dayanıyor; kutu iki tile'a birden yayıldığında engel BAŞKA bir
    ///    sırada olabilir ve hizalama kutuyu doğrudan onun içine sokar.
    ///    Bu denetim olmadan "duvara kadar git" mantığı bazı köşelerde
    ///    kutuyu duvarın İÇİNE yerleştiriyordu.
    /// </summary>
    private static float Validate(TileMap map, Aabb box, float aligned, float delta,
                                  bool horizontal)
    {
        if (Math.Sign(aligned) != Math.Sign(delta))
        {
            return 0f;
        }

        var landed = horizontal ? box.Offset(aligned, 0f) : box.Offset(0f, aligned);

        return Overlaps(map, landed) ? 0f : aligned;
    }

    /// <summary>
    /// Köşe düzeltmesi: dik eksende küçük bir itmeyle hareketin açılıp
    /// açılmadığına bakar.
    /// </summary>
    /// <param name="delta">Tıkanan eksendeki hareket miktarı.</param>
    /// <param name="horizontal">
    /// Tıkanan eksen X mi (itme Y'de olacak) yoksa Y mi (itme X'te).
    /// </param>
    /// <param name="nudge">Bulunan itme miktarı.</param>
    /// <returns>Hareketin TAMAMINI açan bir itme bulunduysa true.</returns>
    private static bool TryNudge(TileMap map, Aabb box, float delta, bool horizontal,
                                 out float nudge)
    {
        nudge = 0f;

        // Küçükten büyüğe: en az itmeyle çözen aday kazansın. Her adımda
        // iki yön de denenir; kutu hangi tarafa daha yakınsa o taraf
        // doğal olarak daha küçük adımda tutar.
        for (var step = 1f; step <= MaxCornerNudge; step++)
        {
            foreach (var direction in Directions)
            {
                var candidate = step * direction;

                var shifted = horizontal
                    ? box.Offset(0f, candidate)
                    : box.Offset(candidate, 0f);

                // İtmenin KENDİSİ kutuyu duvara sokmamalı: aksi hâlde
                // düzeltme, çözdüğünden daha kötü bir durum yaratır.
                if (Overlaps(map, shifted))
                {
                    continue;
                }

                var target = horizontal
                    ? shifted.Offset(delta, 0f)
                    : shifted.Offset(0f, delta);

                if (Overlaps(map, target))
                {
                    continue;
                }

                nudge = candidate;
                CornerNudges++;
                LastNudge = candidate;
                return true;
            }
        }

        return false;
    }

    private static readonly float[] Directions = [1f, -1f];

    /// <summary>
    /// Köşe düzeltmesinin kaç kez devreye girdiği — YALNIZCA doğrulama için.
    ///
    /// Neden gerekli: düzeltmenin gerçek oyunda çalıştığı, ekran
    /// görüntüsünden de oyuncunun konumundan da okunamıyor. Ölçüldü:
    /// düzeltme devreye girdiğinde oyuncu o an duvara saplanmıyor, ama
    /// birkaç kare sonra bir duvara çarpıp ızgaraya geri oturuyor —
    /// yani UZUN bir rotanın bitiş noktası düzeltmeli ve düzeltmesiz
    /// koşumlarda AYNI çıkıyor. Kazanç anlıktır (takılmama hissi), net
    /// yer değiştirme değil.
    ///
    /// Dolayısıyla "gerçekten çalışıyor mu" sorusunun tek dürüst cevabı
    /// olayı saymak. <c>Tools/verify_collision.py</c> bu sayaca bakıyor.
    ///
    /// Maliyeti: düzeltme başına bir tam sayı artırımı. Düzeltme nadir
    /// (16 ayaklı bir slalomda 2 kez), yani sıcak yolda ölçülebilir bir
    /// yük yok. Oyun mantığı bu alanı OKUMUYOR — davranışı etkilemiyor.
    /// </summary>
    public static int CornerNudges { get; private set; }

    /// <summary>Son uygulanan itme miktarı (işaretli) — sınır denetimi için.</summary>
    public static float LastNudge { get; private set; }

    /// <summary>Kutu herhangi bir katı tile ile kesişiyor mu.</summary>
    public static bool Overlaps(TileMap map, Aabb box)
    {
        var tileSize = map.TileSize;

        var minX = (int)MathF.Floor(box.Left / tileSize);
        var maxX = (int)MathF.Floor((box.Right - Skin) / tileSize);
        var minY = (int)MathF.Floor(box.Top / tileSize);
        var maxY = (int)MathF.Floor((box.Bottom - Skin) / tileSize);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (map.IsSolidTile(x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
