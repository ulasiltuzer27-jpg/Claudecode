using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PixelSurvival.Entities;
using PixelSurvival.Systems;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Collision;
using PixelSurvival.Networking;
using PixelSurvival.Systems.Building;
using PixelSurvival.Systems.Climate;
using PixelSurvival.Systems.Combat;
using PixelSurvival.Systems.Dungeons;
using PixelSurvival.Systems.Farming;
using PixelSurvival.Systems.Fishing;
using PixelSurvival.Systems.Hostiles;
using PixelSurvival.Systems.Npcs;
using PixelSurvival.Systems.Zones;
using PixelSurvival.Inventory.Steam;
using PixelSurvival.Systems.Taming;
using PixelSurvival.Systems.Crafting;
using PixelSurvival.Systems.Gathering;
using PixelSurvival.Systems.Input;
using PixelSurvival.Inventory;
using PixelSurvival.UI;
using PixelSurvival.World;

namespace PixelSurvival;

/// <summary>
/// AŞAMA 2 / MADDE 12–19.
///
/// KAPSAM DIŞI: Steamworks tam entegrasyonu (20-21), clan/trade (22),
/// localization (23), photo mode (24), Workshop (25), kaydetme/yükleme.
/// SteamInventory madde 19 — WorldInventory ile ASLA karıştırılmaz.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const float CameraZoom = 3f;

    /// <summary>
    /// Varsayılan dünya tohumu. Aynı tohum her makinede aynı dünyayı üretir.
    /// F5 ile rastgele bir tohuma geçilebilir (üretimi gözle denemek için).
    /// </summary>
    private const int DefaultSeed = 20260908;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    private SpriteSheet _playerSheet = null!;
    private Tileset _tileset = null!;
    private BiomeTable _biomeTable = null!;

    private ResourceTable _resourceTable = null!;
    private GatheringSystem _gathering = null!;

    private TileMap _map = null!;
    private Player _player = null!;
    private Camera2D _camera = null!;

    private ItemDatabase _itemDatabase = null!;
    private WorldInventory _inventory = null!;
    private CraftingSystem _crafting = null!;

    private BitmapFont _font = null!;
    private HudRenderer _hud = null!;
    private Texture2D _itemIcons = null!;

    private BuildableTable _buildableTable = null!;
    private BuildingSystem _building = null!;
    private CombatSystem _combat = null!;
    private NetworkSession _session = null!;

    /// <summary>Yeniden doğma noktası. Host tüm oyuncuları buraya diriltir.</summary>
    private Vector2 _spawnPosition;

    // --- Aşama 2 ---
    private ClimateTable _climateTable = null!;
    private ClimateSystem _climate = null!;
    private CropTable _cropTable = null!;
    private FarmingSystem _farming = null!;
    private FishingTable _fishingTable = null!;
    private FishingSystem _fishing = null!;
    private CreatureTable _creatureTable = null!;
    private TamingSystem _taming = null!;
    private Texture2D _cropSheet = null!;
    private SpriteSheet _creatureSheet = null!;

    /// <summary>Ekilecek tohum. X tuşuyla değişir.</summary>
    private int _selectedSeedIndex;

    // --- Madde 15-17 ---
    private EnemyTable _enemyTable = null!;
    private EnemySystem _enemies = null!;
    private DungeonTable _dungeonTable = null!;
    private DungeonSystem _dungeons = null!;
    private NpcTable _npcTable = null!;
    private NpcSystem _npcs = null!;

    /// <summary>Üst dünya haritası; zindandayken de tutulur.</summary>
    private WorldGenerator _worldGenerator = null!;

    // --- Madde 18-19 ---
    private ZoneTable _zoneTable = null!;
    private ZoneSystem _zones = null!;
    /// <summary>
    /// Steam tarafının TEK giriş noktası. Game1 bilerek SteamInventory
    /// tipini hiç görmez — iki envanterin aynı dosyada buluşması, aralarında
    /// köprü kurmayı bir satırlık iş haline getirirdi.
    /// </summary>
    private SteamSession _steam = null!;

    private bool _showCrafting = true;

    /// <summary>Kısa ömürlü bilgi mesajı (üretim başarısız, envanter dolu vb.).</summary>
    private string _toast = "";
    private float _toastSeconds;

    private Texture2D _pixel = null!;

    private bool _showAssetView;
    private bool _showCollisionDebug;
    private KeyboardState _previousKeyboard;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = WindowWidth,
            PreferredBackBufferHeight = WindowHeight
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.ApplyChanges();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        _playerSheet = SpriteSheet.Load(Content, "Characters/char_free_male");
        _tileset = Tileset.Load(Content, "Tiles/tileset_16");
        _biomeTable = BiomeTable.Load(Content, "World/biomes");
        _resourceTable = ResourceTable.Load(Content, "World/resources", _tileset);
        _gathering = new GatheringSystem(_resourceTable);

        _itemDatabase = ItemDatabase.Load(Content, "Items/items");
        _inventory = new WorldInventory(_itemDatabase);
        _crafting = new CraftingSystem(
            RecipeBook.Load(Content, "Items/recipes", _itemDatabase), _itemDatabase);

        _buildableTable = BuildableTable.Load(Content, "World/buildables", _tileset, _itemDatabase);
        _building = new BuildingSystem(_buildableTable);
        _combat = new CombatSystem();

        _session = new NetworkSession(_playerSheet, _itemDatabase, _resourceTable);
        _session.Notice += message => ShowToast(message);
        _session.SeedReceived += seed => GenerateWorld(seed);
        _session.ItemGranted += (itemId, amount) => _inventory.TryAdd(itemId, amount);

        // --- Aşama 2 sistemleri ---
        _climateTable = ClimateTable.Load(Content, "World/climate");
        _cropTable = CropTable.Load(Content, "World/crops", _tileset, _itemDatabase);
        _fishingTable = FishingTable.Load(Content, "World/fishing", _itemDatabase);
        _creatureTable = CreatureTable.Load(Content, "Entities/creatures", _itemDatabase);

        _cropSheet = Content.Load<Texture2D>("Items/crops_16");
        _creatureSheet = SpriteSheet.Load(Content, _creatureTable.Creatures[0].Sprite);

        _fishing = new FishingSystem(_fishingTable);
        _taming = new TamingSystem(_creatureTable, _creatureSheet, _tileset);

        _enemyTable = EnemyTable.Load(Content, "Entities/enemies", _itemDatabase);
        _dungeonTable = DungeonTable.Load(Content, "World/dungeons", _tileset, _itemDatabase);
        _npcTable = NpcTable.Load(Content, "Entities/npcs", _itemDatabase);

        _dungeons = new DungeonSystem(_dungeonTable, _tileset);
        _npcs = new NpcSystem(_npcTable, Content);

        _zoneTable = ZoneTable.Load(Content, "World/zones", _tileset);
        _zones = new ZoneSystem(_zoneTable, _tileset);

        // Madde 19: Steam katmanı. WorldInventory'den TAMAMEN ayrı kurulur;
        // aralarında hiçbir referans yok ve olmamalı.
        _steam = new SteamSession(
            SteamItemCatalog.Load(Content, "Steam/itemdefs"),
            new UnconfiguredGrantAuthority());

        _steam.Refresh();

        _session.WorldTimeReceived += (seconds, weather) =>
            _climate.ApplyNetworkState(seconds, weather);

        _font = BitmapFont.Load(Content, "UI/font_ascii");
        _itemIcons = Content.Load<Texture2D>("Items/icons_16");
        _hud = new HudRenderer(_font, _pixel, _itemIcons, _itemDatabase,
                               Content, "Items/icons_16");

        _camera = new Camera2D(WindowWidth, WindowHeight, CameraZoom);

        GenerateWorld(DefaultSeed);
    }

    /// <summary>
    /// Verilen tohumla dünyayı sıfırdan kurar ve oyuncuyu güvenli bir noktaya koyar.
    /// F5 aynı yolu rastgele tohumla tekrar çağırır.
    /// </summary>
    private void GenerateWorld(int seed)
    {
        var generator = new WorldGenerator(_biomeTable, _tileset, seed);
        _worldGenerator = generator;
        _map = new TileMap(generator, _tileset);
        _dungeons?.Reset();

        // Karakter denizin ortasına veya kaya cebine doğmasın diye
        // bağlı açık alanı yeterince büyük bir tile aranır.
        var spawnTile = generator.FindSpawnTile();

        var start = new Vector2(
            spawnTile.X * _map.TileSize + _map.TileSize / 2f,
            (spawnTile.Y + 1) * _map.TileSize);

        _spawnPosition = start;
        _player = new Player(_playerSheet, start);
        _camera.SnapTo(_player.Position);

        // Aşama 2: iklim, tarım ve yaratıklar dünyayla birlikte kurulur.
        // İkisi de tohumdan türer, böylece aynı tohum aynı havayı verir.
        _climate = new ClimateSystem(_climateTable, seed);
        _farming = new FarmingSystem(_cropTable, 3);
        _fishing?.Cancel();
        _taming?.Populate(generator, _map, start, seed);

        _enemies = new EnemySystem(_enemyTable, Content, _tileset, seed);
        _npcs?.Populate(start, _map.TileSize);

        if (_zones is not null)
        {
            _zones.Reset();
            _zones.SafeCenter = start;
            _zones.SafePoints = _npcs.Npcs.Select(n => n.Position).ToArray();
        }

        // Sağlama, üretimin beklenen algoritmayla eşleştiğini dışarıdan
        // doğrulamak için: Tools/verify_worldgen.py aynı değeri bağımsız hesaplar.
        var checksum = generator.ComputeChunkChecksum(0, 0, TileMap.ChunkSize);

        var report = $"[worldgen] tohum={seed} " +
                     $"spawn=({spawnTile.X},{spawnTile.Y}) " +
                     $"chunk(0,0) sağlama=0x{checksum:X8}";

        Console.WriteLine(report);
        Debug.WriteLine(report);

        Window.Title = $"PixelSurvival — Madde 5: prosedurel dunya (tohum {seed})";
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.Escape) ||
            GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        if (WasPressed(keyboard, Keys.F1)) _showAssetView = !_showAssetView;
        if (WasPressed(keyboard, Keys.F2)) _showCollisionDebug = !_showCollisionDebug;
        if (WasPressed(keyboard, Keys.F5) && _session.Mode == SessionMode.Offline)
        {
            GenerateWorld(Random.Shared.Next());
        }

        if (WasPressed(keyboard, Keys.Tab)) _showCrafting = !_showCrafting;

        // Madde 9: yapı seçimi
        if (WasPressed(keyboard, Keys.Q)) _building.SelectPrevious();
        if (WasPressed(keyboard, Keys.Z)) _building.SelectNext();

        // Madde 10: oturum tuşları
        // Aşama 2 tuşları
        if (WasPressed(keyboard, Keys.X)) CycleSeed();
        if (WasPressed(keyboard, Keys.C)) DoFarmAction();
        if (WasPressed(keyboard, Keys.G)) ShowToast(_taming.Interact(_player, _inventory));
        if (WasPressed(keyboard, Keys.T)) InteractWithNpc();
        if (WasPressed(keyboard, Keys.B)) ToggleDungeon();

        if (WasPressed(keyboard, Keys.F9)) _session.StartHost(_map.Seed);
        if (WasPressed(keyboard, Keys.F10)) _session.Connect(TransportFactory.DefaultConnectTarget);
        if (WasPressed(keyboard, Keys.F11)) { _session.Leave(); ShowToast("Oturum kapatildi"); }

        HandleCraftingKeys(keyboard);

        if (_toastSeconds > 0f)
        {
            _toastSeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        _previousKeyboard = keyboard;

        var input = InputReader.Read();
        var buildPressed = input.Build && !_previousBuildHeld;
        _previousBuildHeld = input.Build;

        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _combat.Update(delta);
        _building.UpdateAim(_player, _map);

        // Madde 12: saat yalnızca otoriter tarafta ilerler. İstemcide host'tan
        // gelen WorldTime mesajı saati ayarlıyor; yerel olarak da ilerletsek
        // iki taraf kayardı.
        if (_session.Mode != SessionMode.Client)
        {
            _climate.Update(delta);
            _farming.Update(_climate);
            _farming.DropOrphans(_map);
        }

        // Madde 14: binek hız çarpanı
        _player.SpeedMultiplier = _taming.SpeedMultiplier;
        _taming.Update(gameTime, _map, _player);
        _npcs.Update(gameTime);
        _zones.Update(delta);

        // Madde 15: düşmanlar. İstemcide çalıştırılmaz — düşman hasarı
        // otoriter tarafta çözülmeli, yoksa iki tarafın canı ayrışır.
        if (_session.Mode != SessionMode.Client)
        {
            _enemies.UpdateRotation(_climate);

            // Zindanda düşman doğurma kapalı: orada yalnızca boss var.
            var drops = _enemies.Update(gameTime, _map, _player, _climate, _inventory,
                                        allowSpawning: !_dungeons.IsInside);

            foreach (var drop in drops)
            {
                ShowToast($"{drop.EnemyName}: +{drop.Amount} {_itemDatabase.Get(drop.Item).Name}");
            }

            // Madde 16: boss düşünce çıkış açılır ve ödül verilir.
            if (_dungeons.IsInside && _enemies.BossDefeated && !_dungeons.ExitOpen)
            {
                _dungeons.OnBossDefeated(_map, _inventory);
                ShowToast("Boss yenildi! Cikis acildi.");
            }
        }

        var action = PlayerAction.None;

        // İSTEMCİ modunda dünya değişikliği ve hasar host'ta çözülür.
        // Yerel taraf yalnızca hareketi TAHMİN eder; toplama/inşa/saldırı
        // burada çalıştırılsaydı iki taraf ayrışırdı.
        if (_session.Mode == SessionMode.Client)
        {
            _gathering.UpdateAimOnly(_player, _map);
        }
        else
        {
            action = UpdateLocalActions(input, buildPressed, gameTime);
        }

        _player.Update(input, _map, gameTime, action);

        // Madde 13: balıkçılık — V basılı tutulur, doğru anda bırakılır.
        var fishing = _fishing.Update(delta, keyboard.IsKeyDown(Keys.V),
            FishingSystem.IsFacingWater(_player, _map, _tileset), _climate, _inventory);

        if (fishing is { } catchResult)
        {
            ShowToast($"+{catchResult.Amount} {_itemDatabase.Get(catchResult.Item).Name}!");
        }
        else if (_fishing.State == FishingState.Failed && _fishing.Marker > 0f)
        {
            // Sadece bir kez göster; Failed durumu cooldown boyunca sürüyor.
        }

        _session.Update(gameTime, _player, input, _map, _combat, _spawnPosition, _climate);

        // Offline'da yeniden doğmayı da yerel taraf yürütür.
        if (_session.Mode == SessionMode.Offline && _player.IsDead &&
            _player.SecondsDead >= CombatSystem.RespawnSeconds)
        {
            _player.Respawn(_spawnPosition);
            ShowToast("Yeniden dogdun");
        }

        // Üst dünyada sınır yok (Bounds null), zindanda harita sınırına clamp
        // edilir — madde 5'te Camera2D'nin sınırını Rectangle? yapmamızın sebebi.
        _camera.Follow(_player.Position, gameTime, _map.Bounds);

        // Chunk akışı, kamera hareket ettikten SONRA güncellenir; aksi halde
        // hızlı hareket ederken kenarda bir kare gecikmeli boşluk görünür.
        _map.UpdateLoadedChunks(_camera.GetVisibleWorldArea(), _player.Position);

        base.Update(gameTime);
    }

    /// <summary>
    /// Offline ve Host modunda yerel oyuncunun toplama / inşa / saldırı
    /// eylemlerini çözer. İstemci modunda ÇAĞRILMAZ — orada otorite host'ta.
    /// </summary>
    private PlayerAction UpdateLocalActions(PlayerInput input, bool buildPressed,
                                            GameTime gameTime)
    {
        var action = PlayerAction.None;

        // --- Madde 6/7: toplama ---
        var gathered = _gathering.Update(input, _player, _map, gameTime);
        if (gathered is { } result)
        {
            var leftover = _inventory.TryAdd(result.Resource, result.Amount);

            if (leftover > 0)
            {
                ShowToast($"Envanter dolu! {leftover} {_itemDatabase.Get(result.Resource).Name} kayboldu");
            }

            _session.NotifyTileChanged(result.Tile.X, result.Tile.Y,
                _map.GetTileIndex(result.Tile.X, result.Tile.Y));

            Console.WriteLine(
                $"[toplama] +{result.Amount - leftover} {result.Resource} " +
                $"@({result.Tile.X},{result.Tile.Y}) " +
                $"envanter: {_inventory.CountOf(result.Resource)}");
        }

        if (_gathering.IsGathering)
        {
            action = PlayerAction.Gathering;
        }

        // --- Madde 9: inşa ---
        if (buildPressed)
        {
            var outcome = _building.TryPlace(_player, _map, _inventory);

            switch (outcome)
            {
                case BuildOutcome.Success:
                    var tile = _building.AimedTile;
                    _session.NotifyTileChanged(tile.X, tile.Y, _map.GetTileIndex(tile.X, tile.Y));
                    ShowToast($"{_itemDatabase.Get(_building.Selected.Item).Name} yerlestirildi");
                    Console.WriteLine($"[insa] {_building.Selected.Tile} @({tile.X},{tile.Y})");
                    break;

                case BuildOutcome.NotBuildableGround:
                    ShowToast("Buraya insa edilemez");
                    break;

                case BuildOutcome.MissingMaterial:
                    ShowToast("Malzeme yok");
                    break;

                case BuildOutcome.BlockedByPlayer:
                    ShowToast("Uzerinde duruyorsun");
                    break;
            }
        }

        // --- Madde 11: saldırı ---
        if (input.Attack && !_player.IsDead)
        {
            var wasSwinging = _combat.IsSwinging(_session.LocalPlayerId);

            // Offline'da vurulacak oyuncu yok; host'ta bağlı oyuncular hedef.
            var everyone = new Dictionary<byte, Player> { [_session.LocalPlayerId] = _player };
            var hits = _combat.TryAttack(_session.LocalPlayerId, _player, everyone);

            // Aynı vuruş düşmanlara da işler. CombatSystem yalnızca oyuncular
            // arasını çözüyor; düşman hasarı EnemySystem'de, aynı menzille.
            if (!wasSwinging && _combat.IsSwinging(_session.LocalPlayerId))
            {
                var origin = _player.Position + FacingOffset(_player.Facing) * 8f;
                _enemies.ApplyPlayerAttack(origin, 22f, 20);

                // Madde 18: aynı vuruş, PvP bölgesindeki yapılara baskındır.
                TryRaidAimedTile();
            }

            if (hits.Count > 0 || _combat.IsSwinging(_session.LocalPlayerId))
            {
                action = PlayerAction.Attacking;
            }
        }

        if (_combat.IsSwinging(_session.LocalPlayerId))
        {
            action = PlayerAction.Attacking;
        }

        return action;
    }

    private bool _previousBuildHeld;

    private bool WasPressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    /// <summary>
    /// 1-9 tuşları tarif listesindeki sıraya karşılık gelir.
    /// Kenar tespiti kullanılıyor: tuşu basılı tutmak saniyede 60 üretim
    /// yapmamalı, tek basış tek üretim.
    /// </summary>
    private void HandleCraftingKeys(KeyboardState keyboard)
    {
        var recipes = _crafting.Recipes;
        var limit = Math.Min(recipes.Count, 9);

        for (var i = 0; i < limit; i++)
        {
            if (!WasPressed(keyboard, Keys.D1 + i))
            {
                continue;
            }

            var recipe = recipes[i];
            var outcome = _crafting.TryCraft(recipe, _inventory);

            switch (outcome)
            {
                case CraftOutcome.Success:
                    var name = _itemDatabase.Get(recipe.Output.Item).Name;
                    ShowToast($"+{recipe.Output.Amount} {name}");
                    Console.WriteLine($"[uretim] {recipe.Id} -> {recipe.Output.Amount} {recipe.Output.Item}");
                    break;

                case CraftOutcome.MissingIngredients:
                    ShowToast("Malzeme yetersiz");
                    break;

                case CraftOutcome.NoRoomForOutput:
                    // Envanter dolu; girdiler GERI ALINDI, kayıp yok.
                    ShowToast("Envanterde yer yok");
                    break;
            }
        }
    }

    /// <summary>
    /// Madde 18: bakılan tile bir yapıysa ve PvP bölgesindeyse baskın vuruşu.
    ///
    /// Ayrı bir tuş YOK: saldırı tuşu hem oyuncuya, hem düşmana, hem yapıya
    /// işler. Oyuncunun "şimdi hangi moddayım" diye düşünmesi gerekmiyor.
    /// </summary>
    private void TryRaidAimedTile()
    {
        var tile = BuildingSystem.AimTile(_player, _map, 1);
        var tileIndex = _map.GetTileIndex(tile.X, tile.Y);

        if (!_zones.IsRaidable(tileIndex))
        {
            return;
        }

        // Kırılan yapının malzemesi: buildables tablosundan geri çözülür.
        var material = _buildableTable.Buildables
            .FirstOrDefault(b => _buildableTable.TileIndexFor(b) == tileIndex)?.Item;

        var outcome = _zones.Raid(_map, tile, _inventory, material);

        switch (outcome)
        {
            case RaidOutcome.Destroyed:
                _session.NotifyTileChanged(tile.X, tile.Y, _map.GetTileIndex(tile.X, tile.Y));
                ShowToast("Yapi yikildi");
                break;

            case RaidOutcome.Damaged:
                ShowToast($"Baskin: %{(int)(_zones.HealthFraction(tile) * 100)}");
                break;

            case RaidOutcome.InSafeZone:
                ShowToast("Guvenli bolgede yapi kirilamaz");
                break;
        }
    }

    private static Vector2 FacingOffset(Facing facing) => facing switch
    {
        Facing.Left => new Vector2(-1f, 0f),
        Facing.Right => new Vector2(1f, 0f),
        Facing.Up => new Vector2(0f, -1f),
        _ => new Vector2(0f, 1f)
    };

    /// <summary>
    /// T tuşu: yakındaki NPC ile etkileşim (madde 17).
    ///
    /// Tüccarda: envanterdeki ilk satılabilir item'ı satar. Tam bir ticaret
    /// arayüzü Aşama 2'nin sonunda (madde 24 civarı) gelecek; şimdilik
    /// zincirin çalıştığını göstermeye yeten asgari etkileşim.
    /// </summary>
    private void InteractWithNpc()
    {
        var npc = _npcs.Nearest(_player.Position);

        if (npc is null)
        {
            ShowToast("Yakinda NPC yok");
            return;
        }

        if (npc.Definition.Role == "quest")
        {
            ShowToast(_npcs.TurnInQuest(_inventory));
            return;
        }

        // Satılabilecek ilk item'ı bul.
        foreach (var offer in npc.Definition.Buys)
        {
            if (_inventory.Has(offer.Item, 1))
            {
                ShowToast($"{_itemDatabase.Get(offer.Item).Name} sattin: " +
                          _npcs.Sell(npc.Definition, offer.Item, _inventory));
                return;
            }
        }

        ShowToast($"{npc.Definition.Name}: {npc.Definition.Greeting}");
    }

    /// <summary>
    /// B tuşu: zindan girişine/çıkışına bas (madde 16).
    ///
    /// Giriş ve çıkış aynı tuş: oyuncu ayağının altındaki tile'a göre
    /// hangisinin olacağını sistem belirliyor.
    /// </summary>
    private void ToggleDungeon()
    {
        var tile = new Point(
            (int)MathF.Floor(_player.Position.X / _map.TileSize),
            (int)MathF.Floor((_player.Position.Y - 4) / _map.TileSize));

        if (_dungeons.IsInside)
        {
            if (!_dungeons.ExitOpen)
            {
                ShowToast("Once bossu yen");
                return;
            }

            if (!_dungeons.IsExit(_map, tile))
            {
                ShowToast("Cikis burada degil");
                return;
            }

            var (overworld, position) = _dungeons.Leave();
            _map = overworld;
            _player.Position = position;
            _camera.SnapTo(position, _map.Bounds);
            _enemies.Clear();
            ShowToast("Zindandan ciktin");
            return;
        }

        if (!_dungeons.IsEntrance(_map, tile))
        {
            ShowToast("Burada zindan girisi yok");
            return;
        }

        var (dungeon, spawn) = _dungeons.Enter(_map, _player, tile);
        _map = dungeon;
        _player.Position = spawn;
        _camera.SnapTo(spawn, _map.Bounds);

        _enemies.Clear();
        if (_dungeons.Generator is { } generator)
        {
            _enemies.SpawnBoss(generator.WorldPositionOf(generator.BossTile));
        }

        ShowToast($"Zindana girdin — {_enemies.ActiveBoss.Name} bekliyor");
    }

    /// <summary>Ekilebilir tohumlar arasında geçiş yapar (X tuşu).</summary>
    private void CycleSeed()
    {
        var seeds = _cropTable.Crops.Select(c => c.Seed).ToList();
        _selectedSeedIndex = (_selectedSeedIndex + 1) % seeds.Count;
        ShowToast($"Tohum: {_itemDatabase.Get(seeds[_selectedSeedIndex]).Name}");
    }

    private string SelectedSeed => _cropTable.Crops[_selectedSeedIndex].Seed;

    /// <summary>
    /// C tuşu: bağlamsal tarım. Boş zemini sürer, sürülmüş toprağa eker,
    /// olgun ekini hasat eder — hangi modda olduğunu takip etmen gerekmiyor.
    /// </summary>
    private void DoFarmAction()
    {
        if (_session.Mode == SessionMode.Client)
        {
            ShowToast("Tarim henuz istemcide calismiyor");
            return;
        }

        var outcome = _farming.Interact(_player, _map, _inventory, _climate, SelectedSeed);

        ShowToast(outcome switch
        {
            FarmOutcome.Tilled => "Toprak surdun",
            FarmOutcome.Planted => $"{_itemDatabase.Get(SelectedSeed).Name} ektin",
            FarmOutcome.Harvested => "Hasat ettin!",
            FarmOutcome.NotRipe => "Henuz olgunlasmadi",
            FarmOutcome.NoSeed => "Tohum yok",
            FarmOutcome.InventoryFull => "Envanter dolu",
            _ => "Burasi surulemez"
        });

        if (outcome == FarmOutcome.Tilled)
        {
            var tile = BuildingSystem.AimTile(_player, _map, 1);
            _session.NotifyTileChanged(tile.X, tile.Y, _map.GetTileIndex(tile.X, tile.Y));
        }
    }

    /// <summary>Sol üstteki oturum durumu satırı.</summary>
    private string DescribeSession() => _session.Mode switch
    {
        SessionMode.Host => $"SUNUCU  {_session.ConnectedCount} bagli  (F11 kapat)",
        SessionMode.Client => $"ISTEMCI  oyuncu {_session.LocalPlayerId}  (F11 ayril)",
        _ => "TEK KISILIK  (F9 sunucu ac, F10 baglan)"
    };

    private void ShowToast(string message)
    {
        _toast = message;
        _toastSeconds = 2.5f;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(28, 30, 40));

        var visible = _camera.GetVisibleWorldArea();

        // --- Dünya katmanı: kamera matrisi altında, dünya koordinatlarında ---
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: _camera.GetViewMatrix());

        _map.Draw(_spriteBatch, visible);
        _farming.Draw(_spriteBatch, _cropSheet, _map.TileSize, visible);
        DrawGatherTarget();
        DrawBuildGhost();
        _taming.Draw(_spriteBatch);
        _npcs.Draw(_spriteBatch);
        _enemies.Draw(_spriteBatch);

        foreach (var enemy in _enemies.Enemies)
        {
            if (!enemy.IsDead)
            {
                DrawHealthBar(enemy.Position, enemy.Health * 100 / enemy.Definition.Health);
            }
        }

        // Uzak oyuncular önce çizilir: yerel oyuncu üstte kalsın, kendi
        // karakterini kaybetme.
        foreach (var remote in _session.RemotePlayers)
        {
            remote.Draw(_spriteBatch);
            DrawHealthBar(remote.Position, remote.Health);
        }

        _player.Draw(_spriteBatch);

        // Kendi can çubuğu yalnızca hasar aldıysa gösterilir — dolu canda
        // ekranı kirletmesin.
        if (_player.Health < Player.MaxHealth)
        {
            DrawHealthBar(_player.Position, _player.Health);
        }

        if (_showCollisionDebug)
        {
            DrawCollisionDebug(visible);
        }

        _spriteBatch.End();

        // --- İklim bindirmesi: dünyayı karartır, arayüze DOKUNMAZ ---
        // Bu yüzden dünya katmanından sonra, arayüzden önce çiziliyor.
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var overlay = _climate.OverlayColor;
        if (overlay.A > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), overlay);
        }

        DrawWeatherParticles(gameTime);
        _spriteBatch.End();

        // --- Arayüz katmanı: kamera matrisi YOK, ekran koordinatlarında ---
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        if (_showAssetView)
        {
            DrawAssetInspector();
        }
        else
        {
            _hud.DrawInventory(_spriteBatch, _inventory, WindowWidth, WindowHeight);

            _hud.DrawStatus(_spriteBatch,
                selectedBuild: _itemDatabase.Get(_building.Selected.Item).Name,
                sessionLine: DescribeSession());

            if (_showCrafting)
            {
                _hud.DrawCrafting(_spriteBatch, _crafting, _inventory, WindowWidth);
            }

            _hud.DrawZone(_spriteBatch,
                _zones.ZoneAt(_player.Position, _map.TileSize) == ZoneKind.Safe,
                _steam.Status, WindowWidth, WindowHeight);

            _hud.DrawClimate(_spriteBatch,
                $"Gun {_climate.Day}  {_climate.ClockText}  {_climate.Season.Name}  " +
                $"{_climate.Weather.Name}", WindowWidth);

            if (_fishing.IsActive)
            {
                _hud.DrawFishingBar(_spriteBatch, _fishing.Marker,
                    _fishing.ZoneStart, _fishing.ZoneSize,
                    _fishing.State == FishingState.Casting, WindowWidth, WindowHeight);
            }

            if (_toastSeconds > 0f)
            {
                _hud.DrawToast(_spriteBatch, _toast, WindowWidth);
            }
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>
    /// F2 görünümü: katı tile'lar kırmızı, oyuncunun collider'ı yeşil,
    /// yüklü chunk sınırları sarı. Chunk akışının gerçekten çalıştığını
    /// (kenarda yeni chunk belirip arkada kaybolduğunu) gözle görmek için.
    /// </summary>
    private void DrawCollisionDebug(Rectangle visible)
    {
        var solidTint = new Color(255, 60, 60) * 0.35f;
        var tileSize = _map.TileSize;

        var minX = (int)Math.Floor((double)visible.Left / tileSize);
        var minY = (int)Math.Floor((double)visible.Top / tileSize);
        var maxX = (int)Math.Floor((double)visible.Right / tileSize);
        var maxY = (int)Math.Floor((double)visible.Bottom / tileSize);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (_map.IsSolidTile(x, y))
                {
                    _spriteBatch.Draw(_pixel,
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        solidTint);
                }
            }
        }

        foreach (var bounds in _map.LoadedChunkBounds)
        {
            DrawRectangleOutline(bounds, new Color(255, 220, 80) * 0.8f);
        }

        DrawBoxOutline(_player.Collider, new Color(80, 255, 120));
    }

    private void DrawRectangleOutline(Rectangle rectangle, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - 1, rectangle.Width, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, 1, rectangle.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - 1, rectangle.Y, 1, rectangle.Height), color);
    }

    private void DrawBoxOutline(Aabb box, Color color)
    {
        var x = (int)MathF.Round(box.Left);
        var y = (int)MathF.Round(box.Top);
        var w = (int)MathF.Round(box.Width);
        var h = (int)MathF.Round(box.Height);

        _spriteBatch.Draw(_pixel, new Rectangle(x, y, w, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + h - 1, w, 1), color);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, h), color);
        _spriteBatch.Draw(_pixel, new Rectangle(x + w - 1, y, 1, h), color);
    }

    /// <summary>
    /// Hedef tile'ın çerçevesi ve toplama ilerleme çubuğu.
    ///
    /// Yazı YOK — bilinçli. SpriteFont eklemek Content Pipeline'a font
    /// bağımlılığı sokuyor ve platformdan platforma kırılıyor. Madde 7'de
    /// envanter HUD'ı gerçekten yazı gerektirdiğinde, sprite üreticisiyle
    /// bir bitmap font atlası üretilecek.
    /// </summary>
    private void DrawGatherTarget()
    {
        if (_gathering.AimedTile is not { } tile)
        {
            return;
        }

        var tileSize = _map.TileSize;
        var bounds = new Rectangle(tile.X * tileSize, tile.Y * tileSize, tileSize, tileSize);

        // Toplanabilir hedef beyaz, toplanamayan soluk gri.
        var outline = _gathering.AimedTileIsHarvestable
            ? new Color(255, 255, 255) * 0.85f
            : new Color(200, 200, 200) * 0.25f;

        DrawRectangleOutline(bounds, outline);

        if (!_gathering.IsGathering)
        {
            return;
        }

        // Tile'ın üstünde ince bir ilerleme çubuğu.
        var barWidth = tileSize - 2;
        var filled = (int)MathF.Round(barWidth * _gathering.Progress01);

        _spriteBatch.Draw(_pixel,
            new Rectangle(bounds.X + 1, bounds.Y - 4, barWidth, 3),
            new Color(20, 20, 28) * 0.8f);

        if (filled > 0)
        {
            _spriteBatch.Draw(_pixel,
                new Rectangle(bounds.X + 1, bounds.Y - 4, filled, 3),
                new Color(120, 230, 140));
        }
    }

    /// <summary>
    /// Seçili yapının hedef tile'daki önizlemesi. Konulabiliyorsa yeşil,
    /// konulamıyorsa kırmızı çerçeve — R'ye basmadan önce sonucu görürsün.
    /// </summary>
    private void DrawBuildGhost()
    {
        // İstemci henüz inşa edemiyor; hayalet göstermek yanıltıcı olur.
        if (_session.Mode == SessionMode.Client)
        {
            return;
        }

        var tile = _building.AimedTile;
        var tileSize = _map.TileSize;
        var bounds = new Rectangle(tile.X * tileSize, tile.Y * tileSize, tileSize, tileSize);

        var canPlace = _building.CanPlaceAtAim(_player, _map, _inventory);
        var tint = canPlace ? new Color(120, 230, 140) : new Color(230, 100, 100);

        _spriteBatch.Draw(_pixel, bounds, tint * 0.22f);
        DrawRectangleOutline(bounds, tint * 0.85f);
    }

    /// <summary>Karakterin üstünde ince can çubuğu (dünya koordinatlarında).</summary>
    private void DrawHealthBar(Vector2 footPosition, int health)
    {
        const int width = 20;
        var x = (int)MathF.Round(footPosition.X) - width / 2;
        var y = (int)MathF.Round(footPosition.Y) - 38;

        var filled = (int)MathF.Round(width * Math.Clamp(health / (float)Player.MaxHealth, 0f, 1f));

        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 3), new Color(20, 20, 28) * 0.85f);

        if (filled > 0)
        {
            var color = health > 50 ? new Color(120, 230, 140)
                : health > 25 ? new Color(230, 200, 90)
                : new Color(230, 90, 90);

            _spriteBatch.Draw(_pixel, new Rectangle(x, y, filled, 3), color);
        }
    }

    /// <summary>
    /// Yağmur/kar parçacıkları.
    ///
    /// Parçacık nesnesi TUTULMUYOR: her parçacığın konumu indeksinden ve
    /// geçen süreden hesaplanıyor. Yüzlerce nesne ayırıp güncellemekten
    /// hem ucuz hem de kaydedilecek bir durum bırakmıyor.
    /// </summary>
    private void DrawWeatherParticles(GameTime gameTime)
    {
        var count = _climate.Weather.Particles;
        if (count <= 0)
        {
            return;
        }

        var snow = _climate.Weather.Key == "snow";
        var seconds = (float)gameTime.TotalGameTime.TotalSeconds;
        var color = snow ? new Color(235, 240, 255) * 0.85f : new Color(150, 185, 230) * 0.6f;
        var fallSpeed = snow ? 60f : 420f;

        for (var i = 0; i < count; i++)
        {
            // Deterministik sözde-rastgele başlangıç: aynı i her zaman aynı sütun.
            var seedX = i * 2654435761u % 100003u / 100003f;
            var seedPhase = i * 40503u % 9973u / 9973f;

            var x = seedX * WindowWidth;
            if (snow)
            {
                // Kar yatay salınır — dikey düşen kar yağmura benziyor.
                x += MathF.Sin(seconds * 0.8f + seedPhase * 10f) * 14f;
            }

            var y = (seedPhase * WindowHeight + seconds * fallSpeed) % WindowHeight;

            _spriteBatch.Draw(_pixel,
                snow ? new Rectangle((int)x, (int)y, 2, 2)
                     : new Rectangle((int)x, (int)y, 1, 7),
                color);
        }
    }

    /// <summary>F1 denetim görünümü: sheet'leri ham haliyle gösterir.</summary>
    private void DrawAssetInspector()
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight),
            new Color(18, 20, 28) * 0.94f);

        _spriteBatch.Draw(
            _playerSheet.Texture, new Vector2(40, 8), sourceRectangle: null,
            Color.White, rotation: 0f, origin: Vector2.Zero, scale: 2f,
            effects: SpriteEffects.None, layerDepth: 0f);

        _spriteBatch.Draw(
            _tileset.Texture, new Vector2(400, 320), sourceRectangle: null,
            Color.White, rotation: 0f, origin: Vector2.Zero, scale: 6f,
            effects: SpriteEffects.None, layerDepth: 0f);
    }
}
