namespace PixelSurvival.Cosmetics;

/// <summary>
/// Karakter görünümünün katmanları.
///
/// Sıralama ÇİZİM SIRASIDIR: küçük değer arkada kalır. Pelerin gövdenin
/// arkasında, şapka saçın önünde olmalı; sıralamayı enum değerine bağlamak
/// bu kuralı tek bir yerde tutuyor.
///
/// <see cref="Body"/> bir kozmetik değil, temel beden sheet'inin yeridir —
/// katman yığınında nereye girdiği burada görünsün diye listede duruyor.
/// Bir kozmetik tanımı bu slotu KULLANAMAZ (<see cref="CosmeticTable"/>
/// yükleme sırasında reddeder): beden lisansı ayrı bir kavram.
///
/// Aynı sıralama <c>Tools/generate_placeholders.py</c> içindeki
/// <c>SLOT_ORDER</c> tablosunda da var; <c>verify_content.py</c> ikisinin
/// ayrışmadığını denetliyor.
/// </summary>
public enum CosmeticSlot
{
    /// <summary>Gövdenin ARKASINDA çizilir.</summary>
    Cape = 0,

    /// <summary>Temel beden sheet'i. Kozmetik tanımlarına kapalı.</summary>
    Body = 10,

    /// <summary>Gövdenin üstündeki giysi.</summary>
    Outfit = 20,

    Hair = 30,

    /// <summary>Saçın önünde.</summary>
    Hat = 40,

    /// <summary>En önde: küçük efektler, parıltılar.</summary>
    Accessory = 50
}

public static class CosmeticSlotExtensions
{
    /// <summary>Çizim sırası — küçük değer önce (arkada) çizilir.</summary>
    public static int DrawOrder(this CosmeticSlot slot) => (int)slot;

    /// <summary>
    /// Oyuncunun kuşanabileceği slotlar. <see cref="CosmeticSlot.Body"/>
    /// bilerek dışarıda: beden değişimi bir karakter lisansıdır, kozmetik değil.
    /// </summary>
    public static readonly CosmeticSlot[] Equippable =
    [
        CosmeticSlot.Cape,
        CosmeticSlot.Outfit,
        CosmeticSlot.Hair,
        CosmeticSlot.Hat,
        CosmeticSlot.Accessory
    ];
}
