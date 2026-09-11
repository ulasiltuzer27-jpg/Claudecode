using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Accessibility;
using PixelSurvival.Achievements;
using PixelSurvival.Clans;
using PixelSurvival.Trade;
using PixelSurvival.Cosmetics;
using PixelSurvival.Inventory;
using PixelSurvival.Localization;
using Microsoft.Xna.Framework.Input;
using PixelSurvival.Systems.Crafting;
using PixelSurvival.Systems.Input;
using PixelSurvival.Systems.Social;
using PixelSurvival.Workshop;

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
    private const int IconSize = 16;

    // Panel/slot zeminleri anlam TASIMAZ (sadece arka plan), bu yuzden
    // renk korlugu paletinin disinda kaldilar.
    private static readonly Color PanelColor = new(18, 20, 28);
    private static readonly Color SlotColor = new(44, 48, 62);

    /// <summary>
    /// MADDE 23 — erisilebilirlik ayarlari.
    ///
    /// Yazi olcegi ve anlam tasiyan renkler artik sabit degil; ikisi de
    /// buradan okunuyor. Sabit kalsalardi renk korlugu paletini eklemek
    /// her cizim cagrisini tek tek bulmayi gerektirirdi.
    /// </summary>
    public AccessibilitySettings Accessibility { get; set; } = new();

    private int UiScale => Accessibility.TextScale;

    /// <summary>
    /// Envanter slotlarinin olcegi.
    ///
    /// Yazi olceginden AYRI: slotlar ikon, yazi degil. Ikisini birlikte
    /// olceklemek, oyuncu yaziyi buyuttugunde envanter cubugunun pencere
    /// disina tasmasina yol aciyordu (24 slot x 22 px x 3 = 1584 > 1280).
    ///
    /// Yazi olcegini takip eder ama cubugun pencereye SIGDIGI en buyuk
    /// degerle sinirlanir; boylece kucuk olcekte ikisi ayni gorunur,
    /// buyuk olcekte cubuk kirilmaz.
    /// </summary>
    private static int SlotScaleFor(int slotCount, int textScale, int windowWidth)
    {
        for (var scale = textScale; scale > 1; scale--)
        {
            var width = slotCount * (SlotSize + SlotGap) * scale + SlotGap * scale;
            if (width <= windowWidth - 24) return scale;
        }

        return 1;
    }
    private Color TextColor => Accessibility.Palette.Text;
    private Color DimTextColor => Accessibility.Palette.DimText;
    private Color CraftableColor => Accessibility.Palette.Positive;
    private Color WarningColor => Accessibility.Palette.Warning;

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
        var slotScale = SlotScaleFor(inventory.SlotCount, UiScale, windowWidth);

        var slotPixels = (SlotSize + SlotGap) * slotScale;
        var barWidth = inventory.SlotCount * slotPixels + SlotGap * slotScale;

        // Başlık bandı FONTTAN hesaplanır, sabit sayıdan değil: sabit bir
        // değer font satır yüksekliğinden küçük kalınca yazı slotların
        // üstüne taşıyor.
        var titleBand = _font.LineHeight * UiScale + 4;
        var barHeight = titleBand + SlotSize * slotScale + SlotGap * 2 * slotScale;

        var originX = (windowWidth - barWidth) / 2;
        var originY = windowHeight - barHeight - 12;

        Fill(spriteBatch, new Rectangle(originX, originY, barWidth, barHeight),
             PanelColor * 0.88f);

        _font.Draw(spriteBatch, Loc.T("hud.inventory", inventory.UsedSlots, inventory.SlotCount),
                   new Vector2(originX + 4 * UiScale, originY + 3 * UiScale),
                   DimTextColor, UiScale);

        var slotY = originY + titleBand;

        for (var i = 0; i < inventory.SlotCount; i++)
        {
            var slotX = originX + SlotGap * slotScale + i * slotPixels;
            var bounds = new Rectangle(slotX, slotY, SlotSize * slotScale, SlotSize * slotScale);

            Fill(spriteBatch, bounds, SlotColor * 0.9f);

            var stack = inventory[i];
            if (stack.IsEmpty)
            {
                continue;
            }

            var definition = _items.Get(stack.ItemId);

            if (_iconSources.TryGetValue(definition.Icon, out var source))
            {
                var iconOffset = (SlotSize - IconSize) / 2 * slotScale;
                spriteBatch.Draw(_icons,
                    new Rectangle(bounds.X + iconOffset, bounds.Y + iconOffset,
                                  IconSize * slotScale, IconSize * slotScale),
                    source, Color.White);
            }

            // Miktar sağ alta; 1'lik yığınlarda sayı gösterilmez (gürültü olur).
            if (stack.Count > 1)
            {
                // Sayi da SLOT olceginde: yazi olceginde cizilirse buyuk
                // olcekte slotun disina tasiyor.
                var label = stack.Count.ToString();
                _font.Draw(spriteBatch, label,
                    new Vector2(bounds.Right - _font.Measure(label, slotScale) - 2,
                                bounds.Bottom - _font.LineHeight * slotScale + 2),
                    TextColor, slotScale);
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

        _font.Draw(spriteBatch, Loc.T("hud.crafting.title"),
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

        var header = Loc.T("wardrobe.title");
        var rows = new (string Text, Color Color)[slots.Length];

        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var equipped = loadout.InSlot(slot);
            var ownedCount = table.InSlot(slot).Count(ownership.Owns);

            if (equipped is null)
            {
                rows[i] = ($"{i + 1}. {slot,-9} {Loc.T("wardrobe.empty"),-22} {Loc.T("wardrobe.owned", ownedCount)}",
                           DimTextColor);
                continue;
            }

            // Sezonluk kozmetikte pencere bilgisi de gosterilir. Pencere
            // KAPALI olsa bile item kusanili kalir - pencere yalnizca elde
            // edilebilirligi kapatir, kullanimi degil.
            var seasonNote = equipped.Season.IsSeasonal
                ? equipped.Season.IsObtainable(DateTime.UtcNow, worldSeason)
                    ? Loc.T("wardrobe.season.open")
                    : Loc.T("wardrobe.season.closed")
                : "";

            rows[i] = ($"{i + 1}. {slot,-9} {equipped.Name} ({equipped.Rarity.Label()}){seasonNote}",
                       equipped.Rarity.FrameColor(Accessibility.Palette));
        }

        var width = Math.Max(_font.Measure(header, UiScale),
                             rows.Max(r => _font.Measure(r.Text, UiScale)));

        var panelWidth = width + padding * 2;
        var panelHeight = (rows.Length + 1) * lineHeight + padding * 2;

        // Envanter cubugu alt ortada; panel onun USTUNDE bitmeli.
        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   windowHeight - panelHeight - 130,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

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

    /// <summary>
    /// MADDE 21 — başarım listesi.
    ///
    /// Gizli achievement'lar açılana kadar adı/açıklaması yerine "???"
    /// gösterilir; sürpriz olmalarının tek anlamı bu. Açıldıktan sonra
    /// normal satır olur.
    /// </summary>
    public void DrawAchievements(SpriteBatch spriteBatch, AchievementTracker tracker,
                                 int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var header = Loc.T("ach.title", tracker.UnlockedCount, tracker.TotalCount,
                           tracker.Status);

        var rows = new List<(string Text, Color Color)>();

        foreach (var achievement in tracker.All)
        {
            var unlocked = tracker.IsUnlocked(achievement);

            // Gizli VE acilmamis olan sakli kalir.
            if (achievement.Hidden && !unlocked)
            {
                rows.Add(($"[ ] {Loc.T("ach.hidden")}", DimTextColor));
                continue;
            }

            rows.Add(($"[{(unlocked ? "X" : " ")}] {achievement.Name} — {achievement.Description}",
                      unlocked ? CraftableColor : DimTextColor));
        }

        var width = Math.Max(_font.Measure(header, UiScale),
                             rows.Max(r => _font.Measure(r.Text, UiScale)));

        var panelWidth = width + padding * 2;
        var panelHeight = (rows.Count + 1) * lineHeight + padding * 2;

        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   (windowHeight - panelHeight) / 2 - 40,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.94f);

        _font.Draw(spriteBatch, header, new Vector2(originX + padding, originY + padding),
                   TextColor, UiScale);

        for (var i = 0; i < rows.Count; i++)
        {
            _font.Draw(spriteBatch, rows[i].Text,
                new Vector2(originX + padding, originY + padding + (i + 1) * lineHeight),
                rows[i].Color, UiScale);
        }
    }

    /// <summary>
    /// MADDE 21 — leaderboard ekranı.
    ///
    /// Her tablonun başlığında kaynağı da yazar. Bu bilinçli: skor
    /// CLIENT tarafında üretiliyor ve bu, Steam leaderboard'larının bilinen
    /// bir sınırı. "Yerel" ile "Steam" arasındaki farkı gizlemek, oyuncuya
    /// olduğundan güvenilir bir sıralama vaat etmek olurdu.
    /// </summary>
    public void DrawLeaderboard(SpriteBatch spriteBatch, ILeaderboardBackend backend,
                                IReadOnlyList<LeaderboardDefinition> boards,
                                AchievementTracker tracker,
                                int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var rows = new List<(string Text, Color Color)>
        {
            (Loc.T("lb.title", backend.Status), TextColor)
        };

        foreach (var board in boards)
        {
            rows.Add(($"  {board.Name}  [{Loc.T("lb.yourScore", tracker.Value(board.StatKey))}]",
                      CraftableColor));

            var entries = backend.Top(board, 5);

            if (entries.Count == 0)
            {
                rows.Add(($"    {Loc.T("lb.empty")}", DimTextColor));
                continue;
            }

            foreach (var entry in entries)
            {
                rows.Add(($"    {entry.Rank}. {entry.PlayerName,-16} {entry.Score}", TextColor));
            }
        }

        var width = rows.Max(r => _font.Measure(r.Text, UiScale));
        var panelWidth = width + padding * 2;
        var panelHeight = rows.Count * lineHeight + padding * 2;

        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   (windowHeight - panelHeight) / 2 - 40,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.94f);

        for (var i = 0; i < rows.Count; i++)
        {
            _font.Draw(spriteBatch, rows[i].Text,
                new Vector2(originX + padding, originY + padding + i * lineHeight),
                rows[i].Color, UiScale);
        }
    }

    /// <summary>
    /// MADDE 21 — başarım açılma bildirimi.
    ///
    /// Sağ altta, envanter çubuğunun üstünde. Steam'in kendi bildirimi
    /// yalnızca Steam derlemesinde çıkar; bu banner her derlemede çıkar ki
    /// tetikleme mantığı Steam olmadan da görülebilsin.
    /// </summary>
    public void DrawUnlockBanner(SpriteBatch spriteBatch, AchievementDefinition achievement,
                                 int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var lines = new[] { Loc.T("ach.unlocked"), achievement.Name, achievement.Description };
        var width = lines.Max(l => _font.Measure(l, UiScale));

        var panelWidth = width + padding * 2;
        var panelHeight = lines.Length * lineHeight + padding * 2;

        var origin = ClampToWindow(windowWidth - panelWidth - 12,
                                   windowHeight - panelHeight - 130,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.95f);

        // Ust kenarda altin serit: bildirimi diger panellerden ayirir.
        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, 3),
             new Color(240, 186, 74));

        for (var i = 0; i < lines.Length; i++)
        {
            _font.Draw(spriteBatch, lines[i],
                new Vector2(originX + padding, originY + padding + i * lineHeight),
                i == 0 ? new Color(240, 186, 74) : TextColor, UiScale);
        }
    }

    /// <summary>
    /// Ortalanan bir paneli pencere içinde tutar.
    ///
    /// MADDE 23'te ortaya cikti: yazi olcegi buyutulunce paneller
    /// pencerenin disina tasiyordu. Erisilebilirlik ayarinin kendisi
    /// arayuzu okunamaz hale getiriyordu — okunurluk icin buyutulen yazi,
    /// panelin yarisini ekran disinda birakiyordu.
    ///
    /// Kirpma degil KAYDIRMA yapiliyor: panel pencereden buyukse sol/ust
    /// kenara yaslanir, boylece en azindan basi gorunur.
    /// </summary>
    private static Point ClampToWindow(int x, int y, int width, int height,
                                       int windowWidth, int windowHeight)
    {
        const int margin = 8;

        return new Point(
            Math.Max(margin, Math.Min(x, windowWidth - width - margin)),
            Math.Max(margin, Math.Min(y, windowHeight - height - margin)));
    }

    /// <summary>MADDE 22 — klan paneli: üyeler, rütbeler, sahipli yapı sayısı.</summary>
    public void DrawClan(SpriteBatch spriteBatch, ClanSystem clans, byte localPlayerId,
                         int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var rows = new List<(string Text, Color Color)>
        {
            (Loc.T("clan.title"), TextColor)
        };

        var clan = clans.ClanOf(localPlayerId);

        if (clan is null)
        {
            rows.Add(($"  {Loc.T("clan.none")}", DimTextColor));
            rows.Add(($"  {Loc.T("clan.none.hint1")}", DimTextColor));
            rows.Add(($"  {Loc.T("clan.none.hint2")}", DimTextColor));
            rows.Add(($"  {Loc.T("clan.none.hint3")}", DimTextColor));
        }
        else
        {
            rows.Add(($"  {Loc.T("clan.header", clan.Tag, clan.Name, clan.Members.Count)}", CraftableColor));

            foreach (var member in clan.Members.OrderByDescending(m => m.Rank))
            {
                var mine = member.PlayerId == localPlayerId ? Loc.T("clan.you") : "";
                rows.Add(($"    {member.Rank.Label(),-6} {member.Name}{mine}", TextColor));
            }

            rows.Add(($"  {Loc.T("clan.owned", clans.OwnedStructureCount)}", DimTextColor));
        }

        var width = rows.Max(r => _font.Measure(r.Text, UiScale));
        var panelWidth = width + padding * 2;
        var panelHeight = rows.Count * lineHeight + padding * 2;

        var origin = ClampToWindow(12, (windowHeight - panelHeight) / 2,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.94f);

        for (var i = 0; i < rows.Count; i++)
        {
            _font.Draw(spriteBatch, rows[i].Text,
                new Vector2(originX + padding, originY + padding + i * lineHeight),
                rows[i].Color, UiScale);
        }
    }

    /// <summary>
    /// MADDE 22 — takas penceresi.
    ///
    /// İki sütun: senin teklifin ve karşı tarafınki. Onay durumu her iki
    /// tarafta da görünür; teklif değiştiğinde iki onay da düşer ve bu
    /// panelde anında görülür — takas arayüzlerinin klasik "onaydan sonra
    /// teklifi değiştirme" dolandırıcılığına karşı oyuncunun tek savunması
    /// bunu GÖREBİLMESİ.
    /// </summary>
    public void DrawTrade(SpriteBatch spriteBatch, TradeSession trade, ItemDatabase items,
                          WorldInventory inventory, int selectedSlot,
                          byte localPlayerId, bool localAccepted, bool partnerAccepted,
                          string stateLabel, int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var mineIsA = trade.PlayerA == localPlayerId;
        var mine = mineIsA ? trade.OfferA : trade.OfferB;
        var theirs = mineIsA ? trade.OfferB : trade.OfferA;

        var rows = new List<(string Text, Color Color)>
        {
            (Loc.T("trade.title", stateLabel), TextColor),
            ($"  {Loc.T("trade.help")}", DimTextColor),
            ("", DimTextColor)
        };

        var slot = inventory[selectedSlot];
        var slotText = slot.IsEmpty
            ? Loc.T("trade.slotEmpty")
            : $"{items.Get(slot.ItemId).Name} x{slot.Count}";

        rows.Add(($"  {Loc.T("trade.selectedSlot", selectedSlot + 1, slotText)}", CraftableColor));
        rows.Add(("", DimTextColor));

        rows.Add(($"  {Loc.T("trade.yourOffer")} {(localAccepted ? Loc.T("trade.accepted") : "")}",
                  localAccepted ? CraftableColor : TextColor));

        if (mine.Count == 0) rows.Add(($"    {Loc.T("common.none")}", DimTextColor));
        foreach (var stack in mine)
        {
            rows.Add(($"    {items.Get(stack.ItemId).Name} x{stack.Amount}", TextColor));
        }

        rows.Add(("", DimTextColor));
        rows.Add(($"  {Loc.T("trade.theirOffer")} {(partnerAccepted ? Loc.T("trade.accepted") : "")}",
                  partnerAccepted ? CraftableColor : TextColor));

        if (theirs.Count == 0) rows.Add(($"    {Loc.T("common.none")}", DimTextColor));
        foreach (var stack in theirs)
        {
            rows.Add(($"    {items.Get(stack.ItemId).Name} x{stack.Amount}", TextColor));
        }

        var width = rows.Max(r => _font.Measure(r.Text, UiScale));
        var panelWidth = Math.Max(width + padding * 2, 380);
        var panelHeight = rows.Count * lineHeight + padding * 2;

        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   (windowHeight - panelHeight) / 2 - 40,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.95f);

        for (var i = 0; i < rows.Count; i++)
        {
            _font.Draw(spriteBatch, rows[i].Text,
                new Vector2(originX + padding, originY + padding + i * lineHeight),
                rows[i].Color, UiScale);
        }
    }

    /// <summary>
    /// Kontrol ayarları ekranı: tuş atamaları + fare.
    ///
    /// ── Neden sütunlar sabit genişlikte ─────────────────────────────────
    /// Tuş adları çok farklı uzunlukta ("W" ile "LeftControl"). Sütunlar
    /// metne göre akarsa satırlar birbirine göre kayar ve göz hangi tuşun
    /// hangi eyleme ait olduğunu takip edemez. Sabit sütun, bitmap font
    /// karakter genişliğinin katı olarak hesaplanıyor.
    /// </summary>
    public void DrawControls(SpriteBatch spriteBatch, ControlsScreen screen,
                             int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;
        var settings = screen.Settings;

        // ── Sutunlar PIXEL konumunda, bosluk doldurarak DEGIL ──────────
        // Ilk surum sutunlari `PadRight` ile hizalamaya calisiyordu ve
        // ekranda egri duruyordu: bu bitmap font SABIT GENISLIKLI DEGIL,
        // her glyph'in kendi `Advance` degeri var (bkz. BitmapFont).
        // Bosluk doldurmak yalnizca KARAKTER sayisini esitler, pixel
        // genisligini degil. Sutunlar en genis metne gore olculup sabit
        // pixel konumuna ciziliyor.
        var labels = GameActions.All
            .Select(a => Loc.T(GameActions.LabelKey(a)))
            .Concat([Loc.T("controls.mouseAim"), Loc.T("controls.sensitivity"),
                     Loc.T("controls.resetRow")])
            .ToArray();

        var arrow = _font.Measure("> ", UiScale);
        var labelWidth = labels.Max(l => _font.Measure(l, UiScale));

        // Secili yuva koseli parantezle isaretlendigi icin sutun onlari da
        // almali; yoksa secim degistikce sutun genisligi oynar.
        var slotWidth = GameActions.All
            .SelectMany(a => settings.KeysFor(a))
            .Select(k => _font.Measure($"[{SlotText(k)}]", UiScale))
            .Append(_font.Measure(Loc.T("controls.secondaryHeader"), UiScale))
            .Max();

        var gap = _font.Measure("  ", UiScale);
        var columnX = new[]
        {
            arrow,
            arrow + labelWidth + gap,
            arrow + labelWidth + gap + slotWidth + gap
        };

        var contentWidth = columnX[2] + slotWidth;

        // --- Satirlari topla: (segmentler, renk) ---
        var rows = new List<(string[] Cells, Color Color)>
        {
            ([Loc.T("controls.title")], TextColor)
        };

        for (var i = 0; i < GameActions.All.Length; i++)
        {
            var action = GameActions.All[i];
            var keys = settings.KeysFor(action);
            var selected = screen.SelectedRow == i;

            var primary = SlotText(keys[0]);
            var secondary = SlotText(keys[1]);

            // Secili YUVA koseli parantezle isaretleniyor: hangi sutunun
            // duzenlenecegi, renk ayrimina guvenmeden de okunur olmali
            // (renk korlugu paleti bu ekrani da kapsiyor).
            if (selected)
            {
                if (screen.SelectedSlot == 0) primary = $"[{primary}]";
                else secondary = $"[{secondary}]";
            }

            // Tussuz kalan eylem UYARI renginde: oyuncu ekrandan cikmadan
            // once gormeli.
            var color = settings.IsUnbound(action) ? WarningColor
                      : selected ? CraftableColor
                      : TextColor;

            rows.Add(([selected ? "> " : "", Loc.T(GameActions.LabelKey(action)),
                       primary, secondary], color));
        }

        var extras = new (string Label, string Value)[]
        {
            (Loc.T("controls.mouseAim"),
             Loc.T(settings.MouseAimEnabled ? "controls.on" : "controls.off")),
            (Loc.T("controls.sensitivity"), $"{settings.MouseSensitivity:0.00}x"),
            (Loc.T("controls.resetRow"), "")
        };

        for (var i = 0; i < extras.Length; i++)
        {
            var rowIndex = GameActions.All.Length + i;
            var selected = screen.SelectedRow == rowIndex;

            // Secili satirda deger ok icinde: sol/sag ile degisecegini
            // gosteren tek ipucu bu.
            var value = extras[i].Value.Length > 0 && selected
                ? $"< {extras[i].Value} >"
                : extras[i].Value;

            rows.Add(([selected ? "> " : "", extras[i].Label, value, ""],
                      selected ? CraftableColor : TextColor));
        }

        // --- Alt bilgi satirlari (tek sutun) ---
        var footer = new List<(string Text, Color Color)>
        {
            ("", DimTextColor),

            // Yakalama modunda ipucu DEGISIYOR: oyuncu tusa basmasi
            // gerektigini bilmeli, yoksa ekranin kilitlendigini sanir.
            (Loc.T(screen.IsCapturing ? "controls.press" : "controls.hint"),
             screen.IsCapturing ? WarningColor : DimTextColor)
        };

        if (screen.Message is { } message)
        {
            var text = message == "controls.stolen" && screen.StolenFrom is { } from
                ? Loc.T(message, Loc.T(GameActions.LabelKey(from)))
                : Loc.T(message);

            footer.Add((text, CraftableColor));
        }

        if (screen.HasUnbound) footer.Add((Loc.T("controls.unbound"), WarningColor));

        // --- Panel ---
        var footerWidth = footer.Max(f => _font.Measure(f.Text, UiScale));
        var headerWidth = contentWidth;
        var panelWidth = Math.Max(Math.Max(contentWidth, footerWidth), headerWidth)
                         + padding * 2;

        // +1 satir: sutun basligi.
        var panelHeight = (rows.Count + footer.Count + 1) * lineHeight + padding * 2;

        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   (windowHeight - panelHeight) / 2,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        Fill(spriteBatch, new Rectangle(origin.X, origin.Y, panelWidth, panelHeight),
             PanelColor * 0.95f);

        var y = origin.Y + padding;

        // Baslik
        _font.Draw(spriteBatch, rows[0].Cells[0],
                   new Vector2(origin.X + padding, y), rows[0].Color, UiScale);
        y += lineHeight;

        // Sutun basligi: sutunlarla AYNI x'lerde, tek bir dize degil.
        var headers = new[] { Loc.T("controls.actionHeader"),
                              Loc.T("controls.primaryHeader"),
                              Loc.T("controls.secondaryHeader") };
        for (var c = 0; c < headers.Length; c++)
        {
            _font.Draw(spriteBatch, headers[c],
                new Vector2(origin.X + padding + columnX[c], y), DimTextColor, UiScale);
        }
        y += lineHeight;

        for (var i = 1; i < rows.Count; i++)
        {
            var cells = rows[i].Cells;

            // 0 = imlec oku (sutunun soluna), 1..3 = sutunlar.
            _font.Draw(spriteBatch, cells[0],
                       new Vector2(origin.X + padding, y), rows[i].Color, UiScale);

            for (var c = 1; c < cells.Length; c++)
            {
                if (cells[c].Length == 0) continue;

                _font.Draw(spriteBatch, cells[c],
                    new Vector2(origin.X + padding + columnX[c - 1], y),
                    rows[i].Color, UiScale);
            }

            y += lineHeight;
        }

        foreach (var (text, color) in footer)
        {
            _font.Draw(spriteBatch, text, new Vector2(origin.X + padding, y),
                       color, UiScale);
            y += lineHeight;
        }
    }

    /// <summary>Boş yuva tire ile gösterilir; boşluk "kayıp" gibi okunurdu.</summary>
    private static string SlotText(Keys key) =>
        key == Keys.None ? Loc.T("controls.unboundSlot") : key.ToString();


    /// <summary>
    /// MADDE 23 — erişilebilirlik ve dil ayarları paneli.
    ///
    /// Panelin altındaki not bilinçli: renk körlüğü paleti tek başına
    /// erişilebilirlik değildir. Kritik bilgi her yerde METİNLE de
    /// veriliyor ([X]/[ ], "GUVENLI BOLGE"/"PvP BOLGESI", "Nadir"/"Efsanevi");
    /// palet yalnızca ayırt etmeyi hızlandırıyor.
    /// </summary>
    public void DrawSettings(SpriteBatch spriteBatch, AccessibilitySettings settings,
                             int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var rows = new (string Text, Color Color)[]
        {
            (Loc.T("settings.title"), TextColor),
            (Loc.T("settings.language", Loc.CurrentName), CraftableColor),
            (Loc.T("settings.textScale", settings.TextScale), CraftableColor),
            (Loc.T("settings.colorVision", settings.ColorVisionLabel), CraftableColor),
            ("", DimTextColor),
            (Loc.T("settings.note"), DimTextColor),

            // Palet ornekleri: secilen modun kademeleri gercekten
            // ayrisiyor mu, oyuncu BURADA gorur.
            ($"  {Loc.T("rarity.common")} / {Loc.T("rarity.uncommon")} / " +
             $"{Loc.T("rarity.rare")} / {Loc.T("rarity.epic")} / {Loc.T("rarity.legendary")}",
             TextColor)
        };

        var width = rows.Max(r => _font.Measure(r.Text, UiScale));
        var panelWidth = width + padding * 2;
        var panelHeight = (rows.Length + 2) * lineHeight + padding * 2;

        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   (windowHeight - panelHeight) / 2 - 40,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        var originX = origin.X;
        var originY = origin.Y;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.95f);

        for (var i = 0; i < rows.Length; i++)
        {
            _font.Draw(spriteBatch, rows[i].Text,
                new Vector2(originX + padding, originY + padding + i * lineHeight),
                rows[i].Color, UiScale);
        }

        // Nadirlik renkleri yan yana: secili palette kademeler birbirinden
        // ayrisiyor mu, tek bakista gorulur.
        var swatchY = originY + padding + (rows.Length + 1) * lineHeight;
        var swatchWidth = Math.Max(24, (panelWidth - padding * 2) / 5);

        var palette = settings.Palette;
        var swatches = new[]
        {
            palette.RarityCommon, palette.RarityUncommon, palette.RarityRare,
            palette.RarityEpic, palette.RarityLegendary
        };

        for (var i = 0; i < swatches.Length; i++)
        {
            Fill(spriteBatch,
                new Rectangle(originX + padding + i * swatchWidth, swatchY,
                              swatchWidth - 4, lineHeight - 4),
                swatches[i]);
        }
    }

    /// <summary>
    /// MADDE 24 — yama notları ekranı.
    ///
    /// Sürümler dosyadaki sırayla çizilir. Ekran uzun olabildiği için
    /// <paramref name="scroll"/> ile kaydırılır; sığmayan satırlar
    /// çizilmez (kırpma yerine atlama: kesik yarım satır, listenin
    /// bittiği izlenimi verirdi).
    /// </summary>
    public void DrawPatchNotes(SpriteBatch spriteBatch, PatchNotes notes, int scroll,
                               int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var rows = new List<(string Text, Color Color)>
        {
            ($"{Loc.T("patch.title")}   v{notes.LatestVersion}", TextColor)
        };

        foreach (var release in notes.Releases)
        {
            rows.Add(("", DimTextColor));
            rows.Add(($"v{release.Version}  {release.Date}  —  {release.Title}", CraftableColor));

            foreach (var change in release.Changes)
            {
                rows.Add(($"   - {change}", DimTextColor));
            }
        }

        // Panel pencerenin cogunu kaplar; ic yukseklik kac satir
        // sigacagini belirler.
        var panelWidth = windowWidth - 80;
        var panelHeight = windowHeight - 100;
        var visibleRows = Math.Max(1, (panelHeight - padding * 2) / lineHeight);

        var maxScroll = Math.Max(0, rows.Count - visibleRows);
        var offset = Math.Clamp(scroll, 0, maxScroll);

        var originX = (windowWidth - panelWidth) / 2;
        var originY = (windowHeight - panelHeight) / 2;

        Fill(spriteBatch, new Rectangle(originX, originY, panelWidth, panelHeight),
             PanelColor * 0.96f);

        for (var i = 0; i < visibleRows && offset + i < rows.Count; i++)
        {
            var row = rows[offset + i];

            _font.Draw(spriteBatch, Truncate(row.Text, panelWidth - padding * 2),
                new Vector2(originX + padding, originY + padding + i * lineHeight),
                row.Color, UiScale);
        }

        // Kaydirma gostergesi: liste devam ediyor mu, oyuncu gorsun.
        if (maxScroll > 0)
        {
            var barHeight = Math.Max(12, panelHeight * visibleRows / rows.Count);
            var barY = originY + (panelHeight - barHeight) * offset / maxScroll;

            Fill(spriteBatch, new Rectangle(originX + panelWidth - 6, barY, 4, barHeight),
                 DimTextColor);
        }
    }

    /// <summary>
    /// Metni verilen pixel genişliğine sığacak kadar kısaltır.
    ///
    /// Yama notu satırları uzun olabiliyor; taşan metin panelin dışına
    /// çıkıp arka plana karışıyordu.
    /// </summary>
    private string Truncate(string text, int maxWidth)
    {
        if (_font.Measure(text, UiScale) <= maxWidth) return text;

        // Karakter karakter kisaltmak yerine oransal bir tahminle baslamak
        // uzun satirlarda cok daha az olcum yapar.
        var estimate = Math.Max(1, text.Length * maxWidth / Math.Max(1, _font.Measure(text, UiScale)));
        var result = text[..Math.Min(text.Length, estimate)];

        while (result.Length > 1 && _font.Measure(result + "...", UiScale) > maxWidth)
        {
            result = result[..^1];
        }

        return result + "...";
    }

    /// <summary>
    /// MADDE 24 — photo mode bilgi şeridi.
    ///
    /// Photo mode arayüzü gizler; bu şerit TEK istisnadır ve yalnızca
    /// kareyi kaydetmeden ÖNCE görünür. Kaydedilen karede hiçbir arayüz
    /// olmamalı, yoksa photo mode'un amacı kalmaz.
    /// </summary>
    public void DrawPhotoModeBar(SpriteBatch spriteBatch, string lastSaved,
                                 int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 5 * UiScale;

        var line = "PHOTO MODE  -  yon: kamera   +/-: zoom   F12: kaydet   P: cik";
        if (lastSaved.Length > 0) line += $"   [{lastSaved}]";

        var width = _font.Measure(line, UiScale);
        var x = (windowWidth - width) / 2 - padding;
        var y = windowHeight - lineHeight - padding * 3;

        Fill(spriteBatch, new Rectangle(x, y, width + padding * 2, lineHeight + padding),
             PanelColor * 0.85f);

        _font.Draw(spriteBatch, line, new Vector2(x + padding, y + padding / 2), TextColor, UiScale);
    }

    /// <summary>
    /// MADDE 24 — oyuncunun üstündeki emote balonu.
    ///
    /// DÜNYA katmanında, kamera matrisi altında çizilir: balon karakterle
    /// birlikte hareket etmeli.
    /// </summary>
    public void DrawEmote(SpriteBatch spriteBatch, Vector2 worldPosition, EmoteKind kind)
    {
        var symbol = SocialSystem.Symbol(kind);

        // Dunya katmaninda arayuz olcegi kullanilmaz: kamera zaten
        // yakinlastiriyor, ustune UiScale eklenirse balon devasa olur.
        var width = _font.Measure(symbol, 1);
        var x = (int)worldPosition.X - width / 2;
        var y = (int)worldPosition.Y - 44;

        // Kutu SATIR ARALIGINA degil MUREKKEBE gore: hucrenin bos payi da
        // boyansa balon gozle gorulur bicimde yaziyi askin cikiyor.
        Fill(spriteBatch,
             new Rectangle(x - 2, y + _font.InkTop - 2, width + 4, _font.InkHeight + 4),
             PanelColor * 0.85f);

        _font.Draw(spriteBatch, symbol, new Vector2(x, y), TextColor, 1);
    }

    /// <summary>
    /// MADDE 24 — dünyadaki ping işareti.
    ///
    /// Ömrü doldukça SOLAR: aniden kaybolan bir işaret, oyuncuya hâlâ
    /// orada mı diye baktırır.
    /// </summary>
    public void DrawPing(SpriteBatch spriteBatch, WorldPing ping)
    {
        var (symbol, color) = SocialSystem.PingStyle(ping.Kind);

        var width = _font.Measure(symbol, 1);
        var x = (int)ping.Position.X - width / 2;
        var y = (int)ping.Position.Y - 20;

        var fade = Math.Clamp(ping.Freshness, 0f, 1f);

        Fill(spriteBatch,
             new Rectangle(x - 2, y + _font.InkTop - 2, width + 4, _font.InkHeight + 4),
             PanelColor * (0.8f * fade));

        _font.Draw(spriteBatch, symbol, new Vector2(x, y), color * fade, 1);
    }

    /// <summary>
    /// MADDE 25 — mod/Workshop paneli.
    ///
    /// Parmak izi bilerek görünür: çok oyunculu bir oturum kurulamadığında
    /// oyuncunun ilk bakacağı yer burası olmalı. Parmak izi gizli olsaydı
    /// "neden bağlanamıyorum" sorusunun cevabı hiçbir ekranda olmazdı.
    /// </summary>
    public void DrawWorkshop(SpriteBatch spriteBatch, ModRegistry registry,
                             IWorkshopBackend backend, int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var padding = 6 * UiScale;

        var rows = new List<(string Text, Color Color)>
        {
            ($"{Loc.T("workshop.title")}   {backend.Status}", TextColor),
            ("", DimTextColor),
            (Loc.T("workshop.fingerprint", registry.Fingerprint), CraftableColor),
            ($"  {Loc.T("workshop.fingerprintNote")}", DimTextColor),
            ("", DimTextColor)
        };

        if (registry.Mods.Count == 0)
        {
            rows.Add(($"  {Loc.T("workshop.none")}", DimTextColor));
            rows.Add(($"  {Loc.T("workshop.hint")}", DimTextColor));
        }
        else
        {
            foreach (var mod in registry.Mods)
            {
                var source = Loc.T(mod.FromWorkshop
                    ? "workshop.source.workshop"
                    : "workshop.source.local");

                var mark = mod.Enabled ? "X" : " ";

                // Renk TEK BASINA anlam tasimasin (madde 23): acik/kapali
                // durumu [X]/[ ] ile de veriliyor.
                rows.Add(($"  [{mark}] {mod.Manifest.Name}  v{mod.Manifest.Version}  " +
                          $"({source})  {mod.Files.Count} dosya",
                          mod.Enabled ? CraftableColor : DimTextColor));

                rows.Add(($"        {mod.Manifest.Description}", DimTextColor));
            }
        }

        rows.Add(("", DimTextColor));
        rows.Add(($"  {Loc.T("workshop.noCode")}", DimTextColor));

        foreach (var warning in registry.Warnings)
        {
            rows.Add(($"  ! {warning}", Accessibility.Palette.Negative));
        }

        var width = rows.Max(r => _font.Measure(r.Text, UiScale));
        var panelWidth = Math.Min(width + padding * 2, windowWidth - 24);
        var panelHeight = rows.Count * lineHeight + padding * 2;

        var origin = ClampToWindow((windowWidth - panelWidth) / 2,
                                   (windowHeight - panelHeight) / 2 - 40,
                                   panelWidth, panelHeight, windowWidth, windowHeight);

        Fill(spriteBatch, new Rectangle(origin.X, origin.Y, panelWidth, panelHeight),
             PanelColor * 0.95f);

        for (var i = 0; i < rows.Count; i++)
        {
            _font.Draw(spriteBatch, Truncate(rows[i].Text, panelWidth - padding * 2),
                new Vector2(origin.X + padding, origin.Y + padding + i * lineHeight),
                rows[i].Color, UiScale);
        }
    }

    /// <summary>
    /// Ana menü.
    ///
    /// Menü tam ekran ve arkasındaki dünyayı KOYULTUR, gizlemez: oyuncu
    /// nereye döneceğini görsün. Tamamen opak bir menü, oyunun kapandığı
    /// hissini verirdi.
    /// </summary>
    public void DrawMainMenu(SpriteBatch spriteBatch, MainMenu menu,
                             string versionLine, int windowWidth, int windowHeight)
    {
        var lineHeight = _font.LineHeight * UiScale;

        // Arkadaki dunyayi koyultan tam ekran perde.
        Fill(spriteBatch, new Rectangle(0, 0, windowWidth, windowHeight),
             new Color(10, 11, 16) * 0.82f);

        // --- Baslik ---
        // Menu basligi HUD'dan daha buyuk: ekranin neresi oldugunu tek
        // bakista soylemeli.
        var titleScale = UiScale + 2;
        var title = Loc.T("menu.title");
        var titleWidth = _font.Measure(title, titleScale);

        var titleY = Math.Max(24, windowHeight / 2 - Entries(menu) * lineHeight / 2
                                  - _font.LineHeight * titleScale - 40);

        _font.Draw(spriteBatch, title,
                   new Vector2((windowWidth - titleWidth) / 2, titleY),
                   Accessibility.Palette.Warning, titleScale);

        // Baslik altinda ince ayrac.
        Fill(spriteBatch,
             new Rectangle((windowWidth - titleWidth) / 2,
                           titleY + _font.LineHeight * titleScale + 4, titleWidth, 2),
             Accessibility.Palette.Warning * 0.6f);

        // --- Satirlar ---
        var startY = titleY + _font.LineHeight * titleScale + 34;

        var row = 0;

        for (var i = 0; i < menu.Items.Count; i++)
        {
            var entry = menu.Items[i];

            // Gorunmeyen satir yer de KAPLAMAZ: bosluk birakmak listede
            // delik gibi okunur.
            if (!menu.IsVisible(entry)) continue;

            var enabled = menu.IsEnabled(entry);
            var selected = i == menu.SelectedIndex;

            var label = menu.LabelOf(entry);
            if (!enabled) label += "  " + Loc.T("menu.needWorld");
            else if (entry.IsNewGame) label += "  " + Loc.T("menu.newGameWarn");

            var width = _font.Measure(label, UiScale);
            var x = (windowWidth - width) / 2;
            var y = startY + row * (lineHeight + 6);
            row++;

            // Secili satir hem RENKLE hem ISARETLE gosteriliyor: madde
            // 23'un kurali — renk tek basina anlam tasimaz.
            if (selected)
            {
                Fill(spriteBatch,
                     new Rectangle(x - 14, y - 3, width + 28, lineHeight + 6),
                     Accessibility.Palette.Positive * 0.22f);

                _font.Draw(spriteBatch, ">", new Vector2(x - 12 - _font.Measure(">", UiScale), y),
                           Accessibility.Palette.Positive, UiScale);
            }

            var color = !enabled ? DimTextColor
                      : selected ? Accessibility.Palette.Positive
                      : TextColor;

            _font.Draw(spriteBatch, label, new Vector2(x, y), color, UiScale);
        }

        // --- Alt bilgi ---
        var hint = Loc.T("menu.hint");
        _font.Draw(spriteBatch, hint,
                   new Vector2((windowWidth - _font.Measure(hint, UiScale)) / 2,
                               windowHeight - lineHeight * 2 - 18),
                   DimTextColor, UiScale);

        _font.Draw(spriteBatch, versionLine,
                   new Vector2(12, windowHeight - lineHeight - 10), DimTextColor, UiScale);
    }

    private static int Entries(MainMenu menu) => menu.Items.Count;

    /// <summary>
    /// Alt ekranlarin ustunde duran "Esc ile geri don" seridi.
    ///
    /// Menuden acilan her ekranda gorunur; oyuncunun cikisi arayip
    /// bulmasi gerekmesin.
    /// </summary>
    /// <param name="dim">
    /// Perdenin koyulugu. Gardirop gibi arkadaki DUNYAYI gostermesi gereken
    /// ekranlarda dusuk tutulur: kusanilan kozmetigi karakterin uzerinde
    /// gormek o ekranin butun amaci.
    /// </param>
    public void DrawScreenChrome(SpriteBatch spriteBatch, string titleKey,
                                 int windowWidth, int windowHeight, float dim = 0.86f)
    {
        var lineHeight = _font.LineHeight * UiScale;

        Fill(spriteBatch, new Rectangle(0, 0, windowWidth, windowHeight),
             new Color(10, 11, 16) * dim);

        var title = Loc.T(titleKey);
        _font.Draw(spriteBatch, title, new Vector2(16, 14),
                   Accessibility.Palette.Warning, UiScale);

        var hint = Loc.T("menu.backHint");
        _font.Draw(spriteBatch, hint,
                   new Vector2(windowWidth - _font.Measure(hint, UiScale) - 16, 14),
                   DimTextColor, UiScale);

        Fill(spriteBatch, new Rectangle(16, 14 + lineHeight + 4, windowWidth - 32, 2),
             Accessibility.Palette.Warning * 0.5f);
    }

    /// <summary>Sol üstte seçili yapı ve ağ oturumu durumu.</summary>
    public void DrawStatus(SpriteBatch spriteBatch, string selectedBuild, string sessionLine)
    {
        var lineHeight = _font.LineHeight * UiScale;
        var lines = new[] { sessionLine, Loc.T("hud.build", selectedBuild) };

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
        var line = Loc.T(safe ? "hud.zone.safe" : "hud.zone.pvp");

        // "Olumsuz" rengi de paletten: PvP kirmizisi, renk korlugu modunda
        // guvenli yesilinden ayrisan bir tona kayiyor.
        var color = safe ? CraftableColor : Accessibility.Palette.Negative;

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
