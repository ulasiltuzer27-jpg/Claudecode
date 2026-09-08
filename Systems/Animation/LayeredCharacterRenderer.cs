using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Cosmetics;

namespace PixelSurvival.Systems.Animation;

/// <summary>
/// AŞAMA 2 / MADDE 20 — katmanlı karakter çizimi.
///
/// Bir karakterin görünümü tek bir sheet değil, bir YIĞIN:
///
///     pelerin  →  BEDEN  →  kıyafet  →  saç  →  şapka  →  aksesuar
///
/// Bütün katmanlar aynı grid'i paylaştığı için (bkz.
/// <see cref="CosmeticTable.Load"/> içindeki grid denetimi) tek bir kaynak
/// dikdörtgeni hepsine uyar. Bu, katmanların animasyonla senkron kalmasını
/// bir çalışma zamanı davranışı olmaktan çıkarıp veri sözleşmesi haline
/// getiriyor: yürüyüş bobbing'i bedende bir pixel yukarı çıktığında şapka
/// da çıkar, çünkü ikisi aynı frame'in aynı satırından okunuyor.
///
/// Katman sayısı kadar draw çağrısı yapılır. 32x32'lik sprite'larda ve
/// en fazla 5 katmanda bu, ölçülebilir bir maliyet değil; bir render
/// target'a önceden pişirmek (atlas cache) erken optimizasyon olurdu ve
/// kuşanma değiştiğinde geçersizleştirme derdi getirirdi.
/// </summary>
public static class LayeredCharacterRenderer
{
    /// <summary>
    /// Beden ve kuşanılan kozmetikleri doğru sırayla çizer.
    /// </summary>
    /// <param name="source">
    /// Animatörün o karedeki kaynak dikdörtgeni. TÜM katmanlar için aynıdır.
    /// </param>
    /// <param name="position">Karakterin AYAK konumu (sprite'ın alt-ortası).</param>
    /// <param name="table">
    /// Katman sheet'lerinin kaynağı. <c>null</c> ise yalnızca beden çizilir —
    /// kozmetik sistemi olmadan da karakter görünmeye devam eder.
    /// </param>
    public static void Draw(SpriteBatch spriteBatch,
                            SpriteSheet body,
                            Rectangle source,
                            Vector2 position,
                            Color tint,
                            CosmeticTable? table = null,
                            CosmeticLoadout? loadout = null)
    {
        // Origin ayakta: sprite'ın alt-orta noktası. Tüm katmanlar aynı
        // origin'i kullanır, yoksa katmanlar birbirine göre kayar.
        var origin = new Vector2(body.FrameWidth / 2f, body.FrameHeight);

        if (table is not null && loadout is not null)
        {
            foreach (var cosmetic in loadout.BehindBody)
            {
                DrawLayer(spriteBatch, table.SheetFor(cosmetic), source, position, origin, tint);
            }
        }

        DrawLayer(spriteBatch, body, source, position, origin, tint);

        if (table is not null && loadout is not null)
        {
            foreach (var cosmetic in loadout.InFrontOfBody)
            {
                DrawLayer(spriteBatch, table.SheetFor(cosmetic), source, position, origin, tint);
            }
        }
    }

    private static void DrawLayer(SpriteBatch spriteBatch, SpriteSheet sheet, Rectangle source,
                                  Vector2 position, Vector2 origin, Color tint) =>
        spriteBatch.Draw(
            sheet.Texture,
            position,
            source,
            tint,
            rotation: 0f,
            origin: origin,
            scale: 1f,
            effects: SpriteEffects.None,
            layerDepth: 0f);
}
