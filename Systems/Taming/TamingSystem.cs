using System.Text.Json;
using System.Text.Json.Serialization;
using PixelSurvival.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Networking;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Collision;
using PixelSurvival.World;

using PixelSurvival.Workshop;

namespace PixelSurvival.Systems.Taming;

public sealed class CreatureDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);
    [JsonPropertyName("sprite")] public string Sprite { get; init; } = "";

    /// <summary>Evcilleştirmek için verilecek item.</summary>
    [JsonPropertyName("tameItem")] public string TameItem { get; init; } = "feed";

    /// <summary>Kaç kez beslenirse evcilleşir.</summary>
    [JsonPropertyName("tameFeedings")] public int TameFeedings { get; init; } = 3;

    [JsonPropertyName("moveSpeed")] public float MoveSpeed { get; init; } = 68f;
    [JsonPropertyName("wanderRadius")] public float WanderRadius { get; init; } = 96f;
    [JsonPropertyName("followDistance")] public float FollowDistance { get; init; } = 34f;
    [JsonPropertyName("interactRange")] public float InteractRange { get; init; } = 26f;
    [JsonPropertyName("mountSpeedMultiplier")] public float MountSpeedMultiplier { get; init; } = 1.8f;
    [JsonPropertyName("maxAlive")] public int MaxAlive { get; init; } = 6;
    [JsonPropertyName("spawnRadiusTiles")] public int SpawnRadiusTiles { get; init; } = 40;
    [JsonPropertyName("spawnBiomeTiles")] public List<string> SpawnBiomeTiles { get; init; } = [];
}

public sealed class CreatureTable
{
    [JsonPropertyName("creatures")] public List<CreatureDefinition> Creatures { get; init; } = [];

    public static CreatureTable Load(ContentManager content, string assetName, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        // Mod bindirmesinden GECIYOR: bir mod bu tabloyu degistirebilir
        // ya da yeni satir ekleyebilir (bkz. ModdedContent).
        using var stream = ModdedContent.Open(content, assetName);

        var table = JsonSerializer.Deserialize<CreatureTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (table.Creatures.Count == 0)
        {
            throw new InvalidOperationException($"'{relativePath}': hiç yaratık tanımı yok.");
        }

        foreach (var creature in table.Creatures)
        {
            if (!items.Contains(creature.TameItem))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{creature.Id}' tanımsız '{creature.TameItem}' " +
                    $"item'ıyla evcilleştiriliyor.");
            }

            if (creature.TameFeedings < 1)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{creature.Id}' için tameFeedings en az 1 olmalı — " +
                    $"sıfır olsaydı yaratık dokunmadan evcilleşirdi.");
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
/// Bağlamsal yaratık etkileşiminin sonucu.
///
/// Değerler ağ üzerinden bayt olarak gidiyor (madde 10'daki yetki
/// notlarıyla birlikte tarım/evcilleştirme host'a taşındı), bu yüzden
/// SIRA PROTOKOL SABİTİ: yeni sonuçlar SONA eklenmeli.
/// </summary>
public enum TameOutcome : byte
{
    /// <summary>Menzilde yaratık yok.</summary>
    NoCreature = 0,

    /// <summary>Yem yok.</summary>
    NoFeed = 1,

    /// <summary>Beslendi ama henüz evcilleşmedi.</summary>
    Fed = 2,

    /// <summary>Bu beslemeyle evcilleşti.</summary>
    Tamed = 3,

    /// <summary>Sırtına binildi.</summary>
    Mounted = 4,

    /// <summary>Sırtından inildi.</summary>
    Dismounted = 5
}

/// <summary>Yaratığın davranış durumu.</summary>
public enum CreatureState
{
    /// <summary>Doğduğu noktanın çevresinde rastgele dolaşır.</summary>
    Wandering,

    /// <summary>Evcilleşti, sahibini takip ediyor.</summary>
    Following,

    /// <summary>Sahibi biniyor — çizilmez, oyuncu hızlı hareket eder.</summary>
    Ridden
}

/// <summary>
/// AŞAMA 2 / MADDE 14 — evcilleştirilebilir yaratık.
///
/// Çarpışma <see cref="TileCollider"/> üzerinden çözülür; oyuncuyla aynı
/// kurallardan geçer, böylece yaratık duvarın içine giremez.
/// </summary>
public sealed class Creature
{
    private const float ColliderWidth = 14f;
    private const float ColliderHeight = 8f;

    private readonly SpriteSheet _sheet;
    private readonly SpriteAnimator _animator;
    private readonly Vector2 _home;

    private Vector2 _wanderTarget;
    private float _retargetSeconds;
    private uint _randomState;

    public CreatureDefinition Definition { get; }
    public Vector2 Position { get; private set; }
    public Facing Facing { get; private set; } = Facing.Down;
    public CreatureState State { get; private set; } = CreatureState.Wandering;

    /// <summary>
    /// Ağ kimliği. Host atar, istemci bununla eşleştirir.
    ///
    /// Yaratıklar dolaşıyor, yani konum kimlik olamaz; liste indeksi de
    /// olamaz çünkü evcilleşen yaratık listede yer değiştirmese bile
    /// ileride silinme/eklenme indeksleri kaydırır.
    /// </summary>
    public ushort NetworkId { get; }

    /// <summary>Son karede gerçekten yer değiştirdi mi (ağ bayrağı).</summary>
    public bool IsMoving { get; private set; }

    /// <summary>Kaç kez beslendi. Eşiğe ulaşınca evcilleşir.</summary>
    public int Feedings { get; private set; }

    public bool IsTamed => State != CreatureState.Wandering;

    public Aabb Collider => new(
        Position.X - ColliderWidth / 2f, Position.Y - ColliderHeight,
        ColliderWidth, ColliderHeight);

    public Creature(CreatureDefinition definition, SpriteSheet sheet, Vector2 position, uint seed,
                    ushort networkId = 0)
    {
        Definition = definition;
        NetworkId = networkId;
        _sheet = sheet;
        _animator = new SpriteAnimator(sheet, "idle_down");

        Position = position;
        _home = position;
        _wanderTarget = position;
        _randomState = seed == 0 ? 0x9E3779B9u : seed;

        _sheet.RequireStates("idle_down", "idle_up", "idle_left", "idle_right",
                             "walk_down", "walk_up", "walk_left", "walk_right");
    }

    public void Update(GameTime gameTime, TileMap map, Player owner)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (State == CreatureState.Ridden)
        {
            // Binilirken yaratık çizilmez; konumu binicide tutulur ki
            // inildiğinde doğru yerde belirsin.
            Position = owner.Position;
            IsMoving = false;
            return;
        }

        var target = State == CreatureState.Following ? owner.Position : PickWanderTarget(delta);
        var toTarget = target - Position;
        var distance = toTarget.Length();

        // Takipte belli bir mesafeden sonra durur — sahibinin üstüne binmesin.
        var stopDistance = State == CreatureState.Following ? Definition.FollowDistance : 3f;

        if (distance <= stopDistance)
        {
            IsMoving = false;
            _animator.Play($"idle_{Facing.ToString().ToLowerInvariant()}");
            _animator.Update(gameTime);
            return;
        }

        var direction = toTarget / distance;
        var desired = direction * Definition.MoveSpeed * delta;

        var applied = TileCollider.Move(map, Collider, desired);
        Position += applied;

        // Duvara dayanmis bir yaratik "yuruyor" gorunmemeli.
        IsMoving = applied.LengthSquared() > 0.0001f;

        Facing = Math.Abs(direction.X) >= Math.Abs(direction.Y)
            ? direction.X < 0 ? Facing.Left : Facing.Right
            : direction.Y < 0 ? Facing.Up : Facing.Down;

        _animator.Play($"walk_{Facing.ToString().ToLowerInvariant()}");
        _animator.Update(gameTime);
    }

    private Vector2 PickWanderTarget(float delta)
    {
        _retargetSeconds -= delta;

        if (_retargetSeconds <= 0f)
        {
            // Yeni hedef, doğduğu noktanın çevresinde. Sürüsü haritanın
            // öbür ucuna göç etmesin diye evden uzaklaşmaz.
            var angle = NextUnit() * MathHelper.TwoPi;
            var radius = NextUnit() * Definition.WanderRadius;

            _wanderTarget = _home + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            _retargetSeconds = 2f + NextUnit() * 4f;
        }

        return _wanderTarget;
    }

    /// <summary>Bir kez besler. Eşiğe ulaşınca evcilleşir.</summary>
    /// <returns>Bu beslemeyle evcilleştiyse true.</returns>
    public bool Feed()
    {
        if (IsTamed)
        {
            return false;
        }

        Feedings++;

        if (Feedings < Definition.TameFeedings)
        {
            return false;
        }

        State = CreatureState.Following;
        return true;
    }

    public bool TryMount()
    {
        if (State != CreatureState.Following)
        {
            return false;
        }

        State = CreatureState.Ridden;
        return true;
    }

    public void Dismount(Vector2 position)
    {
        if (State != CreatureState.Ridden)
        {
            return;
        }

        State = CreatureState.Following;
        Position = position;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (State == CreatureState.Ridden)
        {
            return;
        }

        var origin = new Vector2(_sheet.FrameWidth / 2f, _sheet.FrameHeight);

        spriteBatch.Draw(_sheet.Texture, Position, _animator.CurrentSourceRectangle,
            Color.White, rotation: 0f, origin: origin, scale: 1f,
            effects: SpriteEffects.None, layerDepth: 0f);
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

/// <summary>
/// AŞAMA 2 / MADDE 14 — yaratık doğurma, evcilleştirme ve binek.
///
/// KAPSAM DIŞI: yaratık üretimi/yavru, açlık, envanter taşıyan binek,
/// yaratık savaşı, ağ senkronizasyonu. Yaratıklar şu an YEREL — çok
/// oyunculuda her istemci kendi sürüsünü görür. Senkronizasyon protokole
/// yeni bir mesaj türü eklemeyi gerektiriyor; madde 10'daki yetki
/// notlarıyla birlikte kapatılmalı.
/// </summary>
public sealed class TamingSystem(CreatureTable table, SpriteSheet creatureSheet, Tileset tileset)
{
    private readonly List<Creature> _creatures = [];
    private readonly HashSet<int> _spawnTiles = [];

    public IReadOnlyList<Creature> Creatures => _creatures;
    public Creature? Mount { get; private set; }

    /// <summary>
    /// Bu dunyada evcillestirilen yaratik sayisi.
    ///
    /// Neden sayac: cagiran taraf sonucu <see cref="Interact"/>'in dondugu
    /// METINDEN cikarsayamaz. O metin arayuz icin ve madde 23'te dile
    /// cevrilecek; ona bagli bir kontrol dil degisince sessizce bozulurdu.
    /// </summary>
    public int TamedCount { get; private set; }

    /// <summary>Binekteyken hareket hızı çarpanı; binek yoksa 1.</summary>
    public float SpeedMultiplier => Mount?.Definition.MountSpeedMultiplier ?? 1f;

    /// <summary>Oyuncunun çevresinde yaratık doğurur. Dünya kurulunca çağrılır.</summary>
    public void Populate(WorldGenerator generator, TileMap map, Vector2 around, int seed)
    {
        _creatures.Clear();
        Mount = null;

        var definition = table.Creatures[0];

        foreach (var key in definition.SpawnBiomeTiles)
        {
            _spawnTiles.Add(tileset.IndexOf(key));
        }

        var centerTile = new Point(
            (int)(around.X / map.TileSize), (int)(around.Y / map.TileSize));

        var placed = 0;
        for (var radius = 4; radius <= definition.SpawnRadiusTiles && placed < definition.MaxAlive; radius += 3)
        {
            for (var angle = 0; angle < 8 && placed < definition.MaxAlive; angle++)
            {
                var a = angle / 8f * MathHelper.TwoPi;
                var tx = centerTile.X + (int)(MathF.Cos(a) * radius);
                var ty = centerTile.Y + (int)(MathF.Sin(a) * radius);

                if (!_spawnTiles.Contains(map.GetTileIndex(tx, ty)) || map.IsSolidTile(tx, ty))
                {
                    continue;
                }

                var position = new Vector2(
                    tx * map.TileSize + map.TileSize / 2f, (ty + 1) * map.TileSize);

                // Kimlik uzayinin ust yarisi yaratiklarin (bkz.
                // EntityState.CreatureIdBase): dusman sistemiyle
                // haberlesmeden benzersizlik garanti ediliyor.
                _creatures.Add(new Creature(definition, creatureSheet, position,
                    (uint)(seed + placed * 7919),
                    (ushort)(EntityState.CreatureIdBase + placed)));
                placed++;
            }
        }
    }

    public void Update(GameTime gameTime, TileMap map, Player owner)
    {
        foreach (var creature in _creatures)
        {
            creature.Update(gameTime, map, owner);
        }
    }

    /// <summary>
    /// Yerel sürüyü boşaltır.
    ///
    /// İstemci olarak bağlanınca çağrılır: yaratıklar artık host otoriter
    /// ve istemci onları <see cref="Entities.RemoteEntity"/> olarak
    /// çiziyor. Yerel kopyalar silinmezse aynı yaratık İKİ KEZ görünür —
    /// biri host'un otoriter konumunda, biri istemcinin kendi başına
    /// dolaştırdığı hayalette.
    /// </summary>
    public void Clear()
    {
        _creatures.Clear();
        Mount = null;
    }

    /// <summary>
    /// Bir tanım indeksinin sprite sayfası — istemci uzak yaratığı bununla çizer.
    ///
    /// Şu an tek yaratık türü var ve hepsi aynı sayfayı paylaşıyor; imza
    /// yine de indeks alıyor ki ikinci tür eklendiğinde ağ tarafı
    /// değişmesin.
    /// </summary>
    public SpriteSheet? SheetFor(byte typeIndex) =>
        typeIndex < table.Creatures.Count ? creatureSheet : null;

    /// <summary>Bir yaratığın tanım indeksi — snapshot'a yazmak için.</summary>
    public byte TypeIndexOf(Creature creature) =>
        (byte)Math.Max(0, table.Creatures.FindIndex(c => c.Id == creature.Definition.Id));

    /// <summary>Menzildeki en yakın yaratık — etkileşim için.</summary>
    public Creature? Nearest(Vector2 position)
    {
        Creature? best = null;
        var bestDistance = float.MaxValue;

        foreach (var creature in _creatures)
        {
            if (creature.State == CreatureState.Ridden)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(position, creature.Position);
            if (distance < bestDistance && distance <= creature.Definition.InteractRange *
                creature.Definition.InteractRange)
            {
                best = creature;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Bağlamsal etkileşim: evcil değilse besler, evcilse biner, binekteyse iner.
    ///
    /// ── Neden metin değil, kod dönüyor ─────────────────────────────────
    /// Eskiden hazır Türkçe cümle dönüyordu. İki sorun: çağıran taraf
    /// sonucu ancak METNE bakarak anlayabiliyordu (madde 23'te diller
    /// gelince sessizce bozulacak bir bağ), ve ağ üzerinden istemciye
    /// gönderilemiyordu — host'un dili istemciye dayatılırdı. Kod dönünce
    /// her istemci kendi dil tablosundan okuyor.
    /// </summary>
    /// <param name="detail">
    /// Sonucu tamamlayan sayı: <see cref="TameOutcome.Fed"/> için kaç
    /// besleme kaldığı, diğerlerinde 0.
    /// </param>
    public TameOutcome Interact(Player player, WorldInventory inventory, out int detail)
    {
        detail = 0;

        if (Mount is not null)
        {
            Mount.Dismount(player.Position);
            Mount = null;
            return TameOutcome.Dismounted;
        }

        var creature = Nearest(player.Position);
        if (creature is null) return TameOutcome.NoCreature;

        if (creature.IsTamed)
        {
            creature.TryMount();
            Mount = creature;
            return TameOutcome.Mounted;
        }

        if (!inventory.TryRemove(creature.Definition.TameItem, 1)) return TameOutcome.NoFeed;

        if (creature.Feed())
        {
            TamedCount++;
            return TameOutcome.Tamed;
        }

        detail = creature.Definition.TameFeedings - creature.Feedings;
        return TameOutcome.Fed;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var creature in _creatures)
        {
            creature.Draw(spriteBatch);
        }
    }
}
