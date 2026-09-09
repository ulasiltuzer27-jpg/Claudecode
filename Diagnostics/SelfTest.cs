using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using PixelSurvival.Clans;
using PixelSurvival.Inventory;
using PixelSurvival.Localization;
using PixelSurvival.Networking;
using PixelSurvival.Entities;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Climate;
using PixelSurvival.Systems.Farming;
using PixelSurvival.Systems.Taming;
using PixelSurvival.Systems.Npcs;
using PixelSurvival.Systems.Hostiles;
using PixelSurvival.Systems.Social;
using PixelSurvival.Trade;
using PixelSurvival.Persistence;
using PixelSurvival.Workshop;
using PixelSurvival.World;

namespace PixelSurvival.Diagnostics;

/// <summary>
/// Saf mantık sistemlerinin çalışma zamanı denetimi.
///
/// ── Neden ekran görüntüsü yetmiyor ──────────────────────────────────────
/// Madde 20 ve 21 gözle doğrulanabildi: katman ekranda göründü, başarım
/// bildirimi çıktı. Madde 22'nin kritik kuralları ise GÖRÜNMEZ:
///
///   • takas atomik mi (yarım uygulanıp item yok ediyor mu),
///   • teklif değişince onaylar gerçekten düşüyor mu,
///   • sahip olunmayan item teklif edilince ne oluyor,
///   • klan rütbeleri yetki sınırlarını koruyor mu.
///
/// Bunlar ancak KOŞTURULARAK doğrulanır. Ekran görüntüsü "takas penceresi
/// açıldı" der; ekonomiyi bitiren hata pencerenin açılmasında değil,
/// envanterin yarım kalmasında olur.
///
/// Çalıştırma:  <c>dotnet run -- --self-test</c>
/// Çıkış kodu 0 = hepsi geçti, 1 = en az bir kural bozuk.
/// </summary>
public static class SelfTest
{
    private static int _passed;
    private static int _failed;

    /// <summary>
    /// Denetimlerin ihtiyaç duyduğu, Content Pipeline'dan yüklenmiş GERÇEK veri.
    ///
    /// Neden bir kayıt: denetimler büyüdükçe parametre listesi de büyüyor ve
    /// yedi konumsal argüman, çağrı yerinde hangisinin hangisi olduğunu
    /// okunamaz hale getiriyordu. Sahte veri yerine gerçeğinin kullanılması
    /// bilinçli — sınamanın oyunun yüklediği veriyle aynı şeyi görmesi
    /// isteniyor.
    /// </summary>
    public sealed record SelfTestWorld(
        ItemDatabase Items,
        Tileset Tileset,
        SpriteSheet PlayerSheet,
        EnemySystem Enemies,
        ClimateSystem Climate,
        NpcSystem Npcs,
        CropTable Crops,
        ClimateTable ClimateTable);

    /// <summary>Tüm denetimleri koşturur; başarısız sayısını döndürür.</summary>
    public static int Run(SelfTestWorld world)
    {
        _passed = _failed = 0;

        Console.WriteLine("=== PixelSurvival kendi kendini denetleme ===\n");

        ClanRankRules();
        StructureOwnershipRules();
        TradeAcceptanceRules(world.Items);
        TradeAtomicityRules(world.Items);
        SocialSignalRules();
        WorkshopRules();
        SaveGameRules();
        PathfindingRules();
        EnemyChaseRules(world.Items, world.Tileset, world.PlayerSheet, world.Enemies,
                        world.Climate);
        EntitySnapshotRules();
        ProgressPersistenceRules(world);
        DataNameRules(world);
        ModOverlayRules();

        Console.WriteLine($"\n{_passed} gecti, {_failed} kaldi.");
        return _failed;
    }

    // ==================== EMOTE / PING (madde 24) ====================

    private static void SocialSignalRules()
    {
        Section("5) Emote ve ping (spam korumasi, omur)");

        var social = new SocialSystem();

        Check("ilk emote gecer", social.TryEmote(0, EmoteKind.Wave));

        // Ping ve emote, sohbeti olmayan bir oyunda en kolay taciz araci.
        // Bekleme suresi VERI sinifinda, arayuzde degil: arayuzde olsaydi
        // agdan gelen mesajlar siniri atlardi.
        Check("bekleme suresi dolmadan ikinci isaret REDDEDILIR",
            !social.TryEmote(0, EmoteKind.Laugh));

        Check("reddedilen isaret listeye EKLENMEZ", social.Emotes.Count == 1);

        // Baska bir oyuncunun beklemesi ayri.
        Check("baska oyuncu ayni anda isaret verebilir",
            social.TryPing(1, PingKind.Danger, new Microsoft.Xna.Framework.Vector2(10, 10)));

        // Bekleme suresi 1.2 sn; 1.5 sn ilerlet.
        social.Update(1.5f);
        Check("bekleme suresi dolunca tekrar isaret verilebilir",
            social.TryEmote(0, EmoteKind.Yes));

        // Ayni oyuncunun onceki emote'u dusmeli: iki balon ust uste binmesin.
        Check("oyuncu basina TEK emote kalir",
            social.Emotes.Count(e => e.PlayerId == 0) == 1);

        Check("en son emote gecerli",
            social.Emotes.First(e => e.PlayerId == 0).Kind == EmoteKind.Yes);

        // Omur dolunca temizlenmeli.
        social.Update(SocialSystem.EmoteSeconds + 0.1f);
        Check("omru dolan emote silinir", social.Emotes.Count == 0);

        social.Update(SocialSystem.PingSeconds);
        Check("omru dolan ping silinir", social.Pings.Count == 0);

        // Oyuncu ayrilinca isaretleri de gitmeli.
        var leaving = new SocialSystem();
        leaving.TryPing(3, PingKind.Go, Microsoft.Xna.Framework.Vector2.Zero);
        leaving.Forget(3);
        Check("ayrilan oyuncunun isaretleri temizlenir", leaving.Pings.Count == 0);

        // Ping oyuncu basina TEK: harita isaret coplugune donmesin.
        var single = new SocialSystem();
        single.TryPing(4, PingKind.Look, new Microsoft.Xna.Framework.Vector2(1, 1));
        single.Update(2f);
        single.TryPing(4, PingKind.Danger, new Microsoft.Xna.Framework.Vector2(9, 9));
        Check("oyuncu basina TEK ping kalir",
            single.Pings.Count(p => p.PlayerId == 4) == 1);
    }

    // ==================== WORKSHOP / MODLAR (madde 25) ====================

    private static void WorkshopRules()
    {
        Section("6) Mod manifesti ve parmak izi");

        var root = Path.Combine(Path.GetTempPath(), "pixelsurvival_selftest_mods");

        // Onceki kosudan kalinti kalmasin: parmak izi dosya listesinden
        // turedigi icin artik bir dosya sonucu degistirirdi.
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);

        try
        {
            // --- Gecerli mod ---
            WriteMod(root, "alpha", """
                {"id":"alpha","name":"Alpha","version":"1.0.0","provides":["Items"],"loadOrder":10}
                """, ("data.json", "{}"));

            // --- Bozuk manifest: kimlik yok ---
            WriteMod(root, "bozuk", """
                {"name":"Kimliksiz","version":"1.0.0"}
                """);

            // --- mod.json'i olmayan klasor: mod DEGIL ---
            Directory.CreateDirectory(Path.Combine(root, "modolmayan"));
            File.WriteAllText(Path.Combine(root, "modolmayan", "readme.txt"), "selam");

            var registry = new ModRegistry();
            registry.Discover([(root, false)]);

            Check("gecerli mod bulundu",
                registry.Mods.Any(m => m.Manifest.Id == "alpha"));

            // Bozuk mod OYUNU COKERTMEZ; atlanip raporlanir. Workshop'tan
            // gelen tek bozuk abonelik oyunu acilmaz hale getirmemeli.
            Check("bozuk manifest yuklenmedi",
                registry.Mods.All(m => m.Manifest.Name != "Kimliksiz"));

            Check("bozuk manifest UYARI uretti", registry.Warnings.Count > 0);

            Check("mod.json'i olmayan klasor mod sayilmadi",
                registry.Mods.Count == 1);

            // Yalnizca .json/.png icerik sayilir (beyaz liste).
            Check("izin verilmeyen uzanti icerik sayilmaz",
                registry.Mods[0].Files.All(f => f.EndsWith(".json") || f.EndsWith(".png")));

            var withAlpha = registry.Fingerprint;
            Check("parmak izi modsuzdan farkli", withAlpha != "modsuz");

            // Ayni kume -> ayni parmak izi. Deterministik olmazsa ayni
            // modlara sahip iki oyuncu birbirine baglanamazdi.
            var again = new ModRegistry();
            again.Discover([(root, false)]);
            Check("ayni kume AYNI parmak izi verir", again.Fingerprint == withAlpha);

            // Icerik degisince parmak izi de degismeli: ayni kimlikli ama
            // farkli icerikli mod, ayni tohumdan farkli dunya uretir.
            File.WriteAllText(Path.Combine(root, "alpha", "yeni.json"), "{}");

            var changed = new ModRegistry();
            changed.Discover([(root, false)]);
            Check("icerik degisince parmak izi DEGISIR",
                changed.Fingerprint != withAlpha);

            // Mod kapatilinca parmak izi modsuza donmeli.
            changed.Mods[0].Enabled = false;
            Check("tum modlar kapaliyken parmak izi 'modsuz'",
                changed.Fingerprint == "modsuz");

            // Kimlikte yol ayirici: mod klasorunun disina yazmaya acilan kapi.
            var traversal = new ModManifest { Id = "../kotu", Name = "Kotu" };
            var rejected = false;
            try { traversal.Validate("test"); }
            catch (InvalidOperationException) { rejected = true; }

            Check("yol ayirici iceren mod kimligi REDDEDILIR", rejected);

            // Bilinmeyen icerik turu de reddedilmeli: manifest ile icerigin
            // ayrismasi "neden calismiyor"un en sik cevabi.
            var badKind = new ModManifest { Id = "x", Name = "X", Provides = ["Scripts"] };
            var kindRejected = false;
            try { badKind.Validate("test"); }
            catch (InvalidOperationException) { kindRejected = true; }

            Check("bilinmeyen icerik turu REDDEDILIR", kindRejected);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteMod(string root, string id, string manifest,
                                 params (string Name, string Body)[] files)
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModManifest.FileName), manifest);

        foreach (var (name, body) in files)
        {
            File.WriteAllText(Path.Combine(folder, name), body);
        }
    }

    // ==================== KAYIT / YUKLEME ====================

    private static void SaveGameRules()
    {
        Section("7) Kayit yazma/okuma");

        // Testin gercek kayit dosyasina dokunmamasi icin calisma dizini
        // gecici bir klasore alinir: SaveGame yollari goreli.
        var original = Directory.GetCurrentDirectory();
        var sandbox = Path.Combine(Path.GetTempPath(), "pixelsurvival_selftest_save");

        if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
        Directory.CreateDirectory(sandbox);
        Directory.SetCurrentDirectory(sandbox);

        try
        {
            Check("kayit yokken Exists false", !SaveGame.Exists());
            Check("kayit yokken NotFound doner",
                SaveGame.Load(out _, out _) == LoadOutcome.NotFound);

            var data = new SaveData
            {
                Seed = 12345,
                WorldSeconds = 480.5,
                Weather = "rain",
                PlayerX = 10.5f,
                PlayerY = -20.25f,
                PlayerHealth = 73,
                Tiles = [new SavedTile { X = -2, Y = 0, Tile = 0 }],
                Inventory = [new SavedSlot { Slot = 0, Item = "wood", Count = 3 }],
                Unlocked = ["ACH_FIRST_WOOD"],
                Quests = ["q_first_wood"],
                Crops =
                [
                    new SavedCrop
                    {
                        X = 7, Y = -3, Crop = "wheat",
                        PlantedAtDay = 1.25, GrowthDays = 0.75
                    }
                ]
            };
            data.Stats["wood_gathered"] = 3;
            data.Cosmetics["Hat"] = "hat_cap";

            Check("kayit yazilabiliyor", SaveGame.Save(data) is null);
            Check("kayit dosyasi olustu", SaveGame.Exists());

            Check("kayit okunabiliyor",
                SaveGame.Load(out var loaded, out _) == LoadOutcome.Success && loaded is not null);

            // Gidis-donus: yazilan her alan aynen geri gelmeli.
            Check("tohum korunuyor", loaded!.Seed == 12345);
            Check("dunya saati korunuyor", Math.Abs(loaded.WorldSeconds - 480.5) < 0.001);
            Check("hava korunuyor", loaded.Weather == "rain");
            Check("oyuncu konumu korunuyor",
                Math.Abs(loaded.PlayerX - 10.5f) < 0.001f &&
                Math.Abs(loaded.PlayerY + 20.25f) < 0.001f);
            Check("can korunuyor", loaded.PlayerHealth == 73);

            Check("tile degisikligi korunuyor",
                loaded.Tiles.Count == 1 && loaded.Tiles[0].X == -2 && loaded.Tiles[0].Tile == 0);

            Check("envanter korunuyor",
                loaded.Inventory.Count == 1 && loaded.Inventory[0].Item == "wood" &&
                loaded.Inventory[0].Count == 3);

            Check("kozmetik korunuyor", loaded.Cosmetics.GetValueOrDefault("Hat") == "hat_cap");
            Check("istatistik korunuyor", loaded.Stats.GetValueOrDefault("wood_gathered") == 3);
            Check("acilan basarim korunuyor", loaded.Unlocked.Contains("ACH_FIRST_WOOD"));

            Check("tamamlanan gorev korunuyor", loaded.Quests.Contains("q_first_wood"));

            // Ekinin AŞAMASI degil, biriken buyumesi kaydediliyor: asama
            // buyumeden turuyor ve ikisini birden yazmak ayrisma kapisi
            // acardi.
            Check("ekili tarla korunuyor",
                loaded.Crops.Count == 1 && loaded.Crops[0].X == 7 && loaded.Crops[0].Y == -3 &&
                loaded.Crops[0].Crop == "wheat");

            Check("tarlanin buyumesi korunuyor",
                Math.Abs(loaded.Crops[0].GrowthDays - 0.75) < 1e-9 &&
                Math.Abs(loaded.Crops[0].PlantedAtDay - 1.25) < 1e-9);

            // Ikinci yazim once mevcut kaydi yedeklemeli: atomik yazim
            // yarim dosyaya karsi korur, yedek MANTIK hatasina karsi.
            SaveGame.Save(data);
            Check("ikinci yazimda yedek olusuyor", File.Exists(SaveGame.Path + ".bak"));

            // Gecici dosya ortada birakilmamali: bir sonraki yazim onu
            // gormemeli.
            Check("gecici dosya temizlenmis", !File.Exists(SaveGame.Path + ".tmp"));

            // GELECEKTEN gelen kayit reddedilmeli: bu surumun hic bilmedigi
            // alanlar var ve okumak alanlari yanlis yerlere oturtur.
            var raw = File.ReadAllText(SaveGame.Path);
            File.WriteAllText(SaveGame.Path,
                raw.Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 99"));

            Check("gelecekten gelen surum reddediliyor",
                SaveGame.Load(out var rejected, out _) == LoadOutcome.VersionMismatch
                && rejected is null);

            // Okunabilir aralik ALTINDAKI surum de reddedilmeli.
            File.WriteAllText(SaveGame.Path,
                raw.Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 0"));

            Check("cok eski surum reddediliyor",
                SaveGame.Load(out _, out _) == LoadOutcome.VersionMismatch);

            // ESKI ama okunabilir surum yuklenmeli: JSON alanlari isimle
            // eslesiyor, yani EKLENEN bir alan eski kaydi bozmuyor. Sirf
            // yeni alan eklendi diye oyuncunun kaydini comege atmak
            // korumanin amacini asan bir ceza olurdu.
            File.WriteAllText(SaveGame.Path,
                raw.Replace($"\"version\": {SaveData.CurrentVersion}",
                            $"\"version\": {SaveData.MinimumReadableVersion}"));

            Check("eski ama okunabilir surum yukleniyor",
                SaveGame.Load(out var old, out var note) == LoadOutcome.Migrated &&
                old is not null);

            Check("eski surum sessizce gecmiyor (aciklama var)", note.Length > 0, note);

            // Eski surumden gelen kayitta eski alanlar YERINDE olmali:
            // "okunabilir" demek "bos donuyor" demek degil.
            Check("eski surumde envanter yerinde",
                old!.Inventory.Count == 1 && old.Inventory[0].Item == "wood");

            // Bozuk dosya COKERTMEMELI.
            File.WriteAllText(SaveGame.Path, "{ bu gecerli json degil");
            Check("bozuk kayit cokertmiyor",
                SaveGame.Load(out _, out _) == LoadOutcome.Unreadable);

            // Silme kaydi yedege TASIR, yok etmez: "yeni oyun"a yanlislikla
            // basmak geri donulemez olmamali.
            File.WriteAllText(SaveGame.Path, raw);
            SaveGame.Delete();
            Check("silinen kayit yedekte duruyor",
                !SaveGame.Exists() && File.Exists(SaveGame.Path + ".bak"));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
        }
    }

    // ==================== YOL BULMA ====================

    /// <summary>
    /// Düşman yol bulmasının kuralları.
    ///
    /// Ekranda "düşman geldi" görünür ama DUVARI DOLAŞTIĞI görünmez —
    /// düz çizgide gelirken duvara sürtüp yanından geçen düşman da uzaktan
    /// aynıdır. Bu yüzden yol elle çizilmiş labirentlerde denetleniyor.
    ///
    /// Labirentler <see cref="TileMap"/> değil saf bir katılık fonksiyonu
    /// kullanıyor: yol bulmanın doğruluğu Content Pipeline'dan gelen
    /// tileset'e bağlı değil.
    /// </summary>
    private static void PathfindingRules()
    {
        Section("8) Dusman yol bulma (A*)");

        var pathfinder = new TilePathfinder();
        var path = new List<Point>();

        // --- Bos arazi: duz gitmeli ---
        var open = (int x, int y) => false;

        Check("bos arazide hedefe ulasilir",
            pathfinder.FindPath(open, new Point(0, 0), new Point(5, 0), path)
                == PathResult.Complete);

        // 5 dik adim; capraz kisayol yok cunku ayni satirdayiz.
        Check("bos arazide yol en kisa (5 adim)", path.Count == 5);
        Check("yol baslangic karesini ICERMEZ", path[0] != new Point(0, 0));
        Check("yolun sonu hedeftir", path[^1] == new Point(5, 0));

        // Capraz: (0,0) -> (4,4) sekiz yonlu izgarada 4 capraz adim.
        pathfinder.FindPath(open, new Point(0, 0), new Point(4, 4), path);
        Check("capraz hareket kullanilir (4 adim)", path.Count == 4);

        // --- Duvar: dolasmali ---
        // x = 3 sutunu y = -2..2 arasi kapali, ustunden/altindan gecilir.
        var wall = (int x, int y) => x == 3 && y >= -2 && y <= 2;

        Check("duvarin arkasindaki hedefe yol bulunur",
            pathfinder.FindPath(wall, new Point(0, 0), new Point(6, 0), path)
                == PathResult.Complete);

        Check("yol duvarin ICINDEN gecmez",
            path.TrueForAll(p => !wall(p.X, p.Y)));

        // Duz cizgi 6 adim olurdu; dolasma daha uzun OLMALI. Bu kontrol
        // "yol bulundu ama duvari yok saydi" hatasini yakalar.
        Check("dolasma duz cizgiden uzun", path.Count > 6);

        Check("dolasan yolun sonu yine hedeftir", path[^1] == new Point(6, 0));

        // --- Kapali oda: Partial donmeli, DONMAMALI ---
        // Hedefin cevresi tamamen duvar.
        var sealed_ = (int x, int y) =>
            Math.Abs(x - 10) <= 2 && Math.Abs(y) <= 2 &&
            (Math.Abs(x - 10) == 2 || Math.Abs(y) == 2);

        var outcome = pathfinder.FindPath(sealed_, new Point(0, 0), new Point(10, 0), path);

        Check("ulasilamaz hedefte Partial doner", outcome == PathResult.Partial);
        Check("Partial yol da BOS DEGIL (dusman donmaz)", path.Count > 0);
        Check("Partial yol duvarin icine girmez",
            path.TrueForAll(p => !sealed_(p.X, p.Y)));

        // --- Butce gercekten kesiyor mu ---
        // Kucuk butce ile ayni ulasilamaz hedef: arama sonsuz dunyada
        // butun chunk'lari taramamali.
        pathfinder.FindPath(sealed_, new Point(0, 0), new Point(10, 0), path, nodeBudget: 32);
        Check("dugum butcesi asilmaz", pathfinder.LastExpandedNodes <= 32);

        // --- Kose kesme yasak ---
        // (1,0) ve (0,1) dolu. (1,1) karesi hala ULASILABILIR (etrafindan
        // dolasilarak) ama (0,0) -> (1,1) TEK CAPRAZ ADIMI yasak: carpisma
        // kutusu iki duvarin kosesine sikisirdi.
        var corner = (int x, int y) => (x == 1 && y == 0) || (x == 0 && y == 1);

        pathfinder.FindPath(corner, new Point(0, 0), new Point(1, 1), path);

        Check("kose kesen tek capraz adim SECILMEZ", path.Count > 1);
        Check("kose labirentinde her adim gecerli",
            AllStepsLegal(corner, new Point(0, 0), path));

        // Tek dik komsu kapaliyken de capraz yasak; dolasma iki adim surer.
        var halfCorner = (int x, int y) => x == 1 && y == 0;

        pathfinder.FindPath(halfCorner, new Point(0, 0), new Point(1, 1), path);
        Check("tek komsu kapaliyken capraz yerine dolasilir", path.Count == 2);

        // Duvar labirentindeki yolun her adimi da ayni kurallara uymali:
        // "hedefe vardi" yetmez, ARADAKI her adim yurunebilir olmali.
        pathfinder.FindPath(wall, new Point(0, 0), new Point(6, 0), path);
        Check("duvar labirentinde her adim gecerli",
            AllStepsLegal(wall, new Point(0, 0), path));

        // --- Ayni kare ---
        Check("baslangic = hedef ise yol bostur",
            pathfinder.FindPath(open, new Point(4, 4), new Point(4, 4), path)
                == PathResult.Complete && path.Count == 0);
    }

    /// <summary>
    /// Görev ilerlemesi ve ekili tarlaların kaydı — SİSTEM düzeyinde.
    ///
    /// <see cref="SaveGameRules"/> yalnızca <see cref="SaveData"/> alanlarının
    /// diske gidip geldiğini sınıyor. Asıl soru ise başka: kayıttaki veri
    /// GERÇEK sistemlere geri yüklendiğinde oyuncu kaldığı yerden mi devam
    /// ediyor? Bir alan doğru yazılıp yanlış yere yüklenirse ilk sınama
    /// bunu göremez — oyuncu ilk görevi ikinci kez yapmak zorunda kalır.
    ///
    /// Bu yüzden burada gerçek <see cref="NpcSystem"/> ve
    /// <see cref="FarmingSystem"/> kullanılıyor.
    /// </summary>
    private static void ProgressPersistenceRules(SelfTestWorld world)
    {
        Section("11) Gorev ve tarla kaydi (sistem duzeyinde)");

        // --- Gorev ilerlemesi ---
        var npcs = world.Npcs;
        npcs.RestoreQuests([]);

        var firstQuest = npcs.ActiveQuest;
        Check("temiz baslangicta ilk gorev aktif", firstQuest is not null, firstQuest?.Id ?? "yok");

        var inventory = new WorldInventory(world.Items);
        inventory.TryAdd(firstQuest!.Require.Item, firstQuest.Require.Amount);

        npcs.TurnInQuest(inventory);
        Check("gorev teslim edildi", npcs.CompletedQuests == 1);

        var secondQuest = npcs.ActiveQuest;
        Check("siradaki gorev degisti", secondQuest is not null && secondQuest.Id != firstQuest.Id,
              secondQuest?.Id ?? "yok");

        // Kayit yolundan gecir: ihrac -> geri yukle.
        var savedQuests = npcs.CompletedQuestIds.ToList();

        npcs.RestoreQuests([]);
        Check("sifirlama gercekten siliyor", npcs.CompletedQuests == 0);

        npcs.RestoreQuests(savedQuests);
        Check("kayittan sonra ayni gorevde kaliniyor",
            npcs.ActiveQuest?.Id == secondQuest!.Id, npcs.ActiveQuest?.Id ?? "yok");

        // Tanimsiz kimlik yuklemeyi BOZMAMALI: veriden kaldirilmis ya da
        // modla degistirilmis bir gorev, oyuncunun butun ilerlemesini
        // gecersiz kilmamali.
        npcs.RestoreQuests([.. savedQuests, "artik_olmayan_gorev"]);
        Check("tanimsiz gorev kimligi yuklemeyi bozmuyor",
            npcs.ActiveQuest?.Id == secondQuest.Id);

        npcs.RestoreQuests([]);

        // --- Ekili tarlalar ---
        var cropId = world.Crops.Crops[0].Id;
        var tile = new Point(7, -3);

        var farming = new FarmingSystem(world.Crops, 3);

        var restored = farming.Restore([(tile, cropId, 1.25, 0.75)]);
        Check("tarla geri kuruldu", restored == 1 && farming.PlantedCount == 1);

        var crop = farming.At(tile);
        Check("tarla dogru karede", crop is not null);
        Check("ekin tanimi dogru", crop!.Definition.Id == cropId, crop.Definition.Id);
        Check("buyume korundu", Math.Abs(crop.GrowthDays - 0.75) < 1e-9);
        Check("ekildigi gun korundu", Math.Abs(crop.PlantedAtDay - 1.25) < 1e-9);

        // --- Yuklemeden sonra "gun sicramasi" olmamali ---
        //
        // Buyume gecen DUNYA GUNUNDEN hesaplaniyor. Restore icindeki gun
        // sayaci sifirlanmazsa, uzun sure calismis bir oyuna kayit
        // yuklendiginde ILK Update aradaki butun gunleri bir anda buyume
        // olarak eklerdi.
        //
        // Once KURULUMUN gercekten buyume uretebildigi gosteriliyor:
        // uretemiyorsa asil kontrol bos yere gecer (bu tuzaga bir kez
        // dusuldu — taze bir FarmingSystem zaten sifirlanmis geliyordu ve
        // kontrol hicbir seyi sinamiyordu).
        var clock = new ClimateSystem(world.ClimateTable, 1);

        var control = new FarmingSystem(world.Crops, 3);
        control.Restore([(tile, cropId, 0, 0)]);
        control.Update(clock);                       // gun sayaci dolar
        AdvanceDays(clock, world.ClimateTable, 2);
        control.Update(clock);                       // 2 gunluk buyume eklenmeli

        var grew = control.At(tile)!.GrowthDays;
        Check("kurulum gercekten buyume uretiyor", grew > 0.5, $"{grew:F3} gun");

        // Asil kontrol: AYNI kosulda, ama arada Restore var.
        var reloaded = new FarmingSystem(world.Crops, 3);
        reloaded.Update(clock);                      // gun sayaci dolar
        AdvanceDays(clock, world.ClimateTable, 2);
        reloaded.Restore([(tile, cropId, 0, 0.75)]);
        reloaded.Update(clock);

        Check("yuklemeden sonraki ilk gun sicramasi YOK",
            Math.Abs(reloaded.At(tile)!.GrowthDays - 0.75) < 1e-9,
            $"{reloaded.At(tile)!.GrowthDays:F3} gun");

        // Tanimsiz ekin atlanmali, gecerli olan yuklenmeli.
        var mixed = new FarmingSystem(world.Crops, 3);
        var count = mixed.Restore(
        [
            (new Point(1, 1), cropId, 0, 0.5),
            (new Point(2, 2), "artik_olmayan_ekin", 0, 0.5)
        ]);

        Check("tanimsiz ekin atlaniyor", count == 1 && mixed.PlantedCount == 1);
        Check("gecerli ekin yine de yuklendi", mixed.At(new Point(1, 1)) is not null);

        // Elle duzenlenmis kayitta negatif buyume asama hesabini ters
        // cevirirdi.
        var clamped = new FarmingSystem(world.Crops, 3);
        clamped.Restore([(tile, cropId, 0, -5.0)]);
        Check("negatif buyume sifira kirpiliyor", clamped.At(tile)!.GrowthDays >= 0,
              clamped.At(tile)!.GrowthDays.ToString("F2"));
    }

    /// <summary>
    /// Mod içerik bindirmesinin birleştirme kuralları.
    ///
    /// Gerçek bir modun gerçekten etki ettiği <c>mods/ornek_mod</c> ile
    /// ve ekran görüntüsüyle gösteriliyor (üretim panelinde yedinci satır).
    /// Burada sınanan, o gösterimin KENARLARI: modun yalnızca değiştirdiği
    /// alanı yazması yeterli mi, kimlik eşleşmesi doğru mu, yeni öğe
    /// ekleniyor mu, Steam yolları gerçekten kapalı mı.
    /// </summary>
    private static void ModOverlayRules()
    {
        Section("13) Mod icerik bindirmesi");

        static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

        // --- Alan duzeyinde birlesme ---
        var target = Parse("""
            {"slotCount": 24, "items": [
                {"id": "wood", "name": "Odun", "icon": 0, "maxStack": 99}
            ]}
            """);

        ModdedContent.Merge(target, Parse("""
            {"items": [{"id": "wood", "maxStack": 64}]}
            """));

        var wood = target["items"]!.AsArray().OfType<JsonObject>().First();

        Check("mod'un yazdigi alan kazaniyor", wood["maxStack"]!.GetValue<int>() == 64);
        Check("yazilmayan alanlar KORUNUYOR", wood["name"]!.GetValue<string>() == "Odun",
              wood["name"]!.GetValue<string>());
        Check("dizi disindaki alanlar korunuyor", target["slotCount"]!.GetValue<int>() == 24);

        // Diziyi tumden degistirmek daha basit olurdu ama o zaman tek bir
        // alani degistirmek isteyen mod BUTUN listeyi kopyalamak zorunda
        // kalirdi -- ve o kopya, temel oyun bir item ekledigi anda eskirdi.
        Check("temel oge silinmedi", target["items"]!.AsArray().Count == 1);

        // --- Yeni oge EKLENIYOR ---
        ModdedContent.Merge(target, Parse("""
            {"items": [{"id": "mod_charcoal", "name": "Odun Komuru", "icon": 0}]}
            """));

        Check("yeni kimlik ekleniyor", target["items"]!.AsArray().Count == 2);
        Check("eklenen oge dogru", target["items"]![1]!["id"]!.GetValue<string>() == "mod_charcoal");

        // --- 'key' de kimlik sayiliyor (istatistikler ve hava turleri) ---
        var keyed = Parse("""{"stats": [{"key": "wood_gathered", "name": "Odun"}]}""");
        ModdedContent.Merge(keyed, Parse("""{"stats": [{"key": "wood_gathered", "name": "Kutuk"}]}"""));

        Check("'key' alani da kimlik sayiliyor", keyed["stats"]!.AsArray().Count == 1);
        Check("'key' ile eslesen oge birlesti",
            keyed["stats"]![0]!["name"]!.GetValue<string>() == "Kutuk");

        // --- Ic ice nesneler ---
        var nested = Parse("""{"a": {"x": 1, "y": 2}}""");
        ModdedContent.Merge(nested, Parse("""{"a": {"y": 9}}"""));

        Check("ic ice nesne alan alan birlesiyor",
            nested["a"]!["x"]!.GetValue<int>() == 1 && nested["a"]!["y"]!.GetValue<int>() == 9);

        // --- Kimliksiz oge sadece EKLENIR ---
        var plain = Parse("""{"list": [1, 2]}""");
        ModdedContent.Merge(plain, Parse("""{"list": [3]}"""));
        Check("kimliksiz oge ekleniyor", plain["list"]!.AsArray().Count == 3);

        // --- Steam yollari KAPALI ---
        //
        // Bir Workshop paketi itemdefs'e yazabilseydi pazarlanabilir item
        // uydurabilir, achievements'a yazabilseydi esikleri 1'e cekip
        // Steam profilinde gercek degeri olan basarimlari bedavaya acardi.
        Check("Steam/itemdefs bindirmeye KAPALI",
            !ModdedContent.IsOverlayAllowed("Steam/itemdefs"));
        Check("Steam/achievements bindirmeye KAPALI",
            !ModdedContent.IsOverlayAllowed("Steam/achievements"));

        Check("oyun verisi bindirmeye ACIK",
            ModdedContent.IsOverlayAllowed("Items/items") &&
            ModdedContent.IsOverlayAllowed("World/crops"));
    }

    /// <summary>
    /// Veri dosyalarındaki adların dile çevrilmesi.
    ///
    /// ── Asıl tehlike ────────────────────────────────────────────────────
    /// Adları çevirmek görünürde masum bir iş. Tehlike, o adların bazı
    /// yerlerde MANTIK anahtarı olarak kullanılıyor olması:
    /// <c>crops.json</c> bir ekinin mevsimlerini mevsim adıyla yazıyordu.
    /// Ad çevrilir çevrilmez karşılaştırma İngilizce oynayan oyuncuda
    /// hiç tutmaz ve ekinler **sessizce hiç büyümezdi** — ekranda
    /// görünmeyen, ancak günler sonra fark edilecek bir hata.
    ///
    /// Bu yüzden burada iki şey ayrı ayrı sınanıyor: adın dile göre
    /// DEĞİŞTİĞİ ve anahtarın DEĞİŞMEDİĞİ.
    /// </summary>
    private static void DataNameRules(SelfTestWorld world)
    {
        Section("12) Veri adlarinin dile cevrilmesi");

        var startingLanguage = Loc.CurrentCode;

        try
        {
            var season = world.ClimateTable.Seasons[0];
            var item = world.Items.Items[0];

            Loc.Use("tr");
            var trSeason = season.Name;
            var trItem = item.Name;
            var trKey = season.Key;

            Loc.Use("en");
            var enSeason = season.Name;
            var enItem = item.Name;
            var enKey = season.Key;

            Check("item adi dile gore degisiyor", trItem != enItem, $"{trItem} / {enItem}");
            Check("mevsim adi dile gore degisiyor", trSeason != enSeason,
                  $"{trSeason} / {enSeason}");

            Check("mevsim ANAHTARI dile gore DEGISMIYOR", trKey == enKey, trKey);

            // Asil kural: ekin tablosu anahtari taniyor mu. Tanimiyorsa
            // ekinler o dilde hic buyumez.
            Check("ekin tablosu mevsim anahtarini taniyor",
                world.Crops.Crops.Any(c => c.Seasons.Contains(enKey)),
                $"'{enKey}' -> {world.Crops.Crops.Count(c => c.Seasons.Contains(enKey))} ekin");

            // Anahtar verilmemis (eski/mod) veri hala calismali. Ham adi
            // burada ELLE kurmak sart: sinanan sey tam olarak "anahtar
            // yokken ne oluyor".
            var legacy = new SeasonDefinition { RawName = "Ilkbahar" };  // ham ad kasten
            Check("anahtarsiz mevsim ham ada dusuyor", legacy.Key == "Ilkbahar", legacy.Key);

            // Anahtari olmayan tanim, veri dosyasindaki ada dusmeli:
            // modlar dil satiri saglamak ZORUNDA olmamali.
            var modItem = new ItemDefinition { RawName = "Mod Item" };  // ham ad kasten
            Check("anahtarsiz ad veri dosyasindaki adi kullanir",
                modItem.Name == "Mod Item", modItem.Name);
        }
        finally
        {
            // Denetim dili degistirdi; sonraki denetimler ve oyun
            // baslangic dilinde devam etmeli.
            Loc.Use(startingLanguage);
        }
    }

    /// <summary>
    /// Dünya saatini verilen gün kadar ileri sarar.
    ///
    /// Tek büyük adım yerine küçük adımlar: hava ve mevsim geçişleri
    /// oyunda da küçük adımlarla oluyor ve tek sıçrama onları atlardı.
    /// </summary>
    private static void AdvanceDays(ClimateSystem climate, ClimateTable table, int days)
    {
        var steps = days * 60;
        var step = table.DayLengthSeconds / 60f;

        for (var i = 0; i < steps; i++) climate.Update(step);
    }

    /// <summary>
    /// Varlık snapshot'ının C# tarafındaki kuralları.
    ///
    /// Bayt düzenini <c>Tools/verify_protocol.py</c> BAĞIMSIZ olarak
    /// doğruluyor (iki implementasyon kasten ayrı). Burada onun
    /// göremediği iki şey sınanıyor: kimlik uzayının bölünmesi ve
    /// sayı tavanının gerçekten kırpması.
    /// </summary>
    private static void EntitySnapshotRules()
    {
        Section("10) Protokol mesajlari (varlik, eylem, tarla)");

        var sent = new List<EntityState>
        {
            new(1, (byte)EntityKind.Enemy, 3, 328.5f, -224.25f, 2, 100,
                EntityState.FlagDead | EntityState.FlagBoss),
            new(EntityState.CreatureIdBase, (byte)EntityKind.Creature, 0, -1e6f, 1e6f, 3, 100,
                EntityState.FlagMoving | EntityState.FlagTamed)
        };

        var packet = NetworkProtocol.WriteEntitySnapshot(9, sent);

        Check("snapshot okunabiliyor",
            NetworkProtocol.TryReadEntitySnapshot(packet, out var tick, out var back));

        Check("tick korunuyor", tick == 9);
        Check("gidis-donus degeri koruyor", back.SequenceEqual(sent));

        Check("boss bayragi tasindi", back[0].IsBoss && back[0].IsDead);
        Check("evcil bayragi tasindi", back[1].IsTamed && back[1].IsMoving);

        // Kimlik uzayi bolunmus olmali: dusman ve yaratik sistemleri
        // birbirinin sayacini bilmiyor ama istemcide TEK sozlukte
        // yasiyorlar.
        Check("dusman kimligi alt yarida", back[0].EntityId < EntityState.CreatureIdBase);
        Check("yaratik kimligi ust yarida", back[1].EntityId >= EntityState.CreatureIdBase);

        // Tavan MTU yuzunden var: 255 varlik her tick parcalanmis paket
        // demek olurdu.
        var many = Enumerable.Range(0, 200)
            .Select(i => new EntityState((ushort)i, 0, 0, 0f, 0f, 0, 100, 0))
            .ToList();

        NetworkProtocol.TryReadEntitySnapshot(
            NetworkProtocol.WriteEntitySnapshot(0, many), out _, out var capped);

        Check("varlik sayisi tavanda kirpiliyor",
            capped.Count == NetworkProtocol.MaxEntitiesPerSnapshot, $"{capped.Count} varlik");

        Check("tavandaki paket tipik MTU altinda",
            6 + NetworkProtocol.MaxEntitiesPerSnapshot * NetworkProtocol.EntityStateBytes < 1200);

        // Kirpik paket cokme yerine false donmeli: bozuk bir paket oyunu
        // kapatmamali.
        Check("kirpik paket false doner",
            !NetworkProtocol.TryReadEntitySnapshot(packet.AsSpan(0, 14), out _, out _));

        // --- Dunya eylemi (istemci -> host -> istemci) ---

        Check("WorldAction gidis-donus",
            NetworkProtocol.TryReadWorldAction(
                NetworkProtocol.WriteWorldAction(WorldActionKind.Farm, "wheat_seed"),
                out var readAction, out var readArg)
            && readAction == WorldActionKind.Farm && readArg == "wheat_seed");

        // Hedef kare mesajda YOK: host onu kendi bildigi oyuncu konumundan
        // hesapliyor. Olsaydi istemci haritanin obur ucundaki bir tarlayi
        // hasat edebilirdi. Parametresiz eylem 3 bayt: tur + eylem + uzunluk.
        Check("WorldAction hedef kare tasimiyor",
            NetworkProtocol.WriteWorldAction(WorldActionKind.Tame).Length == 3);

        // Sonuc METIN degil KOD: metin gonderilse host'un dili istemciye
        // dayatilirdi (madde 23'te dil istemcinin kendi ayari).
        Check("ActionResult gidis-donus",
            NetworkProtocol.TryReadActionResult(
                NetworkProtocol.WriteActionResult(WorldActionKind.Tame,
                                                  (byte)TameOutcome.Fed, 2),
                out var resultAction, out var resultCode, out var resultDetail)
            && resultAction == WorldActionKind.Tame
            && (TameOutcome)resultCode == TameOutcome.Fed && resultDetail == 2);

        Check("ActionResult sabit 4 bayt",
            NetworkProtocol.WriteActionResult(WorldActionKind.Farm, 8, 255).Length == 4);

        // --- Tarla snapshot'i ---
        var cropsSent = new List<(int X, int Y, byte Crop, float Growth)>
        {
            (-40000, 31337, 1, 2.5f),
            (0, 0, 0, 0f)
        };

        Check("CropSnapshot gidis-donus",
            NetworkProtocol.TryReadCropSnapshot(
                NetworkProtocol.WriteCropSnapshot(cropsSent), out var cropsBack)
            && cropsBack.SequenceEqual(cropsSent));

        var manyCrops = Enumerable.Range(0, 200)
            .Select(i => (i, i, (byte)0, 0f))
            .ToList();

        NetworkProtocol.TryReadCropSnapshot(
            NetworkProtocol.WriteCropSnapshot(manyCrops), out var cappedCrops);

        Check("tarla sayisi tavanda kirpiliyor",
            cappedCrops.Count == NetworkProtocol.MaxCropsPerSnapshot, $"{cappedCrops.Count}");
    }

    /// <summary>
    /// Elle çizilmiş sınama haritası: uzun bir duvar ve iki yanı açık arazi.
    ///
    /// <c>x = WallColumn</c> sütunu <c>y = -6..6</c> arasında kapalı.
    /// Düşman ile oyuncu duvarın iki yanında; aradaki tek yol duvarın
    /// ucundan dolaşmak.
    /// </summary>
    private sealed class WallMap(int groundTile, int wallTile) : ITileGenerator
    {
        public const int WallColumn = 6;
        public const int WallHalfHeight = 6;

        public int Seed => 1;

        /// <summary>Sınırsız: düşman duvarı istediği kadar dolaşabilsin.</summary>
        public Rectangle? Bounds => null;

        public int GetTileIndex(int tileX, int tileY) =>
            IsSolid(tileX, tileY) ? wallTile : groundTile;

        public bool IsSolid(int tileX, int tileY) =>
            tileX == WallColumn && tileY >= -WallHalfHeight && tileY <= WallHalfHeight;
    }

    /// <summary>
    /// Düşman gerçekten duvarı DOLAŞIYOR mu.
    ///
    /// <see cref="PathfindingRules"/> yol bulucunun kendisini sınıyor;
    /// burada gerçek <see cref="Enemy"/>, gerçek <see cref="TileMap"/> ve
    /// gerçek çarpışma kodu ile bir kovalama koşturuluyor. İkisi ayrı:
    /// doğru bir yol bulucu, yolu takip etmeyen bir düşmanla birlikte de
    /// var olabilir — eski hata (duvara yaslanıp titreme) tam olarak
    /// buydu.
    /// </summary>
    private static void EnemyChaseRules(ItemDatabase items, Tileset tileset,
                                        SpriteSheet playerSheet, EnemySystem enemies,
                                        ClimateSystem climate)
    {
        Section("9) Dusman duvari dolasiyor mu (gercek kovalama)");

        var map = new TileMap(
            new WallMap(tileset.IndexOf("grass"), tileset.IndexOf("stone_wall")), tileset);

        var tileSize = map.TileSize;

        // Oyuncu duvarin SAG yaninda, dusman SOL yaninda, ayni satirda.
        // Duz cizgi tam duvara denk geliyor.
        var player = new Player(playerSheet, TileFoot(12, 0, tileSize));

        enemies.Clear();
        enemies.SpawnBoss(TileFoot(0, 0, tileSize));

        Check("sinama dusmani dogdu", enemies.Enemies.Count == 1);

        var enemy = enemies.Enemies[0];
        var startDistance = Vector2.Distance(enemy.Position, player.Position);

        Check("baslangicta duvarin arkasinda (gorus kapali)",
            !TilePathfinder.HasLineOfSight(map, enemy.Center, player.Center, 7f));

        var inventory = new WorldInventory(items);
        var frame = TimeSpan.FromSeconds(1.0 / 60.0);

        var usedPath = false;
        var maxAbsY = 0f;
        var enteredWall = false;
        var reachedFrame = -1;

        // 20 saniye: dolasma mesafesi ~26 tile, boss hizi 44 px/sn.
        for (var i = 0; i < 1200; i++)
        {
            enemies.Update(new GameTime(frame * i, frame), map, player, climate, inventory,
                           allowSpawning: false);

            if (enemy.PathLength > 0) usedPath = true;

            maxAbsY = MathF.Max(maxAbsY, MathF.Abs(enemy.Position.Y - player.Position.Y));

            // Duvarin ICINDE hic bulunmamali.
            if (map.IsSolidAtWorld(enemy.Center.X, enemy.Center.Y)) enteredWall = true;

            if (reachedFrame < 0 &&
                Vector2.Distance(enemy.Position, player.Position) <= enemy.Definition.AttackRange)
            {
                reachedFrame = i;
                break;
            }
        }

        Check("dusman YOL BULMA kullandi (duz cizgi degil)", usedPath);
        Check("dusman duvarin icine hic girmedi", !enteredWall);

        // Duz cizgide gelseydi y sapmasi ~0 kalirdi. Duvarin yarim
        // yuksekligi 6 tile; en az 5 tile sapma bekleniyor.
        Check("dusman duvari DOLASTI (y sapmasi > 5 tile)", maxAbsY > 5 * tileSize);

        Check("dusman oyuncuya ULASTI", reachedFrame >= 0);

        Check("baslangic mesafesi gercekten uzaklasmis degil",
            startDistance > enemy.Definition.AttackRange);

        Console.WriteLine($"         (ulasma: {reachedFrame} kare, " +
                          $"en buyuk y sapmasi: {maxAbsY / tileSize:F1} tile)");

        enemies.Clear();
    }

    /// <summary>Tile koordinatından ayak hizası dünya konumu.</summary>
    private static Vector2 TileFoot(int tileX, int tileY, int tileSize) => new(
        tileX * tileSize + tileSize / 2f,
        (tileY + 1) * tileSize);

    /// <summary>
    /// Yolun her adımı gerçekten yürünebilir mi.
    ///
    /// "Hedefe vardı" tek başına yetmez: aradaki bir adım duvarın içinden
    /// ya da iki duvarın köşesinden geçiyorsa düşman orada takılır ve
    /// hata ekranda "düşman bazen donuyor" diye görünür.
    /// </summary>
    private static bool AllStepsLegal(Func<int, int, bool> isSolid, Point start,
                                      IReadOnlyList<Point> path)
    {
        var previous = start;

        foreach (var step in path)
        {
            var dx = step.X - previous.X;
            var dy = step.Y - previous.Y;

            // Komsu olmali.
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0)) return false;

            // Hedef kare bos olmali.
            if (isSolid(step.X, step.Y)) return false;

            // Capraz adimda iki dik komsu da bos olmali.
            if (dx != 0 && dy != 0 &&
                (isSolid(previous.X + dx, previous.Y) || isSolid(previous.X, previous.Y + dy)))
            {
                return false;
            }

            previous = step;
        }

        return true;
    }

    /// <param name="detail">
    /// Başarısızlıkta işe yarayan ek bilgi (beklenen/bulunan değer).
    /// Sonuç satırının sonuna eklenir.
    /// </param>
    private static void Check(string what, bool condition, string detail = "")
    {
        var suffix = detail.Length > 0 ? $"  ({detail})" : "";

        if (condition)
        {
            _passed++;
            Console.WriteLine($"  GECTI  {what}{suffix}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  KALDI  {what}{suffix}");
        }
    }

    private static void Section(string name) => Console.WriteLine($"\n{name}");

    // ==================== KLAN ====================

    private static void ClanRankRules()
    {
        Section("1) Klan rutbe ve yetki kurallari");

        var clans = new ClanSystem();

        Check("klan kurulabiliyor",
            clans.Create(0, "Host", "Kurucular", "KUR", out var clan) == ClanOutcome.Success
            && clan is not null);

        Check("kurucu LIDER oluyor",
            clans.ClanOf(0)?.Find(0)?.Rank == ClanRank.Leader);

        Check("ayni oyuncu ikinci klan kuramaz",
            clans.Create(0, "Host", "Ikinci", "IKI", out _) == ClanOutcome.AlreadyInClan);

        Check("lider davet edebiliyor",
            clans.Invite(0, 1, "Oyuncu1") == ClanOutcome.Success);

        Check("davet edilen UYE rutbesinde basliyor",
            clans.ClanOf(1)?.Find(1)?.Rank == ClanRank.Member);

        // Uye kurabilir ama sokemez: yeni katilanin klanin butun yapilarini
        // sokup kacmasi, klan sistemlerinin klasik istismari.
        Check("UYE insa edebilir",
            ClanRank.Member.Can(ClanPermission.Build));

        Check("UYE yapi SOKEMEZ",
            !ClanRank.Member.Can(ClanPermission.Dismantle));

        Check("UYE davet edemez",
            clans.Invite(1, 2, "Oyuncu2") == ClanOutcome.NoPermission);

        Check("UYE atamaz",
            clans.Kick(1, 0) == ClanOutcome.NoPermission);

        // Kendi rutbesine esit/ustune cikaramaz: aksi halde bir uye ikinci
        // bir lider yaratip klani ele gecirebilirdi.
        Check("lider bir uyeyi LIDER yapamaz (ele gecirme)",
            clans.SetRank(0, 1, ClanRank.Leader) == ClanOutcome.NoPermission);

        Check("lider bir uyeyi SUBAY yapabilir",
            clans.SetRank(0, 1, ClanRank.Officer) == ClanOutcome.Success);

        Check("subay artik sokebilir",
            ClanRank.Officer.Can(ClanPermission.Dismantle));

        clans.Invite(1, 2, "Oyuncu2");
        Check("subay davet edebiliyor", clans.ClanOf(2) is not null);

        // Esit rutbeyi atamaz: subaylarin birbirini atmasi klani bosaltirdi.
        clans.SetRank(0, 2, ClanRank.Officer);
        Check("subay esit rutbeyi atamaz",
            clans.Kick(1, 2) == ClanOutcome.NoPermission);

        Check("lider baska uye varken ayrilamaz",
            clans.Leave(0) == ClanOutcome.LeaderCannotLeave);

        Check("uye ayrilabilir", clans.Leave(2) == ClanOutcome.Success);
        Check("ayrilan artik klansiz", clans.ClanOf(2) is null);
    }

    // ==================== YAPI SAHIPLIGI ====================

    private static void StructureOwnershipRules()
    {
        Section("2) Yapi sahipligi (madde 18'in acik biraktigi kayit)");

        var clans = new ClanSystem();
        clans.Create(0, "Host", "Kurucular", "KUR", out _);
        clans.Invite(0, 1, "Oyuncu1");
        clans.SetRank(0, 1, ClanRank.Officer);

        // 2 = klansiz yabanci
        var mine = new Point(5, 5);
        var free = new Point(9, 9);

        clans.RegisterStructure(mine, 0);
        clans.RegisterStructure(free, 2);   // klansiz kurdu -> kayit ACILMAZ

        Check("klan uyesinin kurdugu yapi SAHIPLI",
            !clans.OwnerOf(mine).IsNone);

        Check("klansizin kurdugu yapi SAHIPSIZ kalir",
            clans.OwnerOf(free).IsNone);

        Check("sahipsiz yapiyi herkes sokebilir",
            clans.CanDismantle(free, 2));

        Check("sahibi (lider) sokebiliyor",
            clans.CanDismantle(mine, 0));

        Check("ayni klanin SUBAYI sokebiliyor",
            clans.CanDismantle(mine, 1));

        Check("yabanci SOKEMEZ (baskin yapmali)",
            !clans.CanDismantle(mine, 2));

        // Uye rutbesinde birini ekleyip sokemedigini gosterelim.
        clans.Invite(0, 3, "Oyuncu3");
        Check("ayni klanin UYESI sokemez (yetki yok)",
            !clans.CanDismantle(mine, 3));

        clans.ForgetStructure(mine);
        Check("yikilan yapinin sahiplik kaydi silinir",
            clans.OwnerOf(mine).IsNone);
    }

    // ==================== TAKAS: ONAY ====================

    private static void TradeAcceptanceRules(ItemDatabase items)
    {
        Section("3) Takas onay kurallari (teklif degisince onay duser)");

        var trade = new TradeSession();

        Check("kendinle takas reddedilir", trade.Begin(0, 0) == TradeOutcome.SelfTrade);
        Check("takas acilabiliyor", trade.Begin(0, 1) == TradeOutcome.Success);
        Check("acikken ikinci takas acilamaz", trade.Begin(0, 2) == TradeOutcome.AlreadyTrading);

        trade.Offer(0, "wood", 5);
        trade.Offer(1, "stone", 3);

        trade.Accept(0);
        trade.Accept(1);
        Check("iki onayla BothAccepted'a gecer", trade.State == TradeState.BothAccepted);

        // Takas arayuzlerinin en bilinen dolandiriciligi: karsi taraf
        // onayladiktan SONRA teklifi sessizce degistirip kabul beklemek.
        trade.Offer(0, "wood", -4);

        Check("teklif degisince A'nin onayi duser", !trade.AcceptedA);
        Check("teklif degisince B'nin onayi da duser", !trade.AcceptedB);
        Check("durum pazarliga geri doner", trade.State == TradeState.Negotiating);

        _ = items;
    }

    // ==================== TAKAS: ATOMIKLIK ====================

    private static void TradeAtomicityRules(ItemDatabase items)
    {
        Section("4) Takas atomikligi (ya tamamen olur ya hic)");

        // --- Basarili takas ---
        var a = new WorldInventory(items);
        var b = new WorldInventory(items);
        a.TryAdd("wood", 10);
        b.TryAdd("stone", 10);

        var trade = new TradeSession();
        trade.Begin(0, 1);
        trade.Offer(0, "wood", 4);
        trade.Offer(1, "stone", 6);
        trade.Accept(0);
        trade.Accept(1);

        Check("gecerli takas uygulanir",
            trade.TryExecute(a, b) == TradeOutcome.Success);

        Check("A verdigini kaybetti (10-4=6 odun)", a.CountOf("wood") == 6);
        Check("A aldigini kazandi (6 tas)", a.CountOf("stone") == 6);
        Check("B verdigini kaybetti (10-6=4 tas)", b.CountOf("stone") == 4);
        Check("B aldigini kazandi (4 odun)", b.CountOf("wood") == 4);

        // --- Sahip olunmayan item teklif edilirse ---
        var c = new WorldInventory(items);
        var d = new WorldInventory(items);
        c.TryAdd("wood", 2);
        d.TryAdd("stone", 5);

        var cheat = new TradeSession();
        cheat.Begin(0, 1);
        cheat.Offer(0, "wood", 99);   // sadece 2 tane var
        cheat.Offer(1, "stone", 5);
        cheat.Accept(0);
        cheat.Accept(1);

        Check("sahip olunmayan item reddedilir",
            cheat.TryExecute(c, d) == TradeOutcome.ItemNotOwned);

        // KRITIK: reddedilen takas HICBIR SEYI degistirmemeli.
        Check("reddedilince C'nin envanteri DEGISMEZ", c.CountOf("wood") == 2);
        Check("reddedilince D'nin envanteri DEGISMEZ", d.CountOf("stone") == 5);
        Check("reddedilince C tas KAZANMAZ", c.CountOf("stone") == 0);

        // --- Envanter dolu: yer yoksa da hicbir sey degismemeli ---
        var full = new WorldInventory(items);
        var giver = new WorldInventory(items);

        // Butun slotlari doldur: her slotu farkli bir item'la kapatmak
        // gerekmiyor, ayni item'in yigin limitini asan miktari yeter.
        var maxStack = items.Get("wood").MaxStack;
        full.TryAdd("wood", maxStack * full.SlotCount);

        giver.TryAdd("stone", 5);

        var overflow = new TradeSession();
        overflow.Begin(0, 1);
        overflow.Offer(1, "stone", 5);   // full oyuncusu ALIR ama yeri yok
        overflow.Accept(0);
        overflow.Accept(1);

        var beforeWood = full.CountOf("wood");
        var outcome = overflow.TryExecute(full, giver);

        Check("yer yoksa takas reddedilir",
            outcome == TradeOutcome.InventoryFull);

        Check("reddedilince dolu envanter DEGISMEZ",
            full.CountOf("wood") == beforeWood && full.CountOf("stone") == 0);

        Check("reddedilince veren de item KAYBETMEZ",
            giver.CountOf("stone") == 5);

        // --- Onaysiz takas uygulanamaz ---
        var e = new WorldInventory(items);
        var f = new WorldInventory(items);
        e.TryAdd("wood", 5);

        var unaccepted = new TradeSession();
        unaccepted.Begin(0, 1);
        unaccepted.Offer(0, "wood", 5);
        unaccepted.Accept(0);   // yalnizca BIR taraf

        Check("tek onayla takas uygulanmaz",
            unaccepted.TryExecute(e, f) == TradeOutcome.NotNegotiating);

        Check("tek onayda envanter DEGISMEZ",
            e.CountOf("wood") == 5 && f.CountOf("wood") == 0);
    }
}
