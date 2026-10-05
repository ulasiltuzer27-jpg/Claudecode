# -*- coding: utf-8 -*-
# Ceviri kaynagi (3/3): ekonomi, rapor, olaylar, musteri replikleri, egitim.
from loc_part1 import k

# ── Bildirimler ──────────────────────────────────────────────────────
for key, tr, en in [
    ("screenshot", "Ekran görüntüsü kaydedildi", "Screenshot saved"),
    ("saved", "Oyun kaydedildi", "Game saved"),
    ("save_failed", "Kayıt başarısız!", "Saving failed!"),
    ("achievement", "Başarım: {0}", "Achievement: {0}"),
    ("levelup", "Seviye {0}: {1}", "Level {0}: {1}"),
    ("burning", "{0} yanıyor!", "{0} is burning!"),
    ("item_rescued", "{0} bulunduğu yere geri kondu", "{0} was put back where it belongs"),
    ("cart_gas_empty", "Arabanın tüpü bitti", "The cart's gas bottle is empty"),
    ("cart_towed", "Araba gece sokakta kaldı, çekildi: -{0} ₺", "The cart was left out overnight and towed: -₺{0}"),
    ("cat_stole", "Kedi tavuğu kaptı!", "A cat stole the chicken!"),
    ("mascot", "Kedi seni sevdi: artık arabanın maskotu!", "The cat loves you: it's now your cart's mascot!"),
    ("mascot_stays", "Maskot kedi arabanın yanında kalıyor", "The mascot cat stays by your cart"),
    ("delivery_arrived", "Toptancı geldi: kolileri depoya taşı", "The wholesaler arrived: carry the boxes into the depot"),
    ("food_ready", "{0} hazır (kalite {1})", "{0} is ready (quality {1})"),
    ("pilav_ready", "Pilav hazır! Kalite {0}. {1}", "Pilaf is ready! Quality {0}. {1}"),
    ("ruined", "Yemek bozuldu. {0}", "The food is ruined. {0}"),
    ("tray_ready", "Tavuk tepsisi hazır: {0} porsiyon", "Chicken tray ready: {0} servings"),
    ("no_money", "Yeterli paran yok", "Not enough money"),
    ("no_spot", "Burası satış noktası değil", "This is not a selling spot"),
    ("ordered", "Sipariş verildi: {0} ₺ (yarın sabah gelir)", "Order placed: ₺{0} (arrives tomorrow morning)"),
    ("ordered_express", "Ekspres sipariş: {0} ₺ (bir saat içinde gelir)", "Express order: ₺{0} (arrives within an hour)"),
    ("player_joined", "{0} oyuna katıldı", "{0} joined the game"),
    ("player_left", "{0} oyundan ayrıldı", "{0} left the game"),
    ("sleep_wait", "Uyumaya hazır: {0}/{1}", "Ready to sleep: {0}/{1}"),
    ("stocked", "Rafa dizildi: {0} ({1})", "Unpacked: {0} ({1})"),
    ("upgrade_bought", "Satın alındı: {0}", "Purchased: {0}"),
    ("zabita_escaped", "Zabıtadan kaçtın!", "You escaped the inspectors!"),
    ("zabita_fined", "Zabıta ceza kesti: -{0} ₺", "The inspectors fined you: -₺{0}"),
]:
    k("toast." + key, tr, en)

# ── Kasa ─────────────────────────────────────────────────────────────
k("cash.title", "Kasa", "Cash Box")
k("cash.due", "Tutar", "Due")
k("cash.paid", "Verilen", "Paid")
k("cash.change", "Para üstü", "Change")
k("cash.given_list", "Uzattığın para", "Money you're handing over")
k("cash.undo", "Geri al", "Undo")
k("cash.clear", "Temizle", "Clear")
k("cash.give", "Ver: {0}", "Give: {0}")
k("cash.hint", "Para üstü {0} olmalı. Eksik verirsen müşteri kızar, fazla verirsen zarar edersin.",
  "The change should be {0}. Give too little and the customer gets angry; too much and you lose money.")

# ── Fiyat tabelasi ───────────────────────────────────────────────────
k("price.title", "Fiyat Tabelası", "Price Board")
k("price.col_item", "Ürün", "Item")
k("price.col_ref", "Piyasa", "Market")
k("price.col_price", "Senin fiyatın", "Your price")
k("price.cheap", "Ucuz", "Cheap")
k("price.fair", "Makul", "Fair")
k("price.high", "Pahalı", "Pricey")
k("price.too_high", "Çok pahalı", "Too expensive")
k("price.locked", "(Seviye {0})", "(Level {0})")
k("price.hint",
  "Pahalı fiyat daha az müşteri ama daha çok kâr demek. Turistler ve memurlar fiyata daha az bakar; öğrenciler ve işçiler daha çok.",
  "Higher prices mean fewer customers but more profit. Tourists and office workers care less about price; students and workers care more.")

# ── Laptop ───────────────────────────────────────────────────────────
k("laptop.title", "Laptop", "Laptop")
k("laptop.tab_supplies", "Toptancı", "Wholesaler")
k("laptop.tab_upgrades", "Yükseltmeler", "Upgrades")
k("laptop.tab_cosmetics", "Görünüm", "Cosmetics")
k("laptop.tab_stats", "İstatistik", "Stats")
k("laptop.stock", "Depoda: {0}", "In stock: {0}")
k("laptop.basket", "Sepet", "Basket")
k("laptop.express", "Ekspres teslimat (+%35)", "Express delivery (+35%)")
k("laptop.express_hint", "Kamyonet bir saat içinde gelir.", "The van arrives within an hour.")
k("laptop.normal_hint", "Kamyonet yarın sabah gelir. Kolileri depodaki rafa taşıman gerekir.", "The van arrives tomorrow morning. Carry the boxes to the shelf in the depot.")
k("laptop.total", "Toplam", "Total")
k("laptop.order", "Sipariş ver", "Place order")
k("laptop.pending", "Yoldaki siparişler", "Orders on the way")
k("laptop.eta_min", "{0} dk içinde", "in {0} min")
k("laptop.eta_morning", "yarın sabah", "tomorrow morning")
k("laptop.buy", "Satın al · {0}", "Buy · {0}")
k("laptop.owned", "Sahipsin", "Owned")
k("laptop.daily", "Günlük gider: {0}", "Daily cost: {0}")
k("laptop.select", "Seç", "Select")
k("laptop.active", "Kullanılıyor", "In use")
k("laptop.paint_default", "Varsayılan boya", "Default paint")
k("upg.reason.level", "Seviye {0} gerekli", "Requires level {0}")
k("upg.reason.money", "Para yetmiyor ({1})", "Not enough money ({1})")
k("upg.reason.owned", "Zaten sahipsin", "Already owned")
k("upg.reason.requires", "Önce bir önceki yükseltme", "Requires the previous upgrade")
k("upg.reason.unknown", "Satın alınamaz", "Unavailable")

for key, tr, en in [
    ("days", "Oynanan gün", "Days played"), ("served", "Servis edilen tabak", "Plates served"),
    ("kazan", "Pişen kazan", "Pots cooked"), ("perfect", "Kusursuz pilav", "Perfect pilafs"),
    ("change", "Doğru para üstü", "Correct change"), ("best_day", "En iyi gün (kâr)", "Best day (profit)"),
    ("total", "Toplam ciro", "Total revenue"), ("zabita", "Zabıta: kaçış / ceza", "Inspectors: escaped / fined"),
    ("cat", "Kedi sevme", "Cat pets"), ("inflation", "Toptancı zammı (toplam)", "Wholesale inflation (total)"),
    ("achievements", "Başarımlar {0}/{1}", "Achievements {0}/{1}"),
]:
    k("stats." + key, tr, en)

# ── Telefon ──────────────────────────────────────────────────────────
k("phone.title", "Telefon", "Phone")
k("phone.depot", "Depo", "Depot")
k("phone.spots", "Satış noktaları", "Selling spots")
k("phone.licensed", "Ruhsatlı", "Licensed")
k("phone.risk", "Zabıta riski %{0}", "Inspector risk {0}%")
k("phone.weather", "Hava", "Weather")
k("phone.match", "Bugün maç var", "Match today")
k("phone.news", "Haberler", "News")
k("phone.close_hint", "Tab ya da Esc ile kapat", "Close with Tab or Esc")
k("weather.clear", "Açık", "Clear")
k("weather.cloudy", "Bulutlu", "Cloudy")
k("weather.rain", "Yağmurlu", "Rainy")
for key, tr, en in [("mon", "Pazartesi", "Monday"), ("tue", "Salı", "Tuesday"), ("wed", "Çarşamba", "Wednesday"),
                    ("thu", "Perşembe", "Thursday"), ("fri", "Cuma", "Friday"), ("sat", "Cumartesi", "Saturday"),
                    ("sun", "Pazar", "Sunday")]:
    k("day." + key, tr, en)

# ── Haberler ─────────────────────────────────────────────────────────
k("news.header", "Gün {0} · {1}", "Day {0} · {1}")
k("news.welcome", "Sahil Mahallesi'ne hoş geldin! İlk kazanını pişir ve sokağa çık.", "Welcome to the seaside neighbourhood! Cook your first pot and hit the street.")
k("news.rain", "Saat {0} civarı yağmur bekleniyor: müşteri azalır.", "Rain expected around {0}:00: fewer customers.")
k("news.match", "Bugün stadyumda maç var! Maç çıkışı stadyum önü çok kalabalık olur.", "There's a match at the stadium today! Huge crowds after the game.")
k("news.inflation", "Zam geldi! Toptancı fiyatları %{0} arttı.", "Prices went up! Wholesale prices rose by {0}%.")
k("news.delivery", "Bu sabah {0} toptancı siparişi geliyor.", "{0} wholesale order(s) arrive this morning.")

# ── Gun sonu raporu ──────────────────────────────────────────────────
for key, tr, en in [
    ("title", "Gün {0} Raporu", "Day {0} Report"),
    ("passout", "Gece yarısını geçirdin ve sokakta sızdın. Para kaybettin.", "You stayed out past midnight and passed out. You lost some money."),
    ("revenue", "Satış", "Sales"), ("tips", "Bahşiş", "Tips"), ("supplies", "Toptancı", "Supplies"),
    ("upgrades", "Yükseltmeler", "Upgrades"), ("gas", "Tüp gaz", "Gas"), ("fines", "Cezalar", "Fines"),
    ("wages", "Maaş ve kira", "Wages and rent"), ("change_loss", "Fazla verilen para üstü", "Change overpaid"),
    ("profit", "Kâr", "Profit"), ("served", "Servis", "Served"), ("satisfaction", "Memnuniyet", "Satisfaction"),
    ("angry", "Kızgın giden", "Left angry"), ("price_refused", "Fiyatı beğenmeyen", "Turned away by price"),
    ("refused", "Reddedilen sipariş", "Orders refused"), ("xp", "Tecrübe", "Experience"),
    ("rep", "İtibar", "Reputation"), ("money", "Kasadaki para", "Money"), ("history", "Son günlerin kârı", "Recent profit"),
    ("next", "Yeni güne başla", "Start the next day"), ("wait_host", "Ev sahibinin devam etmesi bekleniyor…", "Waiting for the host to continue…"),
    ("tip_price", "İpucu: fiyatların yüksek, birçok müşteri vazgeçti.", "Tip: your prices are high, many customers walked away."),
    ("tip_slow", "İpucu: müşteriler çok bekledi. Daha fazla tabak hazırla, kuyruğu hızlı erit.", "Tip: customers waited too long. Prepare more plates and keep the queue moving."),
    ("tip_more", "İpucu: daha kalabalık bir nokta dene ya da daha erken çık.", "Tip: try a busier spot or head out earlier."),
    ("tip_quality", "İpucu: pilavın kalitesi düşüktü. Yıkama, kavurma ve su oranına dikkat et.", "Tip: the pilaf quality was low. Watch the washing, toasting and water ratio."),
    ("tip_good", "Harika bir gün! Mahalle seni konuşuyor.", "A great day! The whole neighbourhood is talking about you."),
]:
    k("report." + key, tr, en)

# ── Veri: malzemeler, yukseltmeler, noktalar, musteriler ─────────────
for key, tr, en in [
    ("pirinc_5", "Baldo pirinç (5 kg)", "Baldo rice (5 kg)"), ("pirinc_25", "Baldo pirinç çuvalı (25 kg)", "Baldo rice sack (25 kg)"),
    ("tereyagi_1", "Tereyağı (1 kg)", "Butter (1 kg)"), ("tuz_1", "Tuz (1 kg)", "Salt (1 kg)"),
    ("nohut_2", "Kuru nohut (2 kg)", "Dried chickpeas (2 kg)"), ("konserve_nohut", "Haşlanmış nohut (kavanoz)", "Canned chickpeas (jar)"),
    ("tavuk_3", "Tavuk göğsü (3 kg)", "Chicken breast (3 kg)"), ("karabiber", "Karabiber", "Black pepper"),
    ("ayran_24", "Ayran (24'lü koli)", "Ayran (case of 24)"), ("tursu_30", "Turşu (30 porsiyon)", "Pickles (30 servings)"),
    ("kopuk_50", "Paket kabı (50'li)", "Takeaway boxes (50)"), ("tup", "Tüp gaz", "Gas bottle"),
    ("bulgur_5", "Pilavlık bulgur (5 kg)", "Coarse bulgur (5 kg)"), ("fasulye_2", "Kuru fasulye (2 kg)", "Dried beans (2 kg)"),
    ("et_2", "Kuşbaşı et (2 kg)", "Diced beef (2 kg)"),
]:
    k("supply." + key, tr, en)

for key, tr, en, dtr, den in [
    ("tencere_ek", "İkinci tencere", "Second pot", "Nohut ve tavuğu aynı anda pişir.", "Cook chickpeas and chicken at the same time."),
    ("tabak_seti", "Tabak seti", "Plate set", "Arabaya 12 tabak daha. Daha az bulaşık molası.", "12 more plates on the cart. Fewer washing-up breaks."),
    ("semsiye", "Arabaya şemsiye", "Cart umbrella", "Yağmurda müşteri kaybı azalır.", "Lose fewer customers in the rain."),
    ("ayran_dolabi", "Ayran dolabı", "Ayran fridge", "Arabada 36 ayran soğuk kalır.", "Keeps 36 ayrans cold on the cart."),
    ("kazan_orta", "Orta kazan", "Medium cauldron", "6 kg pilav: günde iki kazan yerine bir.", "6 kg of pilaf: one pot a day instead of two."),
    ("ocak_ek", "Çift gözlü ocak", "Double stovetop", "Depoda ikinci ocak gözü açılır.", "Unlocks a second burner in the depot."),
    ("tezgah_isitici", "Tezgah ısıtıcısı", "Counter warmer", "Pilav arabada sıcak kalır (tüp yakar).", "Keeps the pilaf hot on the cart (uses gas)."),
    ("pos_cihazi", "POS cihazı", "Card terminal", "Kartla ödeme: para üstü derdi yok, bahşiş artar.", "Card payments: no change to count, more tips."),
    ("araba_buyuk", "Büyük araba", "Large cart", "Daha geniş tezgah, ikinci kazan yuvası.", "Wider counter and a second cauldron slot."),
    ("isikli_tabela", "Işıklı tabela", "Neon sign", "Akşamları müşteri çeker.", "Draws customers in the evening."),
    ("kazan_buyuk", "Büyük kazan", "Large cauldron", "10 kg pilav. Kalabalık noktalar için.", "10 kg of pilaf. For the busiest spots."),
    ("ruhsat_okul", "Okul önü ruhsatı", "School licence", "", ""),
    ("ruhsat_sanayi", "Sanayi ruhsatı", "Industrial estate licence", "", ""),
    ("ruhsat_hastane", "Hastane önü ruhsatı", "Hospital licence", "", ""),
    ("ruhsat_stadyum", "Stadyum ruhsatı", "Stadium licence", "", ""),
    ("ruhsat_iskele", "İskele ruhsatı", "Pier licence", "", ""),
    ("cirak", "Çırak", "Apprentice", "Bulaşıkları yıkar, tabakları doldurur. Günlük maaş ister.", "Washes dishes and refills plates. Paid daily."),
    ("dukkan", "Masalı dükkan", "Restaurant with tables", "Kendi dükkanın: masalar, ruhsat derdi yok, sürekli müşteri. Günlük kira.", "Your own shop: tables, no licence worries, steady customers. Daily rent."),
    ("boya_kirmizi", "Kırmızı boya", "Red paint", "", ""),
    ("boya_mavi", "Mavi boya", "Blue paint", "", ""),
    ("boya_yesil", "Yeşil boya", "Green paint", "", ""),
    ("boya_altin", "Altın boya", "Gold paint", "", ""),
]:
    k("upg." + key, tr, en)
    if dtr:
        k("upg." + key + ".d", dtr, den)
k("upg.ruhsat.d", "Bu noktada zabıta derdi biter.", "No more trouble with inspectors at this spot.")
k("upg.boya.d", "Arabanın rengi. Co-op'ta herkes görür.", "Your cart's colour. Everyone sees it in co-op.")

for key, tr, en in [("okul", "Okul önü", "School gate"), ("hastane", "Hastane önü", "Hospital"),
                    ("sanayi", "Sanayi", "Industrial estate"), ("stadyum", "Stadyum", "Stadium"),
                    ("iskele", "İskele", "Ferry pier"), ("dukkan", "Dükkan", "Shop")]:
    k("spot." + key, tr, en)

for key, tr, en in [("ogrenci", "Öğrenci", "Student"), ("isci", "İşçi", "Worker"), ("memur", "Memur", "Office worker"),
                    ("teyze", "Teyze", "Auntie"), ("turist", "Turist", "Tourist"), ("cocuk", "Çocuk", "Kid"),
                    ("balikci", "Balıkçı", "Fisherman"), ("zabita", "Zabıta", "Inspector")]:
    k("cust." + key, tr, en)

# ── Musteri konusma balonlari ────────────────────────────────────────
for voice, lines in {
    "casual": [("Usta, bir {0}!", "Mate, one {0}!"), ("Bana bir {0} ver abi.", "Give me one {0}, boss."),
               ("{0} alayım, eline sağlık.", "I'll have {0}, cheers."), ("Bir {0}, acele lütfen!", "One {0}, quick please!")],
    "formal": [("Bir {0} rica edeceğim.", "I'd like one {0}, please."), ("Merhaba, bir {0} alabilir miyim?", "Hello, could I get one {0}?"),
               ("Kolay gelsin, bir {0} lütfen.", "Good day, one {0} please."), ("Evladım, bana bir {0} ver.", "Dear, give me one {0}.")],
    "tourist": [("Hello! One {0}, please.", "Hello! One {0}, please."), ("Merhaba! {0}… please?", "Merhaba! {0}… please?"),
                ("Bir {0}, teşekkürler!", "One {0}, thank you!"), ("Is this pilaf? One {0}!", "Is this pilaf? One {0}!")],
    "kid": [("Amca bir {0}!", "Mister, one {0}!"), ("Abla bir {0} olur mu?", "Can I have one {0}?"),
            ("Bir {0}! Annem parasını verdi.", "One {0}! My mum gave me the money."), ("Ben de {0} istiyorum!", "I want {0} too!")],
}.items():
    for i, (tr, en) in enumerate(lines):
        k(f"say.order.{voice}.{i}", tr, en)

for key, tr, en in [
    ("happy", "Eline sağlık, harika olmuş!", "Delicious, great job!"),
    ("ok", "Fena değil.", "Not bad."),
    ("thanks", "Teşekkürler!", "Thanks!"),
    ("bye_happy", "Yarın yine gelirim!", "I'll come again tomorrow!"),
    ("tip", "Üstü kalsın! (+{0} ₺)", "Keep the change! (+₺{0})"),
    ("bad_taste", "Bu pilav olmamış.", "This pilaf isn't right."),
    ("stale", "Bu bayat!", "This is stale!"),
    ("cold", "Pilav buz gibi!", "The pilaf is ice cold!"),
    ("lukewarm", "Biraz soğumuş.", "It's a bit cold."),
    ("slow", "Çok beklettin.", "That took ages."),
    ("small", "Bu porsiyon az!", "That's too small!"),
    ("wrong", "Ben bunu istemedim!", "That's not what I ordered!"),
    ("missing_nohut", "Nohudu nerede?", "Where are the chickpeas?"),
    ("missing_tavuk", "Tavuk yok mu?", "No chicken?"),
    ("missing_fasulye", "Fasulyesi eksik.", "The beans are missing."),
    ("missing_pepper", "Karabiber istemiştim.", "I asked for pepper."),
    ("missing_ayran", "Ayranım nerede?", "Where's my ayran?"),
    ("missing_tursu", "Turşu unutuldu.", "You forgot the pickles."),
    ("wanted_package", "Paket istemiştim.", "I wanted it to go."),
    ("wanted_plate", "Burada yiyecektim, tabakta.", "I wanted to eat here, on a plate."),
    ("expensive", "Bu fiyata mı? Kalsın.", "At that price? No thanks."),
    ("nothing", "İstediğim yok, sonra gelirim.", "You don't have what I want, maybe later."),
    ("closed", "Kapalı mı? Yazık.", "Closed? Too bad."),
    ("too_long", "Bekleyemem, gidiyorum!", "I can't wait any longer, I'm off!"),
    ("no_change", "Para üstümü vermedin!", "You didn't give me my change!"),
    ("change_short", "Para üstü eksik! {0} ₺ daha.", "The change is short! ₺{0} more."),
    ("change_extra", "Fazla verdin ama… sağ ol!", "You gave me too much… thanks!"),
    ("change_extra_honest", "{0} ₺ fazla verdin, al bakalım.", "You gave me ₺{0} too much, here you go."),
    ("refused", "Hmph, peki.", "Hmph, fine."),
    ("seagull", "Martı tabağımı kaptı!", "A seagull stole my plate!"),
    ("smell", "Mis gibi pilav kokuyor!", "That pilaf smells amazing!"),
]:
    k("say." + key, tr, en)

# ── Basarimlar ───────────────────────────────────────────────────────
for key, tr, en in [
    ("ilk_tabak", "İlk Tabak", "First Plate"), ("yuz_tabak", "Yüz Tabak", "A Hundred Plates"),
    ("bin_tabak", "Bin Tabak", "A Thousand Plates"), ("ilk_kazan", "İlk Kazan", "First Pot"),
    ("kusursuz_pilav", "Kusursuz Pilav", "Perfect Pilaf"), ("dibi_tuttu", "Dibi Tuttu", "Burnt Bottom"),
    ("para_ustu_ustasi", "Para Üstü Ustası", "Change Master"), ("zabitadan_kactik", "Zabıtadan Kaçtık", "Got Away"),
    ("zabitaya_yakalandik", "Zabıtaya Yakalandık", "Busted"), ("kedi_dostu", "Kedi Dostu", "Cat Friend"),
    ("marti_saldirisi", "Martı Saldırısı", "Seagull Attack"), ("ruhsatli_esnaf", "Ruhsatlı Esnaf", "Licensed Vendor"),
    ("tam_ruhsat", "Tam Ruhsat", "Fully Licensed"), ("ilk_dukkan", "İlk Dükkan", "First Shop"),
    ("bin_lira", "Bin Liralık Gün", "A Thousand-Lira Day"), ("on_bin_lira", "On Bin Liralık Gün", "A Ten-Thousand-Lira Day"),
    ("milyoner", "Milyoner", "Millionaire"), ("takim_oyunu", "Takım Oyunu", "Teamwork"),
    ("yagmurda_satis", "Yağmurda Satış", "Selling in the Rain"), ("gece_kusu", "Gece Kuşu", "Night Owl"),
    ("turist_rehberi", "Turist Rehberi", "Tour Guide"), ("mac_gunu", "Maç Günü", "Match Day"),
    ("nohut_ustasi", "Nohut Ustası", "Chickpea Master"), ("bir_hafta", "Bir Hafta", "One Week"),
    ("bir_ay", "Bir Ay", "One Month"), ("usta", "Usta", "Master"), ("pilav_sultani", "Pilav Sultanı", "Pilaf Sultan"),
]:
    k("ach." + key, tr, en)

# ── Egitim ({interact} gibi isaretler oyuncunun tusuyla degisir) ─────
k("tut.title", "Rehber {0}/{1}", "Guide {0}/{1}")
for i, (tr, en) in enumerate([
    ("Günaydın usta! Depo mutfağındasın. {moveforward}{moveleft}{moveback}{moveright} ile yürü, fareyle etrafa bak. Bugün ilk pilavını pişirip satacaksın.",
     "Good morning, chef! You're in the depot kitchen. Walk with {moveforward}{moveleft}{moveback}{moveright} and look around with the mouse. Today you'll cook and sell your first pilaf."),
    ("Tezgahtaki süzgece bak ve {interact} ile al.", "Look at the colander on the counter and pick it up with {interact}."),
    ("Süzgeç elindeyken kilerdeki pirinç çuvalına bak, {use} ile en az 2 kg pirinç al.", "With the colander in hand, look at the rice sack in the pantry and take at least 2 kg with {use}."),
    ("Lavaboya git, musluğa bakıp {interact} tuşunu basılı tutarak pirinci suyu berraklaşana kadar yıka.", "Go to the sink, look at the tap and hold {interact} to wash the rice until the water runs clear."),
    ("Süzgeci bir yere bırak ({drop}). Büyük kazanı al ve kazan ocağının üstüne yerleştir.", "Put the colander down ({drop}). Pick up the big cauldron and place it on the cauldron burner."),
    ("Ocağın düğmesine bak ve {interact} ile ateşi aç. {secondary} kısar.", "Look at the burner knob and turn on the heat with {interact}. {secondary} turns it down."),
    ("Buzdolabından tereyağı al ve kazana bakarak {use} ile ekle (en az 100 g).", "Take butter from the fridge and add it to the cauldron with {use} (at least 100 g)."),
    ("Tereyağı erirken yıkanmış pirinci al ve kazana dök ({use}).", "While the butter melts, grab the washed rice and pour it into the cauldron ({use})."),
    ("Kazana bakıp {use} tuşunu basılı tutarak pirinci kavur. Taneler parlayınca tamam.", "Hold {use} while looking at the cauldron to toast the rice. Done when the grains turn glossy."),
    ("Ölçü kabını musluktan doldur ve kazana su ekle. Altın oran: 1 kg pirince 1,5 litre su.", "Fill the measuring jug at the tap and add water. Golden ratio: 1.5 litres per kg of rice."),
    ("Tuz kutusunu al ve tuz ekle: kilo başına yaklaşık 14 gram.", "Take the salt box and add salt: about 14 grams per kilo."),
    ("Kazanın kapağını kapat ({secondary}). Su kaynayınca ateşi 1'e kısarsan pilav daha yavaş ama güvenle pişer.", "Put the lid on the cauldron ({secondary}). Once it boils, turning the heat down to 1 cooks slower but safer."),
    ("Pilav suyunu çekiyor. Kazana bakarak durumunu izle; bu sırada mutfağı tanı.", "The pilaf is absorbing the water. Watch its status by looking at the cauldron; explore the kitchen meanwhile."),
    ("Su çekildi! Hemen ateşi kapat ({secondary}) yoksa dibi tutar.", "The water is absorbed! Turn off the heat ({secondary}) right away or it will burn."),
    ("Pilav demleniyor. Birkaç dakika bekle; demlenen pilav daha lezzetli olur.", "The pilaf is resting. Wait a few minutes; rested pilaf tastes better."),
    ("Kazanı al ve kapının önündeki arabanın kazan yuvasına yerleştir.", "Pick up the cauldron and place it in the cauldron slot of the cart by the door."),
    ("Arabanın sapına bakıp {interact} ile tut ve sokağa it. Bırakmak için tekrar {interact}.", "Grab the cart handle with {interact} and push it out to the street. Press {interact} again to let go."),
    ("Okul önüne git: sarı işaret seni yönlendirir. Haritaya {phone} ile bakabilirsin.", "Head to the school gate: the yellow marker guides you. Check the map with {phone}."),
    ("Arabaya bakıp {interact} ile satışa aç. Müşteriler kuyruk olacak.", "Look at the cart and open for business with {interact}. Customers will start queueing."),
    ("Siparişi oku. Arabadan tabak al, kazandan {use} ile kepçele ve müşteriye bakıp {use} ile uzat.", "Read the order. Take a plate from the cart, scoop from the cauldron with {use}, then hand it over with {use}."),
    ("Müşteri ödüyor. Kasada doğru para üstünü seç ve ver.", "The customer is paying. Pick the right change in the cash box and hand it over."),
    ("Harika! Fiyatları tabeladan, stoku depodaki laptoptan yönetirsin. Ruhsatsız noktada zabıta gelebilir: arabayı kaçır!",
     "Great! Manage prices on the board and stock on the depot laptop. Inspectors may show up at unlicensed spots: run with the cart!"),
    ("Akşam olunca arabayı depoya geri getir ve yatakta uyuyarak günü bitir.", "In the evening bring the cart back to the depot and sleep in the bed to end the day."),
]):
    k(f"tut.{i}", tr, en)
