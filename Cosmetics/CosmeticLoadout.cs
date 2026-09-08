namespace PixelSurvival.Cosmetics;

/// <summary>
/// Bir karakterin o anda kuşandığı kozmetikler — slot başına en fazla bir tane.
///
/// Kuşanma kuralı tek yerde: <see cref="TryEquip"/>. Doğrudan sözlüğe
/// yazmak mümkün değil, çünkü sahiplik denetimini atlamak bir satırlık iş
/// haline gelirdi.
/// </summary>
public sealed class CosmeticLoadout(ICosmeticOwnership ownership)
{
    private readonly Dictionary<CosmeticSlot, CosmeticDefinition> _equipped = [];

    /// <summary>Kuşanılan kozmetikler, ÇİZİM SIRASINA göre.</summary>
    public IEnumerable<CosmeticDefinition> InDrawOrder =>
        _equipped.Values.OrderBy(c => c.Slot.DrawOrder());

    /// <summary>Gövdenin ARKASINDA çizilecek katmanlar.</summary>
    public IEnumerable<CosmeticDefinition> BehindBody =>
        InDrawOrder.Where(c => c.Slot.DrawOrder() < CosmeticSlot.Body.DrawOrder());

    /// <summary>Gövdenin ÖNÜNDE çizilecek katmanlar.</summary>
    public IEnumerable<CosmeticDefinition> InFrontOfBody =>
        InDrawOrder.Where(c => c.Slot.DrawOrder() > CosmeticSlot.Body.DrawOrder());

    public CosmeticDefinition? InSlot(CosmeticSlot slot) =>
        _equipped.GetValueOrDefault(slot);

    /// <summary>
    /// Kozmetiği kuşanır. Aynı slottaki önceki kozmetik düşer.
    /// </summary>
    /// <remarks>
    /// Sezon penceresine BAKILMAZ — bilerek. Pencere yalnızca item'ın
    /// elde edilebilirliğini kapatır; sahip olunan bir kozmetik sonsuza
    /// kadar kuşanılabilir kalır. Bkz. <see cref="SeasonalWindow"/>.
    /// </remarks>
    /// <returns>Sahiplik yoksa <c>false</c> ve görünüm değişmez.</returns>
    public bool TryEquip(CosmeticDefinition cosmetic, out string reason)
    {
        if (!ownership.Owns(cosmetic))
        {
            reason = $"'{cosmetic.Name}' sahipligi dogrulanamadi.";
            return false;
        }

        _equipped[cosmetic.Slot] = cosmetic;
        reason = "";
        return true;
    }

    /// <summary>Slottaki kozmetiği çıkarır.</summary>
    public void Clear(CosmeticSlot slot) => _equipped.Remove(slot);

    /// <summary>Tüm kozmetikleri çıkarır (çıplak beden).</summary>
    public void ClearAll() => _equipped.Clear();

    /// <summary>
    /// Slottaki kozmetiği listedeki bir sonrakine çevirir; sonu gelince
    /// "hiçbiri"ne döner. Sahip olunmayanlar ATLANIR — oyuncu kuşanamayacağı
    /// bir seçeneğe takılıp kalmasın.
    /// </summary>
    /// <returns>Yeni durum: kuşanılan kozmetik ya da <c>null</c> (boş slot).</returns>
    public CosmeticDefinition? CycleSlot(CosmeticTable table, CosmeticSlot slot)
    {
        var owned = table.InSlot(slot).Where(ownership.Owns).ToArray();
        if (owned.Length == 0) return null;

        var current = InSlot(slot);
        if (current is null)
        {
            TryEquip(owned[0], out _);
            return owned[0];
        }

        var index = Array.FindIndex(owned, c => c.Id == current.Id);

        // Son öğeden sonra "hiçbiri" gelir: oyuncunun slotu boşaltabilmesi
        // için ayrı bir tuş gerekmesin.
        if (index < 0 || index + 1 >= owned.Length)
        {
            Clear(slot);
            return null;
        }

        TryEquip(owned[index + 1], out _);
        return owned[index + 1];
    }
}
