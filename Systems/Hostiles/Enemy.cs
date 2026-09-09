using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Climate;
using PixelSurvival.Systems.Collision;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Hostiles;

public sealed class DropDefinition
{
    [JsonPropertyName("item")] public string Item { get; init; } = "";
    [JsonPropertyName("amount")] public int Amount { get; init; } = 1;
    [JsonPropertyName("chance")] public float Chance { get; init; } = 1f;
}

public sealed class HostileDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("sprite")] public string Sprite { get; init; } = "";
    [JsonPropertyName("health")] public int Health { get; init; } = 30;
    [JsonPropertyName("damage")] public int Damage { get; init; } = 8;
    [JsonPropertyName("moveSpeed")] public float MoveSpeed { get; init; } = 52f;

    /// <summary>Bu mesafede oyuncuyu fark eder ve kovalamaya başlar.</summary>
    [JsonPropertyName("aggroRange")] public float AggroRange { get; init; } = 120f;

    [JsonPropertyName("attackRange")] public float AttackRange { get; init; } = 20f;
    [JsonPropertyName("attackCooldown")] public float AttackCooldown { get; init; } = 1.2f;
    [JsonPropertyName("drops")] public List<DropDefinition> Drops { get; init; } = [];
    [JsonPropertyName("spawnTiles")] public List<string> SpawnTiles { get; init; } = [];
    [JsonPropertyName("nightOnly")] public bool NightOnly { get; init; }
}

public sealed class EnemyTable
{
    [JsonPropertyName("spawnRadiusTiles")] public int SpawnRadiusTiles { get; init; } = 26;
    [JsonPropertyName("despawnRadiusTiles")] public int DespawnRadiusTiles { get; init; } = 70;
    [JsonPropertyName("maxAlive")] public int MaxAlive { get; init; } = 8;
    [JsonPropertyName("spawnIntervalSeconds")] public float SpawnIntervalSeconds { get; init; } = 6f;
    [JsonPropertyName("nightSpawnMultiplier")] public float NightSpawnMultiplier { get; init; } = 2.5f;
    [JsonPropertyName("bossRotationDays")] public int BossRotationDays { get; init; } = 3;
    [JsonPropertyName("enemies")] public List<HostileDefinition> Enemies { get; init; } = [];
    [JsonPropertyName("bosses")] public List<HostileDefinition> Bosses { get; init; } = [];

    public static EnemyTable Load(ContentManager content, string assetName, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<EnemyTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (table.Enemies.Count == 0 || table.Bosses.Count == 0)
        {
            throw new InvalidOperationException($"'{relativePath}': düşman/boss listesi boş.");
        }

        foreach (var hostile in table.Enemies.Concat(table.Bosses))
        {
            if (hostile.Health < 1 || hostile.Damage < 0)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{hostile.Id}' için health pozitif, damage negatif olmamalı.");
            }

            if (hostile.AttackRange >= hostile.AggroRange)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{hostile.Id}' saldırı menzili fark etme menzilinden " +
                    $"büyük — düşman oyuncuyu fark etmeden vurabilirdi.");
            }

            foreach (var drop in hostile.Drops)
            {
                if (!items.Contains(drop.Item))
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{hostile.Id}' tanımsız '{drop.Item}' düşürüyor.");
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

/// <summary>
/// AŞAMA 2 / MADDE 15 — düşman.
///
/// Davranış: oyuncu fark menziline girene kadar bekler, sonra kovalar,
/// saldırı menzilinde vurur. Bekleme süresi tanımdan gelir.
///
/// Hareket <see cref="TileCollider"/> üzerinden — oyuncuyla aynı çarpışma
/// kurallarından geçer, duvarın içinden geçemez.
///
/// ── Kovalama: görüş varsa düz, yoksa yol bulma ──────────────────────────
/// Açık arazide düşman oyuncuya doğrudan yürür. Arada engel varsa
/// <see cref="TilePathfinder"/> devreye girer ve düşman duvarı DOLAŞIR.
/// Önce görüş kontrolü yapılmasının sebebi maliyet: düşmanların çoğu
/// çoğu zaman oyuncuyu görüyor ve o durumda A* çalıştırmak boşuna.
/// </summary>
public sealed class Enemy
{
    private const float ColliderWidth = 14f;
    private const float ColliderHeight = 8f;

    /// <summary>
    /// Yol kaç saniyede bir yeniden hesaplanır.
    ///
    /// Her karede hesaplamak gereksiz: oyuncu 50 ms'de yarım tile ancak
    /// gidiyor. Çok seyrek hesaplamak ise düşmanın eski yolu takip edip
    /// oyuncunun arkasından geç kalmasına yol açar.
    /// </summary>
    private const float RepathIntervalSeconds = 0.5f;

    /// <summary>Bu mesafeye girilince ara nokta geçilmiş sayılır.</summary>
    private const float WaypointReachedDistance = 3f;

    /// <summary>Sıkışma kontrolünün periyodu.</summary>
    private const float StuckCheckSeconds = 0.4f;

    private readonly SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;
    private float _attackCooldown;
    private float _hurtSeconds;

    /// <summary>Takip edilen ara noktalar (tile koordinatı). Boşsa düz çizgi.</summary>
    private readonly List<Point> _path = [];

    private int _pathIndex;
    private float _repathTimer;

    // Sikisma tespiti: yol bulucu iyi bir yol verse bile carpisma
    // kutusu bir kosede takilabilir. Ilerlemeyen dusman yeniden
    // planlamaya zorlanir.
    private float _stuckTimer;
    private Vector2 _stuckAnchor;

    public HostileDefinition Definition { get; }
    public Vector2 Position { get; private set; }
    public Facing Facing { get; private set; } = Facing.Down;
    public int Health { get; private set; }
    public bool IsBoss { get; }
    public bool IsDead => Health <= 0;

    /// <summary>Ölüm animasyonunun bitmesi için beklenen süre.</summary>
    public float SecondsDead { get; private set; }

    public Aabb Collider => new(
        Position.X - ColliderWidth / 2f, Position.Y - ColliderHeight,
        ColliderWidth, ColliderHeight);

    /// <summary>
    /// Çarpışma kutusunun ORTASI. <see cref="Position"/> ayakların altında
    /// (çizim için) ve tek başına kullanılırsa bir alt tile'a düşebilir;
    /// yol bulma ve görüş kontrolü gövdenin merkezini ister.
    /// </summary>
    public Vector2 Center => new(Position.X, Position.Y - ColliderHeight / 2f);

    /// <summary>Takip edilen yolun uzunluğu — teşhis ve test için.</summary>
    public int PathLength => _path.Count;

    public Enemy(HostileDefinition definition, SpriteSheet sheet, Vector2 position, bool isBoss)
    {
        Definition = definition;
        _sheet = sheet;
        Position = position;
        Health = definition.Health;
        IsBoss = isBoss;

        _sheet.RequireStates("idle_down", "walk_down", "hurt", "death");
        _animator = new SpriteAnimator(sheet, "idle_down");

        _stuckAnchor = position;

        // Yeniden planlamalar dusmanlar arasinda YAYILIR: hepsi ayni
        // karede planlarsa o kare digerlerinden kat kat uzun surer ve
        // yarim saniyede bir gorunur bir takilma olusur. Baslangic
        // sayaci konumdan tureyen sabit bir kesirle kaydiriliyor.
        _repathTimer = MathF.Abs(position.X * 0.37f + position.Y * 0.11f)
                       % RepathIntervalSeconds;
    }

    /// <summary>
    /// Bir karelik yapay zekâ. Host çağırır.
    /// </summary>
    /// <param name="pathfinder">
    /// Düşmanlar arasında PAYLAŞILAN yol bulucu. Her düşmanın kendi
    /// örneğini tutması, arama tablolarını düşman sayısı kadar çoğaltırdı.
    /// </param>
    /// <returns>Oyuncuya verilen hasar; vuruş yoksa 0.</returns>
    public int Update(GameTime gameTime, TileMap map, Player target, TilePathfinder pathfinder)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (IsDead)
        {
            SecondsDead += delta;
            _animator.Play("death");
            _animator.Update(gameTime);
            return 0;
        }

        if (_attackCooldown > 0f) _attackCooldown -= delta;
        if (_hurtSeconds > 0f) _hurtSeconds -= delta;

        var toTarget = target.Position - Position;
        var distance = toTarget.Length();

        // Ölü oyuncuyu kovalamaz — cesedin başında beklemek anlamsız.
        if (target.IsDead || distance > Definition.AggroRange)
        {
            // Kovalama bitti: yol da unutulur. Yoksa oyuncu menzile geri
            // girdiginde dusman once ESKI yolu yurumeye calisirdi.
            _path.Clear();

            _animator.Play(_hurtSeconds > 0f ? "hurt" : $"idle_{Facing.ToString().ToLowerInvariant()}");
            _animator.Update(gameTime);
            return 0;
        }

        var damage = 0;
        var heading = toTarget;

        if (distance <= Definition.AttackRange)
        {
            _path.Clear();

            if (_attackCooldown <= 0f)
            {
                _attackCooldown = Definition.AttackCooldown;
                damage = Definition.Damage;
            }

            _animator.Play(_hurtSeconds > 0f ? "hurt" : "harvest");
        }
        else
        {
            var direction = ChooseDirection(map, target, pathfinder, delta);

            // Yon, gidilen yerden turer: yol bir duvari dolasirken dusman
            // oyuncuya degil, YURUDUGU yone bakmali.
            heading = direction;

            Position += TileCollider.Move(map, Collider, direction * Definition.MoveSpeed * delta);
            UpdateStuckDetection(delta);

            _animator.Play(_hurtSeconds > 0f
                ? "hurt"
                : $"walk_{Facing.ToString().ToLowerInvariant()}");
        }

        Facing = Math.Abs(heading.X) >= Math.Abs(heading.Y)
            ? heading.X < 0 ? Facing.Left : Facing.Right
            : heading.Y < 0 ? Facing.Up : Facing.Down;

        _animator.Update(gameTime);
        return damage;
    }

    /// <summary>
    /// Bu karede hangi yöne yürüneceği (birim vektör).
    ///
    /// Görüş açıksa doğrudan oyuncuya; değilse yolun sıradaki ara noktasına.
    /// </summary>
    private Vector2 ChooseDirection(TileMap map, Player target, TilePathfinder pathfinder,
                                    float delta)
    {
        _repathTimer -= delta;

        if (_repathTimer <= 0f)
        {
            _repathTimer = RepathIntervalSeconds;
            Replan(map, target, pathfinder);
        }

        // Yol yoksa duz cizgi. Bu, gorusun acik oldugu (cok yaygin) durum
        // ve yol bulmanin hic calismadigi ucuz yol.
        if (_pathIndex >= _path.Count) return Normalize(target.Position - Position);

        var tileSize = map.TileSize;
        var waypoint = TileCenter(_path[_pathIndex], tileSize);

        // Ara noktaya varildiysa sonrakine gec. While: yuksek hizda bir
        // karede birden fazla ara nokta gecilebilir.
        while (Vector2.Distance(Center, waypoint) <= WaypointReachedDistance)
        {
            if (++_pathIndex >= _path.Count)
            {
                _path.Clear();
                return Normalize(target.Position - Position);
            }

            waypoint = TileCenter(_path[_pathIndex], tileSize);
        }

        return Normalize(waypoint - Center);
    }

    /// <summary>Yolu yeniden hesaplar (ya da görüş açıksa yolu bırakır).</summary>
    private void Replan(TileMap map, Player target, TilePathfinder pathfinder)
    {
        var targetCenter = new Vector2(target.Position.X, target.Position.Y - ColliderHeight / 2f);

        // Gorus varsa yol bulmaya hic girilmez. Yaricap carpisma kutusunun
        // yarisi: govdesi sigmayan bir aralik "acik" sayilmamali.
        if (TilePathfinder.HasLineOfSight(map, Center, targetCenter, ColliderWidth / 2f))
        {
            _path.Clear();
            _pathIndex = 0;
            return;
        }

        var tileSize = map.TileSize;
        var start = ToTile(Center, tileSize);
        var goal = ToTile(targetCenter, tileSize);

        // Partial da KABUL EDILIR: hedefe ulasilamiyorsa bile en yakin
        // ulasilabilir kareye yurumek, oldugu yerde duvara yaslanmaktan
        // iyidir.
        var result = pathfinder.FindPath(map, start, goal, _path);

        _pathIndex = 0;
        if (result == PathResult.None) _path.Clear();
    }

    /// <summary>
    /// İlerleme olmadığında yeniden planlamayı öne çeker.
    ///
    /// Yol doğru olsa bile çarpışma kutusu dar bir geçitte takılabilir;
    /// o durumda bir sonraki planlamayı yarım saniye beklemek, düşmanın
    /// duvara yaslanıp titremesi demek olurdu.
    /// </summary>
    private void UpdateStuckDetection(float delta)
    {
        _stuckTimer += delta;
        if (_stuckTimer < StuckCheckSeconds) return;

        // 4 = 2 pixel^2: yarim saniyede 2 pixel'den az giden dusman
        // ilerlemiyor demektir.
        if (Vector2.DistanceSquared(Position, _stuckAnchor) < 4f)
        {
            _repathTimer = 0f;
            _path.Clear();
        }

        _stuckAnchor = Position;
        _stuckTimer = 0f;
    }

    private static Point ToTile(Vector2 world, int tileSize) => new(
        (int)MathF.Floor(world.X / tileSize),
        (int)MathF.Floor(world.Y / tileSize));

    private static Vector2 TileCenter(Point tile, int tileSize) => new(
        (tile.X + 0.5f) * tileSize,
        (tile.Y + 0.5f) * tileSize);

    /// <summary>Sıfır vektörde <c>NaN</c> üretmeyen normalizasyon.</summary>
    private static Vector2 Normalize(Vector2 value)
    {
        var length = value.Length();
        return length < 0.001f ? Vector2.Zero : value / length;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        Health = Math.Max(0, Health - amount);
        _hurtSeconds = 0.3f;

        if (IsDead)
        {
            SecondsDead = 0f;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var origin = new Vector2(_sheet.FrameWidth / 2f, _sheet.FrameHeight);

        spriteBatch.Draw(_sheet.Texture, Position, _animator.CurrentSourceRectangle,
            Color.White, rotation: 0f, origin: origin, scale: 1f,
            effects: SpriteEffects.None, layerDepth: 0f);
    }
}
