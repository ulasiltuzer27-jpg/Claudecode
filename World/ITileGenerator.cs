namespace PixelSurvival.World;

/// <summary>
/// AŞAMA 2 / MADDE 16 — tile üretimi soyutlaması.
///
/// <see cref="TileMap"/> zaten üreticiden yalnızca iki şey istiyordu:
/// "şu koordinatta hangi tile var" ve "katı mı". Bu arayüz o sözleşmeyi
/// açığa çıkarıyor; böylece chunk'lı sonsuz dünya (<see cref="WorldGenerator"/>)
/// ile sınırlı zindan (<see cref="DungeonGenerator"/>) AYNI TileMap sınıfını
/// kullanabiliyor.
///
/// Alternatifi ikinci bir harita sınıfı yazmaktı; o zaman TileCollider,
/// Player, GatheringSystem, BuildingSystem, FarmingSystem ve Creature'ın
/// hepsinin imzasını değiştirmek gerekirdi. Tek arayüz o çığı önlüyor.
/// </summary>
public interface ITileGenerator
{
    /// <summary>Üretimin türediği tohum.</summary>
    int Seed { get; }

    /// <summary>
    /// Haritanın sınırları (dünya pixel'i). Sonsuz dünyada <c>null</c>.
    /// Kamera clamp'i ve zindan çıkış kontrolü bunu kullanır.
    /// </summary>
    Microsoft.Xna.Framework.Rectangle? Bounds { get; }

    int GetTileIndex(int tileX, int tileY);
    bool IsSolid(int tileX, int tileY);
}
