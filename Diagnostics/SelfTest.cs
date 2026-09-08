using Microsoft.Xna.Framework;
using PixelSurvival.Clans;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Social;
using PixelSurvival.Trade;
using PixelSurvival.Workshop;

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

    /// <summary>Tüm denetimleri koşturur; başarısız sayısını döndürür.</summary>
    public static int Run(ItemDatabase items)
    {
        _passed = _failed = 0;

        Console.WriteLine("=== PixelSurvival kendi kendini denetleme ===\n");

        ClanRankRules();
        StructureOwnershipRules();
        TradeAcceptanceRules(items);
        TradeAtomicityRules(items);
        SocialSignalRules();
        WorkshopRules();

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

    private static void Check(string what, bool condition)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  GECTI  {what}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  KALDI  {what}");
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
