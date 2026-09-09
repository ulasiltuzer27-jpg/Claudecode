using System.Text.Json;
using System.Text.Json.Serialization;
using PixelSurvival.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Animation;

namespace PixelSurvival.Systems.Npcs;

public sealed class TradeOffer
{
    [JsonPropertyName("item")] public string Item { get; init; } = "";
    [JsonPropertyName("price")] public int Price { get; init; }
}

public sealed class NpcDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);
    [JsonPropertyName("sprite")] public string Sprite { get; init; } = "";

    /// <summary>"trade" veya "quest".</summary>
    [JsonPropertyName("role")] public string Role { get; init; } = "trade";

    [JsonPropertyName("offsetTiles")] public TileOffset OffsetTiles { get; init; } = new();

    [JsonPropertyName("greeting")] public string RawGreeting { get; init; } = "";
    [JsonPropertyName("greetingKey")] public string GreetingKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek selamlama — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Greeting => DataName.Of(GreetingKey, RawGreeting);

    /// <summary>NPC'nin oyuncudan SATIN ALDIĞI item'lar (oyuncu satar).</summary>
    [JsonPropertyName("buys")] public List<TradeOffer> Buys { get; init; } = [];

    /// <summary>NPC'nin oyuncuya SATTIĞI item'lar (oyuncu alır).</summary>
    [JsonPropertyName("sells")] public List<TradeOffer> Sells { get; init; } = [];
}

public sealed class TileOffset
{
    [JsonPropertyName("x")] public int X { get; init; }
    [JsonPropertyName("y")] public int Y { get; init; }
}

public sealed class QuestRequirement
{
    [JsonPropertyName("item")] public string Item { get; init; } = "";
    [JsonPropertyName("amount")] public int Amount { get; init; } = 1;
}

public sealed class QuestDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string RawTitle { get; init; } = "";
    [JsonPropertyName("titleKey")] public string TitleKey { get; init; } = "";

    [JsonPropertyName("text")] public string RawText { get; init; } = "";
    [JsonPropertyName("textKey")] public string TextKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek baslik — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Title => DataName.Of(TitleKey, RawTitle);

    /// <summary>Ekranda gosterilecek aciklama — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Text => DataName.Of(TextKey, RawText);
    [JsonPropertyName("require")] public QuestRequirement Require { get; init; } = new();
    [JsonPropertyName("reward")] public List<QuestRequirement> Reward { get; init; } = [];
}

public sealed class NpcTable
{
    [JsonPropertyName("interactRange")] public float InteractRange { get; init; } = 34f;
    [JsonPropertyName("currency")] public string Currency { get; init; } = "coin";
    [JsonPropertyName("npcs")] public List<NpcDefinition> Npcs { get; init; } = [];
    [JsonPropertyName("quests")] public List<QuestDefinition> Quests { get; init; } = [];

    public static NpcTable Load(ContentManager content, string assetName, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<NpcTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (!items.Contains(table.Currency))
        {
            throw new InvalidOperationException(
                $"'{relativePath}': para birimi '{table.Currency}' tanımlı değil.");
        }

        foreach (var npc in table.Npcs)
        {
            foreach (var offer in npc.Buys.Concat(npc.Sells))
            {
                if (!items.Contains(offer.Item))
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{npc.Id}' tanımsız '{offer.Item}' ticareti yapıyor.");
                }

                if (offer.Price < 1)
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{npc.Id}' için '{offer.Item}' fiyatı en az 1 olmalı — " +
                        $"sıfır fiyat bedava item demek.");
                }
            }

            // Aynı item'ı hem alıp hem satan NPC sonsuz para döngüsü yaratır:
            // ucuza al, pahalıya sat. Alış fiyatı satıştan düşük olmalı.
            foreach (var sell in npc.Sells)
            {
                var buy = npc.Buys.FirstOrDefault(b => b.Item == sell.Item);
                if (buy is not null && buy.Price >= sell.Price)
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{npc.Id}' '{sell.Item}' item'ını " +
                        $"{sell.Price} altına satıp {buy.Price} altına geri alıyor — " +
                        $"sonsuz para istismarı.");
                }
            }
        }

        foreach (var quest in table.Quests)
        {
            foreach (var itemId in quest.Reward.Select(r => r.Item).Append(quest.Require.Item))
            {
                if (!items.Contains(itemId))
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{quest.Id}' tanımsız '{itemId}' item'ını kullanıyor.");
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

/// <summary>Dünyada duran bir NPC.</summary>
public sealed class Npc(NpcDefinition definition, SpriteSheet sheet, Vector2 position)
{
    private readonly SpriteAnimator _animator = new(sheet, "idle_down");

    public NpcDefinition Definition { get; } = definition;
    public Vector2 Position { get; } = position;

    public void Update(GameTime gameTime) => _animator.Update(gameTime);

    public void Draw(SpriteBatch spriteBatch)
    {
        var origin = new Vector2(sheet.FrameWidth / 2f, sheet.FrameHeight);

        spriteBatch.Draw(sheet.Texture, Position, _animator.CurrentSourceRectangle,
            Color.White, rotation: 0f, origin: origin, scale: 1f,
            effects: SpriteEffects.None, layerDepth: 0f);
    }
}

/// <summary>
/// AŞAMA 2 / MADDE 17 — NPC tüccar ve görev sistemi.
///
/// TİCARET: NPC'nin aldığı ve sattığı item'lar veriden gelir. Fiyat dengesi
/// yeniden derleme gerektirmez. Yükleme sırasında bir istismar kontrolü
/// yapılır: aynı item'ı sattığından pahalıya geri alan NPC sonsuz para
/// üretir; bu durumda oyun açılışta patlar.
///
/// GÖREVLER: sıralı getir-götür zinciri. Biri tamamlanmadan sonraki
/// görünmez — oyuncuya tek bir sonraki hedef gösterilir.
///
/// KAPSAM DIŞI: diyalog ağacı, itibar/ilişki, günlük görev, NPC gezinmesi
/// (şu an sabit duruyorlar), stok limiti, fiyat dalgalanması, ağ
/// senkronizasyonu. NPC'ler ve görev ilerlemesi YEREL.
/// </summary>
public sealed class NpcSystem
{
    private readonly NpcTable _table;
    private readonly Dictionary<string, SpriteSheet> _sheets = [];
    private readonly List<Npc> _npcs = [];
    private readonly HashSet<string> _completed = [];

    public IReadOnlyList<Npc> Npcs => _npcs;
    public string Currency => _table.Currency;

    /// <summary>Sıradaki tamamlanmamış görev; hepsi bittiyse null.</summary>
    public QuestDefinition? ActiveQuest =>
        _table.Quests.FirstOrDefault(q => !_completed.Contains(q.Id));

    public int CompletedQuests => _completed.Count;

    /// <summary>Tamamlanmış görev kimlikleri — kaydetmek için.</summary>
    public IReadOnlyCollection<string> CompletedQuestIds => _completed;

    /// <summary>
    /// Kayıttan gelen görev ilerlemesini geri kurar.
    ///
    /// Tanımsız kimlikler bilinçli olarak yok sayılıyor: bir görev
    /// <c>npcs.json</c>'dan kaldırıldığında ya da bir mod onu
    /// değiştirdiğinde eski kayıttaki kimlik karşılıksız kalır. Bu yüzden
    /// yüklemeyi reddetmek, oyuncunun bütün ilerlemesini bir veri
    /// düzenlemesi uğruna çöpe atmak olurdu. Karşılıksız kimlik sadece
    /// hiçbir göreve denk gelmez.
    /// </summary>
    public void RestoreQuests(IEnumerable<string> completed)
    {
        _completed.Clear();
        foreach (var id in completed) _completed.Add(id);
    }

    public NpcSystem(NpcTable table, ContentManager content)
    {
        _table = table;

        foreach (var npc in table.Npcs)
        {
            _sheets[npc.Id] = SpriteSheet.Load(content, npc.Sprite);
        }
    }

    /// <summary>NPC'leri spawn noktasının çevresine yerleştirir.</summary>
    public void Populate(Vector2 spawnPosition, int tileSize)
    {
        _npcs.Clear();

        foreach (var definition in _table.Npcs)
        {
            var position = spawnPosition + new Vector2(
                definition.OffsetTiles.X * tileSize,
                definition.OffsetTiles.Y * tileSize);

            _npcs.Add(new Npc(definition, _sheets[definition.Id], position));
        }
    }

    public void Update(GameTime gameTime)
    {
        foreach (var npc in _npcs)
        {
            npc.Update(gameTime);
        }
    }

    /// <summary>Menzildeki en yakın NPC.</summary>
    public Npc? Nearest(Vector2 position)
    {
        Npc? best = null;
        var bestDistance = _table.InteractRange * _table.InteractRange;

        foreach (var npc in _npcs)
        {
            var distance = Vector2.DistanceSquared(position, npc.Position);
            if (distance <= bestDistance)
            {
                best = npc;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Oyuncunun envanterindeki para.</summary>
    public int Coins(WorldInventory inventory) => inventory.CountOf(_table.Currency);

    /// <summary>
    /// Oyuncu bir item satar. NPC'nin "buys" listesindeki fiyattan.
    /// </summary>
    public string Sell(NpcDefinition npc, string itemId, WorldInventory inventory)
    {
        var offer = npc.Buys.FirstOrDefault(o => o.Item == itemId);
        if (offer is null)
        {
            return $"{npc.Name} bunu almıyor";
        }

        if (!inventory.TryRemove(itemId, 1))
        {
            return "Satacak item yok";
        }

        // Para envantere sığmazsa satış geri alınır — item kaybolmasın.
        if (inventory.TryAdd(_table.Currency, offer.Price) > 0)
        {
            inventory.TryAdd(itemId, 1);
            return "Envanterde yer yok";
        }

        return $"+{offer.Price} altın";
    }

    /// <summary>Oyuncu bir item satın alır.</summary>
    public string Buy(NpcDefinition npc, string itemId, WorldInventory inventory)
    {
        var offer = npc.Sells.FirstOrDefault(o => o.Item == itemId);
        if (offer is null)
        {
            return $"{npc.Name} bunu satmıyor";
        }

        if (!inventory.Has(_table.Currency, offer.Price))
        {
            return $"Yetersiz altın ({offer.Price} gerekli)";
        }

        var snapshot = inventory.Snapshot();

        inventory.TryRemove(_table.Currency, offer.Price);

        // İşlem bütünlüğü: item sığmazsa para geri.
        if (inventory.TryAdd(itemId, 1) > 0)
        {
            inventory.Restore(snapshot);
            return "Envanterde yer yok";
        }

        return $"-{offer.Price} altın";
    }

    /// <summary>
    /// Aktif görevi teslim etmeyi dener.
    /// </summary>
    public string TurnInQuest(WorldInventory inventory)
    {
        if (ActiveQuest is not { } quest)
        {
            return "Şimdilik iş yok";
        }

        if (!inventory.Has(quest.Require.Item, quest.Require.Amount))
        {
            return $"{quest.Title}: {inventory.CountOf(quest.Require.Item)}/{quest.Require.Amount}";
        }

        var snapshot = inventory.Snapshot();
        inventory.TryRemove(quest.Require.Item, quest.Require.Amount);

        foreach (var reward in quest.Reward)
        {
            if (inventory.TryAdd(reward.Item, reward.Amount) > 0)
            {
                inventory.Restore(snapshot);
                return "Envanterde yer yok";
            }
        }

        _completed.Add(quest.Id);
        return $"'{quest.Title}' tamamlandi!";
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var npc in _npcs)
        {
            npc.Draw(spriteBatch);
        }
    }
}
