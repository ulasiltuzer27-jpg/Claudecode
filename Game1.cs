using System.Diagnostics;
using PixelSurvival.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PixelSurvival.Entities;
using PixelSurvival.Systems;
using PixelSurvival.Accessibility;
using PixelSurvival.Achievements;
using PixelSurvival.Clans;
using PixelSurvival.Trade;
using PixelSurvival.Cosmetics;
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
using PixelSurvival.Localization;
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

    // --- Madde 20: kozmetik / katmanli sprite ---
    private CosmeticTable _cosmetics = null!;
    private CosmeticLoadout _loadout = null!;
    private ICosmeticOwnership _ownership = null!;

    /// <summary>
    /// Gardirop paneli acik mi (K). Acikken 1-5 tuslari uretim yerine
    /// kozmetik slotlarini dolasir — ayni tuslara iki anlam yuklemek yerine
    /// modal bir panel tercih edildi, yoksa her yeni sistem yeni bir tus
    /// istiyor ve klavye tukeniyor.
    /// </summary>
    private bool _showWardrobe;

    // --- Madde 21: Steamworks (basarim / leaderboard / arkadas daveti) ---
    private AchievementCatalog _achievementCatalog = null!;
    private AchievementTracker _achievements = null!;
    private ILeaderboardBackend _leaderboards = null!;
    private SteamFriendsService _friends = null!;

    private bool _showAchievements;
    private bool _showLeaderboard;

    // --- Madde 23: dil ve erisilebilirlik ---
    private readonly AccessibilitySettings _accessibility = new();
    private bool _showSettings;

    // --- Madde 22: klan ve takas ---
    private readonly ClanSystem _clans = new();
    private bool _showClan;
    private bool _showTrade;

    /// <summary>Takas penceresinde secili envanter slotu.</summary>
    private int _tradeSlot;

    /// <summary>Ekranda duran basarim bildirimi ve kalan suresi.</summary>
    private AchievementDefinition? _unlockBanner;
    private float _unlockBannerSeconds;

    /// <summary>Basarim bildiriminin ekranda kalma suresi.</summary>
    private const float UnlockBannerSeconds = 4f;

    /// <summary>
    /// Leaderboard skorlari kac saniyede bir yazilir.
    ///
    /// Her karede yazmak Steam'e saniyede 60 ag cagrisi demek olurdu ve
    /// Valve tarafinda hiz sinirina takilirdi.
    /// </summary>
    private const float LeaderboardUploadSeconds = 10f;

    private float _leaderboardTimer = LeaderboardUploadSeconds;

    /// <summary>Kısa ömürlü bilgi mesajı (üretim başarısız, envanter dolu vb.).</summary>
    private string _toast = "";
    private float _toastSeconds;

    private Texture2D _pixel = null!;

    private bool _showAssetView;
    private bool _showCollisionDebug;
    private KeyboardState _previousKeyboard;

    /// <summary>
    /// Otomatik dogrulama surucusu. Normal calistirmada <c>null</c>'dur ve
    /// oyun davranisi hicbir sekilde degismez.
    /// </summary>
    private readonly CaptureHarness? _capture;

    /// <summary>Acilista saf mantik denetimlerini kosturup cikilacak mi.</summary>
    private readonly bool _selfTest;

    public Game1(CaptureHarness? capture = null, bool selfTest = false)
    {
        _capture = capture;
        _selfTest = selfTest;

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

        // Madde 23: dil tablolari EN ONCE yuklenir — sonraki her sistem
        // (nadirlik etiketleri, klan rutbeleri, takas sonuclari) ceviri
        // istiyor. Ilk kod varsayilan dil; "en" yedek olarak sart.
        Loc.Load(Content, "tr", "en");

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
        // Delta NEGATIF de olabilir: madde 22'de takas item goturur.
        // Eskiden yalnizca TryAdd cagriliyordu ve negatif miktar sessizce
        // yanlis davranirdi.
        _session.ItemGranted += (itemId, amount) =>
        {
            if (amount > 0) _inventory.TryAdd(itemId, amount);
            else if (amount < 0) _inventory.TryRemove(itemId, -amount);
        };

        // Madde 22: host kendi envanterini oturuma tanitir; takas iki
        // tarafin envanterine de dokunacak.
        _session.LocalInventory = _inventory;

        // Madde 22: uretim artik HOST'ta dogrulanir. Istemci yalnizca
        // tarif kimligini yollar; malzemesi olup olmadigina host kendi
        // aynasindan karar verir.
        _session.CraftRequested += (playerId, recipeId) => HostCraftFor(playerId, recipeId);


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
                               Content, "Items/icons_16")
        {
            Accessibility = _accessibility
        };

        // Madde 20: kozmetik katmanlari. Temel beden sheet'i referans olarak
        // veriliyor; grid'i tutmayan bir katman yuklemede HATA verir.
        _cosmetics = CosmeticTable.Load(Content, "Cosmetics/cosmetics", _playerSheet);

        // Gelistirme derlemesinde sahiplik kaynagi yalnizca ucretsiz
        // kozmetikler. Steam derlemesinde bu, Steam Inventory'ye bagli bir
        // implementasyonla degistirilir — kusanma kodu degismez.
        _ownership = new FreeCosmeticsOnly();
        _loadout = new CosmeticLoadout(_ownership);

        // Madde 21: basarim / istatistik / leaderboard.
        // Hedef secimi TEK noktada (StatsBackendFactory); Game1 Steamworks
        // tiplerini hic gormuyor.
        _achievementCatalog = AchievementCatalog.Load(Content, "Steam/achievements");
        _achievements = new AchievementTracker(_achievementCatalog,
                                               StatsBackendFactory.CreateStats());
        _leaderboards = StatsBackendFactory.CreateLeaderboard();

        _friends = new SteamFriendsService();
        _friends.Initialize();

        // Arkadas "Katil" dedigi anda oyun ayni adrese baglanir. Adres
        // bicimi rich presence ile transport arasinda ORTAK: iki yerde
        // ayri tanimlanirsa davet sessizce calismaz.
        _friends.JoinRequested += address =>
        {
            ShowToast($"Davet kabul edildi: {address}");
            _session.Connect(address);
        };

        _camera = new Camera2D(WindowWidth, WindowHeight, CameraZoom);

        GenerateWorld(DefaultSeed);

        // Denetim modunda dunya kurulduktan sonra kosturulur (item veritabani
        // Content Pipeline'dan geliyor, gercek veriyle test edilsin) ve cikilir.
        if (_selfTest)
        {
            Environment.ExitCode = SelfTest.Run(_itemDatabase) == 0 ? 0 : 1;
            Exit();
        }
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
        _player = new Player(_playerSheet, start)
        {
            // Katmanlar dunya yeniden uretilse de korunur: kusanilan
            // kozmetik dunyaya degil oyuncuya aittir.
            Cosmetics = _cosmetics,
            Loadout = _loadout
        };
        _camera.SnapTo(_player.Position);

        // Aşama 2: iklim, tarım ve yaratıklar dünyayla birlikte kurulur.
        // İkisi de tohumdan türer, böylece aynı tohum aynı havayı verir.
        _climate = new ClimateSystem(_climateTable, seed);
        _farming = new FarmingSystem(_cropTable, 3);
        _fishing?.Cancel();
        _taming?.Populate(generator, _map, start, seed);

        _enemies = new EnemySystem(_enemyTable, Content, _tileset, seed);
        _npcs?.Populate(start, _map.TileSize);

        // Guvenli bolge merkezleri NPC konumlarindan turer, bu yuzden iki
        // sistem BIRLIKTE hazir olmali. Ayri ayri null kontrolu, _npcs
        // hazir degilken _zones'a bos liste yazip guvenli bolgeyi sessizce
        // yok ederdi.
        if (_zones is not null && _npcs is not null)
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
        // Yakalama script'i once islenir: bastigi tuslar AYNI karede okunsun.
        if (_capture is not null)
        {
            _capture.Update();
            if (_capture.ShouldExit) Exit();
        }

        var keyboard = InputSource.GetKeyboard();

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

        // Madde 20: gardirop
        if (WasPressed(keyboard, Keys.K))
        {
            _showWardrobe = !_showWardrobe;
            if (_showWardrobe) _showClan = false;
        }

        // Madde 23: ayarlar paneli
        if (WasPressed(keyboard, Keys.F7))
        {
            _showSettings = !_showSettings;
            if (_showSettings) { _showClan = false; _showWardrobe = false; }
        }

        // Madde 22: klan paneli ve takas penceresi.
        // Sayi tuslarini paylasan paneller ayni anda acik kalmasin:
        // hangisinin tusu isledigi oyuncu icin belirsiz olurdu.
        if (WasPressed(keyboard, Keys.N))
        {
            _showClan = !_showClan;
            if (_showClan) _showWardrobe = false;
        }

        if (WasPressed(keyboard, Keys.Y)) ToggleTradeWindow();

        if (_showTrade) HandleTradeKeys(keyboard);

        // Madde 21: basarim listesi / leaderboard / arkadas daveti
        if (WasPressed(keyboard, Keys.F3)) _showAchievements = !_showAchievements;
        if (WasPressed(keyboard, Keys.F4)) _showLeaderboard = !_showLeaderboard;
        if (WasPressed(keyboard, Keys.F6))
        {
            ShowToast(Loc.T(_friends.OpenInviteOverlay()
                ? "steam.inviteOpened"
                : "steam.inviteNeedsSteam"));
        }

        // Madde 9: yapı seçimi
        if (WasPressed(keyboard, Keys.Q)) _building.SelectPrevious();
        if (WasPressed(keyboard, Keys.Z)) _building.SelectNext();

        // Madde 10: oturum tuşları
        // Aşama 2 tuşları
        if (WasPressed(keyboard, Keys.X)) CycleSeed();
        if (WasPressed(keyboard, Keys.C)) DoFarmAction();
        if (WasPressed(keyboard, Keys.G))
        {
            ShowToast(_taming.Interact(_player, _inventory));

            // Sayac uzerinden: Interact'in dondugu metne bakmak, madde 23'te
            // metinler dile cevrilince sessizce bozulurdu.
            _achievements.SetMax(new StatKey("creatures_tamed"), _taming.TamedCount);
        }
        if (WasPressed(keyboard, Keys.T)) InteractWithNpc();
        if (WasPressed(keyboard, Keys.B)) ToggleDungeon();

        if (WasPressed(keyboard, Keys.F9)) _session.StartHost(_map.Seed);
        if (WasPressed(keyboard, Keys.F10)) _session.Connect(TransportFactory.DefaultConnectTarget);
        if (WasPressed(keyboard, Keys.F11)) { _session.Leave(); ShowToast("Oturum kapatildi"); }

        // Sayi tuslarinin sahibi HER ZAMAN tek bir panel.
        //
        // Onceki surumde gardirop uretimle ayrilmisti ama klan paneli
        // ayrilmamisti: klan paneli acikken 1'e basmak hem klan kuruyor
        // hem uretim deniyordu ("Malzeme yetersiz" toast'i cikiyordu).
        // Zincir if/else bu cakismayi yapisal olarak imkansiz kiliyor.
        if (_showSettings) HandleSettingsKeys(keyboard);
        else if (_showWardrobe) HandleWardrobeKeys(keyboard);
        else if (_showClan) HandleClanKeys(keyboard);
        else if (!_showTrade) HandleCraftingKeys(keyboard);

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

            // Madde 21: hayatta kalinan gun BIRIKMEZ, en yuksek deger tutulur.
            // Add() kullanilsaydi her karede gun sayisi tekrar eklenirdi.
            _achievements.SetMax(new StatKey("days_survived"), _climate.Day);
        }

        // Madde 14: binek hız çarpanı
        UpdateAchievements(delta);

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

            // Madde 21: oldurmeler ganimetten BAGIMSIZ sayilir. Ganimet
            // sansa bagli; drop'lari saysaydik sanssiz oldurmeler
            // basarim ilerlemesine hic yazilmazdi.
            foreach (var kill in _enemies.KillsThisFrame)
            {
                _achievements.Add(new StatKey("enemies_killed"));
                if (kill.IsBoss) _achievements.Add(new StatKey("bosses_killed"));
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
            _achievements.Add(new StatKey("fish_caught"), catchResult.Amount);
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

            // Madde 21: toplama istatistigi. Envantere GIREN miktar sayilir,
            // dusen degil - envanter doluyken kaybolan kaynak basarim
            // ilerlemesi saymamali.
            var stored = result.Amount - leftover;
            if (stored > 0)
            {
                if (result.Resource == "wood") _achievements.Add(new StatKey("wood_gathered"), stored);
                else if (result.Resource == "stone") _achievements.Add(new StatKey("stone_gathered"), stored);
            }

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
                    _achievements.Add(new StatKey("structures_built"));

                    // Madde 22: yapinin sahibi kuranin klanidir. Kuran
                    // klansizsa kayit acilmaz ve yapi sahipsiz kalir —
                    // madde 9'daki davranis korunur.
                    _clans.RegisterStructure(tile, _session.LocalPlayerId);
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
    /// MADDE 23 — ayar tuşları: 1 dil, 2 yazı boyutu, 3 renk paleti.
    ///
    /// Üçü de "sırayla değiştir" mantığında. Ayar ekranı bir menü
    /// çatısı ister; bu maddenin kapsamı ayarların KENDİSİ, menü değil.
    /// </summary>
    private void HandleSettingsKeys(KeyboardState keyboard)
    {
        if (WasPressed(keyboard, Keys.D1))
        {
            Loc.Cycle();
            ShowToast(Loc.T("settings.language", Loc.CurrentName));
        }

        if (WasPressed(keyboard, Keys.D2))
        {
            _accessibility.CycleTextScale();
            ShowToast(Loc.T("settings.textScale", _accessibility.TextScale));
        }

        if (WasPressed(keyboard, Keys.D3))
        {
            _accessibility.CycleColorVision();
            ShowToast(Loc.T("settings.colorVision", _accessibility.ColorVisionLabel));
        }
    }

    /// <summary>
    /// MADDE 22 — takas penceresini açar/kapatır.
    ///
    /// Takas iki oyuncu gerektirir: tek kişilik oyunda pencere açılmaz,
    /// çünkü açılsaydı "karşı taraf" diye boş bir sütun çizmek zorunda
    /// kalırdık ve oyuncu neyin eksik olduğunu anlamazdı.
    /// </summary>
    private void ToggleTradeWindow()
    {
        if (_showTrade)
        {
            _showTrade = false;
            return;
        }

        if (_session.Mode == SessionMode.Offline)
        {
            ShowToast("Takas icin ikinci oyuncu gerekli (F9/F10)");
            return;
        }

        if (_session.Mode == SessionMode.Host)
        {
            // Host, bagli ilk oyuncuyla takas acar. Hedef secimi bir
            // oyuncu listesi ekrani ister; bu asamada kapsam disi.
            var partner = _session.ConnectedPlayerIds.FirstOrDefault();

            if (partner == 0)
            {
                ShowToast("Bagli oyuncu yok");
                return;
            }

            var outcome = _session.BeginTrade(_session.LocalPlayerId, partner);

            if (outcome != TradeOutcome.Success)
            {
                ShowToast($"Takas: {TradeSession.Describe(outcome)}");
                return;
            }
        }
        else
        {
            // Istemci host'tan takas acmasini ister; kararı host verir.
            _session.RequestTrade(0);
        }

        _showTrade = true;
        _tradeSlot = 0;
    }

    /// <summary>
    /// MADDE 22 — takas penceresi tuşları.
    ///
    /// Sol/sağ slot seçer, yukarı/aşağı teklife ekler/çıkarır, Enter
    /// onaylar. Onay, teklif her değiştiğinde İKİ tarafta da düşer —
    /// kural <see cref="TradeSession"/> içinde, burada değil.
    /// </summary>
    private void HandleTradeKeys(KeyboardState keyboard)
    {
        if (WasPressed(keyboard, Keys.Left)) _tradeSlot = Math.Max(0, _tradeSlot - 1);
        if (WasPressed(keyboard, Keys.Right))
        {
            _tradeSlot = Math.Min(_inventory.SlotCount - 1, _tradeSlot + 1);
        }

        var stack = _inventory[_tradeSlot];

        if (WasPressed(keyboard, Keys.Up) && !stack.IsEmpty)
        {
            _session.OfferLocal(stack.ItemId, 1);
        }

        if (WasPressed(keyboard, Keys.Down) && !stack.IsEmpty)
        {
            _session.OfferLocal(stack.ItemId, -1);
        }

        if (WasPressed(keyboard, Keys.Enter))
        {
            var accepted = _session.Mode == SessionMode.Host
                ? !_session.Trade.AcceptedA
                : !_session.ClientTradeAcceptedSelf;

            _session.SetLocalAccepted(accepted);
            ShowToast(accepted ? "Takasi onayladin" : "Onayini geri cektin");
        }
    }

    /// <summary>
    /// MADDE 22 — klan paneli tuşları.
    ///
    /// 1 klan kurar, 2 bağlı oyuncuyu davet eder, 3 klandan ayrılır.
    /// Klan adı bu aşamada sabit: metin girişi bir arayüz işi ve bu
    /// maddenin kapsamı klan MANTIĞI.
    /// </summary>
    private void HandleClanKeys(KeyboardState keyboard)
    {
        var me = _session.LocalPlayerId;

        if (WasPressed(keyboard, Keys.D1))
        {
            var outcome = _clans.Create(me, $"Oyuncu{me}", $"Klan{me}", $"K{me}", out var clan);

            ShowToast(outcome == ClanOutcome.Success
                ? $"Klan kuruldu: [{clan!.Tag}] {clan.Name}"
                : ClanSystem.Describe(outcome));
        }

        if (WasPressed(keyboard, Keys.D2))
        {
            var target = _session.ConnectedPlayerIds.FirstOrDefault(id => id != me);

            if (target == 0 && _session.Mode != SessionMode.Host)
            {
                target = 0;   // istemci icin host'un kimligi 0
            }

            var outcome = _clans.Invite(me, target, $"Oyuncu{target}");

            ShowToast(outcome == ClanOutcome.Success
                ? $"Oyuncu {target} klana katildi"
                : ClanSystem.Describe(outcome));
        }

        if (WasPressed(keyboard, Keys.D3))
        {
            ShowToast(ClanSystem.Describe(_clans.Leave(me)));
        }
    }

    /// <summary>
    /// MADDE 22 — bir istemcinin üretim isteğini HOST tarafında çözer.
    ///
    /// Madde 10'da üretim istemcide yerel çalışıyordu ve host doğrulamıyordu.
    /// Takas geldiğinde bu açık doğrudan "malzemem yokken üretip takas
    /// etmek" anlamına gelirdi; bu yüzden üretim host'a taşındı.
    ///
    /// Karar host'un AYNASI üzerinden veriliyor; istemcinin gönderdiği tek
    /// şey tarif kimliği.
    /// </summary>
    private void HostCraftFor(byte playerId, string recipeId)
    {
        var recipe = _crafting.Recipes.FirstOrDefault(r => r.Id == recipeId);

        if (recipe is null)
        {
            Console.WriteLine($"[uretim] oyuncu {playerId} bilinmeyen tarif istedi: {recipeId}");
            return;
        }

        var mirror = _session.InventoryOf(playerId);
        if (mirror is null) return;

        var outcome = _crafting.TryCraft(recipe, mirror);

        if (outcome != CraftOutcome.Success)
        {
            Console.WriteLine($"[uretim] oyuncu {playerId} reddedildi ({outcome}): {recipeId}");
            return;
        }

        // Ayna zaten degisti; istemciye yalnizca farki bildiriyoruz.
        foreach (var ingredient in recipe.Inputs)
        {
            _session.GrantTo(playerId, ingredient.Item, -ingredient.Amount);
        }

        _session.GrantTo(playerId, recipe.Output.Item, recipe.Output.Amount);
        Console.WriteLine($"[uretim] oyuncu {playerId} -> {recipeId} (host dogruladi)");
    }

    /// <summary>
    /// MADDE 21 — kare sonu başarım işleri.
    ///
    /// Üç iş burada toplanıyor: bekleyen açılış bildirimini ekrana almak,
    /// istatistikleri hedefe TEK SEFERDE göndermek ve leaderboard skorlarını
    /// yazmak. Steamworks'te <c>StoreStats</c> ve skor yükleme ağ
    /// çağrılarıdır; her istatistik artışında çağrılmaları gereksiz trafik
    /// olurdu.
    /// </summary>
    private void UpdateAchievements(float delta)
    {
        if (_achievements.TryDequeueUnlock(out var unlock))
        {
            _unlockBanner = unlock.Definition;
            _unlockBannerSeconds = UnlockBannerSeconds;
            Console.WriteLine($"[basarim] {unlock.Definition.Id} — {unlock.Definition.Name}");
        }

        if (_unlockBannerSeconds > 0f)
        {
            _unlockBannerSeconds -= delta;
            if (_unlockBannerSeconds <= 0f) _unlockBanner = null;
        }

        _achievements.Flush();

        // Skorlar istatistiklerden turer; hangi tablonun hangi istatistigi
        // yazdigi achievements.json'da tanimli, koda gomulu degil.
        _leaderboardTimer -= delta;
        if (_leaderboardTimer <= 0f)
        {
            _leaderboardTimer = LeaderboardUploadSeconds;

            foreach (var board in _achievementCatalog.Leaderboards)
            {
                _leaderboards.Upload(board, _achievements.Value(board.StatKey));
            }
        }
    }

    /// <summary>
    /// Madde 20: gardirop tuşları. 1-5, kuşanılabilir slotların sırasına
    /// karşılık gelir; her basış o slottaki kozmetiği bir sonrakine çevirir,
    /// listenin sonunda slotu boşaltır.
    ///
    /// Sahip olunmayan kozmetikler <see cref="CosmeticLoadout.CycleSlot"/>
    /// tarafından atlanır — oyuncu kuşanamayacağı bir seçeneğe takılmaz.
    /// </summary>
    private void HandleWardrobeKeys(KeyboardState keyboard)
    {
        var slots = CosmeticSlotExtensions.Equippable;

        for (var i = 0; i < slots.Length; i++)
        {
            if (!WasPressed(keyboard, Keys.D1 + i)) continue;

            var slot = slots[i];
            var equipped = _loadout.CycleSlot(_cosmetics, slot);

            if (equipped is not null)
            {
                _achievements.Add(new StatKey("cosmetics_worn"));
            }

            ShowToast(equipped is null
                ? $"{slot}: cikarildi"
                : $"{slot}: {equipped.Name} ({equipped.Rarity.Label()})");
        }

        // 0: hepsini cikar. Tek tek dolasmadan cıplak bedene donmek icin.
        if (WasPressed(keyboard, Keys.D0))
        {
            _loadout.ClearAll();
            ShowToast("Kozmetikler cikarildi");
        }
    }

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

            // Madde 22: ISTEMCI kendi envanterine dokunmaz. Istek host'a
            // gider, host kendi aynasindan dogrular ve sonucu delta olarak
            // geri yollar. Yerel uretim, host'un bilmedigi item yaratirdi
            // ve takas o item'i gercek sayardi.
            if (_session.Mode == SessionMode.Client)
            {
                _session.RequestCraft(recipe.Id);
                ShowToast("Uretim istegi gonderildi");
                continue;
            }

            var outcome = _crafting.TryCraft(recipe, _inventory);

            switch (outcome)
            {
                case CraftOutcome.Success:
                    var name = _itemDatabase.Get(recipe.Output.Item).Name;
                    ShowToast($"+{recipe.Output.Amount} {name}");
                    _achievements.Add(new StatKey("items_crafted"), recipe.Output.Amount);
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

        // Madde 22: KENDI klaninin yapisina baskin yapilmaz — sokulur.
        // Baskinla malzemenin yalnizca yarisi doner; oyuncunun kendi
        // duvarini yikarken ceza odemesi anlamsiz olurdu.
        var owner = _clans.OwnerOf(tile);

        if (!owner.IsNone && _clans.CanDismantle(tile, _session.LocalPlayerId))
        {
            ShowToast("Kendi klaninin yapisi — Space ile sok");
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

                // Yapi yok oldu: sahiplik kaydi da silinmeli, yoksa ayni
                // tile'a baskasi insa ettiginde eski sahibin klani
                // gorunurdu.
                _clans.ForgetStructure(tile);
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
        _achievements.Add(new StatKey("dungeons_entered"));
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

        if (outcome == FarmOutcome.Harvested)
        {
            _achievements.Add(new StatKey("crops_harvested"));
        }

        if (outcome == FarmOutcome.Tilled)
        {
            var tile = BuildingSystem.AimTile(_player, _map, 1);
            _session.NotifyTileChanged(tile.X, tile.Y, _map.GetTileIndex(tile.X, tile.Y));
        }
    }

    /// <summary>Sol üstteki oturum durumu satırı.</summary>
    private string DescribeSession() => _session.Mode switch
    {
        SessionMode.Host => Loc.T("hud.session.host", _session.ConnectedCount),
        SessionMode.Client => Loc.T("hud.session.client", _session.LocalPlayerId),
        _ => Loc.T("hud.session.solo")
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

            // Iklim seridi ONCE cizilir: sag ust kosedeki uretim paneli
            // onun altindan baslasin diye alt kenarini geri veriyor.
            var climateBottom = _hud.DrawClimate(_spriteBatch,
                Loc.T("hud.climate", _climate.Day, _climate.ClockText,
                      _climate.Season.Name, _climate.Weather.Name), WindowWidth);

            if (_showCrafting)
            {
                _hud.DrawCrafting(_spriteBatch, _crafting, _inventory, WindowWidth,
                                  climateBottom + 6);
            }

            _hud.DrawZone(_spriteBatch,
                _zones.ZoneAt(_player.Position, _map.TileSize) == ZoneKind.Safe,
                _steam.Status, WindowWidth, WindowHeight);

            // Madde 23: ayarlar paneli
            if (_showSettings)
            {
                _hud.DrawSettings(_spriteBatch, _accessibility, WindowWidth, WindowHeight);
            }

            // Madde 22: klan paneli ve takas penceresi.
            if (_showClan)
            {
                _hud.DrawClan(_spriteBatch, _clans, _session.LocalPlayerId,
                              WindowWidth, WindowHeight);
            }

            if (_showTrade)
            {
                var host = _session.Mode == SessionMode.Host;
                var trade = _session.Trade;

                var localAccepted = host
                    ? (_session.LocalPlayerId == trade.PlayerA ? trade.AcceptedA : trade.AcceptedB)
                    : _session.ClientTradeAcceptedSelf;

                var partnerAccepted = host
                    ? (_session.LocalPlayerId == trade.PlayerA ? trade.AcceptedB : trade.AcceptedA)
                    : _session.ClientTradeAcceptedOther;

                var stateLabel = host ? trade.State.ToString() : _session.ClientTradeState.ToString();

                _hud.DrawTrade(_spriteBatch, trade, _itemDatabase, _inventory, _tradeSlot,
                               _session.LocalPlayerId, localAccepted, partnerAccepted,
                               stateLabel, WindowWidth, WindowHeight);
            }

            // Madde 21: basarim listesi / siralamalar / acilma bildirimi.
            if (_showAchievements)
            {
                _hud.DrawAchievements(_spriteBatch, _achievements, WindowWidth, WindowHeight);
            }

            if (_showLeaderboard)
            {
                _hud.DrawLeaderboard(_spriteBatch, _leaderboards,
                                     _achievementCatalog.Leaderboards, _achievements,
                                     WindowWidth, WindowHeight);
            }

            if (_unlockBanner is { } banner)
            {
                _hud.DrawUnlockBanner(_spriteBatch, banner, WindowWidth, WindowHeight);
            }

            if (_showWardrobe)
            {
                _hud.DrawWardrobe(_spriteBatch, _cosmetics, _loadout, _ownership,
                                  _climate.Season.Name, WindowWidth, WindowHeight);
            }

            if (_fishing.IsActive)
            {
                _hud.DrawFishingBar(_spriteBatch, _fishing.Marker,
                    _fishing.ZoneStart, _fishing.ZoneSize,
                    _fishing.State == FishingState.Casting, WindowWidth, WindowHeight);
            }

            // Ayar paneli acikken toast gizlenir: panel zaten ayni degeri
            // gosteriyor ve buyuk yazi olceginde ikisi ust uste biniyordu.
            if (_toastSeconds > 0f && !_showSettings)
            {
                _hud.DrawToast(_spriteBatch, _toast, WindowWidth);
            }
        }

        _spriteBatch.End();

        base.Draw(gameTime);

        // Her sey cizildikten SONRA: yakalama script'i bu kareyi istediyse
        // back buffer PNG'ye yazilir.
        _capture?.CaptureIfRequested(GraphicsDevice);
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
