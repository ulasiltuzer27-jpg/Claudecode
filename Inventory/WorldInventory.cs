using System.Text.Json;
using System.Text.Json.Serialization;
using PixelSurvival.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

using PixelSurvival.Workshop;

namespace PixelSurvival.Inventory;

/// <summary>Bir envanter slotunun içeriği. Boş slot <see cref="Empty"/>.</summary>
public readonly record struct ItemStack(string ItemId, int Count)
{
    public static ItemStack Empty => new("", 0);
    public bool IsEmpty => Count <= 0 || string.IsNullOrEmpty(ItemId);
}

public sealed class ItemDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";

    /// <summary>
    /// Görüntülenen ad. Madde 23'te bu alan bir string tablo anahtarına
    /// dönüşecek; metin Localization/ altından gelecek.
    /// </summary>
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);

    /// <summary>icons_16.png şeridindeki sütun indeksi.</summary>
    [JsonPropertyName("icon")] public int Icon { get; init; }

    [JsonPropertyName("maxStack")] public int MaxStack { get; init; } = 99;
    [JsonPropertyName("category")] public string Category { get; init; } = "material";
}

/// <summary>
/// <c>Content/Items/items.json</c> dosyasının kod karşılığı.
/// </summary>
public sealed class ItemDatabase
{
    [JsonPropertyName("slotCount")] public int SlotCount { get; init; } = 20;
    [JsonPropertyName("items")] public List<ItemDefinition> Items { get; init; } = [];

    private readonly Dictionary<string, ItemDefinition> _byId = [];

    public static ItemDatabase Load(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        // Mod bindirmesinden GECIYOR: bir mod bu tabloyu degistirebilir
        // ya da yeni satir ekleyebilir (bkz. ModdedContent).
        using var stream = ModdedContent.Open(content, assetName);

        var database = JsonSerializer.Deserialize<ItemDatabase>(stream, JsonOptions)
                       ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (database.SlotCount < 1)
        {
            throw new InvalidOperationException($"'{relativePath}': slotCount en az 1 olmalı.");
        }

        foreach (var item in database.Items)
        {
            if (item.MaxStack < 1)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{item.Id}' için maxStack en az 1 olmalı. " +
                    $"Sıfır olsaydı item envantere hiç girmezdi.");
            }

            if (!database._byId.TryAdd(item.Id, item))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{item.Id}' iki kez tanımlanmış.");
            }
        }

        return database;
    }

    public ItemDefinition Get(string itemId) =>
        _byId.TryGetValue(itemId, out var item)
            ? item
            : throw new KeyNotFoundException(
                $"'{itemId}' item'ı tanımlı değil. Mevcut: {string.Join(", ", _byId.Keys)}");

    public bool Contains(string itemId) => _byId.ContainsKey(itemId);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// AŞAMA 1 / MADDE 7 — dünya kaynakları envanteri. HOST AUTHORITATIVE.
///
/// ════════════════════════════════════════════════════════════════════════
/// KRİTİK MİMARİ KURAL — bu sınıf ile SteamInventory ASLA karıştırılmaz
/// ════════════════════════════════════════════════════════════════════════
/// Burası odun, taş, maden, yiyecek, alet ve crafting materyalleri içindir.
/// Bu veriye dünyayı açan oyuncunun makinesi tam yetkilidir.
///
/// Karakter lisansları, satılabilir kozmetikler ve pazar değeri taşıyan
/// varlıklar BURAYA GİRMEZ. Onlar madde 19'da eklenecek ayrı bir
/// <c>PixelSurvival.Inventory.Steam</c> namespace'inde yaşayacak ve
/// sahiplikleri Steam Inventory Service tarafından doğrulanacak.
///
/// Bu sınıftaki hiçbir veri, tek başına marketable/tradable bir Steam item'ı
/// GRANT EDEMEZ. Oyun içi crafting sonucu bir Steam item'ı üretilecekse bu,
/// Steam'in doğrulayabildiği bir exchange recipe veya güvenilir backend
/// üzerinden yapılır — asla WorldInventory → SteamInventory geçişi olarak
/// kodlanmaz.
///
/// Pratik sonuç: bu dosyaya Steam'e ait bir tip, alan veya using EKLEMEYİN.
/// Böyle bir ihtiyaç doğuyorsa tasarım yanlıştır.
///
/// (Neden namespace 'Inventory.World' değil? C#'ta 'PixelSurvival.World'
///  harita namespace'i ile çakışıp nitelenmiş isimleri belirsiz hale
///  getiriyordu. Ayrım namespace adında değil, sınıf ve dosya düzeyinde
///  net tutuldu.)
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class WorldInventory(ItemDatabase database)
{
    private readonly ItemStack[] _slots = new ItemStack[database.SlotCount];

    public int SlotCount => _slots.Length;
    public ItemStack this[int index] => _slots[index];

    /// <summary>Envanter her değiştiğinde tetiklenir (HUD'ı tazelemek için).</summary>
    public event Action? Changed;

    /// <summary>
    /// Mümkün olduğu kadar ekler: önce mevcut yığınları doldurur, sonra boş
    /// slot açar.
    /// </summary>
    /// <returns>SIĞMAYAN miktar. 0 ise hepsi eklendi.</returns>
    public int TryAdd(string itemId, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var definition = database.Get(itemId);
        var remaining = amount;

        // 1) Var olan yığınları tamamla — envanteri parçalı yığınlarla doldurmamak için.
        for (var i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty || _slots[i].ItemId != itemId)
            {
                continue;
            }

            var room = definition.MaxStack - _slots[i].Count;
            if (room <= 0)
            {
                continue;
            }

            var moved = Math.Min(room, remaining);
            _slots[i] = _slots[i] with { Count = _slots[i].Count + moved };
            remaining -= moved;
        }

        // 2) Boş slotlara yeni yığın aç.
        for (var i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (!_slots[i].IsEmpty)
            {
                continue;
            }

            var moved = Math.Min(definition.MaxStack, remaining);
            _slots[i] = new ItemStack(itemId, moved);
            remaining -= moved;
        }

        if (remaining != amount)
        {
            Changed?.Invoke();
        }

        return remaining;
    }

    public int CountOf(string itemId)
    {
        var total = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsEmpty && slot.ItemId == itemId)
            {
                total += slot.Count;
            }
        }

        return total;
    }

    public bool Has(string itemId, int amount) => CountOf(itemId) >= amount;

    /// <summary>
    /// İstenen miktarı çıkarır. YETERSİZSE HİÇBİR ŞEY ÇIKARMAZ.
    ///
    /// Bu bütünlük şart: crafting birden fazla girdi tüketiyor ve ikinci
    /// girdi yetmediğinde birincinin çoktan silinmiş olması item kaybına
    /// yol açardı.
    /// </summary>
    public bool TryRemove(string itemId, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (!Has(itemId, amount))
        {
            return false;
        }

        var remaining = amount;
        for (var i = 0; i < _slots.Length && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty || _slots[i].ItemId != itemId)
            {
                continue;
            }

            var taken = Math.Min(_slots[i].Count, remaining);
            var left = _slots[i].Count - taken;
            _slots[i] = left > 0 ? _slots[i] with { Count = left } : ItemStack.Empty;
            remaining -= taken;
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Slotların anlık kopyası. İşlem geri alınacaksa kullanılır.</summary>
    public ItemStack[] Snapshot() => (ItemStack[])_slots.Clone();

    /// <summary>Bir <see cref="Snapshot"/>'a geri döner.</summary>
    public void Restore(ItemStack[] snapshot)
    {
        Array.Copy(snapshot, _slots, _slots.Length);
        Changed?.Invoke();
    }

    /// <summary>
    /// Bağımsız bir kopya.
    ///
    /// Madde 22'deki takas bunu KURU ÇALIŞTIRMA için kullanıyor: "bu takas
    /// iki tarafa da sığar mı?" sorusu gerçek envantere dokunmadan
    /// cevaplanmalı. <see cref="Snapshot"/>/<see cref="Restore"/> ile de
    /// olurdu ama o yol, aradaki bir hata durumunda envanteri değişmiş
    /// bırakma riskini açık tutuyor.
    /// </summary>
    public WorldInventory Clone()
    {
        var copy = new WorldInventory(database);
        Array.Copy(_slots, copy._slots, _slots.Length);
        return copy;
    }

    /// <summary>
    /// Envanteri bosaltir.
    ///
    /// Kayit yuklenirken gerekli: slotlar once temizlenmezse kaydedilen
    /// esyalar mevcut olanlarin USTUNE eklenir ve oyuncu her yuklemede
    /// zenginlesir.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_slots);
        Changed?.Invoke();
    }

    /// <summary>Dolu slot sayısı — HUD ve doluluk kontrolü için.</summary>
    public int UsedSlots => _slots.Count(s => !s.IsEmpty);
}
