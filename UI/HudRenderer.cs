using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Cosmetics;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Crafting;

namespace PixelSurvival.UI;

/// <summary>
/// AŞAMA 1 / MADDE 7–8 — envanter çubuğu ve crafting paneli.
///
/// Tamamen EKRAN koordinatlarında çizer; kamera matrisi uygulanmaz.
/// Kalıcı bir UI çatısı değil, sistemlerin çalıştığını görmeye yeten
/// asgari bir katman. Gerçek HUD tasarımı Aşama 2'de (madde 24 civarı).
/// </summary>
public sealed class HudRenderer
{
    private const int SlotSize = 20;
    private const int SlotGap = 2;
    private const int UiScale = 2;
    private const int IconSize = 16;

    private static readonly Color PanelColor = new(18, 20, 28);
    private static readonly Color SlotColor = new(44, 48, 62);
    private static readonly Color TextColor = new(228, 232, 240);
    private static readonly Color DimTextColor = new(128, 134, 150);
    private static readonly Color CraftableColor = new(120, 230, 140);

    private readonly BitmapFont _font;
    private readonly Texture2D _pixel;
    private readonly Texture2D _icons;
    private readonly ItemDatabase _items;
    private readonly Dictionary<int, Rectangle> _iconSources = [];

    public HudRenderer(BitmapFont font, Texture2D pixel, Texture2D icons,
                       ItemDatabase items, ContentManager content, string iconAsset)
    {
        _font = font;
        _pixel = pixel;
        _icons = icons;
        _items = items;

        var relativePath = $"{content.RootDirectory}/{iconAsset}.json";
        using var stream = TitleContainer.OpenStream(relativePath);
        var meta = JsonSerializer.Deserialize<IconAtlasMetadata>(stream, JsonOptions)
                   ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        foreach (var icon in meta.Icons)
        {
            _iconSources[icon.Index] =
                new Rectangle(icon.Index * meta.IconSize, 0, meta.IconSize, meta.IconSize);
        }
    }

    /// <summary>Sol altta envanter slotları.</summary>
    public void DrawInventory(SpriteBatch spriteBatch, WorldInventory inventory,
                              int windowWidth, int windowHeight)
    {
        var slotPixels = (SlotSize + SlotGap) * UiScale;
        var barWidth = inventory.SlotCount * slotPixels + SlotGap * UiScale;

        // Başlık bandı FONTTAN hesaplanır, sabit sayıdan değil: sabit bir
        // değer font satır yüksekliğinden küçük kalınca yazı slotların
        // üstüne taşıyor.
        var titleBand = _font.LineHeight * UiScale + 4;
        var barHeight = titleBand + SlotSize * UiScale + SlotGap * 2 * UiScale;

        var originX = (windowWidth - barWidth) / 2;
        var originY = windowHeight - barHeight - 12;

        Fill(spriteBatch, new Rectangle(originX, originY, barWidth, barHeight),
             PanelColor * 0.88f);

        _font.Draw(spriteBatch, $"ENVANTER  {inventory.UsedSlots}/{inventory.SlotCount}",
                   new Vector2(originX + 4 * UiScale, originY + 3 * UiScale),
                   DimTextColor, UiScale);

        var slotY = originY + titleBand;

        for (var i = 0; i < inventory.SlotCount; i++)
        {
            var slotX = originX + SlotGap * UiScale + i * slotPixels;
            var bounds = new Rectangle(slotX, slotY, SlotSize * UiScale, SlotSize * UiScale);

            Fill(spriteBatch, bounds, SlotColor * 0.9f);

            var stack = inventory[i];
            if (stack.IsEmpty)
            {
                continue;
            }

            var definition = _items.Get(stack.ItemId);

            if (_iconSources.TryGetValue(definition.Icon, out var source))
            {
                var iconOffset = (SlotSize - IconSize) / 2 * UiScale;
                spriteBatch.Draw(_icons,
                    new Rectangle(bounds.X + iconOffset, bounds.Y + iconOffset,
                                  IconSize * UiScale, IconSize * UiScale),
                    source, Color.White);
            }

            // Miktar sağ alta; 1'lik yığınlarda sayı gösterilmez (gürültü olur).
            if (stack.Count > 1)
            {
                var label = stack.Count.ToString();
                _font.Draw(spriteBatch, label,
                    new Vector2(bounds.Right - _font.Measure(label, UiScale) - 2,
                                bounds.Bottom - _font.LineHeight * UiScale + 2),
                    TextColor, UiScale);
            }
        }
    }

    /// <summary>Sağ üstte crafting paneli. Tuş numarası = tarif sırası.</summary>
    /// <param name="topY">
    /// Panelin baslayabilecegi en ust Y. Iklim seridi de sag ust kosede
    /// duruyor; sabit 12 yazilinca ikisi ust uste biniyordu.
    /// </param>
    public void DrawCrafting(SpriteBatch spriteBatch, CraftingSystem crafting,
                             WorldInventory inventory, int windowWidth, int topY = 12)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var rows = crafting.Recipes.Count;

        var width = 0;
        var labels = new string[rows];
        for (var i = 0; i < rows; i++)
        {
            labels[i] = $"{i + 1}. {crafting.Describe(crafting.Recipes[i])}";
            width = Math.Max(width, _font.Measure(labels[i], UiScale));
        }

        var padding = 6 * UiScale;
        var panelWidth = width + padding * 2;
        var panelHeight = (rows + 1) * lineHeight + padding * 2;
        var originX = windowWidth - panelWidth - 12;
        var originY = topY;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.88f);

        _font.Draw(spriteBatch, "URETIM  (TAB kapat)",
                   new Vector2(originX + padding, originY + padding), DimTextColor, UiScale);

        for (var i = 0; i < rows; i++)
        {
            // Yapılabilir tarifler yeşil, eksik malzemeliler soluk.
            var color = crafting.HasIngredients(crafting.Recipes[i], inventory)
                ? CraftableColor
                : DimTextColor;

            _font.Draw(spriteBatch, labels[i],
                new Vector2(originX + padding, originY + padding + (i + 1) * lineHeight),
                color, UiScale);
        }
    }

    /// <summary>
    /// MADDE 20 — gardirop paneli.
    ///
    /// Her kuşanılabilir slot bir satır: slot adı, kuşanılan kozmetiğin adı
    /// ve NADİRLİK RENGİ. Nadirliğin oyundaki tek etkisi bu renktir; panelde
    /// bilerek hiçbir istatistik satırı yok, çünkü kozmetiklerin istatistiği
    /// de yok (bkz. <see cref="CosmeticRarity"/>).
    ///
    /// Sahip olunmayan kozmetikler sayıya dahil edilmez: oyuncuya
    /// kuşanamayacağı bir liste göstermek, mağaza olmayan bir ekranda
    /// yalnızca kafa karıştırır.
    /// </summary>
    public void DrawWardrobe(SpriteBatch spriteBatch, CosmeticTable table,
                             CosmeticLoadout loadout, ICosmeticOwnership ownership,
                             string worldSeason, int windowWidth, int windowHeight)
    {
        var slots = CosmeticSlotExtensions.Equippable;
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var header = "GARDIROP  (K kapat, 0 hepsini cikar)";
        var rows = new (string Text, Color Color)[slots.Length];

        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var equipped = loadout.InSlot(slot);
            var ownedCount = table.InSlot(slot).Count(ownership.Owns);

            if (equipped is null)
            {
                rows[i] = ($"{i + 1}. {slot,-9} -                      ({ownedCount} sahip)",
                           DimTextColor);
                continue;
            }

            // Sezonluk kozmetikte pencere bilgisi de gosterilir. Pencere
            // KAPALI olsa bile item kusanili kalir - pencere yalnizca elde
            // edilebilirligi kapatir, kullanimi degil.
            var seasonNote = equipped.Season.IsSeasonal
                ? equipped.Season.IsObtainable(DateTime.UtcNow, worldSeason)
                    ? "  [sezon acik]"
                    : "  [sezon kapali - kusanili kalir]"
                : "";

            rows[i] = ($"{i + 1}. {slot,-9} {equipped.Name} ({equipped.Rarity.Label()}){seasonNote}",
                       equipped.Rarity.FrameColor());
        }

        var width = Math.Max(_font.Measure(header, UiScale),
                             rows.Max(r => _font.Measure(r.Text, UiScale)));

        var panelWidth = width + padding * 2;
        var panelHeight = (rows.Length + 1) * lineHeight + padding * 2;

        // Envanter cubugu alt ortada; panel onun USTUNDE bitmeli.
        var originX = (windowWidth - panelWidth) / 2;
        var originY = windowHeight - panelHeight - 130;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.92f);

        _font.Draw(spriteBatch, header, new Vector2(originX + padding, originY + padding),
                   DimTextColor, UiScale);

        for (var i = 0; i < rows.Length; i++)
        {
            _font.Draw(spriteBatch, rows[i].Text,
                new Vector2(originX + padding, originY + padding + (i + 1) * lineHeight),
                rows[i].Color, UiScale);
        }
    }

    /// <summary>Sol üstte seçili yapı ve ağ oturumu durumu.</summary>
    public void DrawStatus(SpriteBatch spriteBatch, string selectedBuild, string sessionLine)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var lines = new[] { sessionLine, $"YAPI: {selectedBuild}  (Q/Z degistir, R koy)" };

        var width = lines.Max(l => _font.Measure(l, UiScale));
        var padding = 5 * UiScale;

        Fill(spriteBatch,
            new Rectangle(12, 12, width + padding * 2, lines.Length * lineHeight + padding * 2),
            PanelColor * 0.82f);

        for (var i = 0; i < lines.Length; i++)
        {
            _font.Draw(spriteBatch, lines[i],
                new Vector2(12 + padding, 12 + padding + i * lineHeight),
                i == 0 ? TextColor : DimTextColor, UiScale);
        }
    }

    /// <summary>
    /// Sol altta bölge göstergesi ve Steam durumu (madde 18-19).
    ///
    /// Bölge rengi kasten belirgin: oyuncu PvP bölgesine geçtiğini
    /// fark etmeden geçmemeli.
    /// </summary>
    public void DrawZone(SpriteBatch spriteBatch, bool safe, string steamStatus,
                         int windowWidth, int windowHeight)
    {
        var line = safe ? "GUVENLI BOLGE" : "PvP BOLGESI";
        var color = safe ? CraftableColor : new Color(235, 110, 110);

        var width = Math.Max(_font.Measure(line, UiScale), _font.Measure(steamStatus, UiScale));
        var padding = 5 * UiScale;
        var lineHeight = _font.LineHeight * UiScale;
        var y = windowHeight - lineHeight * 2 - padding * 2 - 200;

        Fill(spriteBatch, new Rectangle(12, y, width + padding * 2, lineHeight * 2 + padding),
             PanelColor * 0.82f);

        _font.Draw(spriteBatch, line, new Vector2(12 + padding, y + padding / 2), color, UiScale);
        _font.Draw(spriteBatch, steamStatus,
                   new Vector2(12 + padding, y + padding / 2 + lineHeight), DimTextColor, UiScale);
    }

    /// <summary>Sağ altta gün/saat/mevsim/hava (madde 12).</summary>
    /// <returns>
    /// Seridin alt kenari. Cagiran taraf sag ust kosedeki diger panelleri
    /// bunun altina yerlestirir; boylece cakisma yerlesim hatasi olmaktan
    /// cikip hesaplanan bir deger haline gelir.
    /// </returns>
    public int DrawClimate(SpriteBatch spriteBatch, string line, int windowWidth)
    {
        var width = _font.Measure(line, UiScale);
        var padding = 5 * UiScale;
        var x = windowWidth - width - padding * 2 - 12;
        var height = _font.LineHeight * UiScale + padding;

        Fill(spriteBatch, new Rectangle(x, 12, width + padding * 2, height),
             PanelColor * 0.82f);

        _font.Draw(spriteBatch, line, new Vector2(x + padding, 12 + padding / 2), TextColor, UiScale);

        return 12 + height;
    }

    /// <summary>
    /// Balıkçılık çubuğu (madde 13): yeşil bölge hedef, beyaz işaret gezer.
    /// Ekranın altında, envanterin üstünde.
    /// </summary>
    public void DrawFishingBar(SpriteBatch spriteBatch, float marker, float zoneStart,
                               float zoneSize, bool casting, int windowWidth, int windowHeight)
    {
        const int barWidth = 260;
        const int barHeight = 16;

        var x = (windowWidth - barWidth) / 2;
        var y = windowHeight - 190;

        Fill(spriteBatch, new Rectangle(x - 3, y - 3, barWidth + 6, barHeight + 6),
             PanelColor * 0.9f);
        Fill(spriteBatch, new Rectangle(x, y, barWidth, barHeight), SlotColor);

        if (casting)
        {
            // Olta havada: henüz nişan alınacak bir şey yok.
            _font.Draw(spriteBatch, "...", new Vector2(x + barWidth / 2 - 8, y - 2),
                       DimTextColor, UiScale);
            return;
        }

        Fill(spriteBatch,
            new Rectangle(x + (int)(zoneStart * barWidth), y,
                          Math.Max(2, (int)(zoneSize * barWidth)), barHeight),
            CraftableColor * 0.75f);

        Fill(spriteBatch,
            new Rectangle(x + (int)(marker * barWidth) - 1, y - 3, 3, barHeight + 6),
            TextColor);
    }

    /// <summary>Ekranın üstünde kısa ömürlü bilgi mesajı.</summary>
    public void DrawToast(SpriteBatch spriteBatch, string message, int windowWidth)
    {
        var width = _font.Measure(message, UiScale);
        var x = (windowWidth - width) / 2;

        // Durum paneli sol üstte; toast onunla çakışmasın diye biraz aşağıda.
        const int top = 96;

        Fill(spriteBatch, new Rectangle(x - 8, top, width + 16, _font.LineHeight * UiScale + 8),
             PanelColor * 0.85f);

        _font.Draw(spriteBatch, message, new Vector2(x, top + 4), TextColor, UiScale);
    }

    private void Fill(SpriteBatch spriteBatch, Rectangle bounds, Color color) =>
        spriteBatch.Draw(_pixel, bounds, color);

    private sealed class IconAtlasMetadata
    {
        [JsonPropertyName("iconSize")] public int IconSize { get; set; } = 16;
        [JsonPropertyName("icons")] public List<IconEntry> Icons { get; set; } = [];
    }

    private sealed class IconEntry
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("key")] public string Key { get; set; } = "";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
