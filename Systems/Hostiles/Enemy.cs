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
/// </summary>
public sealed class Enemy
{
    private const float ColliderWidth = 14f;
    private const float ColliderHeight = 8f;

    private readonly SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;
    private float _attackCooldown;
    private float _hurtSeconds;

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

    public Enemy(HostileDefinition definition, SpriteSheet sheet, Vector2 position, bool isBoss)
    {
        Definition = definition;
        _sheet = sheet;
        Position = position;
        Health = definition.Health;
        IsBoss = isBoss;

        _sheet.RequireStates("idle_down", "walk_down", "hurt", "death");
        _animator = new SpriteAnimator(sheet, "idle_down");
    }

    /// <summary>
    /// Bir karelik yapay zekâ. Host çağırır.
    /// </summary>
    /// <returns>Oyuncuya verilen hasar; vuruş yoksa 0.</returns>
    public int Update(GameTime gameTime, TileMap map, Player target)
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
            _animator.Play(_hurtSeconds > 0f ? "hurt" : $"idle_{Facing.ToString().ToLowerInvariant()}");
            _animator.Update(gameTime);
            return 0;
        }

        Facing = Math.Abs(toTarget.X) >= Math.Abs(toTarget.Y)
            ? toTarget.X < 0 ? Facing.Left : Facing.Right
            : toTarget.Y < 0 ? Facing.Up : Facing.Down;

        var damage = 0;

        if (distance <= Definition.AttackRange)
        {
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = Definition.AttackCooldown;
                damage = Definition.Damage;
            }

            _animator.Play(_hurtSeconds > 0f ? "hurt" : "harvest");
        }
        else
        {
            var direction = toTarget / distance;
            Position += TileCollider.Move(map, Collider, direction * Definition.MoveSpeed * delta);

            _animator.Play(_hurtSeconds > 0f
                ? "hurt"
                : $"walk_{Facing.ToString().ToLowerInvariant()}");
        }

        _animator.Update(gameTime);
        return damage;
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
