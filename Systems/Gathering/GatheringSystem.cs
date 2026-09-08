using Microsoft.Xna.Framework;
using PixelSurvival.Entities;
using PixelSurvival.Systems.Input;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Gathering;

/// <summary>Tamamlanan bir toplamanın sonucu.</summary>
public readonly record struct GatherResult(string Resource, int Amount, Point Tile);

/// <summary>
/// AŞAMA 1 / MADDE 6 — kaynak toplama.
///
/// Akış: karakterin baktığı tile'a bak → toplanabilir mi → tuş basılı tutuldukça
/// ilerleme biriktir → süre dolunca tile'ı tüket ve sonucu döndür.
///
/// KAPSAM DIŞI: envanter. Bu sistem yalnızca "şu kadar odun toplandı" bilgisini
/// ÜRETİR, nereye yazılacağını bilmez. Madde 7'de WorldInventory bu sonucu
/// tüketecek; şimdilik Game1 geçici bir sayaçta tutuyor.
/// </summary>
public sealed class GatheringSystem(ResourceTable table)
{
    private Point? _target;
    private float _progressSeconds;
    private float _requiredSeconds;

    /// <summary>Şu an geçerli bir hedefe toplama yapılıyor mu.</summary>
    public bool IsGathering { get; private set; }

    /// <summary>Karakterin baktığı tile — geçerli hedef olmasa da gösterilir.</summary>
    public Point? AimedTile { get; private set; }

    /// <summary>Hedef toplanabilir mi (highlight rengini belirlemek için).</summary>
    public bool AimedTileIsHarvestable { get; private set; }

    /// <summary>Toplama ilerlemesi, 0..1.</summary>
    public float Progress01 =>
        _requiredSeconds <= 0f ? 0f : Math.Clamp(_progressSeconds / _requiredSeconds, 0f, 1f);

    /// <summary>
    /// Bir karelik toplama mantığını işler.
    /// </summary>
    /// <returns>Toplama TAMAMLANDIYSA sonuç, aksi halde null.</returns>
    public GatherResult? Update(PlayerInput input, Player player, TileMap map, GameTime gameTime)
    {
        AimedTile = GetAimedTile(player, map);
        var aimed = AimedTile.Value;

        var tileIndex = map.GetTileIndex(aimed.X, aimed.Y);
        var harvestable = table.TryGet(tileIndex, out var definition);
        AimedTileIsHarvestable = harvestable;

        // Tuş bırakıldıysa veya hedef geçersizse ilerlemeyi sıfırla.
        if (!input.Gather || !harvestable)
        {
            Reset();
            return null;
        }

        // Hedef değiştiyse baştan başla. Yoksa oyuncu bir ağacı yarıya kadar
        // kesip yan tarafa dönerek ikinci ağacı bedavaya devirebilirdi.
        if (_target != aimed)
        {
            _target = aimed;
            _progressSeconds = 0f;
            _requiredSeconds = definition.HarvestSeconds;
        }

        IsGathering = true;
        _progressSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_progressSeconds < _requiredSeconds)
        {
            return null;
        }

        // --- Toplama tamamlandı ---
        map.SetTile(aimed.X, aimed.Y, table.GetReplacementIndex(definition));

        var result = new GatherResult(definition.Resource, definition.Amount, aimed);
        Reset();
        return result;
    }

    /// <summary>
    /// Yalnızca hedefi tazeler, toplama mantığını ÇALIŞTIRMAZ.
    ///
    /// İstemci modunda kullanılır: toplamayı host çözer ama oyuncu neye
    /// baktığını yine de görmeli. Bu metod olmasaydı istemcide hedef
    /// çerçevesi hiç görünmezdi.
    /// </summary>
    public void UpdateAimOnly(Player player, TileMap map)
    {
        AimedTile = GetAimedTile(player, map);
        AimedTileIsHarvestable = table.TryGet(
            map.GetTileIndex(AimedTile.Value.X, AimedTile.Value.Y), out _);
        IsGathering = false;
    }

    private void Reset()
    {
        IsGathering = false;
        _target = null;
        _progressSeconds = 0f;
        _requiredSeconds = 0f;
    }

    /// <summary>
    /// Karakterin baktığı yöndeki hedef tile.
    ///
    /// Referans nokta collider'ın MERKEZİ, ayak konumu değil: ayak konumu tam
    /// tile sınırında durduğu için <c>floor</c> bir alt tile'a kayabilir ve
    /// hedef bir kare titrer.
    /// </summary>
    private Point GetAimedTile(Player player, TileMap map)
    {
        var collider = player.Collider;
        var centerX = collider.Left + collider.Width / 2f;
        var centerY = collider.Top + collider.Height / 2f;

        var tileX = (int)MathF.Floor(centerX / map.TileSize);
        var tileY = (int)MathF.Floor(centerY / map.TileSize);

        var (offsetX, offsetY) = player.Facing switch
        {
            Facing.Left => (-1, 0),
            Facing.Right => (1, 0),
            Facing.Up => (0, -1),
            _ => (0, 1)
        };

        return new Point(
            tileX + offsetX * table.ReachTiles,
            tileY + offsetY * table.ReachTiles);
    }
}
