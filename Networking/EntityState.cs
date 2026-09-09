namespace PixelSurvival.Networking;

/// <summary>
/// Ağda taşınan varlık türü.
///
/// Değerler protokol sabiti — DEĞİŞTİRİLMEZ. Tür ile tanım indeksi ayrı
/// alanlar: tek bir "tip" alanında birleştirilselerdi, düşman tablosuna
/// yeni bir satır eklemek yaratıkların numaralarını kaydırırdı.
/// </summary>
public enum EntityKind : byte
{
    /// <summary>Düşman veya boss (<c>Entities/enemies.json</c>).</summary>
    Enemy = 0,

    /// <summary>Evcilleştirilebilir yaratık (<c>Entities/creatures.json</c>).</summary>
    Creature = 1
}

/// <summary>
/// AŞAMA 2 — bir varlığın kablo üzerindeki durumu.
///
/// ── Neden NPC yok ───────────────────────────────────────────────────────
/// NPC'ler tohumdan türeyen SABİT konumlarda duruyor ve hiç hareket
/// etmiyorlar; iki taraf da aynı tohumdan aynı yerlere koyuyor. Onları
/// her tick göndermek, hiç değişmeyen bir veriyi tekrar tekrar yollamak
/// olurdu. Görev ilerlemesi de bilinçli olarak oyuncuya özel: aynı görevi
/// herkesin ayrı yapması isteniyor.
///
/// ── Neden can yüzde ─────────────────────────────────────────────────────
/// Boss'un canı 320; tek bayta sığmıyor. Ham değeri kırpmak boss can
/// çubuğunu yalancı yapardı. İstemcinin can ile yaptığı tek şey çubuğu
/// çizmek, o da zaten oran istiyor.
/// </summary>
public readonly record struct EntityState(
    ushort EntityId,
    byte Kind,
    byte TypeIndex,
    float X,
    float Y,
    byte Facing,
    byte HealthPercent,
    byte Flags)
{
    /// <summary>
    /// Yaratık kimliklerinin başladığı yer.
    ///
    /// Düşmanlar ve yaratıklar AYRI sistemlerde doğuyor ve birbirlerinin
    /// sayaçlarını bilmiyorlar; ama istemcide tek bir sözlükte yaşıyorlar,
    /// yani kimlikler bütün türler arasında benzersiz olmak zorunda.
    /// Kimlik uzayı ikiye bölünerek iki sistemin haberleşmesi gerekmeden
    /// benzersizlik garanti ediliyor: düşmanlar 1..0x7FFF, yaratıklar
    /// 0x8000..0xFFFF.
    /// </summary>
    public const ushort CreatureIdBase = 0x8000;

    public const byte FlagDead = 1 << 0;
    public const byte FlagMoving = 1 << 1;
    public const byte FlagAttacking = 1 << 2;

    /// <summary>Yaratık evcilleştirilmiş (istemcide farklı gösterilir).</summary>
    public const byte FlagTamed = 1 << 3;

    /// <summary>Boss — istemci can çubuğunu daha belirgin çizer.</summary>
    public const byte FlagBoss = 1 << 4;

    public bool IsDead => (Flags & FlagDead) != 0;
    public bool IsMoving => (Flags & FlagMoving) != 0;
    public bool IsAttacking => (Flags & FlagAttacking) != 0;
    public bool IsTamed => (Flags & FlagTamed) != 0;
    public bool IsBoss => (Flags & FlagBoss) != 0;

    public EntityKind KindValue => (EntityKind)Kind;
}
