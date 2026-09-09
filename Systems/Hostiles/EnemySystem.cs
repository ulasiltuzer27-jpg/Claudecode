using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Networking;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Climate;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Hostiles;

/// <summary>Bir düşman öldüğünde düşen ganimet.</summary>
public readonly record struct EnemyDrop(string Item, int Amount, string EnemyName);

/// <summary>
/// Bu karede olen bir dusman.
///
/// Neden <see cref="EnemyDrop"/> yetmiyor: ganimet SANSA bagli. Butun
/// sanslar tutmazsa olum hic drop uretmez ve drop'lari sayan bir taraf
/// (orn. madde 21'deki basarim sayaci) oldurmeleri EKSIK sayar.
/// </summary>
public readonly record struct EnemyKill(string Name, bool IsBoss);

/// <summary>
/// AŞAMA 2 / MADDE 15 — düşman doğurma ve boss rotasyonu.
///
/// ════════════════════════════════════════════════════════════════════════
/// BOSS ROTASYONU
/// ════════════════════════════════════════════════════════════════════════
/// Aktif boss dünya gününe göre döner: <c>bossRotationDays</c> günde bir
/// sıradakine geçer. Rastgele seçilmiyor — oyuncu "bu hafta hangi boss
/// var" bilgisini öğrenip planlayabilmeli. Rotasyon dünya saatinden
/// (madde 12) türediği için host otoriter olması da otomatik geliyor.
/// ════════════════════════════════════════════════════════════════════════
///
/// Düşmanlar oyuncunun ÇEVRESİNDE ama GÖRÜŞ ALANI DIŞINDA doğar. Gözünün
/// önünde belirmeleri adaletsiz hissettirir. Uzaklaşınca silinirler ki
/// oyuncu haritada gezerken arkasında bir ordu birikmesin.
///
/// KAPSAM DIŞI: sürü davranışı, menzilli düşman.
///
/// Düşmanlar duvarları DOLAŞIR (<see cref="TilePathfinder"/>) ve host
/// otoriterdir: konumları ağ üzerinden istemcilere yayınlanır.
/// </summary>
public sealed class EnemySystem
{
    private readonly EnemyTable _table;
    private readonly Dictionary<string, SpriteSheet> _sheets = [];
    private readonly List<Enemy> _enemies = [];
    private readonly Tileset _tileset;

    /// <summary>
    /// Bütün düşmanların PAYLAŞTIĞI yol bulucu.
    ///
    /// Arama tabloları çağrılar arasında yeniden kullanılıyor; düşman
    /// başına bir örnek, aynı tabloları düşman sayısı kadar çoğaltırdı.
    /// Düşmanlar sırayla güncellendiği için paylaşım güvenli.
    /// </summary>
    private readonly TilePathfinder _pathfinder = new();

    private float _spawnTimer;
    private uint _randomState;
    private int _lastBossDay = -1;

    /// <summary>
    /// Sıradaki ağ kimliği.
    ///
    /// 0 "kimliksiz" için ayrıldı, bu yüzden 1'den başlıyor. Üst sınıra
    /// gelindiğinde başa sarıyor: 32766 kimlik, aynı anda en çok 8 düşman
    /// varken saatlerce yetiyor ve sarma anında o kimlikli bir düşmanın
    /// hâlâ yaşıyor olma ihtimali yok denecek kadar düşük.
    /// </summary>
    private ushort _nextNetworkId = 1;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>Şu anki rotasyonun boss'u.</summary>
    public HostileDefinition ActiveBoss { get; private set; }

    /// <summary>Bu rotasyondaki boss yenildi mi.</summary>
    public bool BossDefeated { get; private set; }

    private readonly List<EnemyKill> _killsThisFrame = [];

    /// <summary>Son <c>Update</c>'te olen dusmanlar. Ganimetten BAGIMSIZ.</summary>
    public IReadOnlyList<EnemyKill> KillsThisFrame => _killsThisFrame;

    public EnemySystem(EnemyTable table, ContentManager content, Tileset tileset, int seed)
    {
        _table = table;
        _tileset = tileset;

        foreach (var hostile in table.Enemies.Concat(table.Bosses))
        {
            _sheets[hostile.Id] = SpriteSheet.Load(content, hostile.Sprite);
        }

        _randomState = (uint)seed ^ 0x7F4A7C15u;
        if (_randomState == 0)
        {
            _randomState = 0x9E3779B9u;
        }

        ActiveBoss = table.Bosses[0];
    }

    /// <summary>Gün değişince boss rotasyonunu ilerletir.</summary>
    public void UpdateRotation(ClimateSystem climate)
    {
        var slot = (climate.Day - 1) / _table.BossRotationDays;

        if (slot == _lastBossDay)
        {
            return;
        }

        _lastBossDay = slot;
        ActiveBoss = _table.Bosses[slot % _table.Bosses.Count];
        BossDefeated = false;
    }

    /// <summary>
    /// Düşmanları günceller, doğurur ve ölüleri temizler. Host çağırır.
    /// </summary>
    /// <returns>Bu karede düşen ganimetler.</returns>
    public IReadOnlyList<EnemyDrop> Update(GameTime gameTime, TileMap map, Player player,
                                           ClimateSystem climate, WorldInventory inventory,
                                           bool allowSpawning)
    {
        // Oldurmeler kare basina raporlanir: onceki karenin listesi
        // birikmemeli, yoksa ayni olum defalarca sayilir.
        _killsThisFrame.Clear();

        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        List<EnemyDrop>? drops = null;

        foreach (var enemy in _enemies)
        {
            var damage = enemy.Update(gameTime, map, player, _pathfinder);

            if (damage > 0)
            {
                player.TakeDamage(damage);
            }
        }

        // Ölüleri temizle ve ganimeti ver. Ölüm animasyonu bitsin diye
        // hemen değil, kısa bir gecikmeyle siliniyorlar.
        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];

            if (!enemy.IsDead || enemy.SecondsDead < 0.8f)
            {
                continue;
            }

            foreach (var drop in enemy.Definition.Drops)
            {
                if (NextUnit() > drop.Chance)
                {
                    continue;
                }

                inventory.TryAdd(drop.Item, drop.Amount);
                (drops ??= []).Add(new EnemyDrop(drop.Item, drop.Amount, enemy.Definition.Name));
            }

            if (enemy.IsBoss)
            {
                BossDefeated = true;
            }

            _killsThisFrame.Add(new EnemyKill(enemy.Definition.Name, enemy.IsBoss));
            _enemies.RemoveAt(i);
        }

        // Çok uzaklaşanları sil.
        var despawn = _table.DespawnRadiusTiles * map.TileSize;
        _enemies.RemoveAll(e => !e.IsBoss &&
            Vector2.Distance(e.Position, player.Position) > despawn);

        if (allowSpawning)
        {
            _spawnTimer -= delta * (climate.IsDaytime ? 1f : _table.NightSpawnMultiplier);

            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _table.SpawnIntervalSeconds;
                TrySpawn(map, player, climate);
            }
        }

        return drops ?? (IReadOnlyList<EnemyDrop>)[];
    }

    private void TrySpawn(TileMap map, Player player, ClimateSystem climate)
    {
        if (_enemies.Count(e => !e.IsBoss) >= _table.MaxAlive)
        {
            return;
        }

        var candidates = _table.Enemies
            .Where(e => !e.NightOnly || !climate.IsDaytime)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var definition = candidates[(int)(NextUnit() * candidates.Count) % candidates.Count];
        var allowed = definition.SpawnTiles.Select(_tileset.IndexOf).ToHashSet();

        // Görüş alanının dışında bir halka üzerinde 12 deneme.
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var angle = NextUnit() * MathHelper.TwoPi;
            var radius = _table.SpawnRadiusTiles * (0.7f + NextUnit() * 0.3f);

            var tileX = (int)(player.Position.X / map.TileSize + MathF.Cos(angle) * radius);
            var tileY = (int)(player.Position.Y / map.TileSize + MathF.Sin(angle) * radius);

            if (map.IsSolidTile(tileX, tileY) ||
                !allowed.Contains(map.GetTileIndex(tileX, tileY)))
            {
                continue;
            }

            var position = new Vector2(
                tileX * map.TileSize + map.TileSize / 2f, (tileY + 1) * map.TileSize);

            _enemies.Add(new Enemy(definition, _sheets[definition.Id], position, isBoss: false,
                                   NextNetworkId()));
            return;
        }
    }

    /// <summary>Zindanda boss doğurur (madde 16).</summary>
    public void SpawnBoss(Vector2 position)
    {
        _enemies.Add(new Enemy(ActiveBoss, _sheets[ActiveBoss.Id], position, isBoss: true,
                               NextNetworkId()));
    }

    private ushort NextNetworkId()
    {
        var id = _nextNetworkId++;

        // EntityState.CreatureIdBase'e kadar: ust yarisi yaratiklarin.
        if (_nextNetworkId >= EntityState.CreatureIdBase) _nextNetworkId = 1;

        return id;
    }

    /// <summary>
    /// Bir tanım indeksinin sprite sayfası — istemci uzak düşmanı bununla çizer.
    ///
    /// İndeks <see cref="EnemyTable.All"/> sırasına göre; tanımsız indekste
    /// <c>null</c> döner ve varlık sessizce atlanır (sürüm farkı oyunu
    /// çökertmemeli).
    /// </summary>
    public SpriteSheet? SheetFor(byte typeIndex) =>
        typeIndex < _table.All.Count ? _sheets[_table.All[typeIndex].Id] : null;

    /// <summary>Bir düşmanın tanım indeksi — snapshot'a yazmak için.</summary>
    public byte TypeIndexOf(Enemy enemy) => (byte)_table.IndexOf(enemy.Definition.Id);

    /// <summary>Harita değiştiğinde (zindana giriş/çıkış) düşmanları temizler.</summary>
    public void Clear() => _enemies.Clear();

    /// <summary>
    /// Oyuncunun saldırısını düşmanlara uygular.
    /// <see cref="Combat.CombatSystem"/> yalnızca oyuncular arasını çözüyor;
    /// düşman hasarı burada, aynı menzil mantığıyla.
    /// </summary>
    public int ApplyPlayerAttack(Vector2 origin, float range, int damage)
    {
        var hits = 0;

        foreach (var enemy in _enemies)
        {
            if (enemy.IsDead)
            {
                continue;
            }

            if (Vector2.DistanceSquared(origin, enemy.Position) <= range * range)
            {
                enemy.TakeDamage(damage);
                hits++;
            }
        }

        return hits;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var enemy in _enemies)
        {
            enemy.Draw(spriteBatch);
        }
    }

    /// <summary>xorshift32 — Noise.cs ile aynı taşınabilir PRNG.</summary>
    private float NextUnit()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return _randomState / 4294967296f;
    }
}
